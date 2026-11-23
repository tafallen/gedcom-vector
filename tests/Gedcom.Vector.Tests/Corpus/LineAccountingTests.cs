using System.Globalization;
using System.Text;
using Gedcom.Vector.Diagnostics;
using Gedcom.Vector.Model;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Corpus;

public class LineAccountingTests
{
    private const int MaxReported = 20;

    [Fact]
    public void AccountsForEverySmokeLine()
    {
        var files = PrivateCorpus.SmokeFiles();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var report = Check(Path.GetFileName(file), File.ReadAllBytes(file), failOnError: false);
            Assert.True(report.Problems.Count == 0, report.Describe());
            Assert.True(report.LineCount > 0, report.Label + " produced no accounted lines.");
        }
    }

    [SkippableFact]
    public void AccountsForEveryPrivateCorpusLine()
    {
        foreach (var file in PrivateCorpus.Files())
        {
            var report = Check(Path.GetFileName(file), File.ReadAllBytes(file), failOnError: true);
            Assert.True(report.Problems.Count == 0, report.Describe());
            Assert.True(report.LineCount > 0, report.Label + " produced no accounted lines.");
        }
    }

    [Fact]
    public void ContentCheckBitesOnLostContent()
    {
        var head = Split("1 NAME John /Smith/");
        var name = new GedcomStructure { Tag = "NAME", Value = "John /Smith/" };
        var structure = new LineDisposition(LineDispositionKind.Structure, name, null);
        var problems = new List<string>();

        CheckContent(1, head, "1 NAME John /Smith/", structure, isAnsel: false, problems);
        Assert.Empty(problems);

        CheckContent(1, head, "1 NAME John /Smith/", new LineDisposition(LineDispositionKind.Structure, new GedcomStructure { Tag = "", Value = "John /Smith/" }, null), isAnsel: false, problems);
        CheckContent(1, head, "1 NAME John /Smith/", new LineDisposition(LineDispositionKind.Structure, new GedcomStructure { Tag = "NAME", Value = "John" }, null), isAnsel: false, problems);
        CheckContent(2, Split("2 CONC xyz"), "2 CONC xyz", new LineDisposition(LineDispositionKind.Folded, name, null), isAnsel: false, problems);
        CheckContent(3, Split("  lost text"), "  lost text", new LineDisposition(LineDispositionKind.Folded, new GedcomStructure { Tag = "NOTE", Value = "a\nlost text" }, null), isAnsel: false, problems);
        CheckContent(4, Split("stray"), "stray", new LineDisposition(LineDispositionKind.Diagnostic, null, new DiagnosticBag(1).Add(GedcomDiagnosticCode.BlankLinesIgnored)), isAnsel: false, problems);

        Assert.Equal(5, problems.Count);
        Assert.All(problems, p => Assert.DoesNotContain("John", p, StringComparison.Ordinal));
        Assert.All(problems, p => Assert.DoesNotContain("lost", p, StringComparison.Ordinal));
    }

    private static CorpusReport Check(string label, byte[] bytes, bool failOnError)
    {
        GedcomReadResult result;
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            result = GedcomDocumentReader.Read(stream, new GedcomReadOptions { RecordLineAccounting = true });
        }

        var document = result.Document;
        var accounting = result.Accounting;
        Assert.NotNull(accounting);

        var reachable = new HashSet<GedcomStructure>(ReferenceEqualityComparer.Instance);
        foreach (var record in document.ExtensionRecords)
        {
            Collect(record.Structure, reachable);
        }

        var diagnostics = new HashSet<GedcomDiagnostic>(ReferenceEqualityComparer.Instance);
        foreach (var diagnostic in document.Diagnostics)
        {
            diagnostics.Add(diagnostic);
        }

        var (physicalLines, isAnsel) = PhysicalLines(bytes);
        var problems = new List<string>();
        if (LineCountProblem(accounting.LineCount, physicalLines.Count) is { } countProblem)
        {
            problems.Add(countProblem);
        }

        for (var line = 1; line <= accounting.LineCount; line++)
        {
            if (accounting[line] is not { } disposition)
            {
                problems.Add(Describe(line, "no disposition"));
                continue;
            }

            switch (disposition.Kind)
            {
                case LineDispositionKind.Structure:
                case LineDispositionKind.Folded:
                    if (disposition.Structure is null)
                    {
                        problems.Add(Describe(line, disposition.Kind + " without structure"));
                        continue;
                    }

                    if (!reachable.Contains(disposition.Structure))
                    {
                        problems.Add(Describe(line, disposition.Kind + " into a structure not reachable from the document, tag " + TagOf(disposition.Structure)));
                        continue;
                    }

                    break;
                case LineDispositionKind.Diagnostic:
                    if (disposition.Diagnostic is null)
                    {
                        problems.Add(Describe(line, "Diagnostic without a diagnostic"));
                        continue;
                    }

                    if (!diagnostics.Contains(disposition.Diagnostic))
                    {
                        problems.Add(Describe(line, "Diagnostic not in the document, code " + Code(disposition.Diagnostic)));
                        continue;
                    }

                    break;
                default:
                    problems.Add(Describe(line, "unknown disposition kind"));
                    continue;
            }

            if (line <= physicalLines.Count)
            {
                var physical = physicalLines[line - 1];
                if (physical.Length > GedcomLineReader.DefaultMaxLineLength)
                {
                    physical = physical[..GedcomLineReader.DefaultMaxLineLength];
                }

                CheckContent(line, Split(physical), physical, disposition, isAnsel, problems);
            }
        }

        if (failOnError)
        {
            foreach (var diagnostic in document.Diagnostics)
            {
                if (diagnostic.Severity == GedcomDiagnosticSeverity.Error)
                {
                    problems.Add(
                        "Error " + Code(diagnostic)
                        + " line " + (diagnostic.Line?.ToString(CultureInfo.InvariantCulture) ?? "?")
                        + " record " + (diagnostic.RecordXref ?? "-")
                        + " path " + (diagnostic.TagPath ?? "-"));
                }
            }
        }

        return new CorpusReport(label, accounting.LineCount, problems);
    }

    // Proves the line's content arrived where its disposition says. Problems name line numbers, disposition kinds,
    // codes and safe tags only, never values or xrefs.
    private static void CheckContent(int line, SplitLine split, string physical, LineDisposition disposition, bool isAnsel, List<string> problems)
    {
        var structure = disposition.Structure;
        switch (disposition.Kind)
        {
            case LineDispositionKind.Diagnostic:
                if (!split.Blank)
                {
                    problems.Add(Describe(line, "a non-blank line is accounted only by diagnostic " + Code(disposition.Diagnostic!)));
                }

                break;

            case LineDispositionKind.Structure when !split.HasLevel:
                // A level-less line with nothing before it becomes a placeholder record holding the whole line.
                if (structure!.Tag.Length != 0)
                {
                    problems.Add(Describe(line, "Structure from a level-less line has tag " + TagOf(structure) + ", expected a placeholder"));
                }
                else if (!Normalize(structure.Value).StartsWith(ExpectedPrefix(FirstLineValue(physical, isAnsel)), StringComparison.Ordinal))
                {
                    problems.Add(Describe(line, "Structure from a level-less line does not hold the line's text"));
                }

                break;

            case LineDispositionKind.Structure:
                if (!string.Equals(structure!.Tag, split.Tag, StringComparison.Ordinal))
                {
                    problems.Add(Describe(line, "Structure tag " + TagOf(structure) + " differs from the line's tag " + SafeTag(split.Tag)));
                }

                if (!string.Equals(structure.Xref, split.Xref, StringComparison.Ordinal))
                {
                    problems.Add(Describe(line, "Structure xref differs from the line's xref, tag " + TagOf(structure)));
                }

                if (split.Value is { } value
                    && !Normalize(structure.Value).StartsWith(ExpectedPrefix(FirstLineValue(value, isAnsel)), StringComparison.Ordinal))
                {
                    problems.Add(Describe(line, "Structure value does not start with the line's value, tag " + TagOf(structure)));
                }

                break;

            case LineDispositionKind.Folded when !split.HasLevel:
                if (!Normalize(structure!.Value).Contains(ExpectedPrefix(Ansel(physical, isAnsel)), StringComparison.Ordinal))
                {
                    problems.Add(Describe(line, "Folded level-less line text is not in the value of tag " + TagOf(structure)));
                }

                break;

            case LineDispositionKind.Folded:
                if (split.Tag is not ("CONC" or "CONT"))
                {
                    problems.Add(Describe(line, "Folded line has tag " + SafeTag(split.Tag) + ", not CONC or CONT"));
                }
                else if (split.Value is { } folded
                    && !Normalize(structure!.Value).Contains(ExpectedPrefix(FirstLineValue(folded, isAnsel)), StringComparison.Ordinal))
                {
                    problems.Add(Describe(line, split.Tag + " text is not in the value of tag " + TagOf(structure)));
                }

                break;
        }
    }

    // The line value as it reads after decoding: ANSEL first, then a leading "@@" loses one "@".
    private static string FirstLineValue(string raw, bool isAnsel)
    {
        var decoded = Ansel(raw, isAnsel);
        return decoded.StartsWith("@@", StringComparison.Ordinal) ? decoded[1..] : decoded;
    }

    private static string Ansel(string raw, bool isAnsel) => isAnsel ? AnselDecoder.Decode(raw) : raw;

    private static string Normalize(string? value) => (value ?? "").Normalize(NormalizationForm.FormD);

    // Decomposed, without trailing combining marks: an ANSEL mark at the end of a line composes with the next line.
    private static string ExpectedPrefix(string value)
    {
        var normalized = Normalize(value);
        var end = normalized.Length;
        while (end > 0 && CharUnicodeInfo.GetUnicodeCategory(normalized[end - 1]) == UnicodeCategory.NonSpacingMark)
        {
            end--;
        }

        return normalized[..end];
    }

    internal readonly record struct SplitLine(bool Blank, bool HasLevel, string? Xref, string Tag, string? Value);

    // Splits a line independently of the tokenizer: optional leading spaces and tabs, a 1 to 3 digit level, runs of
    // spaces between level, xref and tag, an optional @xref, the tag, then the value after exactly one space.
    internal static SplitLine Split(string line)
    {
        var i = 0;
        while (i < line.Length && line[i] is ' ' or '\t')
        {
            i++;
        }

        if (i == line.Length)
        {
            return new SplitLine(true, false, null, "", null);
        }

        var start = i;
        while (i < line.Length && i - start < 4 && char.IsAsciiDigit(line[i]))
        {
            i++;
        }

        if (i - start is 0 or > 3 || (i < line.Length && line[i] != ' '))
        {
            return new SplitLine(false, false, null, "", null);
        }

        i = SkipSpaces(line, i);
        string? xref = null;
        if (i < line.Length && line[i] == '@')
        {
            var end = NextSpace(line, i);
            xref = line[i..end];
            i = SkipSpaces(line, end);
        }

        var tagEnd = NextSpace(line, i);
        var value = tagEnd + 1 < line.Length ? line[(tagEnd + 1)..] : null;
        return new SplitLine(false, true, xref, line[i..tagEnd], value);
    }

    private static int SkipSpaces(string line, int i)
    {
        while (i < line.Length && line[i] == ' ')
        {
            i++;
        }

        return i;
    }

    private static int NextSpace(string line, int i)
    {
        var space = line.IndexOf(' ', i);
        return space < 0 ? line.Length : space;
    }

    private static string Code(GedcomDiagnostic diagnostic) => "GV" + ((int)diagnostic.Code).ToString(CultureInfo.InvariantCulture);

    private static string Describe(int line, string what) => "line " + line.ToString(CultureInfo.InvariantCulture) + ": " + what;

    // Only the structure's own tag, never a value or an xref.
    // Printed only when it looks like a real tag (it can be arbitrary text on a malformed line).
    internal static string TagOf(GedcomStructure structure) => SafeTag(structure.Tag);

    private static string SafeTag(string tag) => IsSafeTag(tag) ? tag : "?";

    private static bool IsSafeTag(string tag)
    {
        if (tag.Length is 0 or > 15 || !(tag[0] is (>= 'A' and <= 'Z') or '_'))
        {
            return false;
        }

        foreach (var c in tag)
        {
            if (!(c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return true;
    }

    // Splits physical lines independently of GedcomLineReader: terminators are CRLF, lone CR and LF; a final
    // unterminated line counts, an empty segment after the final terminator does not.
    internal static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r' || c == '\n')
            {
                lines.Add(text[start..i]);
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    internal static int CountLines(string text) => SplitLines(text).Count;

    private static (List<string> Lines, bool IsAnsel) PhysicalLines(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var encoding = GedcomEncodingDetector.Detect(stream);
        var decoder = encoding.Encoding ?? Encoding.Latin1;
        return (SplitLines(decoder.GetString(bytes, encoding.PreambleLength, bytes.Length - encoding.PreambleLength)), encoding.IsAnsel);
    }

    // Counts only; null when the accounted count matches the physical count.
    internal static string? LineCountProblem(int accounted, int physical) =>
        accounted == physical
            ? null
            : "accounted line count " + accounted.ToString(CultureInfo.InvariantCulture)
                + " differs from physical line count " + physical.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void LineCountCheckBitesOnADroppedTail()
    {
        Assert.Null(LineCountProblem(10, 10));
        Assert.NotNull(LineCountProblem(9, 10));
        Assert.Equal(3, CountLines("a\r\nb\rc\n"));
        Assert.Equal(4, CountLines("a\nb\n\nc"));
        Assert.Equal(0, CountLines(string.Empty));
        Assert.Equal(2, CountLines("a\n\n"));
        Assert.Equal(["a", "", "b c"], SplitLines("a\r\n\rb c").ToArray());
    }

    [Theory]
    [InlineData("1 NAME John /Smith/", true, null, "NAME", "John /Smith/")]
    [InlineData("1  NAME John /Smith/", true, null, "NAME", "John /Smith/")]
    [InlineData("0 @I1@  INDI", true, "@I1@", "INDI", null)]
    [InlineData("0   @I1@   INDI  x", true, "@I1@", "INDI", " x")]
    [InlineData("1 NOTE ", true, null, "NOTE", null)]
    [InlineData("1", true, null, "", null)]
    [InlineData("0 @I1@ ", true, "@I1@", "", null)]
    [InlineData("1\tNAME x", false, null, "", null)]
    [InlineData("1234 NAME", false, null, "", null)]
    public void SplitsLinesIndependently(string line, bool hasLevel, string? xref, string tag, string? value)
    {
        var split = Split(line);

        Assert.False(split.Blank);
        Assert.Equal(hasLevel, split.HasLevel);
        Assert.Equal(xref, split.Xref);
        Assert.Equal(tag, split.Tag);
        Assert.Equal(value, split.Value);
    }

    private static void Collect(GedcomStructure root, HashSet<GedcomStructure> into)
    {
        var stack = new Stack<GedcomStructure>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            into.Add(current);
            foreach (var child in current.Children)
            {
                stack.Push(child);
            }
        }
    }

    private sealed record CorpusReport(string Label, int LineCount, List<string> Problems)
    {
        public string Describe()
        {
            var text = new StringBuilder();
            text.Append(Label).Append(": ").Append(Problems.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" offending lines of ").Append(LineCount.ToString(CultureInfo.InvariantCulture))
                .Append(". First ").Append(Math.Min(MaxReported, Problems.Count).ToString(CultureInfo.InvariantCulture)).Append(':');
            foreach (var problem in Problems.Take(MaxReported))
            {
                text.Append("\n  ").Append(problem);
            }

            return text.ToString();
        }
    }
}
