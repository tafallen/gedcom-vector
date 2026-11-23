using System.Globalization;
using System.Text;
using Gedcom.Vector.Diagnostics;
using Gedcom.Vector.Parsing;

namespace Gedcom.Vector.Model;

/// <summary>A GEDCOM structure (a line and its substructures) kept verbatim; used for extensions and unmodelled content.</summary>
public sealed class GedcomStructure
{
    /// <summary>Gets or sets the structure tag, for example <c>NOTE</c> or <c>_TAG</c>.</summary>
    public string Tag { get; set; } = "";

    /// <summary>Gets or sets the cross-reference identifier of the line, verbatim including the <c>@</c> delimiters, if any.</summary>
    public string? Xref { get; set; }

    /// <summary>Gets or sets the line value with <c>CONC</c> and <c>CONT</c> continuations already folded.</summary>
    public string? Value { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="Value"/> is a pointer to another record.</summary>
    public bool ValueIsPointer { get; set; }

    /// <summary>Gets or sets the extension URI resolved from <c>HEAD.SCHMA</c> for a GEDCOM 7.0 extension tag.</summary>
    public string? ExtensionUri { get; set; }

    /// <summary>Gets or sets the 1-based source line the structure was read from, if known.</summary>
    public int? SourceLine { get; set; }

    /// <summary>Gets the substructures of this structure, in source order.</summary>
    public List<GedcomStructure> Children { get; } = [];

    private const int MaxBaseLevel = 99;

    /// <summary>
    /// Writes this structure and its substructures as GEDCOM text, one line per structure, each ending with a line feed:
    /// <c>level [xref] tag [value]</c>. Every line feed inside a value becomes a <c>CONT</c> line directly after the
    /// structure's own line, and no <c>CONC</c> folding is done to limit line length (that is the writer's concern).
    /// Values are escaped with the same rules the reader undoes. A structure with an empty <see cref="Tag"/> (a
    /// placeholder for a malformed line) is written as <c>level [xref]</c> with no tag; its value, if any, follows as
    /// <c>CONC</c>/<c>CONT</c> children. <see cref="ExtensionUri"/> and <see cref="SourceLine"/> are not written. A
    /// <see cref="Value"/> of <c>""</c> is written like <see langword="null"/>; the reader never produces <c>""</c>.
    /// </summary>
    /// <param name="version">The GEDCOM version whose escaping rules apply.</param>
    /// <param name="baseLevel">The level of this structure's line, 0 to 99; substructures follow at deeper levels.</param>
    /// <returns>The GEDCOM text.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="baseLevel"/> is outside 0 to 99.</exception>
    public string ToGedcomText(GedcomSpecVersion version, int baseLevel = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseLevel);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(baseLevel, MaxBaseLevel);

        var builder = new StringBuilder(EstimateTextLength());
        var stack = new Stack<(GedcomStructure Structure, int Level)>();
        stack.Push((this, baseLevel));
        while (stack.Count > 0)
        {
            var (structure, level) = stack.Pop();
            structure.AppendLines(builder, level, version);
            for (var i = structure.Children.Count - 1; i >= 0; i--)
            {
                stack.Push((structure.Children[i], level + 1));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses GEDCOM text holding exactly one top-level structure, using the GEDCOM 7.0 value rules. See
    /// <see cref="ParseGedcomText(string, GedcomSpecVersion)"/>.
    /// </summary>
    /// <param name="text">The GEDCOM text.</param>
    /// <returns>The structure, with its substructures.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The text is not a single well-formed structure.</exception>
    public static GedcomStructure ParseGedcomText(string text) => ParseGedcomText(text, GedcomSpecVersion.Gedcom70);

    /// <summary>
    /// Parses GEDCOM text holding exactly one top-level structure, the inverse of <see cref="ToGedcomText"/>. The
    /// structure sits at the level of the first line, and deeper lines nest relative to it; the returned structure is a
    /// standalone root. <c>CONC</c> and <c>CONT</c> lines fold into values, a pointer value is flagged from its raw form
    /// and other values are unescaped for <paramref name="version"/> (the text is already Unicode, so no ANSEL
    /// decoding happens). <see cref="SourceLine"/> counts from 1 within the text. Lenient input is accepted exactly as
    /// the document reader accepts it (blank lines, leading whitespace, tagless placeholder lines, misplaced
    /// continuations and similar), so everything the reader produces round-trips.
    /// </summary>
    /// <param name="text">The GEDCOM text.</param>
    /// <param name="version">The GEDCOM version whose escaping rules apply.</param>
    /// <returns>The structure, with its substructures.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    /// The text is empty or whitespace, holds more than one top-level structure, or has a line without a level (GV1003,
    /// GV1004) or a level that skips (GV1009).
    /// </exception>
    public static GedcomStructure ParseGedcomText(string text, GedcomSpecVersion version)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("The text holds no GEDCOM structure.");
        }

        var baseLevel = FirstLineLevel(text);
        if (baseLevel > MaxBaseLevel)
        {
            throw new FormatException("The level of the first line is above 99.");
        }

        // A first line at level N is parsed under N synthetic ancestors (pointer-valued, so a leading CONT or CONC is
        // kept as a structure instead of folding into one) that are stripped again afterwards.
        using var source = new StringReader(baseLevel == 0 ? text : BuildAncestorLines(baseLevel) + text);
        using var lines = new GedcomLineReader(source);
        var diagnostics = new DiagnosticBag(int.MaxValue);
        var reader = new GedcomStructureReader(lines, new GedcomStringPool(), diagnostics, null);
        var first = reader.ReadRecord();
        var second = first is null ? null : reader.ReadRecord();

        foreach (var diagnostic in diagnostics.Items)
        {
            if (diagnostic.Code is GedcomDiagnosticCode.LineWithoutLevel
                or GedcomDiagnosticCode.LineWithoutLevelUnplaced
                or GedcomDiagnosticCode.LevelSkipped)
            {
                var line = diagnostic.Line is { } l ? Math.Max(l - baseLevel, 1) : 0;
                throw new FormatException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"GV{(int)diagnostic.Code} at line {line}: {diagnostic.Message}"));
            }
        }

