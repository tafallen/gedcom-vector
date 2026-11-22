namespace Gedcom.Vector.Model;

/// <summary>Base class of every modelled GEDCOM element; carries its original position and any unmodelled substructures.</summary>
public abstract class GedcomElement
{
    /// <summary>Gets or sets the position among the parent's children when read; null for elements created in code.</summary>
    public int? SourceOrder { get; set; }

    /// <summary>Gets the unmodelled substructures (extension tags such as <c>_TAG</c>) of this element, verbatim.</summary>
    public List<GedcomExtension> Extensions { get; } = [];
}
