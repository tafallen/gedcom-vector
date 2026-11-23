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
}
