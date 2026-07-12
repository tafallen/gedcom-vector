using System.Text;
using Gedcom.Vector;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom;

public class GedcomEncodingDetectorTests
{
    [Fact]
    public void Detect_Utf8Bom_ReturnsUtf8AndSkipsThreeBytes()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat("0 HEAD\n"u8.ToArray()).ToArray();

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Equal(3, result.PreambleLength);
        Assert.Equal("UTF-8", result.Encoding!.WebName.ToUpperInvariant());
    }

    [Fact]
    public void Detect_Utf16LittleEndianBom_ReturnsUtf16LittleEndian()
    {
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("0 HEAD\n")).ToArray();

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Equal(2, result.PreambleLength);
        Assert.Same(Encoding.Unicode, result.Encoding);
    }

    [Fact]
    public void Detect_Utf16BigEndianBom_ReturnsUtf16BigEndian()
    {
        var bytes = new byte[] { 0xFE, 0xFF }.Concat(Encoding.BigEndianUnicode.GetBytes("0 HEAD\n")).ToArray();

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Equal(2, result.PreambleLength);
        Assert.Same(Encoding.BigEndianUnicode, result.Encoding);
    }

    [Fact]
    public void Detect_NoBomCharUtf8_ReturnsUtf8WithNoPreamble()
    {
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR UTF-8\n0 TRLR\n");

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Equal(0, result.PreambleLength);
        Assert.Equal("UTF-8", result.Encoding!.WebName.ToUpperInvariant());
    }

    [Fact]
    public void Detect_NoBomCharAnsel_ReturnsIsAnselTrue()
    {
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR ANSEL\n0 TRLR\n");

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.True(result.IsAnsel);
        Assert.Null(result.Encoding);
        Assert.Equal(0, result.PreambleLength);
    }

    [Fact]
    public void Detect_NoBomCharAnsi_ReturnsWindows1252()
    {
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR ANSI\n0 TRLR\n");

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Equal(1252, result.Encoding!.CodePage);
    }

    [Fact]
    public void Detect_NoBomCharUnicode_ReturnsUtf16LittleEndian()
    {
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR UNICODE\n0 TRLR\n");

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Same(Encoding.Unicode, result.Encoding);
    }

    [Fact]
    public void Detect_NoCharTagAtAll_DefaultsToUtf8()
    {
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 GEDC\n0 TRLR\n");

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Equal("UTF-8", result.Encoding!.WebName.ToUpperInvariant());
    }

    [Fact]
    public void Detect_UnrecognizedCharValue_DefaultsToUtf8()
    {
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR MADE_UP_ENCODING\n0 TRLR\n");

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Equal("UTF-8", result.Encoding!.WebName.ToUpperInvariant());
    }

    [Fact]
    public void Detect_NoBomCharLatin1_ReturnsLatin1()
    {
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR LATIN-1\n0 TRLR\n");

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Same(Encoding.Latin1, result.Encoding);
    }

    [Fact]
    public void Detect_NoBomCharIso88591_ReturnsLatin1()
    {
        var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR ISO-8859-1\n0 TRLR\n");

        using var stream = new MemoryStream(bytes);
        var result = GedcomEncodingDetector.Detect(stream);

        Assert.False(result.IsAnsel);
        Assert.Same(Encoding.Latin1, result.Encoding);
    }
}

