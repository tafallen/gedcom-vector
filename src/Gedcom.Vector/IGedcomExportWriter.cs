using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gedcom.Vector;

public interface IGedcomExportWriter
{
    string Write(GedcomParseResult parseResult);
    void Write(GedcomParseResult parseResult, Stream output);
    Task WriteAsync(GedcomParseResult parseResult, Stream output, CancellationToken cancellationToken = default);
}
