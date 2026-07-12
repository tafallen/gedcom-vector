using System.Text.RegularExpressions;

namespace Gedcom.Vector.Parsing;

public static class GedcomLexer
{
    private static readonly Regex LinePattern = new(
        @"^(?<level>\d+) (?:(?<xref>@[^@\s]+@) )?(?<tag>[A-Za-z0-9_]+)(?: (?<value>.*))?$",
        RegexOptions.Compiled);

    public static IEnumerable<GedcomLine> Tokenize(IEnumerable<string> gedcomLines)
    {
        GedcomLine? previous = null;

        foreach (var rawLine in gedcomLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var match = LinePattern.Match(rawLine.TrimEnd());
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["level"].Value, out var level))
            {
                continue;
            }

            var xref = match.Groups["xref"].Success ? match.Groups["xref"].Value : null;
            var tag = match.Groups["tag"].Value;
            var value = match.Groups["value"].Success ? match.Groups["value"].Value : null;

            if (tag is "CONC" or "CONT")
            {
                if (previous != null)
                {
                    var separator = tag == "CONT" ? "\n" : string.Empty;
                    previous = previous with { Value = (previous.Value ?? string.Empty) + separator + (value ?? string.Empty) };
                }
                continue;
            }

            if (previous != null)
            {
                yield return previous;
            }

            previous = new GedcomLine(level, xref, tag, value);
        }

        if (previous != null)
        {
            yield return previous;
        }
    }

    private static void AppendContinuation(List<GedcomLine> lines, string tag, string? value)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var previous = lines[^1];
        var separator = tag == "CONT" ? "\n" : string.Empty;
        lines[^1] = previous with { Value = (previous.Value ?? string.Empty) + separator + (value ?? string.Empty) };
    }
}
