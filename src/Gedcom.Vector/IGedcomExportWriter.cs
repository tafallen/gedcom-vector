using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gedcom.Vector;

/// <summary>
/// Defines methods for exporting a <see cref="GedcomParseResult"/> to GEDCOM 5.5.1 format.
/// </summary>
public interface IGedcomExportWriter
{
    /// <summary>
    /// Serializes the parsed GEDCOM structure to a string.
    /// </summary>
    /// <param name="parseResult">The parsed GEDCOM data containing individuals, families, events, and media.</param>
    /// <returns>A string containing the serialized GEDCOM data.</returns>
    string Write(GedcomParseResult parseResult);

    /// <summary>
    /// Serializes the parsed GEDCOM structure directly to a seekable or writeable stream.
    /// </summary>
    /// <param name="parseResult">The parsed GEDCOM data containing individuals, families, events, and media.</param>
    /// <param name="output">The output stream to write the GEDCOM data to.</param>
    void Write(GedcomParseResult parseResult, Stream output);

    /// <summary>
    /// Asynchronously serializes the parsed GEDCOM structure directly to a seekable or writeable stream.
    /// </summary>
    /// <param name="parseResult">The parsed GEDCOM data containing individuals, families, events, and media.</param>
    /// <param name="output">The output stream to write the GEDCOM data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    Task WriteAsync(GedcomParseResult parseResult, Stream output, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes an indexed GEDCOM tree context directly to a string without re-indexing lookup tables.
    /// </summary>
    string Write(GedcomTreeContext context);

    /// <summary>
    /// Serializes an indexed GEDCOM tree context directly to an output stream without re-indexing lookup tables.
    /// </summary>
    void Write(GedcomTreeContext context, Stream output);

    /// <summary>
    /// Asynchronously serializes an indexed GEDCOM tree context directly to an output stream without re-indexing lookup tables.
    /// </summary>
    Task WriteAsync(GedcomTreeContext context, Stream output, CancellationToken cancellationToken = default);
}
