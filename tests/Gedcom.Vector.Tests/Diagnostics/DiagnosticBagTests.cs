using Gedcom.Vector.Diagnostics;
using Xunit;

namespace Gedcom.Vector.Tests.Diagnostics;

public class DiagnosticBagTests
{
    [Fact]
    public void AddUsesSeverityAndMessageForCode()
    {
        foreach (var code in Enum.GetValues<GedcomDiagnosticCode>())
        {
            var bag = new DiagnosticBag(10);
            var d = bag.Add(code, tag: "_FOO");
            Assert.Equal(code, d.Code);
            Assert.False(string.IsNullOrWhiteSpace(d.Message));
            Assert.DoesNotContain("{tag}", d.Message, StringComparison.Ordinal);
            Assert.True(Enum.IsDefined(d.Severity));
        }

        var nonUtf8 = new DiagnosticBag(10).Add(GedcomDiagnosticCode.NonUtf8Encoding);
        Assert.Equal(GedcomDiagnosticSeverity.Error, nonUtf8.Severity);
        Assert.Equal("GEDCOM 7.0 requires UTF-8 encoding. Non-UTF-8 encoding detected.", nonUtf8.Message);
        Assert.Equal(GedcomDiagnosticSeverity.Info, new DiagnosticBag(1).Add(GedcomDiagnosticCode.BlankLinesIgnored).Severity);
        Assert.Contains("_FOO", new DiagnosticBag(1).Add(GedcomDiagnosticCode.InvalidTag, tag: "_FOO").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddStopsAtCapAndAppendsOneTruncationEntry()
    {
        var bag = new DiagnosticBag(3);
        for (var i = 0; i < 5; i++)
        {
            bag.Add(GedcomDiagnosticCode.MissingTag, line: i + 1);
        }

        Assert.Equal(4, bag.Items.Count);
        Assert.Equal(GedcomDiagnosticCode.DiagnosticsTruncated, bag.Items[3].Code);
        Assert.Equal(2, bag.SuppressedCount);
        Assert.Null(bag.Items[3].Line);
    }

    [Fact]
    public void AddReturnsTruncationEntryAfterCap()
    {
        var bag = new DiagnosticBag(1);
        bag.Add(GedcomDiagnosticCode.MissingTag);
        var first = bag.Add(GedcomDiagnosticCode.MissingTag);
        var second = bag.Add(GedcomDiagnosticCode.InvalidTag, tag: "X");

        Assert.Equal(GedcomDiagnosticCode.DiagnosticsTruncated, first.Code);
        Assert.Same(first, second);
        Assert.Equal(2, bag.Items.Count);
    }

    [Fact]
    public void ConstructorRejectsCapBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticBag(0));
    }

    [Fact]
    public void ToStringOmitsLineWhenNull()
    {
        var withLine = new GedcomDiagnostic(GedcomDiagnosticCode.MissingTag, GedcomDiagnosticSeverity.Warning, "msg", line: 7);
        var without = new GedcomDiagnostic(GedcomDiagnosticCode.MissingTag, GedcomDiagnosticSeverity.Warning, "msg");

        Assert.Equal("Warning GV1005 line 7: msg", withLine.ToString());
        Assert.Equal("Warning GV1005: msg", without.ToString());
        Assert.Throws<ArgumentException>(() => new GedcomDiagnostic(GedcomDiagnosticCode.MissingTag, GedcomDiagnosticSeverity.Warning, ""));
    }
}
