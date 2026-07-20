using System;
using System.IO;
using System.Linq;
using System.Text;
using Gedcom.Vector;
using Gedcom.Vector.Builder;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests;

public class TechnicalDebtFixesTests
{
    [Fact]
    public void TD01_O1_MediaUnlinking_In_GedcomTreeContext_DeletePerson_And_DeleteFamily()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "Father", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Mother", "Doe", PersonSex.Female)
            .AddFamily("@F1@", "@I1@", "@I2@")
            .AddMedia("@M1@", "Family Photo")
                .LinkTo("@I1@")
                .LinkTo("@F1@")
            .Build();

        var tree = result.ToContext();

        // Verify media link before deletion
        var father = tree.GetPerson("@I1@");
        Assert.NotNull(father);
        Assert.Single(tree.MediaFor(father!.XrefId));

        // TD-01: Delete person unlinks media directly via _mediaByEntityId
        tree.DeletePerson("@I1@");
        Assert.Empty(tree.MediaFor("@I1@"));
        var links = result.Media[0].LinkedXrefIds;
        Assert.DoesNotContain("@I1@", links);
        Assert.Contains("@F1@", links);

        // TD-01: Delete family unlinks media directly via _mediaByEntityId
        tree.DeleteFamily("@F1@");
        Assert.Empty(tree.MediaFor("@F1@"));
        Assert.Empty(result.Media[0].LinkedXrefIds);
    }

    [Fact]
    public void TD02_GedcomStringPool_Clear_ResetsPool()
    {
        var pool = new GedcomStringPool();
        string? s1 = pool.GetOrAdd("INDI");
        string? s2 = pool.GetOrAdd("FAM");

        Assert.Equal(2, pool.Count);
        Assert.Same(s1, pool.GetOrAdd("INDI"));

        // Clear pool
        pool.Clear();
        Assert.Equal(0, pool.Count);

        string? s3 = pool.GetOrAdd("INDI");
        Assert.Equal(1, pool.Count);
        Assert.Equal("INDI", s3);
    }

    [Fact]
    public void TD03_TD04_Lossless_Unparsed_CustomRecords_RoundTrip()
    {
        string gedContent =
            "0 HEAD\n" +
            "1 CHAR UTF-8\n" +
            "0 @SUBM1@ SUBM\n" +
            "1 NAME Jane Submitter\n" +
            "1 ADDR 123 Main St\n" +
            "0 @I1@ INDI\n" +
            "1 NAME John /Doe/\n" +
            "0 _CUSTOM Custom Vendor Record\n" +
            "1 DATA Extra Information\n" +
            "0 TRLR\n";

        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(gedContent));
        var parserResult = StreamingGedcomParser.Parse(inputStream, new GedcomEncodingResult(Encoding.UTF8, false, 0));

        // Verify unparsed custom records captured
        Assert.Equal(2, parserResult.UnparsedRecords.Count);

        var subm = parserResult.UnparsedRecords.First(r => r.Tag == "SUBM");
        Assert.Equal("@SUBM1@", subm.XrefId);
        Assert.Null(subm.Value);
        Assert.NotNull(subm.RawLines);
        Assert.Equal(2, subm.RawLines.Count);
        Assert.Equal("1 NAME Jane Submitter", subm.RawLines[0]);
        Assert.Equal("1 ADDR 123 Main St", subm.RawLines[1]);

        var custom = parserResult.UnparsedRecords.First(r => r.Tag == "_CUSTOM");
        Assert.Null(custom.XrefId);
        Assert.Equal("Custom Vendor Record", custom.Value);
        Assert.NotNull(custom.RawLines);
        Assert.Single(custom.RawLines);
        Assert.Equal("1 DATA Extra Information", custom.RawLines[0]);

        // Verify 100% lossless export round-tripping
        var exporter = new GedcomExportWriter();
        string exportedGed = exporter.Write(parserResult);

        Assert.Contains("0 @SUBM1@ SUBM", exportedGed);
        Assert.Contains("1 NAME Jane Submitter", exportedGed);
        Assert.Contains("1 ADDR 123 Main St", exportedGed);
        Assert.Contains("0 _CUSTOM Custom Vendor Record", exportedGed);
        Assert.Contains("1 DATA Extra Information", exportedGed);
    }
}
