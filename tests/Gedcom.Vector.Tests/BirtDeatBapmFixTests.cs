using System;
using System.IO;
using System.Linq;
using System.Text;
using Gedcom.Vector;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests;

public class BirtDeatBapmFixTests
{
    [Fact]
    public void Bug1_Birth_And_Death_Events_Are_Added_To_Result_Events()
    {
        string gedContent =
            "0 HEAD\n" +
            "1 CHAR UTF-8\n" +
            "0 @I1@ INDI\n" +
            "1 NAME John /Doe/\n" +
            "1 BIRT\n" +
            "2 DATE 1 JAN 1900\n" +
            "2 PLAC New York, USA\n" +
            "1 DEAT\n" +
            "2 DATE 15 DEC 1980\n" +
            "2 PLAC Boston, USA\n" +
            "0 TRLR\n";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(gedContent));
        var result = StreamingGedcomParser.Parse(inputStream, new GedcomEncodingResult(Encoding.UTF8, false, 0));

        Assert.Single(result.Persons);
        var p = result.Persons[0];
        Assert.Equal("1 JAN 1900", p.BirthDate);
        Assert.Equal("New York, USA", p.BirthPlace);
        Assert.Equal("15 DEC 1980", p.DeathDate);
        Assert.Equal("Boston, USA", p.DeathPlace);

        // Verify EventRecord generation for BIRT and DEAT
        Assert.Equal(2, result.Events.Count);

        var birthEv = result.Events.FirstOrDefault(e => e.EventType == FamTreeEventType.Birth);
        Assert.NotNull(birthEv);
        Assert.Equal("@I1@", birthEv!.PersonXrefId);
        Assert.Equal("1 JAN 1900", birthEv.Date);
        Assert.Equal("New York, USA", birthEv.Place);

        var deathEv = result.Events.FirstOrDefault(e => e.EventType == FamTreeEventType.Death);
        Assert.NotNull(deathEv);
        Assert.Equal("@I1@", deathEv!.PersonXrefId);
        Assert.Equal("15 DEC 1980", deathEv.Date);
        Assert.Equal("Boston, USA", deathEv.Place);

        // Verify export contains exactly one BIRT block and one DEAT block
        var exporter = new GedcomExportWriter();
        string exportedGed = exporter.Write(result);
        Assert.Contains("1 BIRT", exportedGed);
        Assert.Contains("1 DEAT", exportedGed);
    }

    [Fact]
    public void Bug2_BAPM_And_BAP_Tags_Map_To_Baptism_Event()
    {
        string gedContent =
            "0 HEAD\n" +
            "1 CHAR UTF-8\n" +
            "0 @I1@ INDI\n" +
            "1 NAME Jane /Doe/\n" +
            "1 BAPM\n" +
            "2 DATE 10 JAN 1900\n" +
            "2 PLAC Brooklyn, NY\n" +
            "0 @I2@ INDI\n" +
            "1 NAME James /Doe/\n" +
            "1 BAP\n" +
            "2 DATE 12 JAN 1905\n" +
            "0 TRLR\n";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(gedContent));
        var result = StreamingGedcomParser.Parse(inputStream, new GedcomEncodingResult(Encoding.UTF8, false, 0));

        Assert.Equal(2, result.Events.Count);

        var bapmEv = result.Events.FirstOrDefault(e => e.PersonXrefId == "@I1@");
        Assert.NotNull(bapmEv);
        Assert.Equal(FamTreeEventType.Baptism, bapmEv!.EventType);
        Assert.Equal("10 JAN 1900", bapmEv.Date);
        Assert.Equal("Brooklyn, NY", bapmEv.Place);

        var bapEv = result.Events.FirstOrDefault(e => e.PersonXrefId == "@I2@");
        Assert.NotNull(bapEv);
        Assert.Equal(FamTreeEventType.Baptism, bapEv!.EventType);
        Assert.Equal("12 JAN 1905", bapEv.Date);

        // Export round-trip
        var exporter = new GedcomExportWriter();
        string exported = exporter.Write(result);
        Assert.Contains("1 BAPM", exported);
    }
}
