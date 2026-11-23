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
}
