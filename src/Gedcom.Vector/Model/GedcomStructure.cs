namespace Gedcom.Vector.Model;

/// <summary>A GEDCOM structure (a line and its substructures) kept verbatim; used for extensions and unmodelled content.</summary>
public sealed class GedcomStructure
{
    /// <summary>Gets or sets the structure tag, for example <c>NOTE</c> or <c>_TAG</c>.</summary>
    public string Tag { get; set; } = "";

    /// <summary>Gets or sets the cross-reference identifier of the line, verbatim including the <c>@</c> delimiters, if any.</summary>
    public string? Xref { get; set; }

    /// <summary>Gets or sets the line value with <c>CONC</c> and <c>CONT</c> continuations already folded.</summary>
    public string? Value { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="Value"/> is a pointer to another record.</summary>
    public bool ValueIsPointer { get; set; }

    /// <summary>Gets or sets the extension URI resolved from <c>HEAD.SCHMA</c> for a GEDCOM 7.0 extension tag.</summary>
    public string? ExtensionUri { get; set; }

    /// <summary>Gets or sets the 1-based source line the structure was read from, if known.</summary>
    public int? SourceLine { get; set; }

    /// <summary>Gets the substructures of this structure, in source order.</summary>
    public List<GedcomStructure> Children { get; } = [];
}
