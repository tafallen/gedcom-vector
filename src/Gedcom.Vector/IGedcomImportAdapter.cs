namespace Gedcom.Vector;

public interface IGedcomImportAdapter
{
    /// <summary>
    /// Parses a GEDCOM stream into a structured <see cref="GedcomParseResult"/>.
    /// </summary>
    /// <param name="gedcomFile">The input stream containing GEDCOM data. Must be seekable (i.e. <see cref="Stream.CanSeek"/> must be true).</param>
    /// <returns>A <see cref="GedcomParseResult"/> containing parsed records and any errors encountered.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided stream does not support seeking.</exception>
    GedcomParseResult Parse(Stream gedcomFile);
}
