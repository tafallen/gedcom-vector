namespace Gedcom.Vector.Parsing;

/// <summary>The shape of a tokenized GEDCOM line.</summary>
internal enum GedcomLineKind
{
    /// <summary>Level, optional xref, tag and optional value were found.</summary>
    Structure,

    /// <summary>The line is empty or only spaces and tabs.</summary>
    Blank,

    /// <summary>The line does not start with a level.</summary>
    NoLevel,

    /// <summary>A level was found but no tag follows it.</summary>
    NoTag,
}

/// <summary>
/// One tokenized GEDCOM line. All spans point into the tokenized input. For <see cref="GedcomLineKind.NoTag"/>
/// the level and xref are filled in as far as they were found.
/// </summary>
internal readonly ref struct GedcomLine
{
    public GedcomLine(
        GedcomLineKind kind,
        ReadOnlySpan<char> text,
        bool hadLeadingWhitespace,
        int level = 0,
        ReadOnlySpan<char> xref = default,
        ReadOnlySpan<char> tag = default,
        ReadOnlySpan<char> value = default,
        bool hasValue = false,
        bool xrefMalformed = false,
        bool tagInvalid = false,
        bool extraDelimiter = false)
    {
        Kind = kind;
        Text = text;
        HadLeadingWhitespace = hadLeadingWhitespace;
        Level = level;
        Xref = xref;
        Tag = tag;
        Value = value;
        HasValue = hasValue;
        XrefMalformed = xrefMalformed;
        TagInvalid = tagInvalid;
        ExtraDelimiter = extraDelimiter;
    }

    public GedcomLineKind Kind { get; }

    /// <summary>The whole input line after leading whitespace is skipped.</summary>
    public ReadOnlySpan<char> Text { get; }

    public bool HadLeadingWhitespace { get; }

    public int Level { get; }

    public ReadOnlySpan<char> Xref { get; }

    public ReadOnlySpan<char> Tag { get; }

    public ReadOnlySpan<char> Value { get; }

    public bool HasValue { get; }

    public bool XrefMalformed { get; }

    public bool TagInvalid { get; }

    /// <summary>More than one space separated the level, xref or tag from the token after it.</summary>
    public bool ExtraDelimiter { get; }
}

/// <summary>
/// Splits a GEDCOM line into level, xref, tag and value with span operations only. Raises no diagnostics. A run of
/// spaces between level and xref, xref and tag, or level and tag counts as one delimiter (flagged as
/// <see cref="GedcomLine.ExtraDelimiter"/>); the value starts after exactly one space following the tag, so its own
/// leading spaces are kept.
/// </summary>
internal static class GedcomLineTokenizer
{
    private const int MaxLevelDigits = 3;

    public static GedcomLine Tokenize(ReadOnlySpan<char> line)
    {
        var start = 0;
        while (start < line.Length && line[start] is ' ' or '\t')
        {
            start++;
        }

        var hadLeading = start > 0;
        var text = line[start..];
        if (text.IsEmpty)
        {
            return new GedcomLine(GedcomLineKind.Blank, text, hadLeading);
        }

        var digits = 0;
        while (digits < text.Length && char.IsAsciiDigit(text[digits]))
        {
            digits++;
        }

        if (digits is 0 or > MaxLevelDigits || (digits < text.Length && text[digits] != ' '))
        {
            return new GedcomLine(GedcomLineKind.NoLevel, text, hadLeading);
        }

        var level = 0;
        for (var i = 0; i < digits; i++)
        {
            level = (level * 10) + (text[i] - '0');
        }

        if (digits == text.Length)
        {
            return new GedcomLine(GedcomLineKind.NoTag, text, hadLeading, level);
        }

        var extraDelimiter = false;
        var rest = SkipDelimiter(text[digits..], ref extraDelimiter);
        var xref = default(ReadOnlySpan<char>);
        var xrefMalformed = false;
        if (!rest.IsEmpty && rest[0] == '@')
        {
            var end = rest.IndexOf(' ');
            xref = end < 0 ? rest : rest[..end];
            xrefMalformed = xref.Length < 3 || xref[^1] != '@' || xref[1..^1].Contains('@');
            if (end < 0)
            {
                return new GedcomLine(GedcomLineKind.NoTag, text, hadLeading, level, xref, xrefMalformed: xrefMalformed, extraDelimiter: extraDelimiter);
            }

            rest = SkipDelimiter(rest[end..], ref extraDelimiter);
        }

        var tagEnd = rest.IndexOf(' ');
        var tag = tagEnd < 0 ? rest : rest[..tagEnd];
        if (tag.IsEmpty)
        {
            return new GedcomLine(GedcomLineKind.NoTag, text, hadLeading, level, xref, xrefMalformed: xrefMalformed, extraDelimiter: extraDelimiter);
        }

        var tagInvalid = false;
        foreach (var c in tag)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                tagInvalid = true;
                break;
            }
        }

        var value = tagEnd < 0 ? default : rest[(tagEnd + 1)..];
        return new GedcomLine(
            GedcomLineKind.Structure,
            text,
            hadLeading,
            level,
            xref,
            tag,
            value,
            !value.IsEmpty,
            xrefMalformed,
            tagInvalid,
            extraDelimiter);
    }

    // Skips the run of spaces that starts span; sets extra when the run is longer than one space and a token follows.
    private static ReadOnlySpan<char> SkipDelimiter(ReadOnlySpan<char> span, scoped ref bool extra)
    {
        var count = 0;
        while (count < span.Length && span[count] == ' ')
        {
            count++;
        }

        if (count > 1 && count < span.Length)
        {
            extra = true;
        }

        return span[count..];
    }
}
