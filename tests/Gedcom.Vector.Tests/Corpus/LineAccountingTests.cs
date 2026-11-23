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
            var report = Check(file, failOnError: false);
            Assert.True(report.Problems.Count == 0, report.Describe());
            Assert.True(report.LineCount > 0, report.FileName + " produced no accounted lines.");
        }
    }

    [SkippableFact]
    public void AccountsForEveryPrivateCorpusLine()
    {
        foreach (var file in PrivateCorpus.Files())
        {
            var report = Check(file, failOnError: true);
            Assert.True(report.Problems.Count == 0, report.Describe());
            Assert.True(report.LineCount > 0, report.FileName + " produced no accounted lines.");
        }
    }

    private static CorpusReport Check(string path, bool failOnError)
    {
        var name = Path.GetFileName(path);
        GedcomReadResult result;
        using (var stream = File.OpenRead(path))
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

        var problems = new List<string>();
        if (LineCountProblem(accounting.LineCount, PhysicalLineCount(path)) is { } countProblem)
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
                    }
                    else if (!reachable.Contains(disposition.Structure))
                    {
                        problems.Add(Describe(line, disposition.Kind + " into a structure not reachable from the document, tag " + TagOf(disposition.Structure)));
                    }

                    break;
                case LineDispositionKind.Diagnostic:
                    if (disposition.Diagnostic is null)
                    {
                        problems.Add(Describe(line, "Diagnostic without a diagnostic"));
                    }
                    else if (!diagnostics.Contains(disposition.Diagnostic))
                    {
                        problems.Add(Describe(line, "Diagnostic not in the document, code GV" + ((int)disposition.Diagnostic.Code).ToString(CultureInfo.InvariantCulture)));
                    }

                    break;
                default:
                    problems.Add(Describe(line, "unknown disposition kind"));
                    break;
            }
        }

        if (failOnError)
        {
            foreach (var diagnostic in document.Diagnostics)
            {
                if (diagnostic.Severity == GedcomDiagnosticSeverity.Error)
                {
                    problems.Add(
                        "Error GV" + ((int)diagnostic.Code).ToString(CultureInfo.InvariantCulture)
                        + " line " + (diagnostic.Line?.ToString(CultureInfo.InvariantCulture) ?? "?")
                        + " record " + (diagnostic.RecordXref ?? "-")
                        + " path " + (diagnostic.TagPath ?? "-"));
                }
            }
        }

        return new CorpusReport(name, accounting.LineCount, problems);
    }

    private static string Describe(int line, string what) => "line " + line.ToString(CultureInfo.InvariantCulture) + ": " + what;

    // Only the structure's own tag, never a value or an xref.
    // Printed only when it looks like a real tag (it can be arbitrary text on a malformed line).
    internal static string TagOf(GedcomStructure structure) => IsSafeTag(structure.Tag) ? structure.Tag : "?";

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

    // Counts physical lines independently of GedcomLineReader: terminators are CRLF, lone CR and LF; a final
    // unterminated line counts, an empty segment after the final terminator does not.
    internal static int CountLines(string text)
    {
        var count = 0;
        var inLine = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r' || c == '\n')
            {
                count++;
                inLine = false;
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else
            {
                inLine = true;
            }
        }

        return inLine ? count + 1 : count;
    }

    private static int PhysicalLineCount(string path)
    {
        using var stream = File.OpenRead(path);
        var encoding = GedcomEncodingDetector.Detect(stream);
        var bytes = File.ReadAllBytes(path);
        var decoder = encoding.Encoding ?? System.Text.Encoding.Latin1;
        return CountLines(decoder.GetString(bytes, encoding.PreambleLength, bytes.Length - encoding.PreambleLength));
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

    private sealed record CorpusReport(string FileName, int LineCount, List<string> Problems)
    {
        public string Describe()
        {
            var text = new StringBuilder();
            text.Append(FileName).Append(": ").Append(Problems.Count.ToString(CultureInfo.InvariantCulture))
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
