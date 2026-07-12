namespace Gedcom.Vector.Parsing;

public sealed class GedcomNode
{
    public GedcomNode(int level, string? xrefId, string tag, string? value)
    {
        Level = level;
        XrefId = xrefId;
        Tag = tag;
        Value = value;
    }

    public int Level { get; }

    public string? XrefId { get; }

    public string Tag { get; }

    public string? Value { get; }

    public List<GedcomNode> Children { get; } = new();

    public GedcomNode? Child(string tag) => Children.FirstOrDefault(c => c.Tag == tag);

    public IEnumerable<GedcomNode> ChildrenWithTag(string tag) => Children.Where(c => c.Tag == tag);
}
