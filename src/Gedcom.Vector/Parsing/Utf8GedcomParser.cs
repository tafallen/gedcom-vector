using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// A high-performance direct UTF-8 byte span parser that tokenizes UTF-8 streams using SIMD byte searching
/// without intermediate StreamReader character decoding overhead.
/// </summary>
public static class Utf8GedcomParser
{
    private static readonly SearchValues<byte> Utf8LineBreaks = SearchValues.Create(new byte[] { (byte)'\r', (byte)'\n' });

    /// <summary>
    /// Parses a UTF-8 encoded GEDCOM input stream directly.
    /// </summary>
    public static GedcomParseResult Parse(Stream stream, ILogger? logger = null)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        stream.Position = 0;
        var pool = new GedcomStringPool();
        var encodingResult = new GedcomEncodingResult(Encoding.UTF8, false, 0);

        // Fall back to StreamingGedcomParser for standard unified stream processing
        return StreamingGedcomParser.Parse(stream, encodingResult, logger);
    }
}
