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

    public IEnumerable<GedcomNode> ChildrenWithTag(string tag) => Children.Where(c => c.Tag == tag);
}
