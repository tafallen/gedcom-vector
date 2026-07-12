using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom.Parsing;

public class AnselDecoderTests
{
    [Fact]
    public void Decode_PlainAscii_PassesThroughUnchanged()
    {
        var bytes = "John Doe"u8.ToArray();
        Assert.Equal("John Doe", AnselDecoder.Decode(bytes));
    }

    [Fact]
    public void Decode_AcuteAccentBeforeE_ProducesPrecomposedEAcute()
    {
        // ANSEL stores the combining mark BEFORE the base character it modifies.
        var bytes = new byte[] { 0xE2, (byte)'e' };
        Assert.Equal("é", AnselDecoder.Decode(bytes)); // e with acute (é)
    }

    [Fact]
    public void Decode_GraveAccentBeforeE_ProducesPrecomposedEGrave()
    {
        var bytes = new byte[] { 0xE1, (byte)'e' };
        Assert.Equal("è", AnselDecoder.Decode(bytes)); // e with grave (è)
    }

    [Fact]
    public void Decode_TildeBeforeN_ProducesPrecomposedNTilde()
    {
        var bytes = new byte[] { 0xE4, (byte)'n' };
        Assert.Equal("ñ", AnselDecoder.Decode(bytes)); // n with tilde (ñ)
    }

    [Fact]
    public void Decode_DiaeresisBeforeU_ProducesPrecomposedUDiaeresis()
    {
        var bytes = new byte[] { 0xE8, (byte)'u' };
        Assert.Equal("ü", AnselDecoder.Decode(bytes)); // u with diaeresis (ü)
    }

    [Fact]
    public void Decode_CedillaBeforeC_ProducesPrecomposedCCedilla()
    {
        var bytes = new byte[] { 0xF0, (byte)'c' };
        Assert.Equal("ç", AnselDecoder.Decode(bytes)); // c with cedilla (ç)
    }

    [Fact]
    public void Decode_CircleAboveBeforeA_ProducesPrecomposedARing()
    {
        var bytes = new byte[] { 0xEA, (byte)'a' };
        Assert.Equal("å", AnselDecoder.Decode(bytes)); // a with ring above (å)
    }

    [Fact]
    public void Decode_SpacingCharacter_MapsDirectly()
    {
        var bytes = new byte[] { 0xA2 }; // uppercase O with stroke
        Assert.Equal("Ø", AnselDecoder.Decode(bytes)); // Ø
    }

    [Fact]
    public void Decode_UnmappedHighByte_ProducesReplacementCharacter()
    {
        var bytes = new byte[] { 0x81 }; // not in the ANSEL table
        Assert.Equal("�", AnselDecoder.Decode(bytes));
    }

    [Fact]
    public void Decode_FullNameWithEmbeddedDiacritic_DecodesAllCharactersInOrder()
    {
        // "Jos" + acute-e (Jos[é]) + " " + "Mu" + cedilla-c + "oz" -> "José Muçoz"
        var bytes = new List<byte>();
        bytes.AddRange("Jos"u8.ToArray());
        bytes.Add(0xE2);
        bytes.Add((byte)'e');
        bytes.AddRange(" Mu"u8.ToArray());
        bytes.Add(0xF0);
        bytes.Add((byte)'c');
        bytes.AddRange("oz"u8.ToArray());

        Assert.Equal("José Muçoz", AnselDecoder.Decode(bytes.ToArray()));
    }

    [Fact]
    public void Decode_TrailingCombiningMarkWithNoBaseCharacter_AppendsMarkAnyway()
    {
        // Malformed input (a combining mark at end of string with nothing to attach to)
        // must not be silently dropped or throw.
        var bytes = new byte[] { (byte)'x', 0xE2 };
        Assert.Equal("x́", AnselDecoder.Decode(bytes));
    }
}

