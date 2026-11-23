using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Parsing;

public class GedcomLineTokenizerTests
{
    [Theory]
    [InlineData("0 @I1@ INDI", 0, "@I1@", "INDI", "", false)]
    [InlineData("1 NAME John /Doe/", 1, "", "NAME", "John /Doe/", true)]
    [InlineData("2 CONC  two leading", 2, "", "CONC", " two leading", true)]
    [InlineData("1 NOTE trailing  ", 1, "", "NOTE", "trailing  ", true)]
    [InlineData("01 TAG x", 1, "", "TAG", "x", true)]
    [InlineData("0 HEAD", 0, "", "HEAD", "", false)]
    [InlineData("1 NOTE  ", 1, "", "NOTE", " ", true)]
    [InlineData("1 NOTE a\tb", 1, "", "NOTE", "a\tb", true)]
    public void TokenizesLevelXrefTagValue(string text, int level, string xref, string tag, string value, bool hasValue)
    {
        var line = GedcomLineTokenizer.Tokenize(text);

        Assert.Equal(GedcomLineKind.Structure, line.Kind);
        Assert.Equal(level, line.Level);
        Assert.Equal(xref, line.Xref.ToString());
        Assert.Equal(tag, line.Tag.ToString());
        Assert.Equal(value, line.Value.ToString());
        Assert.Equal(hasValue, line.HasValue);
        Assert.False(line.XrefMalformed);
        Assert.False(line.TagInvalid);
        Assert.False(line.HadLeadingWhitespace);
        Assert.Equal(text, line.Text.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \t ")]
    public void TreatsWhitespaceOnlyAsBlank(string text)
    {
        var line = GedcomLineTokenizer.Tokenize(text);

        Assert.Equal(GedcomLineKind.Blank, line.Kind);
    }

    [Theory]
    [InlineData("  1 NAME x", "NAME", "x")]
    [InlineData("\t1 NAME x", "NAME", "x")]
    [InlineData(" \t 0 HEAD", "HEAD", "")]
    public void SkipsLeadingWhitespace(string text, string tag, string value)
    {
        var line = GedcomLineTokenizer.Tokenize(text);

        Assert.Equal(GedcomLineKind.Structure, line.Kind);
        Assert.True(line.HadLeadingWhitespace);
        Assert.Equal(tag, line.Tag.ToString());
        Assert.Equal(value, line.Value.ToString());
        Assert.Equal(text.TrimStart(' ', '\t'), line.Text.ToString());
    }

    [Theory]
    [InlineData("Smith family notes")]
    [InlineData("12abc X")]
    [InlineData("1234 TAG")]
    [InlineData("1234")]
    [InlineData("1\tTAG")]
    [InlineData("-1 TAG")]
    [InlineData("1x")]
    public void ReportsNoLevel(string text)
    {
        var line = GedcomLineTokenizer.Tokenize(text);

        Assert.Equal(GedcomLineKind.NoLevel, line.Kind);
        Assert.Equal(text, line.Text.ToString());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1 ")]
    [InlineData("0 @I1@")]
    [InlineData("0 @I1@ ")]
    [InlineData("1  TAG")]
    [InlineData("0 @I1@  INDI")]
    public void ReportsNoTag(string text)
    {
        var line = GedcomLineTokenizer.Tokenize(text);

        Assert.Equal(GedcomLineKind.NoTag, line.Kind);
        Assert.Equal(text, line.Text.ToString());
    }

    [Fact]
    public void NoTagKeepsLevelAndXref()
    {
        var line = GedcomLineTokenizer.Tokenize("0 @I1@");

        Assert.Equal(0, line.Level);
        Assert.Equal("@I1@", line.Xref.ToString());
    }

    [Theory]
    [InlineData("0 @I1 INDI", "@I1")]
    [InlineData("0 @@ INDI", "@@")]
    [InlineData("0 @ INDI", "@")]
    [InlineData("0 @I@1@ INDI", "@I@1@")]
    [InlineData("0 @I1x INDI", "@I1x")]
    public void FlagsMalformedXref(string text, string xref)
    {
        var line = GedcomLineTokenizer.Tokenize(text);

        Assert.Equal(GedcomLineKind.Structure, line.Kind);
        Assert.True(line.XrefMalformed);
        Assert.Equal(xref, line.Xref.ToString());
        Assert.Equal("INDI", line.Tag.ToString());
    }

    [Theory]
    [InlineData("1 BIRT-X")]
    [InlineData("1 BêRT")]
    [InlineData("1 BIRT\tX")]
    public void FlagsInvalidTag(string text)
    {
        var line = GedcomLineTokenizer.Tokenize(text);

        Assert.Equal(GedcomLineKind.Structure, line.Kind);
        Assert.True(line.TagInvalid);
    }

    [Theory]
    [InlineData("1 _MYTAG x")]
    [InlineData("1 FAM1 x")]
    public void AcceptsUnderscoreAndDigitsInTag(string text)
    {
        Assert.False(GedcomLineTokenizer.Tokenize(text).TagInvalid);
    }

    [Theory]
    [InlineData("1 BIRT ")]
    [InlineData("1 BIRT")]
    public void EmptyValueAfterDelimiterHasNoValue(string text)
    {
        var line = GedcomLineTokenizer.Tokenize(text);

        Assert.Equal("BIRT", line.Tag.ToString());
        Assert.False(line.HasValue);
        Assert.True(line.Value.IsEmpty);
    }

    [Fact]
    public void TokenizeDoesNotAllocate()
    {
        var text = "1 NOTE " + new string('x', 5000);
        _ = GedcomLineTokenizer.Tokenize(text).Value.Length;

        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0;
        for (var i = 0; i < 1000; i++)
        {
            var line = GedcomLineTokenizer.Tokenize(text);
            total += line.Value.Length + line.Level;
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(total > 0);
        Assert.Equal(0, after - before);
    }
}
