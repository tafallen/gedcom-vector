using Gedcom.Vector.Diagnostics;

namespace Gedcom.Vector.Model;

/// <summary>A parsed GEDCOM file: the root of the 2.0 model.</summary>
public sealed class GedcomDocument
{
    /// <summary>Gets or sets the GEDCOM specification version the file declared (<c>HEAD.GEDC.VERS</c>).</summary>
    public GedcomSpecVersion SourceVersion { get; set; }

    /// <summary>Gets the unknown and extension (underscore-tagged) level-0 records, verbatim.</summary>
    public List<ExtensionRecord> ExtensionRecords { get; } = [];

    /// <summary>Gets the diagnostics raised while reading the file.</summary>
    public List<GedcomDiagnostic> Diagnostics { get; } = [];
}
