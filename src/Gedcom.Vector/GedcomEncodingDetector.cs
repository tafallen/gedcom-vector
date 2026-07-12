using System.Text;
using System.Text.RegularExpressions;

namespace Gedcom.Vector;

internal readonly record struct GedcomEncodingResult(Encoding? Encoding, bool IsAnsel, int PreambleLength);

internal static class GedcomEncodingDetector
{
    // The header is always at the start of the file and is always plain
    // ASCII, regardless of the encoding the CHAR tag goes on to declare for
    // the rest of the file - so a Latin-1 (byte-preserving) decode of just
    // this window is always safe for locating the CHAR line.
    private const int HeaderScanLength = 4096;

    private static readonly Regex CharTagPattern = new(@"^\d+ CHAR (.+?)\r?$", RegexOptions.Multiline | RegexOptions.Compiled);

    static GedcomEncodingDetector()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static GedcomEncodingResult Detect(Stream stream)
    {
        var buffer = new byte[HeaderScanLength];
        var readLength = stream.Read(buffer, 0, buffer.Length);
        stream.Position = 0;

        if (readLength >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return new GedcomEncodingResult(new UTF8Encoding(false), false, 3);
        }

        if (readLength >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            return new GedcomEncodingResult(Encoding.Unicode, false, 2);
        }

        if (readLength >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            return new GedcomEncodingResult(Encoding.BigEndianUnicode, false, 2);
        }

        var header = Encoding.Latin1.GetString(buffer, 0, readLength);
        var match = CharTagPattern.Match(header);

        if (!match.Success)
        {
            return DefaultToUtf8();
        }

        var declared = match.Groups[1].Value.Trim().ToUpperInvariant();

        return declared switch
        {
            "UTF-8" or "UTF8" => new GedcomEncodingResult(new UTF8Encoding(false), false, 0),
            "ASCII" => new GedcomEncodingResult(new UTF8Encoding(false), false, 0),
            "UNICODE" => new GedcomEncodingResult(Encoding.Unicode, false, 0),
            "ANSI" => new GedcomEncodingResult(Encoding.GetEncoding(1252), false, 0),
            "LATIN-1" or "ISO-8859-1" => new GedcomEncodingResult(Encoding.Latin1, false, 0),
            "ANSEL" => new GedcomEncodingResult(null, true, 0),
            _ => DefaultToUtf8(),
        };
    }

    private static GedcomEncodingResult DefaultToUtf8() => new(new UTF8Encoding(false), false, 0);
}
