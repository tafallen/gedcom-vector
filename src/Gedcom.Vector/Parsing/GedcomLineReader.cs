using System.Buffers;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// Splits GEDCOM text into lines without allocating per line. Terminators are <c>\r\n</c>, <c>\n</c> and a lone
/// <c>\r</c>. The reader does not own the stream it was given. A line longer than the maximum line length is
/// returned truncated to that length and the rest of the physical line is discarded without being buffered.
/// </summary>
internal sealed class GedcomLineReader : IDisposable
{
    internal const int DefaultBufferSize = 65536;
    internal const int DefaultMaxLineLength = 1_048_576;
    private static readonly char[] LineBreakChars = ['\r', '\n'];

    private readonly TextReader _reader;
    private readonly int _maxCapacity;
    private readonly int _maxLineLength;
    private char[] _buffer;
    private int _capacity;
    private int _start;
    private int _end;
    private int _lineNumber;
    private bool _endOfInput;
    private bool _disposed;
    private bool _discarding;
    private bool _skipLeadingLf;

    public GedcomLineReader(Stream stream, GedcomEncodingResult encoding)
        : this(stream, encoding, DefaultBufferSize, DefaultMaxLineLength)
    {
    }

    internal GedcomLineReader(Stream stream, GedcomEncodingResult encoding, int initialBufferSize, int maxLineLength = DefaultMaxLineLength)
        : this(CreateValidated(stream, encoding, initialBufferSize, maxLineLength), initialBufferSize, maxLineLength)
    {
    }

    private GedcomLineReader(TextReader reader, int initialBufferSize, int maxLineLength)
    {
        _reader = reader;
        _maxLineLength = maxLineLength;
        _maxCapacity = maxLineLength + 1;
        _capacity = initialBufferSize;
        _buffer = ArrayPool<char>.Shared.Rent(_capacity);
    }

    // Validates before the StreamReader exists so a bad argument cannot leak it.
    private static StreamReader CreateValidated(Stream stream, GedcomEncodingResult encoding, int initialBufferSize, int maxLineLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialBufferSize, 2);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxLineLength, initialBufferSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxLineLength, Array.MaxLength - 1);
        return GedcomTextReaderFactory.Create(stream, encoding);
    }

    /// <summary>True when the line returned by the last <see cref="TryReadLine"/> call was cut at the maximum line length.</summary>
    public bool LastLineTruncated { get; private set; }

    /// <summary>
    /// Reads the next line. <paramref name="line"/> excludes its terminator and is valid only until the next call
    /// to this method or <see cref="Dispose"/>. <paramref name="lineNumber"/> is 1-based. A line longer than the
    /// maximum is returned cut to that length with <see cref="LastLineTruncated"/> set.
    /// </summary>
    /// <returns>False when no lines remain.</returns>
    public bool TryReadLine(out ReadOnlySpan<char> line, out int lineNumber)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LastLineTruncated = false;

        if (_skipLeadingLf)
        {
            SkipLeadingLf();
        }

        if (_discarding)
        {
            DiscardRestOfLine();
        }

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
                        if (_end - _start >= _maxCapacity)
                        {
                            // The buffer cannot grow to show the next character: take the \r now and swallow a following \n later.
                            _skipLeadingLf = true;
                        }
                        else
                        {
                            // A trailing \r may be the first half of \r\n; wait for the next character.
                            needMore = true;
                        }
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
                if (_end - _start > _maxLineLength)
                {
                    line = _buffer.AsSpan(_start, _maxLineLength);
                    _start += _maxLineLength;
                    _discarding = true;
                    LastLineTruncated = true;
                    lineNumber = ++_lineNumber;
                    return true;
                }

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

    private void SkipLeadingLf()
    {
        if (_start == _end && !_endOfInput)
        {
            Fill();
        }

        if (_start < _end && _buffer[_start] == '\n')
        {
            _start++;
        }

        _skipLeadingLf = false;
    }

    // Drops the remainder of an over-long line up to and including its terminator without keeping it.
    private void DiscardRestOfLine()
    {
        while (true)
        {
            int index = _buffer.AsSpan(_start, _end - _start).IndexOfAny(LineBreakChars);
            if (index < 0)
            {
                _start = _end;
                Fill();
                if (_endOfInput)
                {
                    break;
                }

                continue;
            }

            int position = _start + index;
            if (_buffer[position] == '\r' && position + 1 == _end && !_endOfInput)
            {
                _start = position;
                Fill();
                continue;
            }

            int terminatorLength = _buffer[position] == '\r' && position + 1 < _end && _buffer[position + 1] == '\n' ? 2 : 1;
            _start = position + terminatorLength;
            break;
        }

        _discarding = false;
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
            int newCapacity = (int)Math.Min((long)_capacity * 2, _maxCapacity);
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
