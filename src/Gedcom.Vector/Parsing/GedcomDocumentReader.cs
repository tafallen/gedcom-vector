using System.Text;
using Gedcom.Vector.Diagnostics;
using Gedcom.Vector.Model;

namespace Gedcom.Vector.Parsing;

/// <summary>The outcome of <see cref="GedcomDocumentReader.Read"/>.</summary>
/// <param name="Document">The document read, with its diagnostics in the order raised.</param>
/// <param name="Accounting">The disposition of every input line, when <see cref="GedcomReadOptions.RecordLineAccounting"/> was set.</param>
internal sealed record GedcomReadResult(GedcomDocument Document, LineAccounting? Accounting);

/// <summary>
/// Reads a whole GEDCOM stream into a <see cref="GedcomDocument"/>: detects the encoding and the version, decodes
/// every value (ANSEL, then escapes, then pointer flags), keeps every level-0 record as an
/// <see cref="ExtensionRecord"/> and, for GEDCOM 7.0, resolves <c>HEAD.SCHMA</c> extension URIs record by record as
/// each is read, starting with HEAD itself (SCHMA is read only from a HEAD that is the first record).
/// </summary>
internal static class GedcomDocumentReader
{
    private const string NonSeekableMessage =
        "The provided stream must be seekable. For network or compressed streams, please buffer the content into a MemoryStream first.";

    /// <summary>Reads <paramref name="stream"/>, which must be seekable, from its start.</summary>
    /// <exception cref="ArgumentException">The stream is not seekable.</exception>
    public static GedcomReadResult Read(Stream stream, GedcomReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException(NonSeekableMessage, nameof(stream));
        }

        options ??= new GedcomReadOptions();
        var encoding = GedcomEncodingDetector.Detect(stream);
        var diagnostics = new DiagnosticBag(options.MaxDiagnostics);
        var accounting = options.RecordLineAccounting ? new LineAccounting() : null;
        var document = new GedcomDocument();
        var work = new List<GedcomStructure>();
        var version = GedcomSpecVersion.Gedcom551;
        Dictionary<string, string>? schema = null;
        var trailerSeen = false;
        var recordAfterTrailerReported = false;

        using (var lines = new GedcomLineReader(stream, encoding))
        {
            var reader = new GedcomStructureReader(lines, new GedcomStringPool(), diagnostics, accounting);
            while (reader.ReadRecord() is { } record)
            {
                var index = document.ExtensionRecords.Count;
                if (index == 0)
                {
                    version = DetectVersion(record, encoding, diagnostics);
                }

                DecodeValues(record, encoding.IsAnsel, version, work);
                if (index == 0 && version == GedcomSpecVersion.Gedcom70 && record.Tag == "HEAD")
                {
                    schema = ReadSchema(record);
                }

                if (schema is { Count: > 0 })
                {
                    ApplySchema(record, schema, work);
                }

                if (trailerSeen && !recordAfterTrailerReported)
                {
                    diagnostics.Add(GedcomDiagnosticCode.RecordAfterTrailer, record.SourceLine, DiagnosticArguments.SafeXref(record.Xref));
                    recordAfterTrailerReported = true;
                }

                if (record.Tag == "TRLR")
                {
                    trailerSeen = true;
                }

                document.ExtensionRecords.Add(new ExtensionRecord { Structure = record, SourceOrder = index });
            }
        }

        if (document.ExtensionRecords.Count == 0)
        {
            diagnostics.Add(GedcomDiagnosticCode.MissingHeader);
        }

        if (!trailerSeen)
        {
            diagnostics.Add(GedcomDiagnosticCode.MissingTrailer);
        }

