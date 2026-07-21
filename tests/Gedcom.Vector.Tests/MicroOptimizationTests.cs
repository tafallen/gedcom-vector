using System;
using System.IO;
using System.Text;
using Gedcom.Vector;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests;

public class MicroOptimizationTests
{
    [Fact]
    public void MicroOpt1_TagByEventTypeArray_Serializes_All_Event_Types()
    {
        var result = new GedcomParseResult();
        var p = new PersonRecord("@I1@", "John", "Doe", PersonSex.Male, null, null, null, null);
        result.Persons.Add(p);

        foreach (FamTreeEventType ev in Enum.GetValues<FamTreeEventType>())
        {
            if (ev == FamTreeEventType.Birth || ev == FamTreeEventType.Death) continue;
            result.Events.Add(new EventRecord("@I1@", ev, "1 JAN 1900", "City"));
        }

        var exporter = new GedcomExportWriter();
        string exported = exporter.Write(result);

        Assert.Contains("1 CENS", exported);
        Assert.Contains("1 IMMI", exported);
        Assert.Contains("1 EMIG", exported);
        Assert.Contains("1 RESI", exported);
        Assert.Contains("1 CHR", exported);
        Assert.Contains("1 BURI", exported);
        Assert.Contains("1 BAPM", exported);
    }

    [Fact]
    public void MicroOpt2_LazyConcBuilder_Handles_CONC_And_CONT_Correctly()
    {
        string gedContent =
            "0 HEAD\n" +
            "1 CHAR UTF-8\n" +
            "0 @I1@ INDI\n" +
            "1 NAME Alexander /The Great/\n" +
            "2 CONC  Emperor of Macedonia\n" +
            "2 CONT Line 2 of Name\n" +
            "0 TRLR\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(gedContent));
        var result = StreamingGedcomParser.Parse(stream, new GedcomEncodingResult(Encoding.UTF8, false, 0));

        Assert.Single(result.Persons);
        Assert.Equal("The Great Emperor of Macedonia\nLine 2 of Name", result.Persons[0].LastName);
    }

    [Fact]
    public void MicroOpt3_AnselDecoder_FastPath_And_Special_Chars()
    {
        // Standard ASCII
        Assert.Equal("Hello World", AnselDecoder.Decode("Hello World"));

        // ANSEL combining acute accent on e (0xE2 + e)
        byte[] bytes = new byte[] { 0xE2, (byte)'e' };
        string decoded = AnselDecoder.Decode(bytes);
        Assert.Equal("é", decoded);
    }
}
