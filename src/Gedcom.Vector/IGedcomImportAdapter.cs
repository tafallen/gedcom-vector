namespace Gedcom.Vector;

public interface IGedcomImportAdapter
{
    GedcomParseResult Parse(Stream gedcomFile);
}
