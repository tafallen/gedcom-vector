using System.IO;
using System.Text;
using System.Threading.Tasks;
using Gedcom.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gedcom.Vector.Tests;

public class GedcomExportWriterTests
{
    private readonly GedcomImportAdapter _importer;
    private readonly GedcomExportWriter _exporter;

    public GedcomExportWriterTests()
    {
        _importer = new GedcomImportAdapter(
            NullLogger<GedcomImportAdapter>.Instance,
            Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 10_000_000 })
        );
        _exporter = new GedcomExportWriter();
    }

    [Fact]
    public void Write_ToString_ExportsCorrectly()
    {
        var gedcom = "0 @I1@ INDI\n1 NAME John /Doe/\n2 GIVN John\n2 SURN Doe\n1 SEX M\n0 TRLR";
        using var stream = GedcomTestData.RawToStream(gedcom);
        var parsed = _importer.Parse(stream);

        var output = _exporter.Write(parsed);
        
        Assert.Contains("0 @I1@ INDI", output);
        Assert.Contains("1 NAME John /Doe/", output);
        Assert.Contains("1 SEX M", output);
        Assert.Contains("0 TRLR", output);
    }

    [Fact]
    public void Write_ToStream_ExportsCorrectly()
    {
        var gedcom = "0 @I1@ INDI\n1 NAME John /Doe/\n2 GIVN John\n2 SURN Doe\n1 SEX M\n0 TRLR";
        using var stream = GedcomTestData.RawToStream(gedcom);
        var parsed = _importer.Parse(stream);

        using var outputStream = new MemoryStream();
        _exporter.Write(parsed, outputStream);
        outputStream.Position = 0;

        var reader = new StreamReader(outputStream, Encoding.UTF8);
        var output = reader.ReadToEnd();

        Assert.Contains("0 @I1@ INDI", output);
        Assert.Contains("1 NAME John /Doe/", output);
        Assert.Contains("1 SEX M", output);
        Assert.Contains("0 TRLR", output);
    }

    [Fact]
    public async Task WriteAsync_ToStream_ExportsCorrectly()
    {
        var gedcom = "0 @I1@ INDI\n1 NAME John /Doe/\n2 GIVN John\n2 SURN Doe\n1 SEX M\n0 TRLR";
        using var stream = GedcomTestData.RawToStream(gedcom);
        var parsed = _importer.Parse(stream);

        using var outputStream = new MemoryStream();
        await _exporter.WriteAsync(parsed, outputStream);
        outputStream.Position = 0;

        var reader = new StreamReader(outputStream, Encoding.UTF8);
        var output = await reader.ReadToEndAsync();

        Assert.Contains("0 @I1@ INDI", output);
        Assert.Contains("1 NAME John /Doe/", output);
        Assert.Contains("1 SEX M", output);
        Assert.Contains("0 TRLR", output);
    }

    [Fact]
    public void Export_RoundTrip_MatchesStructurally()
    {
        var gedcom = "0 @I1@ INDI\n1 NAME John /Doe/\n2 GIVN John\n2 SURN Doe\n1 SEX M\n0 @F1@ FAM\n1 HUSB @I1@\n0 TRLR";
        using var stream = GedcomTestData.RawToStream(gedcom);
        var parsed1 = _importer.Parse(stream);

        var exported = _exporter.Write(parsed1);

        using var roundTripStream = GedcomTestData.RawToStream(exported);
        var parsed2 = _importer.Parse(roundTripStream);

        Assert.Equal(parsed1.Persons.Count, parsed2.Persons.Count);
        Assert.Equal(parsed1.Families.Count, parsed2.Families.Count);
        
        Assert.Equal(parsed1.Persons[0].XrefId, parsed2.Persons[0].XrefId);
        Assert.Equal(parsed1.Persons[0].FirstName, parsed2.Persons[0].FirstName);
        Assert.Equal(parsed1.Persons[0].LastName, parsed2.Persons[0].LastName);
        Assert.Equal(parsed1.Persons[0].Sex, parsed2.Persons[0].Sex);
        
        Assert.Equal(parsed1.Families[0].XrefId, parsed2.Families[0].XrefId);
        Assert.Equal(parsed1.Families[0].HusbandXref, parsed2.Families[0].HusbandXref);
    }
}
