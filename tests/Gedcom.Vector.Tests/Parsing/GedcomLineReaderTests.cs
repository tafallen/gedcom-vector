using System.Text;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Parsing;

public class GedcomLineReaderTests
{
    private static readonly GedcomEncodingResult Utf8 = new(new UTF8Encoding(false), false, 0);

    private static List<(string Line, int Number)> ReadAll(byte[] bytes, GedcomEncodingResult encoding, int? bufferSize = null)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = bufferSize is { } size
            ? new GedcomLineReader(stream, encoding, size)
            : new GedcomLineReader(stream, encoding);
        var lines = new List<(string, int)>();
        while (reader.TryReadLine(out var line, out var number))
        {
            lines.Add((line.ToString(), number));
        }

        return lines;
    }

    private static List<(string Line, int Number)> ReadAll(string text, int? bufferSize = null) =>
        ReadAll(Encoding.UTF8.GetBytes(text), Utf8, bufferSize);

    [Fact]
    public void ReadsLfCrlfAndLoneCrAsSeparateLines()
    {
        var lines = ReadAll("a\r\nb\nc\rd");

        Assert.Equal([("a", 1), ("b", 2), ("c", 3), ("d", 4)], lines);
    }

    [Fact]
    public void ReturnsFinalLineWithoutTerminator()
    {
        Assert.Equal([("0 HEAD", 1), ("0 TRLR", 2)], ReadAll("0 HEAD\n0 TRLR"));
    }

    [Fact]
    public void CountsBlankLines()
    {
        Assert.Equal([("a", 1), ("", 2), ("b", 3)], ReadAll("a\n\nb"));
    }

    [Fact]
    public void ReadsLineLongerThanBuffer()
    {
        var longLine = new string('x', 200_000);

        var lines = ReadAll(longLine + "\nnext\n");

        Assert.Equal(2, lines.Count);
        Assert.Equal(longLine, lines[0].Line);
        Assert.Equal(("next", 2), lines[1]);
    }

    [Fact]
    public void CrAtBufferBoundaryFollowedByLfIsOneTerminator()
    {
        // With a 4-character buffer the first fill is "abc\r", so the \r is the last character of the fill.
        var lines = ReadAll("abc\r\nd\n", bufferSize: 4);

        Assert.Equal([("abc", 1), ("d", 2)], lines);
    }

    [Fact]
    public void LoneCrAtBufferBoundaryIsATerminator()
    {
        var lines = ReadAll("abc\rd", bufferSize: 4);

        Assert.Equal([("abc", 1), ("d", 2)], lines);
    }

    [Fact]
    public void LoneCrAtEndOfStreamIsATerminator()
    {
        Assert.Equal([("abc", 1)], ReadAll("abc\r"));
    }

    [Fact]
    public void EmptyStreamReturnsNoLines()
    {
        Assert.Empty(ReadAll(string.Empty));
    }

    [Fact]
    public void AnselIsReturnedUndecoded()
    {
        var lines = ReadAll([(byte)'a', 0xE2, (byte)'\n'], new GedcomEncodingResult(null, true, 0));

        Assert.Equal([("a\u00E2", 1)], lines);
    }

    [Fact]
    public void DisposeLeavesStreamOpenAndBlocksFurtherReads()
    {
        using var stream = new MemoryStream("a\n"u8.ToArray());
        var reader = new GedcomLineReader(stream, Utf8);
        reader.Dispose();

        Assert.True(stream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => reader.TryReadLine(out _, out _));
    }
}