        if (first is null)
        {
            throw new FormatException("The text holds no GEDCOM structure.");
        }

        var root = first;
        var single = second is null;
        for (var i = 0; i < baseLevel && single; i++)
        {
            single = root.Children.Count == 1;
            if (single)
            {
                root = root.Children[0];
            }
        }

        if (!single)
        {
            throw new FormatException("The text holds more than one top-level structure.");
        }

        var work = new List<GedcomStructure>();
        GedcomDocumentReader.DecodeValues(root, isAnsel: false, version, work);
        if (baseLevel > 0)
        {
            work.Clear();
            work.Add(root);
            while (work.Count > 0)
            {
                var structure = work[^1];
                work.RemoveAt(work.Count - 1);
                structure.SourceLine -= baseLevel;
                work.AddRange(structure.Children);
            }
        }

        return root;
    }

    // The level of the first non-blank line, or 0 when it has no leading digits (the reader then reports the line).
    private static int FirstLineLevel(string text)
    {
        var span = text.AsSpan().TrimStart(" \t\r\n");
        var digits = 0;
        while (digits < span.Length && digits < 4 && char.IsAsciiDigit(span[digits]))
        {
            digits++;
        }

        if (digits is 0 or 4 || (digits < span.Length && span[digits] is not (' ' or '\r' or '\n')))
        {
            return 0;
        }

        return int.Parse(span[..digits], NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static string BuildAncestorLines(int count)
    {
        var builder = new StringBuilder(count * 10);
        for (var level = 0; level < count; level++)
        {
            builder.Append(level.ToString(CultureInfo.InvariantCulture)).Append(" X @X@\n");
        }

        return builder.ToString();
    }

    private int EstimateTextLength()
    {
        var total = 0;
        var stack = new Stack<GedcomStructure>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var structure = stack.Pop();
            total += structure.Tag.Length + (structure.Xref?.Length ?? 0) + (structure.Value?.Length ?? 0) + 8;
            foreach (var child in structure.Children)
            {
                stack.Push(child);
            }
        }

        return total;
    }

    // Writes this structure's own line and the CONT/CONC lines for its value; the children are written by the caller.
    private void AppendLines(StringBuilder builder, int level, GedcomSpecVersion version)
    {
        var encoded = GedcomValueCodec.Encode(Value, ValueIsPointer, version).AsSpan();
        var newline = encoded.IndexOf('\n');
        var first = newline < 0 ? encoded : encoded[..newline];
        var rest = newline < 0 ? default : encoded[(newline + 1)..];

        AppendLevel(builder, level);
        if (Xref is not null)
        {
            builder.Append(' ').Append(Xref);
        }

        var placeholder = Tag.Length == 0;
        if (!placeholder)
        {
            builder.Append(' ').Append(Tag);
            if (!first.IsEmpty)
            {
                builder.Append(' ').Append(first);
            }
        }

        builder.Append('\n');
        if (placeholder && !first.IsEmpty)
        {
            AppendLevel(builder, level + 1);
            builder.Append(" CONC ").Append(first).Append('\n');
        }

        while (newline >= 0)
        {
            newline = rest.IndexOf('\n');
            var segment = newline < 0 ? rest : rest[..newline];
            AppendLevel(builder, level + 1);
            builder.Append(" CONT");
            if (!segment.IsEmpty)
            {
                builder.Append(' ').Append(segment);
            }

            builder.Append('\n');
            rest = newline < 0 ? default : rest[(newline + 1)..];
        }
    }

    private static void AppendLevel(StringBuilder builder, int level)
    {
        if (level >= 100)
        {
            builder.Append((char)('0' + (level / 100)));
        }

        if (level >= 10)
        {
            builder.Append((char)('0' + (level / 10 % 10)));
        }

        builder.Append((char)('0' + (level % 10)));
    }
}
