using System.Text;
using Gedcom.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom;

public class GedcomImportAdapterSizeLimitTests
{
    [Fact]
    public void Parse_FileUnderMaxSize_ParsesNormally()
    {
        var options = Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 1024 });
        var adapter = new GedcomImportAdapter(NullLogger<GedcomImportAdapter>.Instance, options);
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME John /Doe/\n0 TRLR\n");

        var result = adapter.Parse(new MemoryStream(bytes));

        Assert.Single(result.Persons);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_FileOverMaxSize_ReportsErrorWithoutThrowing()
    {
        var options = Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 10 });
        var adapter = new GedcomImportAdapter(NullLogger<GedcomImportAdapter>.Instance, options);
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME John /Doe/\n0 TRLR\n");

        var result = adapter.Parse(new MemoryStream(bytes));

        Assert.Empty(result.Persons);
        Assert.Empty(result.Families);
        Assert.Contains(result.Errors, e => e.Contains("exceeding the maximum supported size"));
    }
}

