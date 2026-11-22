using Gedcom.Vector.Model;
using Xunit;
using Xunit.Sdk;

namespace Gedcom.Vector.Tests.Model;

public class ModelSkeletonTests
{
    [Fact]
    public void CollectionsStartEmpty()
    {
        var record = new ExtensionRecord();
        Assert.NotNull(record.Extensions);
        Assert.Empty(record.Extensions);
        Assert.NotNull(new GedcomStructure().Children);
        Assert.Empty(new GedcomStructure().Children);

        var document = new GedcomDocument();
        Assert.NotNull(document.ExtensionRecords);
        Assert.Empty(document.ExtensionRecords);
        Assert.NotNull(document.Diagnostics);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void GedcomDocumentDefaultsToGedcom551()
    {
        Assert.Equal(GedcomSpecVersion.Gedcom551, new GedcomDocument().SourceVersion);
    }

    [Fact]
    public void StructureAssertReportsTagPathNotValue()
    {
        const string Sentinel = "SENTINEL-SECRET-TEXT";
        GedcomStructure Build(string value)
        {
            var indi = new GedcomStructure { Tag = "INDI", Xref = "@I1@" };
            indi.Children.Add(new GedcomStructure { Tag = "NOTE", Value = value, SourceLine = 12 });
            return indi;
        }

        var ex = Assert.Throws<XunitException>(() => StructureAssert.Equal(Build("a"), Build(Sentinel)));
        Assert.Contains("INDI.NOTE", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 12", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, ex.Message, StringComparison.Ordinal);
    }
}
