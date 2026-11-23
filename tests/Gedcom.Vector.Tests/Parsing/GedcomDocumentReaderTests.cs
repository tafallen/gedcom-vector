using System.Text;
using Gedcom.Vector.Diagnostics;
using Gedcom.Vector.Model;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Parsing;

public class GedcomDocumentReaderTests
{
    private const string NonSeekableMessage =
        "The provided stream must be seekable. For network or compressed streams, please buffer the content into a MemoryStream first.";

    private static GedcomReadResult Read(string text, Encoding? encoding = null, GedcomReadOptions? options = null)
    {
        using var stream = new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(text));
        return GedcomDocumentReader.Read(stream, options);
    }

    private static string Head(string? version, string charset = "UTF-8") =>
        "0 HEAD\n1 GEDC\n" + (version is null ? "" : "2 VERS " + version + "\n") + "1 CHAR " + charset + "\n";

    private static int Count(GedcomDocument document, GedcomDiagnosticCode code) =>
        document.Diagnostics.Count(d => d.Code == code);

    private static GedcomDiagnostic Single(GedcomDocument document, GedcomDiagnosticCode code) =>
        Assert.Single(document.Diagnostics, d => d.Code == code);

    private static GedcomStructure Child(GedcomStructure parent, string tag) =>
        parent.Children.First(c => c.Tag == tag);

    [Theory]
    [InlineData("7.0", GedcomSpecVersion.Gedcom70, false)]
    [InlineData("7.0.18", GedcomSpecVersion.Gedcom70, false)]
    [InlineData("5.5.1", GedcomSpecVersion.Gedcom551, false)]
    [InlineData("5.5", GedcomSpecVersion.Gedcom551, false)]
    [InlineData("4.0", GedcomSpecVersion.Gedcom551, true)]
    [InlineData(null, GedcomSpecVersion.Gedcom551, true)]
    public void DetectsVersionFromHead(string? version, GedcomSpecVersion expected, bool unknown)
    {
        var document = Read(Head(version) + "0 TRLR\n").Document;

        Assert.Equal(expected, document.SourceVersion);
        Assert.Equal(unknown ? 1 : 0, Count(document, GedcomDiagnosticCode.UnknownVersion));
        Assert.Equal(0, Count(document, GedcomDiagnosticCode.MissingHeader));
        Assert.Equal(0, Count(document, GedcomDiagnosticCode.NonUtf8Encoding));
    }

    [Fact]
    public void ReportsMissingHeaderAndTrailer()
    {
        var document = Read("0 @I1@ INDI\n1 NAME John /Doe/\n").Document;

        Assert.Equal(GedcomSpecVersion.Gedcom551, document.SourceVersion);
        Single(document, GedcomDiagnosticCode.MissingHeader);
        Assert.Null(Single(document, GedcomDiagnosticCode.MissingTrailer).Line);
        Assert.Equal(0, Count(document, GedcomDiagnosticCode.UnknownVersion));
        Assert.Single(document.ExtensionRecords);
    }

    [Fact]
    public void ReportsMissingHeaderForEmptyInput()
    {
        var document = Read("").Document;

        Assert.Equal(GedcomSpecVersion.Gedcom551, document.SourceVersion);
        Assert.Empty(document.ExtensionRecords);
        Single(document, GedcomDiagnosticCode.MissingHeader);
        Single(document, GedcomDiagnosticCode.MissingTrailer);
    }

    [Fact]
    public void KeepsRecordAfterTrailer()
    {
        // Lines: 1-4 HEAD, 5 TRLR, 6 @I2@, 7 @I3@.
        var document = Read(Head("5.5.1") + "0 TRLR\n0 @I2@ INDI\n0 @I3@ INDI\n").Document;

        Assert.Equal(["HEAD", "TRLR", "INDI", "INDI"], document.ExtensionRecords.ConvertAll(r => r.Structure.Tag));
        Assert.Equal("@I3@", document.ExtensionRecords[3].Structure.Xref);
        Assert.Equal(6, Single(document, GedcomDiagnosticCode.RecordAfterTrailer).Line);
        Assert.Equal(0, Count(document, GedcomDiagnosticCode.MissingTrailer));
    }

    [Theory]
    [InlineData("ANSI")]
    [InlineData("ANSEL")]
    [InlineData("WIN-1252")]
    public void ReportsNonUtf8For70(string charset)
    {
        // Line 3 is the VERS line.
        var document = Read(Head("7.0", charset) + "0 TRLR\n", Encoding.Latin1).Document;

        Assert.Equal(GedcomSpecVersion.Gedcom70, document.SourceVersion);
        var diagnostic = Single(document, GedcomDiagnosticCode.NonUtf8Encoding);
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(GedcomDiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void DoesNotReportNonUtf8For551()
    {
        var document = Read(Head("5.5.1", "ANSI") + "0 TRLR\n", Encoding.Latin1).Document;

        Assert.Equal(0, Count(document, GedcomDiagnosticCode.NonUtf8Encoding));
    }

    [Fact]
    public void DecodesAnselAfterFolding()
    {
        // 0xE2 (combining acute) ends one line; the base letter arrives on the CONC line.
        var text = Head("5.5.1", "ANSEL") + "0 @N1@ NOTE cafâ\n1 CONC e\n0 TRLR\n";

        var document = Read(text, Encoding.Latin1).Document;

        Assert.Equal("café", document.ExtensionRecords[1].Structure.Value);
    }

    [Fact]
    public void DecodesEscapesAndFlagsPointers()
    {
        var text = Head("5.5.1")
            + "0 @I1@ INDI\n1 FAMS @F1@\n1 NOTE @@x\n1 NOTE @@I1@\n1 NOTE a@@b\n1 DATE @#DJULIAN@ 1700\n0 TRLR\n";

        var person = Read(text).Document.ExtensionRecords[1].Structure;

        Assert.Equal("@F1@", person.Children[0].Value);
        Assert.True(person.Children[0].ValueIsPointer);
        Assert.Equal("@x", person.Children[1].Value);
        Assert.False(person.Children[1].ValueIsPointer);
        Assert.Equal("@I1@", person.Children[2].Value);
        Assert.False(person.Children[2].ValueIsPointer);
        Assert.Equal("a@@b", person.Children[3].Value);
        Assert.Equal("@#DJULIAN@ 1700", person.Children[4].Value);
        Assert.False(person.ValueIsPointer);
        Assert.Equal("@I1@", person.Xref);
    }

    [Fact]
    public void ResolvesSchmaExtensionUris()
    {
        var text = "0 HEAD\n1 GEDC\n2 VERS 7.0\n1 SCHMA\n2 TAG _LOC https://example.org/_LOC\n2 TAG _NOSPACE\n"
            + "2 TAG NOPE https://example.org/NOPE\n0 @L1@ _LOC\n1 _LOC nested\n1 _OTHER y\n1 NOTE z\n0 TRLR\n";

        var document = Read(text).Document;

        var location = document.ExtensionRecords[1].Structure;
        Assert.Equal("https://example.org/_LOC", location.ExtensionUri);
        Assert.Equal("https://example.org/_LOC", Child(location, "_LOC").ExtensionUri);
        Assert.Null(Child(location, "_OTHER").ExtensionUri);
        Assert.Null(Child(location, "NOTE").ExtensionUri);
    }

    [Fact]
    public void IgnoresSchmaFor551()
    {
        var text = "0 HEAD\n1 GEDC\n2 VERS 5.5.1\n1 SCHMA\n2 TAG _LOC https://example.org/_LOC\n0 @L1@ _LOC\n0 TRLR\n";

        var document = Read(text).Document;

        Assert.Null(document.ExtensionRecords[1].Structure.ExtensionUri);
    }

    [Fact]
    public void KeepsEveryLevelZeroRecordInOrder()
    {
        var text = Head("5.5.1") + "0 @I1@ INDI\n0 @F1@ FAM\n0 @N1@\n1 NOTE kept\n0 _CUSTOM x\n0 TRLR\n";

        var records = Read(text).Document.ExtensionRecords;

        Assert.Equal(["HEAD", "INDI", "FAM", "", "_CUSTOM", "TRLR"], records.ConvertAll(r => r.Structure.Tag));
        Assert.Equal([0, 1, 2, 3, 4, 5], records.ConvertAll(r => r.SourceOrder ?? -1));
        Assert.Equal("@N1@", records[3].Structure.Xref);
        Assert.Equal("kept", Assert.Single(records[3].Structure.Children).Value);
    }

    [Theory]
    [InlineData("ansel-551.ged")]
    [InlineData("ansi-551.ged")]
    [InlineData("utf16le-551.ged")]
    [InlineData("utf8bom-551.ged")]
    [InlineData("utf8-70.ged")]
    public void ReadsEverySmokeCorpusFile(string fileName)
    {
        var path = CorpusPath(fileName);
        int expectedRecords;
        using (var stream = File.OpenRead(path))
        {
            var encoding = GedcomEncodingDetector.Detect(stream);
            using var reader = GedcomTextReaderFactory.Create(stream, encoding);
            expectedRecords = CountLevelZeroLines(reader.ReadToEnd());
        }

        GedcomDocument document;
        using (var stream = File.OpenRead(path))
        {
            document = GedcomDocumentReader.Read(stream).Document;
        }

        Assert.DoesNotContain(document.Diagnostics, d => d.Severity == GedcomDiagnosticSeverity.Error);
        Assert.Equal(expectedRecords, document.ExtensionRecords.Count);
    }

    [Fact]
    public void RejectsNonSeekableStream()
    {
        using var stream = new NonSeekableStream(Encoding.UTF8.GetBytes(Head("5.5.1") + "0 TRLR\n"));

        var exception = Assert.Throws<ArgumentException>(() => GedcomDocumentReader.Read(stream));

        Assert.Equal("stream", exception.ParamName);
        Assert.StartsWith(NonSeekableMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CapsDiagnostics()
    {
        var builder = new StringBuilder(Head("5.5.1"));
        for (var i = 1; i <= 20; i++)
        {
            builder.Append("1 @X").Append(i).Append("@ NOTE bad\n");
        }

        var document = Read(builder.ToString(), options: new GedcomReadOptions { MaxDiagnostics = 5 }).Document;

        Assert.Equal(6, document.Diagnostics.Count);
        Assert.Equal(GedcomDiagnosticCode.DiagnosticsTruncated, document.Diagnostics[^1].Code);
        Assert.All(document.Diagnostics.Take(5), d => Assert.Equal(GedcomDiagnosticCode.XrefOnSubstructure, d.Code));
    }

    [Fact]
    public void RecordsLineAccountingOnlyWhenAsked()
    {
        var text = Head("5.5.1") + "0 TRLR\n";

        Assert.Null(Read(text).Accounting);
        var accounting = Read(text, options: new GedcomReadOptions { RecordLineAccounting = true }).Accounting;
        Assert.NotNull(accounting);
        Assert.Equal(5, accounting.LineCount);
    }

    private static int CountLevelZeroLines(string text)
    {
        var count = 0;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimStart().TrimEnd('\r');
            if (trimmed == "0" || trimmed.StartsWith("0 ", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static string CorpusPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gedcom.Vector.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "tests", "corpus", "smoke", fileName);
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
