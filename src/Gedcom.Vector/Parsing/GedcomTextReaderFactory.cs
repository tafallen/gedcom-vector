using System.Text;

namespace Gedcom.Vector.Parsing;

/// <summary>Creates the character reader shared by the 1.3.2 parser and the line reader.</summary>
internal static class GedcomTextReaderFactory
{
    /// <summary>
    /// Wraps <paramref name="stream"/> in a reader for the detected encoding. ANSEL is read as Latin-1
    /// characters and decoded later. The stream is left open when the reader is disposed.
    /// </summary>
    public static StreamReader Create(Stream stream, GedcomEncodingResult encoding)
    {
        if (encoding.IsAnsel)
        {
            return new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);
        }
        return new StreamReader(stream, encoding.Encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);
    }
}
