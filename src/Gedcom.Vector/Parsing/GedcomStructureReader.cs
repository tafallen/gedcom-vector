using System.Text;
using Gedcom.Vector.Diagnostics;
using Gedcom.Vector.Model;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// Builds one level-0 record tree at a time from tokenized lines: keeps a level stack, folds <c>CONC</c> and
/// <c>CONT</c> into their parent's value, repairs malformed lines leniently with diagnostics, and records what became
/// of every line. Values stay raw: no decoding of any kind happens here.
/// </summary>
internal sealed class GedcomStructureReader
{
    private const int LargeBuilderCapacity = 65536;

    private readonly GedcomLineReader _lines;
    private readonly GedcomStringPool _pool;
    private readonly DiagnosticBag _diagnostics;
    private readonly LineAccounting? _accounting;

    // Open structures indexed by level; _open[0] is the current record.
    private readonly List<GedcomStructure> _open = [];

    // Per level: a reusable builder and whether it currently holds the folded value of _open[level].
    private StringBuilder?[] _builders = new StringBuilder?[8];
    private bool[] _building = new bool[8];

    private string? _recordXref;
    private int _receiverLevel = -1;
    private int _lineNumber;
    private bool _leadingWhitespaceReported;
    private GedcomDiagnostic? _blankDiagnostic;
    private GedcomStructure? _completed;

