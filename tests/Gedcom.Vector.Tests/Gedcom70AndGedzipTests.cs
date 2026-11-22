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
    public void ParseGedcom70HeaderSetsSpecVersionToGedcom70()
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
    public void ParseGedcom70NonUtf8AddsError()
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
    public void ExportGedcom70Writes70Header()
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
    public void GedzipAdapterCreateAndParseGedzipArchive()
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

    [Fact]
    public void CreateGedzipIsDeterministic()
    {
        static byte[] Build()
        {
            var result = new GedcomParseResult { SpecVersion = GedcomSpecVersion.Gedcom70 };
            result.Persons.Add(new PersonRecord("@I1@", "Gedzip", "User", PersonSex.Male, null, null, null, null));

            using var ms = new MemoryStream();
            GedzipAdapter.CreateGedzip(result, new GedcomExportWriter(), ms);
            return ms.ToArray();
        }

        byte[] first = Build();
        byte[] second = Build();

        Assert.Equal(first, second);

        using var archive = new ZipArchive(new MemoryStream(first), ZipArchiveMode.Read);
        var entry = Assert.Single(archive.Entries);
        // Reading back converts DOS time to a local-offset value, so compare the wall-clock part only.
        Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0), entry.LastWriteTime.DateTime);

        // Zone-independent proof: the DOS time and date fields in the first local file header
        // (offsets 10 and 12) must be 00:00:00 and 1980-01-01, whatever the machine's time zone.
        Assert.Equal(0x04034B50u, BitConverter.ToUInt32(first, 0));
        Assert.Equal((ushort)0x0000, BitConverter.ToUInt16(first, 10));
        Assert.Equal((ushort)0x0021, BitConverter.ToUInt16(first, 12));
    }

    private const string MinimalGedcom70 =
        "0 HEAD\n" +
        "1 GEDC\n" +
        "2 VERS 7.0\n" +
        "0 @I1@ INDI\n" +
        "1 NAME Zo\u00EB /Baker/\n" +
        "0 TRLR\n";

    [Fact]
    public void ParseUtf8Gedcom70ReportsNoEncodingError()
    {
        using var ms = new MemoryStream(new UTF8Encoding(false).GetBytes(MinimalGedcom70));
        var result = new GedcomImportAdapter().Parse(ms);

        Assert.Equal(GedcomSpecVersion.Gedcom70, result.SpecVersion);
        Assert.Empty(result.Errors);
        Assert.Single(result.Persons);
    }

    [Fact]
    public void ParseUtf8BomGedcom70ReportsNoEncodingError()
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. new UTF8Encoding(false).GetBytes(MinimalGedcom70)];
        using var ms = new MemoryStream(bytes);
        var result = new GedcomImportAdapter().Parse(ms);

        Assert.Equal(GedcomSpecVersion.Gedcom70, result.SpecVersion);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseAnsiGedcom70StillReportsEncodingError()
    {
        string content = MinimalGedcom70.Replace("2 VERS 7.0\n", "2 VERS 7.0\n1 CHAR ANSI\n", StringComparison.Ordinal);
        using var ms = new MemoryStream(Encoding.Latin1.GetBytes(content));
        var result = new GedcomImportAdapter().Parse(ms);

        Assert.Contains(result.Errors, e => e.Contains("GEDCOM 7.0 requires UTF-8", StringComparison.Ordinal));
    }

    // An unrecognised CHAR value falls back to UTF-8 decoding, but the declaration itself is not UTF-8,
    // so a 7.0 file carrying it must still be reported (found by the push security review, 2026-11-22).
    [Fact]
    public void ParseUnrecognisedCharGedcom70ReportsEncodingError()
    {
        string content = MinimalGedcom70.Replace("2 VERS 7.0\n", "2 VERS 7.0\n1 CHAR MACINTOSH\n", StringComparison.Ordinal);
        using var ms = new MemoryStream(Encoding.Latin1.GetBytes(content));
        var result = new GedcomImportAdapter().Parse(ms);

        Assert.Contains(result.Errors, e => e.Contains("GEDCOM 7.0 requires UTF-8", StringComparison.Ordinal));
    }
}
