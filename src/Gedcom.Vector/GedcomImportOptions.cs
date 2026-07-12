namespace Gedcom.Vector;

public class GedcomImportOptions
{
    public const string SectionName = "GedcomImport";

    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB
}
