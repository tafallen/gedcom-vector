using Gedcom.Vector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom;

public class GedcomImportAdapterLoggingTests
{
    [Fact]
    public void Parse_SuccessfulParse_LogsSummaryAtInformationLevel()
    {
        var mockLogger = new Mock<ILogger<GedcomImportAdapter>>();
        var adapter = new GedcomImportAdapter(mockLogger.Object, Options.Create(new GedcomImportOptions()));

        adapter.Parse(GedcomTestData.ToStream("0 @I1@ INDI\n1 NAME John /Doe/\n"));

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                null,
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void Parse_EmptyInput_DoesNotLogSummary()
    {
        var mockLogger = new Mock<ILogger<GedcomImportAdapter>>();
        var adapter = new GedcomImportAdapter(mockLogger.Object, Options.Create(new GedcomImportOptions()));

        var result = adapter.Parse(GedcomTestData.RawToStream("0 HEAD\n0 TRLR\n"));

        Assert.NotEmpty(result.Errors);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                null,
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Never);
    }
}

