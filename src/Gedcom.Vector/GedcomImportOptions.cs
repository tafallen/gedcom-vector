namespace Gedcom.Vector;

/// <summary>
/// Options for configuring the GEDCOM import process.
/// </summary>
public class GedcomImportOptions
{
    /// <summary>
    /// The default configuration section name for these options.
    /// </summary>
    public const string SectionName = "GedcomImport";

    /// <summary>
    /// Gets or sets the maximum allowed size of a GEDCOM file in bytes.
    /// Files exceeding this size will fail to parse and return an error.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB
}