    public GedcomStructureReader(GedcomLineReader lines, GedcomStringPool pool, DiagnosticBag diagnostics, LineAccounting? accounting)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(diagnostics);
        _lines = lines;
        _pool = pool;
        _diagnostics = diagnostics;
        _accounting = accounting;
    }

    /// <summary>Returns the next complete level-0 record tree, or null when the input is exhausted.</summary>
    public GedcomStructure? ReadRecord()
    {
        while (_lines.TryReadLine(out var text, out var lineNumber))
        {
            _lineNumber = lineNumber;
            Process(GedcomLineTokenizer.Tokenize(text), _lines.LastLineTruncated);
            if (_completed is { } completed)
            {
                _completed = null;
                return completed;
            }
        }

        if (_open.Count == 0)
        {
            return null;
        }

        var last = _open[0];
        CloseFrom(0);
        _receiverLevel = -1;
        _recordXref = null;
        return last;
    }

    private void Process(GedcomLine line, bool truncated)
    {
        switch (line.Kind)
        {
            case GedcomLineKind.Blank:
                ProcessBlank(truncated);
                break;
            case GedcomLineKind.NoLevel:
                ProcessNoLevel(line, truncated);
                break;
            default:
                ProcessStructure(line, truncated);
                break;
        }
    }

    private void ProcessBlank(bool truncated)
    {
        if (truncated)
        {
            Add(GedcomDiagnosticCode.LineTooLong, BuildPath(_open.Count, null), null);
        }

        _blankDiagnostic ??= Add(GedcomDiagnosticCode.BlankLinesIgnored, BuildPath(_open.Count, null), null);
        Account(LineDispositionKind.Diagnostic, null, _blankDiagnostic);
    }

    private void ProcessNoLevel(GedcomLine line, bool truncated)
    {
        var path = BuildPath(_receiverLevel + 1, null);
        AddLineFlags(line, truncated, path, null);
        if (_receiverLevel < 0)
        {
            var unplaced = Add(GedcomDiagnosticCode.LineWithoutLevelUnplaced, path, null);
            Account(LineDispositionKind.Diagnostic, null, unplaced);
            return;
        }

        Append(_receiverLevel, newline: true, line.Text);
        Add(GedcomDiagnosticCode.LineWithoutLevel, path, null);
        Account(LineDispositionKind.Folded, _open[_receiverLevel], null);
    }

    // Structure and NoTag lines. A NoTag line becomes a placeholder structure with an empty tag at its level, so the
    // lines after it stay under the right parent and its xref is kept.
    private void ProcessStructure(GedcomLine line, bool truncated)
    {
        var level = line.Level;
        var skipped = level > _open.Count;
        if (skipped)
        {
            level = _open.Count;
        }

        var tagSpan = line.Tag;
        var isContinuation = tagSpan.SequenceEqual("CONC") || tagSpan.SequenceEqual("CONT");
        if (isContinuation
            && level > 0
            && line.Xref.IsEmpty
            && !GedcomValueCodec.IsPointer(_open[level - 1].Value))
        {
            Fold(line, level, skipped, truncated);
            return;
        }

        if (level == 0 && _open.Count > 0)
        {
            _completed = _open[0];
        }

        CloseFrom(level);
        var structure = new GedcomStructure
        {
            Tag = _pool.GetOrAdd(tagSpan),
            Xref = line.Xref.IsEmpty ? null : _pool.GetOrAdd(line.Xref),
            Value = line.HasValue ? line.Value.ToString() : null,
            SourceLine = _lineNumber,
        };

        if (level > 0)
        {
            _open[level - 1].Children.Add(structure);
        }
        else
        {
            _recordXref = DiagnosticArguments.SafeXref(structure.Xref);
        }

        Push(structure);
        _receiverLevel = level;

        var tagless = line.Kind == GedcomLineKind.NoTag;
        var xrefOnSubstructure = level > 0 && !line.Xref.IsEmpty;
        if (tagless || truncated || NeedsLeadingWhitespace(line) || line.ExtraDelimiter || skipped || line.XrefMalformed || line.TagInvalid || xrefOnSubstructure || isContinuation)
        {
            var path = BuildPath(_open.Count, null);
            AddLineFlags(line, truncated, path, structure.Tag);
            if (skipped) Add(GedcomDiagnosticCode.LevelSkipped, path, structure.Tag);
            if (tagless) Add(GedcomDiagnosticCode.MissingTag, path, null);
            if (line.XrefMalformed) Add(GedcomDiagnosticCode.MalformedXref, path, structure.Tag);
            if (line.TagInvalid) Add(GedcomDiagnosticCode.InvalidTag, path, structure.Tag);
            if (xrefOnSubstructure) Add(GedcomDiagnosticCode.XrefOnSubstructure, path, structure.Tag);
            if (isContinuation) Add(GedcomDiagnosticCode.ContinuationMisplaced, path, structure.Tag);
        }

        Account(LineDispositionKind.Structure, structure, null);
    }

    private void Fold(GedcomLine line, int level, bool skipped, bool truncated)
    {
        CloseFrom(level);
        var targetLevel = level - 1;
        var target = _open[targetLevel];
        var afterChildren = target.Children.Count > 0;
        var isCont = line.Tag[3] == 'T';
        if (isCont || line.HasValue)
        {
            Append(targetLevel, newline: isCont, line.Value);
        }

        _receiverLevel = targetLevel;
        if (truncated || NeedsLeadingWhitespace(line) || line.ExtraDelimiter || skipped || afterChildren)
        {
            var tag = isCont ? "CONT" : "CONC";
            var path = BuildPath(level, tag);
            AddLineFlags(line, truncated, path, tag);
            if (skipped) Add(GedcomDiagnosticCode.LevelSkipped, path, tag);
            if (afterChildren) Add(GedcomDiagnosticCode.ContinuationAfterChildren, path, tag);
        }

        Account(LineDispositionKind.Folded, target, null);
    }

    private bool NeedsLeadingWhitespace(GedcomLine line) => line.HadLeadingWhitespace && !_leadingWhitespaceReported;

    // Diagnostics any non-blank line can raise: a cut over-long line, the once-per-file leading whitespace note and
    // the once-per-line note that extra spaces were read as one delimiter.
    private void AddLineFlags(GedcomLine line, bool truncated, string? path, string? tag)
    {
        if (truncated)
        {
            Add(GedcomDiagnosticCode.LineTooLong, path, tag);
        }

        if (NeedsLeadingWhitespace(line))
        {
            _leadingWhitespaceReported = true;
            Add(GedcomDiagnosticCode.LeadingWhitespace, path, tag);
        }

        if (line.ExtraDelimiter)
        {
            Add(GedcomDiagnosticCode.ExtraDelimiter, path, tag);
        }
    }

    private GedcomDiagnostic Add(GedcomDiagnosticCode code, string? path, string? tag) =>
        _diagnostics.Add(code, _lineNumber, _recordXref, path, DiagnosticArguments.SafeTag(tag));

    private void Account(LineDispositionKind kind, GedcomStructure? structure, GedcomDiagnostic? diagnostic) =>
        _accounting?.Record(_lineNumber, new LineDisposition(kind, structure, diagnostic));

    // Dot-separated tags of _open[0..count-1], then extraTag; unsafe tags become "?". Null when there is nothing.
    private string? BuildPath(int count, string? extraTag)
    {
        if (count == 0 && extraTag is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0) builder.Append('.');
            builder.Append(DiagnosticArguments.SafeTag(_open[i].Tag) ?? "?");
        }

        if (extraTag is not null)
        {
            if (count > 0) builder.Append('.');
            builder.Append(DiagnosticArguments.SafeTag(extraTag) ?? "?");
        }

        return builder.ToString();
    }

    private void Push(GedcomStructure structure)
    {
        if (_open.Count == _builders.Length)
        {
            Array.Resize(ref _builders, _builders.Length * 2);
            Array.Resize(ref _building, _building.Length * 2);
        }

        _open.Add(structure);
    }

    private void Append(int level, bool newline, ReadOnlySpan<char> text)
    {
        var builder = _builders[level];
        if (!_building[level])
        {
            builder ??= _builders[level] = new StringBuilder();
            builder.Clear();
            builder.Append(_open[level].Value);
            _building[level] = true;
        }

        if (newline)
        {
            builder!.Append('\n');
        }

        builder!.Append(text);
    }

    // Closes _open[count..]: writes back folded values and pops them off the level stack.
    private void CloseFrom(int count)
    {
        for (var i = _open.Count - 1; i >= count; i--)
        {
            if (_building[i])
            {
                var builder = _builders[i]!;
                _open[i].Value = builder.ToString();
                _building[i] = false;
                if (builder.Capacity > LargeBuilderCapacity)
                {
                    _builders[i] = null;
                }
            }
        }

        _open.RemoveRange(count, _open.Count - count);
    }
}

