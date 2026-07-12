using System.Text;
using Gedcom.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gedcom.Vector.Tests.Gedcom;

public class GedcomImportAdapterEncodingTests
{
    static GedcomImportAdapterEncodingTests()
    {
        // GedcomEncodingDetector's own static constructor registers this too,
        // but this test constructs its Windows-1252 input bytes directly via
        // Encoding.GetEncoding(1252) before the adapter (and therefore the
        // detector) is ever invoked, so the test needs its own registration.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static GedcomImportAdapter CreateAdapter() =>
        new(NullLogger<GedcomImportAdapter>.Instance, Options.Create(new GedcomImportOptions()));

    [Fact]
    public void Parse_Utf8WithBom_DecodesNameCorrectly()
    {
        var gedcom = "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Renée /École/\n0 TRLR\n";
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(gedcom)).ToArray();

        var result = CreateAdapter().Parse(new MemoryStream(bytes));

        Assert.Single(result.Persons);
        Assert.Equal("Renée", result.Persons[0].FirstName);
        Assert.Equal("École", result.Persons[0].LastName);
    }

    [Fact]
    public void Parse_Utf16WithBom_DecodesNameCorrectly()
    {
        var gedcom = "0 HEAD\n1 CHAR UNICODE\n0 @I1@ INDI\n1 NAME Björn /Sørensen/\n0 TRLR\n";
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes(gedcom)).ToArray();

        var result = CreateAdapter().Parse(new MemoryStream(bytes));

        Assert.Single(result.Persons);
        Assert.Equal("Björn", result.Persons[0].FirstName);
        Assert.Equal("Sørensen", result.Persons[0].LastName);
    }

    [Fact]
    public void Parse_DeclaredAnsel_DecodesCombiningDiacriticsCorrectly()
    {
        // "Muñoz" in ANSEL: M, u, [0xE4 tilde], n, o, z. "García" in ANSEL:
        // G, a, r, c, [0xE2 acute], i, a (the acute precedes the 'i' it modifies).
        var header = "0 HEAD\n1 CHAR ANSEL\n0 @I1@ INDI\n1 NAME "u8.ToArray();
        var nameBytes = new List<byte>();
        nameBytes.AddRange("Mu"u8.ToArray());
        nameBytes.Add(0xE4);
        nameBytes.AddRange("noz /Garc"u8.ToArray());
        nameBytes.Add(0xE2);
        nameBytes.AddRange("ia/"u8.ToArray());
        var trailer = "\n0 TRLR\n"u8.ToArray();

        var bytes = header.Concat(nameBytes).Concat(trailer).ToArray();

        var result = CreateAdapter().Parse(new MemoryStream(bytes));

        Assert.Single(result.Persons);
        Assert.Equal("Muñoz", result.Persons[0].FirstName);
        Assert.Equal("García", result.Persons[0].LastName);
    }

    [Fact]
    public void Parse_DeclaredAnsi_DecodesWindows1252Correctly()
    {
        var gedcom = "0 HEAD\n1 CHAR ANSI\n0 @I1@ INDI\n1 NAME François /Müller/\n0 TRLR\n";
        var bytes = Encoding.GetEncoding(1252).GetBytes(gedcom);

        var result = CreateAdapter().Parse(new MemoryStream(bytes));

        Assert.Single(result.Persons);
        Assert.Equal("François", result.Persons[0].FirstName);
        Assert.Equal("Müller", result.Persons[0].LastName);
    }

    [Fact]
    public void Parse_NoCharTagDeclared_StillDefaultsToUtf8BackwardCompatibly()
    {
        var gedcom = "0 HEAD\n0 @I1@ INDI\n1 NAME John /Doe/\n0 TRLR\n";
        var bytes = Encoding.UTF8.GetBytes(gedcom);

        var result = CreateAdapter().Parse(new MemoryStream(bytes));

        Assert.Single(result.Persons);
        Assert.Equal("John", result.Persons[0].FirstName);
        Assert.Equal("Doe", result.Persons[0].LastName);
    }
}

