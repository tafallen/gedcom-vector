using System;
using System.IO;
using System.IO.Compression;
using System.Text;

using Gedcom.Vector;
using Gedcom.Vector.Gedzip;
using Gedcom.Vector.Parsing;

using Xunit;

namespace Gedcom.Vector.Tests;

public class Gedcom70AndGedzipTests
{
    [Fact]
    public void Parse_Gedcom70_Header_Sets_SpecVersion_To_Gedcom70()
    {
        string gedContent =
            "0 HEAD\n" +
            "1 GEDC\n" +
            "2 VERS 7.0.0\n" +
            "1 CHAR UTF-8\n" +
            "0 @I1@ INDI\n" +
            "1 NAME John /Doe/\n" +
            "0 TRLR\n";

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(gedContent));
        var result = StreamingGedcomParser.Parse(ms, new GedcomEncodingResult(Encoding.UTF8, false, 0));

        Assert.Equal(GedcomSpecVersion.Gedcom70, result.SpecVersion);
        Assert.Empty(result.Errors);
        Assert.Single(result.Persons);
    }

    [Fact]
    public void Parse_Gedcom70_NonUtf8_Adds_Error()
    {
        string gedContent =
            "0 HEAD\n" +
            "1 GEDC\n" +
            "2 VERS 7.0.0\n" +
            "0 @I1@ INDI\n" +
            "1 NAME John /Doe/\n" +
            "0 TRLR\n";

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(gedContent));
        // Simulate ANSEL encoding result
        var result = StreamingGedcomParser.Parse(ms, new GedcomEncodingResult(Encoding.Latin1, true, 0));

        Assert.Equal(GedcomSpecVersion.Gedcom70, result.SpecVersion);
        Assert.Contains(result.Errors, e => e.Contains("GEDCOM 7.0 requires UTF-8"));
    }

    [Fact]
    public void Export_Gedcom70_Writes_70_Header()
    {
        var result = new GedcomParseResult
        {
            SpecVersion = GedcomSpecVersion.Gedcom70
        };
        result.Persons.Add(new PersonRecord("@I1@", "Jane", "Doe", PersonSex.Female, null, null, null, null));

        var writer = new GedcomExportWriter();
        string exported = writer.Write(result);

        Assert.Contains("2 VERS 7.0.0", exported);
        Assert.DoesNotContain("LINEAGE-LINKED", exported);
    }

    [Fact]
    public void GedzipAdapter_Create_And_Parse_Gedzip_Archive()
    {
        var result = new GedcomParseResult
        {
            SpecVersion = GedcomSpecVersion.Gedcom70
        };
        result.Persons.Add(new PersonRecord("@I1@", "Gedzip", "User", PersonSex.Male, null, null, null, null));

        var writer = new GedcomExportWriter();
        using var gedzipMs = new MemoryStream();

        // Package into .gdz archive
        GedzipAdapter.CreateGedzip(result, writer, gedzipMs);

        Assert.True(gedzipMs.Length > 0);

        // Read .gdz archive back
        gedzipMs.Position = 0;
        var importAdapter = new GedcomImportAdapter();
        var parsedResult = GedzipAdapter.ParseGedzip(gedzipMs, importAdapter);

        Assert.Equal(GedcomSpecVersion.Gedcom70, parsedResult.SpecVersion);
        Assert.Single(parsedResult.Persons);
        Assert.Equal("Gedzip", parsedResult.Persons[0].FirstName);
    }
}
