namespace Gedcom.Vector.Parsing;

public static class GedcomTreeBuilder
{
    public static IEnumerable<GedcomNode> Build(IEnumerable<GedcomLine> lines)
    {
        var stack = new Stack<GedcomNode>();
        GedcomNode? currentRoot = null;

        foreach (var line in lines)
        {
            var node = new GedcomNode(line.Level, line.XrefId, line.Tag, line.Value);

            while (stack.Count > 0 && stack.Peek().Level >= node.Level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                if (currentRoot != null)
                {
                    yield return currentRoot;
                }
                currentRoot = node;
            }
            else
            {
                stack.Peek().Children.Add(node);
            }

            stack.Push(node);
        }

        if (currentRoot != null)
        {
            yield return currentRoot;
        }
    }
}
