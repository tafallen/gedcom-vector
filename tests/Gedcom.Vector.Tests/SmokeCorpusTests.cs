using System;
using System.IO;

using Gedcom.Vector;
using Gedcom.Vector.Gedzip;

using Xunit;

namespace Gedcom.Vector.Tests;

/// <summary>
/// Pins the expected parse outcome of every smoke corpus file so the AOT and JIT builds cannot both be
/// wrong in the same way without a test noticing.
/// </summary>
public class SmokeCorpusTests
{
    [Theory]
    [InlineData("ansel-551.ged", GedcomSpecVersion.Gedcom551, 4, 1)]
    [InlineData("ansi-551.ged", GedcomSpecVersion.Gedcom551, 4, 1)]
    [InlineData("utf16le-551.ged", GedcomSpecVersion.Gedcom551, 4, 1)]
    [InlineData("utf8bom-551.ged", GedcomSpecVersion.Gedcom551, 3, 1)]
    [InlineData("utf8-70.ged", GedcomSpecVersion.Gedcom70, 3, 1)]
    public void ParseSmokeFileHasExpectedSummary(string fileName, GedcomSpecVersion spec, int persons, int families)
    {
        using var stream = File.OpenRead(CorpusPath(fileName));

        var result = new GedcomImportAdapter().Parse(stream);

        Assert.Equal(spec, result.SpecVersion);
        Assert.Equal(persons, result.Persons.Count);
        Assert.Equal(families, result.Families.Count);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ParseSmokeGedzipHasExpectedSummary()
    {
        using var stream = File.OpenRead(CorpusPath("sample-70.gdz"));

        var result = GedzipAdapter.ParseGedzip(stream, new GedcomImportAdapter());

        Assert.Equal(GedcomSpecVersion.Gedcom70, result.SpecVersion);
        Assert.Equal(3, result.Persons.Count);
        Assert.Single(result.Families);
        Assert.Empty(result.Errors);
    }

    private static string CorpusPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gedcom.Vector.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "tests", "corpus", "smoke", fileName);
    }
}
