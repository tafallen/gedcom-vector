using System.Buffers;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// Splits GEDCOM text into lines without allocating per line. Terminators are <c>\r\n</c>, <c>\n</c> and a lone
/// <c>\r</c>. The reader does not own the stream it was given.
/// </summary>
internal sealed class GedcomLineReader : IDisposable
{
    private const int DefaultBufferSize = 65536;
    private static readonly char[] LineBreakChars = ['\r', '\n'];

    private readonly TextReader _reader;
    private char[] _buffer;
    private int _capacity;
    private int _start;
    private int _end;
    private int _lineNumber;
    private bool _endOfInput;
    private bool _disposed;

    public GedcomLineReader(Stream stream, GedcomEncodingResult encoding)
        : this(stream, encoding, DefaultBufferSize)
    {
    }

    internal GedcomLineReader(Stream stream, GedcomEncodingResult encoding, int initialBufferSize)
        : this(GedcomTextReaderFactory.Create(stream, encoding), initialBufferSize)
    {
    }

    private GedcomLineReader(TextReader reader, int initialBufferSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialBufferSize, 2);
        _reader = reader;
        _capacity = initialBufferSize;
        _buffer = ArrayPool<char>.Shared.Rent(_capacity);
    }

    /// <summary>
    /// Reads the next line. <paramref name="line"/> excludes its terminator and is valid only until the next call
    /// to this method or <see cref="Dispose"/>. <paramref name="lineNumber"/> is 1-based.
    /// </summary>
    /// <returns>False when no lines remain.</returns>
    public bool TryReadLine(out ReadOnlySpan<char> line, out int lineNumber)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int scanFrom = _start;
        while (true)
        {
            int index = _buffer.AsSpan(scanFrom, _end - scanFrom).IndexOfAny(LineBreakChars);
            if (index >= 0)
            {
                int position = scanFrom + index;
                int terminatorLength = 1;
                bool needMore = false;
                if (_buffer[position] == '\r')
                {
                    if (position + 1 < _end)
                    {
                        if (_buffer[position + 1] == '\n') terminatorLength = 2;
                    }
                    else if (!_endOfInput)
                    {
                        // A trailing \r may be the first half of \r\n; wait for the next character.
                        needMore = true;
                    }
                }

                if (!needMore)
                {
                    line = _buffer.AsSpan(_start, position - _start);
                    _start = position + terminatorLength;
                    lineNumber = ++_lineNumber;
                    return true;
                }

                scanFrom = position;
            }
            else
            {
                scanFrom = _end;
                if (_endOfInput)
                {
                    if (_start < _end)
                    {
                        line = _buffer.AsSpan(_start, _end - _start);
                        _start = _end;
                        lineNumber = ++_lineNumber;
                        return true;
                    }

                    line = default;
                    lineNumber = 0;
                    return false;
                }
            }

            scanFrom -= Fill();
        }
    }

    // Reads more characters, compacting or growing the buffer first. Returns how far unread data moved left.
    private int Fill()
    {
        int shift = _start;
        if (shift > 0)
        {
            Array.Copy(_buffer, _start, _buffer, 0, _end - _start);
            _end -= shift;
            _start = 0;
        }

        if (_end == _capacity)
        {
            int newCapacity = _capacity * 2;
            char[] bigger = ArrayPool<char>.Shared.Rent(newCapacity);
            Array.Copy(_buffer, bigger, _end);
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = bigger;
            _capacity = newCapacity;
        }

        int read = _reader.Read(_buffer, _end, _capacity - _end);
        if (read <= 0)
        {
            _endOfInput = true;
        }
        else
        {
            _end += read;
        }

        return shift;
    }

    /// <summary>Disposes the underlying reader (not the stream) and returns the buffer to the pool.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = [];
    }
}
