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
    /// The inverse of <see cref="Decode"/>: a non-pointer value starting with <c>@</c> gains a leading <c>@</c>, except
    /// a 5.5.1 value starting <c>@#</c> (an escape sequence such as a calendar escape), which stays verbatim.
    /// </summary>
    public static string? Encode(string? value, bool isPointer, GedcomSpecVersion version)
    {
        if (isPointer || value is null || !value.StartsWith('@'))
        {
            return value;
        }

        if (version == GedcomSpecVersion.Gedcom551 && value.StartsWith("@#", StringComparison.Ordinal))
        {
            return value;
        }

        return "@" + value;
    }
}
