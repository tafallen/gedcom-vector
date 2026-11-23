using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Parsing;

public class GedcomValueCodecTests
{
    [Theory]
    [InlineData("@I1@", true)]
    [InlineData("@VOID@", true)]
    [InlineData("@I 1@", false)]
    [InlineData("@#DJULIAN@", false)]
    [InlineData("@@x", false)]
    [InlineData("@@", false)]
    [InlineData("@@@", false)]
    [InlineData("@I1@@", false)]
    [InlineData("@I@1@", false)]
    [InlineData("@I1@ x", false)]
    [InlineData("@I\t1@", false)]
    [InlineData("I1@", false)]
    [InlineData("@I1", false)]
    [InlineData("@x@", true)]
    [InlineData("", false)]
    public void IsPointerRecognisesPointers(string raw, bool expected)
    {
        Assert.Equal(expected, GedcomValueCodec.IsPointer(raw));
    }

    [Theory]
    [InlineData("@@x", GedcomSpecVersion.Gedcom551, "@x")]
    [InlineData("@@x", GedcomSpecVersion.Gedcom70, "@x")]
    [InlineData("a@@b", GedcomSpecVersion.Gedcom551, "a@@b")]
    [InlineData("a@@b", GedcomSpecVersion.Gedcom70, "a@@b")]
    [InlineData("@#DJULIAN@ 1700", GedcomSpecVersion.Gedcom551, "@#DJULIAN@ 1700")]
    [InlineData("@I1@", GedcomSpecVersion.Gedcom551, "@I1@")]
    [InlineData("@I1@", GedcomSpecVersion.Gedcom70, "@I1@")]
    [InlineData("@VOID@", GedcomSpecVersion.Gedcom551, "@VOID@")]
    [InlineData("@VOID@", GedcomSpecVersion.Gedcom70, "@VOID@")]
    public void DecodesEscapes(string raw, GedcomSpecVersion version, string expected)
    {
        Assert.Equal(expected, GedcomValueCodec.Decode(raw, version));
    }

    [Theory]
    [InlineData("@@x", GedcomSpecVersion.Gedcom551)]
    [InlineData("@@x", GedcomSpecVersion.Gedcom70)]
    [InlineData("a@@b", GedcomSpecVersion.Gedcom551)]
    [InlineData("a@@b", GedcomSpecVersion.Gedcom70)]
    [InlineData("@#DJULIAN@ 1700", GedcomSpecVersion.Gedcom551)]
    [InlineData("@I1@", GedcomSpecVersion.Gedcom551)]
    [InlineData("@I1@", GedcomSpecVersion.Gedcom70)]
    [InlineData("@VOID@", GedcomSpecVersion.Gedcom551)]
    [InlineData("@VOID@", GedcomSpecVersion.Gedcom70)]
    public void EncodeIsInverseOfDecode(string raw, GedcomSpecVersion version)
    {
        var isPointer = GedcomValueCodec.IsPointer(raw);
        var decoded = GedcomValueCodec.Decode(raw, version);

        Assert.Equal(raw, GedcomValueCodec.Encode(decoded, isPointer, version));
        Assert.Equal(decoded, GedcomValueCodec.Decode(GedcomValueCodec.Encode(decoded, isPointer, version), version));
    }

    [Theory]
    [InlineData("@#DJULIAN@ 1700", GedcomSpecVersion.Gedcom70, "@@#DJULIAN@ 1700")]
    [InlineData("@#DJULIAN@ 1700", GedcomSpecVersion.Gedcom551, "@#DJULIAN@ 1700")]
    [InlineData("@I1@", GedcomSpecVersion.Gedcom70, "@@I1@")]
    [InlineData("x@y", GedcomSpecVersion.Gedcom70, "x@y")]
    public void EncodeEscapesLeadingAtOfNonPointers(string value, GedcomSpecVersion version, string expected)
    {
        Assert.Equal(expected, GedcomValueCodec.Encode(value, isPointer: false, version));
    }

    [Fact]
    public void DecodeAndEncodeKeepNull()
    {
        Assert.Null(GedcomValueCodec.Decode(null, GedcomSpecVersion.Gedcom70));
        Assert.Null(GedcomValueCodec.Encode(null, isPointer: false, GedcomSpecVersion.Gedcom70));
    }
}
