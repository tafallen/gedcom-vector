using System.Globalization;
using System.Text;
using Gedcom.Vector.Diagnostics;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Diagnostics;

public class DiagnosticPrivacyTests
{
    private const string Sentinel = "PRIVATE-SENTINEL-7f3a";

    [Fact]
    public void DiagnosticsNeverContainValues()
    {
        var seen = new HashSet<GedcomDiagnosticCode>();
        var leaks = new List<string>();
        foreach (var text in Documents())
        {
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(text));
            var document = GedcomDocumentReader.Read(stream, new GedcomReadOptions { RecordLineAccounting = true }).Document;
            foreach (var diagnostic in document.Diagnostics)
            {
                seen.Add(diagnostic.Code);
                Check(diagnostic, nameof(diagnostic.Message), diagnostic.Message, leaks);
                Check(diagnostic, nameof(diagnostic.RecordXref), diagnostic.RecordXref, leaks);
                Check(diagnostic, nameof(diagnostic.TagPath), diagnostic.TagPath, leaks);
                Check(diagnostic, nameof(diagnostic.ToString), diagnostic.ToString(), leaks);
            }
        }

        Assert.True(leaks.Count == 0, "The sentinel leaked into diagnostics: " + string.Join("; ", leaks));

        // The test only proves something if every line-level code was actually raised.
        var missing = new List<string>();
        for (var code = (int)GedcomDiagnosticCode.BlankLinesIgnored; code <= (int)GedcomDiagnosticCode.ExtraDelimiter; code++)
        {
            if (!seen.Contains((GedcomDiagnosticCode)code))
            {
                missing.Add("GV" + code.ToString(CultureInfo.InvariantCulture));
            }
        }

        Assert.True(missing.Count == 0, "Line-level codes never raised: " + string.Join(", ", missing));
    }

    private static void Check(GedcomDiagnostic diagnostic, string member, string? text, List<string> leaks)
    {
        if (text is not null && text.Contains(Sentinel, StringComparison.Ordinal))
        {
            leaks.Add("GV" + ((int)diagnostic.Code).ToString(CultureInfo.InvariantCulture) + " " + member);
        }
    }

    private static IEnumerable<string> Documents()
    {
        // 1001 blank, 1002 leading whitespace, 1003 level-less, 1005 missing tag, 1006 invalid tag,
        // 1007 malformed xref, 1008 xref on substructure, 1009 level skipped, 1010 misplaced CONT, 1011 CONT after children,
        // 1013 extra delimiter.
        yield return string.Join(
            "\n",
            "0 HEAD",
            "1 GEDC",
            "2 VERS 5.5.1",
            "",
            "0 @I1@ INDI",
            "1 NAME " + Sentinel,
            "1  NOTE  " + Sentinel,
            "  1 NOTE " + Sentinel,
            Sentinel,
            "1 " + Sentinel + " value",
            "1 PRIVATE-SENTINEL-7f3a",
            "1 _" + new string('A', 199) + " " + Sentinel,
            "1 _" + Sentinel + new string('B', 200 - 1 - Sentinel.Length) + " " + Sentinel,
            "1 @" + Sentinel + "@ NOTE " + Sentinel,
            "1 @X1@ NOTE " + Sentinel,
            "3 DATE " + Sentinel,
            "1 FAMS @F1@",
            "2 CONT " + Sentinel,
            "1 NOTE first",
            "2 _CHILD " + Sentinel,
            "2 CONT " + Sentinel,
            "0 CONT " + Sentinel,
            "0 @" + Sentinel + " INDI",
            "1 " + Sentinel + " " + Sentinel,
            "2 " + Sentinel,
            "0 @" + Sentinel,
            "1 NAME " + Sentinel,
            "0 @" + Sentinel + "@ INDI",
            "1 NAME " + Sentinel,
            "2 " + Sentinel + " " + Sentinel,
            "0 TRLR");

        // 1004: a level-less first line with nothing to append to.
        yield return Sentinel + "\n0 HEAD\n0 TRLR";

        // 1012: an over-long line; the sentinel sits at the start of the value and in the tag position.
        yield return "0 HEAD\n1 NOTE " + Sentinel + new string('x', 1_100_000) + "\n1 _" + Sentinel + new string('y', 1_100_000) + " " + Sentinel + "\n0 TRLR";
    }
}
