namespace Gedcom.Vector.Parsing;

/// <summary>Recognises and converts raw GEDCOM line values.</summary>
internal static class GedcomValueCodec
{
    /// <summary>
    /// True when <paramref name="raw"/> is a pointer such as <c>@I1@</c> or <c>@VOID@</c>: it starts and ends with
    /// <c>@</c>, is at least 3 characters, its second character is neither <c>@</c> nor <c>#</c>, and it contains no
    /// other <c>@</c> and no whitespace.
    /// </summary>
    public static bool IsPointer(ReadOnlySpan<char> raw)
    {
        if (raw.Length < 3 || raw[0] != '@' || raw[^1] != '@' || raw[1] is '@' or '#')
        {
            return false;
        }

        foreach (var c in raw[1..^1])
        {
            if (c == '@' || char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Removes the escape from a raw value: a value starting with <c>@@</c> loses its first <c>@</c>. Nothing else is
    /// rewritten, so mid-value <c>@@</c>, 5.5.1 escapes such as <c>@#DJULIAN@</c> and pointers stay verbatim. The rule
    /// is the same for both versions; <paramref name="version"/> keeps the signature symmetric with <see cref="Encode"/>.
    /// </summary>
    public static string? Decode(string? raw, GedcomSpecVersion version)
    {
        _ = version;
        return raw is not null && raw.StartsWith("@@", StringComparison.Ordinal) ? raw[1..] : raw;
    }

    /// <summary>
    /// The per-line form of <see cref="Decode"/> for a <c>CONT</c> or <c>CONC</c> line value: a segment starting with
    /// <c>@@</c> loses its first <c>@</c>. Every line value is escaped on its own, so each continuation line is
    /// decoded on its own too.
    /// </summary>
    public static ReadOnlySpan<char> DecodeSegment(ReadOnlySpan<char> raw) =>
        raw.StartsWith("@@", StringComparison.Ordinal) ? raw[1..] : raw;

    /// <summary>
    /// The inverse of <see cref="Decode"/>: a non-pointer value starting with <c>@</c> gains a leading <c>@</c>, except
    /// a 5.5.1 value starting <c>@#</c> (an escape sequence such as a calendar escape), which stays verbatim.
    /// </summary>
    public static string? Encode(string? value, bool isPointer, GedcomSpecVersion version) =>
        value is not null && NeedsEscape(value, isPointer, version) ? "@" + value : value;

    /// <summary>
    /// True when the line value <paramref name="value"/> (the first line of a value, or one continuation segment)
    /// must gain a leading <c>@</c> when written; see <see cref="Encode"/>.
    /// </summary>
    public static bool NeedsEscape(ReadOnlySpan<char> value, bool isPointer, GedcomSpecVersion version) =>
        !isPointer
        && value.StartsWith('@')
        && !(version == GedcomSpecVersion.Gedcom551 && value.StartsWith("@#", StringComparison.Ordinal));
}
