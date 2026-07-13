using System.Collections.Generic;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// Provides methods for building a hierarchical tree of <see cref="GedcomNode"/>s from a sequence of <see cref="GedcomLine"/> records.
/// </summary>
public static class GedcomTreeBuilder
{
    /// <summary>
    /// Builds a hierarchical list of level-0 root nodes and their children from a sequence of parsed lines.
    /// </summary>
    /// <param name="lines">The flat sequence of parsed GEDCOM lines.</param>
    /// <returns>An enumerable sequence of level-0 root <see cref="GedcomNode"/>s.</returns>
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
