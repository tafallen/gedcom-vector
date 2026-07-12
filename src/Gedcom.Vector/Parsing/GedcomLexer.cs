using System.Text;

namespace Gedcom.Vector.Parsing;

public static class GedcomLexer
{
    public static IEnumerable<GedcomLine> Tokenize(IEnumerable<string> gedcomLines)
    {
        int currentLevel = 0;
        string? currentXref = null;
        string? currentTag = null;
        string? currentValue = null;
        StringBuilder? currentValueBuilder = null;
        bool hasCurrent = false;

        foreach (var rawLine in gedcomLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            if (!TryParseLine(rawLine.AsSpan().TrimEnd(), out int level, out string? xref, out string tag, out string? value))
            {
                continue;
            }

            if (tag is "CONC" or "CONT")
            {
                if (hasCurrent)
                {
                    if (currentValueBuilder == null)
                    {
                        currentValueBuilder = new StringBuilder(currentValue ?? string.Empty);
                    }
                    
                    if (tag == "CONT")
                    {
                        currentValueBuilder.Append('\n');
                    }
                    if (value != null)
                    {
                        currentValueBuilder.Append(value);
                    }
                }
                continue;
            }

            if (hasCurrent)
            {
                yield return new GedcomLine(
                    currentLevel, 
                    currentXref, 
                    currentTag!, 
                    currentValueBuilder != null ? currentValueBuilder.ToString() : currentValue);
            }

            currentLevel = level;
            currentXref = xref;
            currentTag = tag;
            currentValue = value;
            currentValueBuilder = null;
            hasCurrent = true;
        }

        if (hasCurrent)
        {
            yield return new GedcomLine(
                currentLevel, 
                currentXref, 
                currentTag!, 
                currentValueBuilder != null ? currentValueBuilder.ToString() : currentValue);
        }
    }

    private static bool TryParseLine(
        ReadOnlySpan<char> span, 
        out int level, 
        out string? xref, 
        out string tag, 
        out string? value)
    {
        level = 0;
        xref = null;
        tag = string.Empty;
        value = null;

        int space1 = span.IndexOf(' ');
        if (space1 < 0)
        {
            return false;
        }

        var levelSpan = span.Slice(0, space1);
        if (!int.TryParse(levelSpan, out level))
        {
            return false;
        }

        var rest = span.Slice(space1 + 1);
        if (rest.IsEmpty)
        {
            return false;
        }

        if (rest.StartsWith("@"))
        {
            int nextAt = rest.Slice(1).IndexOf('@');
            if (nextAt < 0)
            {
                return false;
            }

            int xrefLength = nextAt + 2;
            if (rest.Length <= xrefLength || rest[xrefLength] != ' ')
            {
                return false;
            }

            xref = rest.Slice(0, xrefLength).ToString();
            rest = rest.Slice(xrefLength + 1);
        }

        int space2 = rest.IndexOf(' ');
        if (space2 < 0)
        {
            var tagSpan = rest;
            if (!IsValidTag(tagSpan))
            {
                return false;
            }
            tag = tagSpan.ToString();
        }
        else
        {
            var tagSpan = rest.Slice(0, space2);
            if (!IsValidTag(tagSpan))
            {
                return false;
            }
            tag = tagSpan.ToString();
            value = rest.Slice(space2 + 1).ToString();
        }

        return true;
    }

    private static bool IsValidTag(ReadOnlySpan<char> tag)
    {
        if (tag.IsEmpty)
        {
            return false;
        }

        foreach (var c in tag)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
