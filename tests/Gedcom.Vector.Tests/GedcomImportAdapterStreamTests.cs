using Gedcom.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom;

public class GedcomImportAdapterStreamTests
{
    [Fact]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        var options = Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 1024 });
        var adapter = new GedcomImportAdapter(NullLogger<GedcomImportAdapter>.Instance, options);

        Assert.Throws<ArgumentNullException>(() => adapter.Parse(null!));
    }

    [Fact]
    public void Parse_NonSeekableStream_ThrowsArgumentException()
    {
        var options = Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 1024 });
        var adapter = new GedcomImportAdapter(NullLogger<GedcomImportAdapter>.Instance, options);

        var nonSeekableStreamMock = new Mock<Stream>();
        nonSeekableStreamMock.Setup(s => s.CanSeek).Returns(false);

        Assert.Throws<ArgumentException>(() => adapter.Parse(nonSeekableStreamMock.Object));
    }
}
