using Gedcom.Vector;

namespace Gedcom.Vector;

public interface IGedcomExportWriter
{
    string Write(GedcomParseResult parseResult);
}
