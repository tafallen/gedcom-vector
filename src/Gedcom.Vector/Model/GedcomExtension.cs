namespace Gedcom.Vector.Model;

/// <summary>An unmodelled substructure (typically a <c>_TAG</c> extension) attached to its parent element, with its original position.</summary>
public sealed class GedcomExtension
{
    /// <summary>Gets or sets the position among the parent's children when read; the same ordering key as modelled children.</summary>
    public int? SourceOrder { get; set; }

    /// <summary>Gets or sets the substructure kept verbatim.</summary>
    public required GedcomStructure Structure { get; set; }
}