/// <summary>
/// Filters diagnostic arguments so they never carry payload text: only tags of 1 to 31 characters of
/// <c>A-Z a-z 0-9 _</c> and well-formed xrefs of 3 to 22 characters pass.
/// </summary>
internal static class DiagnosticArguments
{
    private const int MaxTagLength = 31;
    private const int MinXrefLength = 3;
    private const int MaxXrefLength = 22;

    /// <summary>Returns <paramref name="tag"/> when it is safe to show in a diagnostic, otherwise null.</summary>
    public static string? SafeTag(string? tag)
    {
        if (tag is null || tag.Length is 0 or > MaxTagLength)
        {
            return null;
        }

        foreach (var c in tag)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                return null;
            }
        }

        return tag;
    }

    /// <summary>Returns <paramref name="xref"/> when it is a well-formed xref safe to show in a diagnostic, otherwise null.</summary>
    public static string? SafeXref(string? xref)
    {
        if (xref is null || xref.Length is < MinXrefLength or > MaxXrefLength || xref[0] != '@' || xref[^1] != '@')
        {
            return null;
        }

        foreach (var c in xref.AsSpan(1, xref.Length - 2))
        {
            if (c == '@' || char.IsWhiteSpace(c))
            {
                return null;
            }
        }

        return xref;
    }
}
