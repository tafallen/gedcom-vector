using System.Collections.Generic;
using System.Linq;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// Represents a node in the hierarchical tree structure parsed from a GEDCOM file.
/// </summary>
public sealed class GedcomNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GedcomNode"/> class.
    /// </summary>
    /// <param name="level">The indentation level of the node.</param>
    /// <param name="xrefId">The optional cross-reference identifier.</param>
    /// <param name="tag">The GEDCOM tag.</param>
    /// <param name="value">The optional value string.</param>
    public GedcomNode(int level, string? xrefId, string tag, string? value)
    {
        Level = level;
        XrefId = xrefId;
        Tag = tag;
        Value = value;
    }

    /// <summary>
    /// Gets the hierarchical level of this node.
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Gets the optional cross-reference identifier of this node.
    /// </summary>
    public string? XrefId { get; }

    /// <summary>
    /// Gets the GEDCOM tag of this node.
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// Gets the optional value text of this node.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Gets the list of sub-nodes (child nodes) under this node.
    /// </summary>
    public List<GedcomNode> Children { get; } = new();

    /// <summary>
    /// Finds the first child node with the specified tag name.
    /// </summary>
    /// <param name="tag">The tag name to search for.</param>
    /// <returns>The matching <see cref="GedcomNode"/>, or null if not found.</returns>
    public GedcomNode? Child(string tag)
    {
        var list = Children;
        for (int i = 0; i < list.Count; i++)
        {
            var child = list[i];
            if (child.Tag == tag)
            {
                return child;
            }
        }
        return null;
    }

    /// <summary>
    /// Retrieves all child nodes matching the specified tag.
    /// </summary>
    /// <param name="tag">The tag name to filter by.</param>
    /// <returns>An enumerable sequence of matching child <see cref="GedcomNode"/>s.</returns>
    public IEnumerable<GedcomNode> ChildrenWithTag(string tag) => Children.Where(c => c.Tag == tag);
}