        document.SourceVersion = version;
        document.Diagnostics.AddRange(diagnostics.Items);
        return new GedcomReadResult(document, accounting);
    }

    // Reads HEAD.GEDC.VERS from the raw first record. The VERS value is ASCII, so it needs no decoding first.
    private static GedcomSpecVersion DetectVersion(GedcomStructure first, GedcomEncodingResult encoding, DiagnosticBag diagnostics)
    {
        if (first.Tag != "HEAD")
        {
            diagnostics.Add(GedcomDiagnosticCode.MissingHeader, first.SourceLine, DiagnosticArguments.SafeXref(first.Xref));
            return GedcomSpecVersion.Gedcom551;
        }

        var vers = FindChild(FindChild(first, "GEDC"), "VERS");
        var value = (vers?.Value ?? "").AsSpan().Trim();
        if (value.StartsWith("7.", StringComparison.Ordinal))
        {
            if (encoding.IsAnsel || encoding.DeclaredUnrecognized || encoding.Encoding is not UTF8Encoding)
            {
                diagnostics.Add(GedcomDiagnosticCode.NonUtf8Encoding, vers!.SourceLine, null, "HEAD.GEDC.VERS");
            }

            return GedcomSpecVersion.Gedcom70;
        }

        if (!value.StartsWith("5.", StringComparison.Ordinal))
        {
            diagnostics.Add(
                GedcomDiagnosticCode.UnknownVersion,
                vers?.SourceLine ?? first.SourceLine,
                null,
                vers is null ? "HEAD" : "HEAD.GEDC.VERS");
        }

        return GedcomSpecVersion.Gedcom551;
    }

    private static GedcomStructure? FindChild(GedcomStructure? parent, string tag)
    {
        if (parent is null)
        {
            return null;
        }

        foreach (var child in parent.Children)
        {
            if (child.Tag == tag)
            {
                return child;
            }
        }

        return null;
    }

    // ANSEL first (CONC folding already happened, so a split diacritic composes), then the pointer flag on the
    // undecoded-escape value, then the escape. Pointers are kept verbatim; "@@I1@" is the text "@I1@", not a pointer.
    internal static void DecodeValues(GedcomStructure record, bool isAnsel, GedcomSpecVersion version, List<GedcomStructure> work)
    {
        work.Clear();
        work.Add(record);
        while (work.Count > 0)
        {
            var structure = work[^1];
            work.RemoveAt(work.Count - 1);
            if (structure.Value is { } value)
            {
                if (isAnsel)
                {
                    value = AnselDecoder.Decode(value.AsSpan());
                }

                var isPointer = GedcomValueCodec.IsPointer(value);
                structure.ValueIsPointer = isPointer;
                structure.Value = isPointer ? value : GedcomValueCodec.Decode(value, version);
            }

            work.AddRange(structure.Children);
        }
    }

    // HEAD.SCHMA.TAG values "_TAG uri", split at the first space; entries without a space, without a URI, or whose
    // tag does not start with "_" are ignored. The first mapping of a tag wins.
    private static Dictionary<string, string> ReadSchema(GedcomStructure head)
    {
        var schema = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var schma in head.Children)
        {
            if (schma.Tag != "SCHMA")
            {
                continue;
            }

            foreach (var entry in schma.Children)
            {
                if (entry.Tag != "TAG" || entry.Value is not { } value)
                {
                    continue;
                }

                var space = value.IndexOf(' ', StringComparison.Ordinal);
                if (space <= 1 || value[0] != '_')
                {
                    continue;
                }

                var uri = value.AsSpan(space + 1).Trim();
                if (!uri.IsEmpty)
                {
                    schema.TryAdd(value[..space], uri.ToString());
                }
            }
        }

        return schema;
    }

    // Applied to each record as it is completed (HEAD included, once its SCHMA is read), so no second pass is needed.
    private static void ApplySchema(GedcomStructure record, Dictionary<string, string> schema, List<GedcomStructure> work)
    {
        work.Clear();
        work.Add(record);
        while (work.Count > 0)
        {
            var structure = work[^1];
            work.RemoveAt(work.Count - 1);
            if (structure.Tag.StartsWith('_') && schema.TryGetValue(structure.Tag, out var uri))
            {
                structure.ExtensionUri = uri;
            }

            work.AddRange(structure.Children);
        }
    }
}
