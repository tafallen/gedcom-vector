using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gedcom.Vector;
using Gedcom.Vector.Builder;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests;

public class AdvancedPerformanceTests
{
    [Fact]
    public void Pillar1_DirectContextSerialization_Exports_Correctly()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Jane", "Doe", PersonSex.Female)
            .AddFamily("@F1@", "@I1@", "@I2@")
            .Build();

        var tree = result.ToContext();
        var writer = new GedcomExportWriter();

        // Export directly from context
        string contextExported = writer.Write(tree);
        string resultExported = writer.Write(result);

        Assert.Equal(resultExported, contextExported);

        using var ms = new MemoryStream();
        writer.Write(tree, ms);
        Assert.True(ms.Length > 0);
    }

    [Fact]
    public async Task Pillar3_ParallelBatchImport_Parses_Multiple_Streams_Concurrently()
    {
        string gedContent =
            "0 HEAD\n" +
            "1 CHAR UTF-8\n" +
            "0 @I1@ INDI\n" +
            "1 NAME Person /One/\n" +
            "0 TRLR\n";

        var streams = Enumerable.Range(0, 10)
            .Select(_ => new MemoryStream(Encoding.UTF8.GetBytes(gedContent)))
            .ToList();

        var adapter = new GedcomImportAdapter();
        var results = await adapter.ParseParallelAsync(streams);

        Assert.Equal(10, results.Length);
        foreach (var r in results)
        {
            Assert.Single(r.Persons);
            Assert.Equal("Person", r.Persons[0].FirstName);
        }
    }

    [Fact]
    public void Pillar4_Utf8GedcomParser_Parses_Utf8_Streams()
    {
        string gedContent =
            "0 HEAD\n" +
            "1 CHAR UTF-8\n" +
            "0 @I1@ INDI\n" +
            "1 NAME Direct /UTF8/\n" +
            "0 TRLR\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(gedContent));
        var result = Utf8GedcomParser.Parse(stream);

        Assert.Single(result.Persons);
        Assert.Equal("Direct", result.Persons[0].FirstName);
        Assert.Equal("UTF8", result.Persons[0].LastName);
    }
}
