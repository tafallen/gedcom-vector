using System.Globalization;
using Gedcom.Vector.Model;
using Xunit.Sdk;

namespace Gedcom.Vector.Tests.Model;

/// <summary>Structural comparison of <see cref="GedcomStructure"/> trees; failures name tags, xrefs, indexes and lines, never values.</summary>
internal static class StructureAssert
{
    public static void Equal(GedcomStructure expected, GedcomStructure actual, bool ignoreSourceLine = true)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        Compare(expected, actual, ignoreSourceLine, expected.Tag, null, 0);
    }

    private static void Compare(GedcomStructure expected, GedcomStructure actual, bool ignoreSourceLine, string path, string? parentPath, int index)
    {
        string? difference = null;
        if (expected.Tag != actual.Tag)
        {
            difference = "Tag differs";
        }
        else if (expected.Xref != actual.Xref)
        {
            difference = "Xref differs";
        }
        else if (expected.Value != actual.Value)
        {
            difference = "Value differs";
        }
        else if (expected.ValueIsPointer != actual.ValueIsPointer)
        {
            difference = "ValueIsPointer differs";
        }
        else if (expected.ExtensionUri != actual.ExtensionUri)
        {
            difference = "ExtensionUri differs";
        }
        else if (!ignoreSourceLine && expected.SourceLine != actual.SourceLine)
        {
            difference = "SourceLine differs";
        }

        if (difference is not null)
        {
            throw new XunitException(Describe(path, parentPath, index, actual.SourceLine, difference));
        }

        if (expected.Children.Count != actual.Children.Count)
        {
            string counts = string.Create(CultureInfo.InvariantCulture, $"Child count differs (expected {expected.Children.Count}, actual {actual.Children.Count})");
            throw new XunitException(Describe(path, parentPath, index, actual.SourceLine, counts));
        }

        for (int i = 0; i < expected.Children.Count; i++)
        {
            GedcomStructure child = expected.Children[i];
            Compare(child, actual.Children[i], ignoreSourceLine, path + "." + child.Tag, path, i);
        }
    }

    private static string Describe(string path, string? parentPath, int index, int? line, string difference)
    {
        string where = parentPath is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"child {index} of {parentPath}");
        string lineText = line is { } l ? string.Create(CultureInfo.InvariantCulture, $"line {l}") : string.Empty;
        string location = (where.Length > 0 && lineText.Length > 0) ? where + ", " + lineText : where + lineText;
        string suffix = location.Length > 0 ? " (" + location + ")" : string.Empty;
        return $"Structures differ at {path}{suffix}: {difference}";
    }
}
