namespace Gedcom.Vector.Model;

/// <summary>An unknown or extension level-0 GEDCOM record (for example <c>_PLAC</c>) kept verbatim.</summary>
public sealed class ExtensionRecord : GedcomElement
{
    /// <summary>Gets or sets the level-0 structure; <see cref="GedcomElement.SourceOrder"/> is its index among the level-0 records.</summary>
    public GedcomStructure Structure { get; set; } = null!;
}
