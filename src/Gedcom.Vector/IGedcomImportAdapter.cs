using System.IO;

namespace Gedcom.Vector;

/// <summary>
/// Defines methods for importing and parsing GEDCOM streams.
/// </summary>
public interface IGedcomImportAdapter
{
    /// <summary>
    /// Parses a GEDCOM stream into a structured <see cref="GedcomParseResult"/>.
    /// </summary>
    /// <param name="gedcomFile">The input stream containing GEDCOM data. Must be seekable (i.e., <see cref="Stream.CanSeek"/> must be true).</param>
    /// <returns>A <see cref="GedcomParseResult"/> containing parsed records and any errors encountered.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the provided stream does not support seeking.</exception>
    GedcomParseResult Parse(Stream gedcomFile);

    /// <summary>
    /// Parses multiple GEDCOM streams concurrently across CPU cores.
    /// </summary>
    /// <param name="gedcomFiles">The collection of GEDCOM input streams.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of <see cref="GedcomParseResult"/> objects corresponding to each input stream.</returns>
    Task<GedcomParseResult[]> ParseParallelAsync(IEnumerable<Stream> gedcomFiles, CancellationToken cancellationToken = default);
}
