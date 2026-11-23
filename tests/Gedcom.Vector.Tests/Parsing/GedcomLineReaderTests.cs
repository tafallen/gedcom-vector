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

    private static List<(string Line, int Number, bool Truncated)> ReadAllWithFlag(string text, int bufferSize, int maxLineLength)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var reader = new GedcomLineReader(stream, Utf8, bufferSize, maxLineLength);
        var lines = new List<(string, int, bool)>();
        while (reader.TryReadLine(out var line, out var number))
        {
            lines.Add((line.ToString(), number, reader.LastLineTruncated));
        }

        return lines;
    }

    [Fact]
    public void LongLineIsTruncatedAndRestDiscarded()
    {
        var lines = ReadAllWithFlag(new string('x', 66) + "\nnext", bufferSize: 4, maxLineLength: 16);

        Assert.Equal([(new string('x', 16), 1, true), ("next", 2, false)], lines);
    }

    [Fact]
    public void LineOfExactlyMaxLengthIsNotTruncated()
    {
        var lines = ReadAllWithFlag(new string('x', 16) + "\nnext", bufferSize: 4, maxLineLength: 16);

        Assert.Equal([(new string('x', 16), 1, false), ("next", 2, false)], lines);
    }

    [Fact]
    public void LineOfMaxLengthPlusOneIsTruncated()
    {
        var lines = ReadAllWithFlag(new string('x', 17) + "\nnext", bufferSize: 4, maxLineLength: 16);

        Assert.Equal([(new string('x', 16), 1, true), ("next", 2, false)], lines);
    }

    [Theory]
    [InlineData(16, "\r\n")]
    [InlineData(17, "\r\n")]
    [InlineData(18, "\r\n")]
    [InlineData(40, "\r\n")]
    [InlineData(16, "\r")]
    [InlineData(40, "\r")]
    public void CrLfAtTheCapOrInTheDiscardRegionIsOneTerminator(int length, string terminator)
    {
        var lines = ReadAllWithFlag(new string('x', length) + terminator + "next", bufferSize: 4, maxLineLength: 16);

        Assert.Equal(2, lines.Count);
        Assert.Equal(("next", 2, false), lines[1]);
        Assert.Equal(length > 16, lines[0].Truncated);
    }

    [Fact]
    public void OverlongFinalLineWithoutTerminatorIsTruncated()
    {
        var lines = ReadAllWithFlag(new string('x', 100), bufferSize: 4, maxLineLength: 16);

        Assert.Equal([(new string('x', 16), 1, true)], lines);
    }

    [Fact]
    public void ReaderCapsLineAtMaxLengthWhateverTheInputSize()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 5000)));
        using var reader = new GedcomLineReader(stream, Utf8, 4, 100);

        Assert.True(reader.TryReadLine(out var line, out _));
        Assert.Equal(100, line.Length);
        Assert.True(reader.LastLineTruncated);
        Assert.False(reader.TryReadLine(out _, out _));
    }

    [Fact]
    public void RejectsBadSizesWithoutDisposingTheStream()
    {
        using var stream = new MemoryStream("a\n"u8.ToArray());

        Assert.Throws<ArgumentOutOfRangeException>(() => new GedcomLineReader(stream, Utf8, 8, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GedcomLineReader(stream, Utf8, 1, 8));
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("ab\ncd\n", 2)]
    [InlineData("ab\ncd\n", 3)]
    [InlineData("ab\ncd\n", 4)]
    [InlineData("ab\ncdefgh\nij", 2)]
    [InlineData("ab\ncdefgh\nij", 3)]
    [InlineData("ab\ncdefgh\nij", 4)]
    [InlineData("abcd\nx", 4)]
    [InlineData("0123456789\nabcdefghijk\nlmnopqrstuvwxyz", 2)]
    [InlineData("0123456789\nabcdefghijk\nlmnopqrstuvwxyz", 3)]
    public void ReadsMultipleLinesWithTinyBuffer(string text, int bufferSize)
    {
        Assert.Equal(ReferenceSplit(text), ReadAll(text, bufferSize));
    }

    [Fact]
    public void CrCrLfGivesTwoEmptyLines()
    {
        Assert.Equal([("", 1), ("", 2)], ReadAll("\r\r\n"));
        Assert.Equal([("", 1), ("", 2)], ReadAll("\r\r\n", bufferSize: 2));
    }

    [Fact]
    public void TrailingLfDoesNotAddEmptyLine()
    {
        Assert.Equal([("a", 1)], ReadAll("a\n"));
    }

    [Fact]
    public void MatchesSimpleSplitterOnRandomInput()
    {
        var random = new Random(20261123);
        const string alphabet = "ab\r\n";
        for (int i = 0; i < 3000; i++)
        {
            var chars = new char[random.Next(0, 40)];
            for (int c = 0; c < chars.Length; c++) chars[c] = alphabet[random.Next(alphabet.Length)];
            var text = new string(chars);
            int bufferSize = random.Next(2, 9);

            Assert.True(ReferenceSplit(text).SequenceEqual(ReadAll(text, bufferSize)), $"case {i} buffer {bufferSize}");
        }
    }

    private static List<(string Line, int Number)> ReferenceSplit(string text)
    {
        var lines = new List<(string, int)>();
        int start = 0;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\r' || text[i] == '\n')
            {
                lines.Add((text[start..i], lines.Count + 1));
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                start = i + 1;
            }

            i++;
        }

        if (start < text.Length) lines.Add((text[start..], lines.Count + 1));
        return lines;
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
