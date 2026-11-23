using System.Globalization;
using System.Text;
using Gedcom.Vector.Model;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Model;

public class GedcomStructureTextTests
{
    private static readonly GedcomSpecVersion[] Versions = [GedcomSpecVersion.Gedcom551, GedcomSpecVersion.Gedcom70];

    private static List<GedcomStructure> Records(string text, string version = "5.5.1")
    {
        var full = "0 HEAD\n1 GEDC\n2 VERS " + version + "\n1 CHAR UTF-8\n" + text + "0 TRLR\n";
        using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(full));
        return GedcomDocumentReader.Read(stream).Document.ExtensionRecords.Select(r => r.Structure).ToList();
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

    private static void AssertRoundTrips(GedcomStructure structure)
    {
        // ExtensionUri is not part of the text, so it is outside the contract.
        foreach (var node in Flatten(structure))
        {
            node.ExtensionUri = null;
        }

        foreach (var version in Versions)
        {
            foreach (var baseLevel in new[] { 0, 3 })
            {
                var text = structure.ToGedcomText(version, baseLevel);
                var parsed = GedcomStructure.ParseGedcomText(text, version);
                StructureAssert.Equal(structure, parsed);
            }
        }
    }

    private static IEnumerable<GedcomStructure> Flatten(GedcomStructure structure)
    {
        yield return structure;
        foreach (var child in structure.Children)
        {
            foreach (var inner in Flatten(child))
            {
                yield return inner;
            }
        }
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("ansel-551.ged")]
    [InlineData("ansi-551.ged")]
    [InlineData("utf16le-551.ged")]
    [InlineData("utf8bom-551.ged")]
    [InlineData("utf8-70.ged")]
    public void RoundTripsEverySmokeCorpusRecord(string fileName)
    {
        using var stream = File.OpenRead(CorpusPath(fileName));
        var document = GedcomDocumentReader.Read(stream).Document;

        Assert.NotEmpty(document.ExtensionRecords);
        foreach (var record in document.ExtensionRecords)
        {
            AssertRoundTrips(record.Structure);
            var own = GedcomStructure.ParseGedcomText(record.Structure.ToGedcomText(document.SourceVersion), document.SourceVersion);
            StructureAssert.Equal(record.Structure, own);
        }
    }

    [Fact]
    public void RoundTripsMultilineValues()
    {
        var records = Records(
            "0 @N1@ NOTE first\n1 CONT  leading\n1 CONT trailing \n1 CONT\n1 CONT\n1 CONC tail\n1 CONT last \n"
            + "0 _A\n1 NOTE\n2 CONT only continuation\n1 NOTE x\n2 CONT\n"
            + "0 _B  two leading spaces \n");

        Assert.Contains('\n', records[1].Value!);
        foreach (var record in records)
        {
            AssertRoundTrips(record);
        }
    }

    [Fact]
    public void RoundTripsLinesWithRunsOfSpacesBeforeTheTag()
    {
        var records = Records("0 @I1@  INDI\n1  NAME John /Smith/\n");

        var person = records[1];
        Assert.Equal("INDI", person.Tag);
        Assert.Equal("@I1@", person.Xref);
        Assert.Equal("John /Smith/", Assert.Single(person.Children).Value);
        AssertRoundTrips(person);

        var parsed = GedcomStructure.ParseGedcomText("0 @I1@  INDI\n1  NAME John /Smith/\n");
        Assert.Equal("INDI", parsed.Tag);
        Assert.Equal("NAME", Assert.Single(parsed.Children).Tag);
        Assert.Equal("0 @I1@ INDI\n1 NAME John /Smith/\n", parsed.ToGedcomText(GedcomSpecVersion.Gedcom70));
    }

    [Theory]
    [InlineData("@@x", "5.5.1")]
    [InlineData("@@I1@", "5.5.1")]
    [InlineData("@I1@", "5.5.1")]
    [InlineData("@VOID@", "5.5.1")]
    [InlineData("@@#DJULIAN@", "5.5.1")]
    [InlineData("@#DJULIAN@", "5.5.1")]
    [InlineData("a@@b", "5.5.1")]
    [InlineData("@@@@", "5.5.1")]
    [InlineData("@", "5.5.1")]
    [InlineData("@@", "5.5.1")]
    [InlineData("@@x", "7.0")]
    [InlineData("@@I1@", "7.0")]
    [InlineData("@I1@", "7.0")]
    [InlineData("@VOID@", "7.0")]
    [InlineData("@@#DJULIAN@", "7.0")]
    [InlineData("a@@b", "7.0")]
    [InlineData("@@@@", "7.0")]
    [InlineData("@", "7.0")]
    [InlineData("@@", "7.0")]
    public void RoundTripsAtSignValues(string raw, string version)
    {
        var text = "0 _X " + raw + "\n1 _Y " + raw + "\n1 _Z\n2 CONT " + raw + "\n2 CONC " + raw + "\n0 _W\n1 CONC " + raw + "\n1 CONT " + raw + "\n";
        foreach (var record in Records(text, version))
        {
            AssertRoundTrips(record);
        }
    }

    [Theory]
    [InlineData("a\n@I1@", "5.5.1", "0 _X a\n1 CONT @@I1@\n")]
    [InlineData("a\n@I1@", "7.0", "0 _X a\n1 CONT @@I1@\n")]
    [InlineData("a\n@@x", "5.5.1", "0 _X a\n1 CONT @@@x\n")]
    [InlineData("a\n@@x", "7.0", "0 _X a\n1 CONT @@@x\n")]
    [InlineData("a\n@#DJULIAN@ 1700", "5.5.1", "0 _X a\n1 CONT @#DJULIAN@ 1700\n")]
    [InlineData("a\n@#DJULIAN@ 1700", "7.0", "0 _X a\n1 CONT @@#DJULIAN@ 1700\n")]
    [InlineData("@x\n@y", "7.0", "0 _X @@x\n1 CONT @@y\n")]
    public void EscapesEachContinuationLineOnItsOwn(string value, string version, string expected)
    {
        var specVersion = version == "7.0" ? GedcomSpecVersion.Gedcom70 : GedcomSpecVersion.Gedcom551;
        var structure = new GedcomStructure { Tag = "_X", Value = value };

        var text = structure.ToGedcomText(specVersion);

        Assert.Equal(expected, text);
        StructureAssert.Equal(structure, GedcomStructure.ParseGedcomText(text, specVersion));
        var read = Records(text, version)[1];
        Assert.Equal(value, read.Value);
        Assert.False(read.ValueIsPointer);
    }

    [Theory]
    [InlineData("0 _X a\n1 CONT @@I1@\n", "a\n@I1@")]
    [InlineData("0 _X a\n1 CONC @@b\n", "a@b")]
    [InlineData("0 _X a\n1 CONC b@@c\n", "ab@@c")]
    [InlineData("0 _X\n1 CONC @@@x\n", "@@x")]
    [InlineData("0 _X\n1 CONC @@x\n1 CONC @@y\n", "@x@y")]
    [InlineData("0 _X\n1 CONT @@x\n", "\n@x")]
    public void DecodesEachContinuationLineOnItsOwn(string text, string expected)
    {
        foreach (var version in new[] { "5.5.1", "7.0" })
        {
            Assert.Equal(expected, Records(text, version)[1].Value);
        }
    }

    [Fact]
    public void PointerFlagSurvivesTheRoundTrip()
    {
        var family = Records("0 @F1@ FAM\n1 HUSB @I1@\n1 NOTE @@I1@\n")[1];

        Assert.True(family.Children[0].ValueIsPointer);
        Assert.False(family.Children[1].ValueIsPointer);
        Assert.Equal("@I1@", family.Children[1].Value);
        AssertRoundTrips(family);
        Assert.Equal("0 @F1@ FAM\n1 HUSB @I1@\n1 NOTE @@I1@\n", family.ToGedcomText(GedcomSpecVersion.Gedcom70));
    }

    [Fact]
    public void Gedcom551CalendarEscapeComesBackThroughADifferentRawForm()
    {
        // Raw "@@#DJULIAN@" in 5.5.1 decodes to "@#DJULIAN@", which Encode keeps verbatim for 5.5.1, so the
        // written raw form differs from the original while the decoded structure is identical.
        var record = Records("0 _X\n1 DATE @@#DJULIAN@ 1 JAN 1700\n")[1];
        Assert.Equal("@#DJULIAN@ 1 JAN 1700", record.Children[0].Value);

        var text = record.ToGedcomText(GedcomSpecVersion.Gedcom551);

        Assert.Equal("0 _X\n1 DATE @#DJULIAN@ 1 JAN 1700\n", text);
        StructureAssert.Equal(record, GedcomStructure.ParseGedcomText(text, GedcomSpecVersion.Gedcom551));
    }

    [Fact]
    public void RoundTripsPlaceholderStructures()
    {
        var records = Records("0 _A\n1\n2 NOTE a\n1 @X1@\n2 CONT line\n2 CONC more\n1 NOTE after\n0\n0 @Z@\n");

        Assert.Contains(records.SelectMany(Flatten), s => s.Tag.Length == 0 && s.Value is not null);
        foreach (var record in records)
        {
            AssertRoundTrips(record);
        }
    }

    [Fact]
    public void FoldsAContinuationAfterTextAppendedToAPointer()
    {
        var person = Records("0 @I1@ INDI\n1 FAMC @F1@\nstray\n2 CONC x\n")[1];
        var famc = Assert.Single(person.Children);

        Assert.Equal("@F1@\nstrayx", famc.Value);
        Assert.False(famc.ValueIsPointer);
        Assert.Empty(famc.Children);

        var parsed = GedcomStructure.ParseGedcomText(person.ToGedcomText(GedcomSpecVersion.Gedcom551), GedcomSpecVersion.Gedcom551);
        Assert.Equal("@F1@\nstrayx", Assert.Single(parsed.Children).Value);
        Assert.Empty(parsed.Children[0].Children);
        AssertRoundTrips(person);
    }

    [Fact]
    public void RoundTripsMisplacedContinuations()
    {
        var records = Records("0 @F1@ FAM\n1 HUSB @I1@\n2 CONT x\n2 CONC y\n0 CONT z\n0 _B\n1 NOTE a\n2 @X@ CONT q\n");

        Assert.Contains(records.SelectMany(Flatten), s => s.Tag == "CONT");
        foreach (var record in records)
        {
            AssertRoundTrips(record);
        }
    }

    [Fact]
    public void ToTextWritesOneLinePerStructureWithContForNewlines()
    {
        var structure = new GedcomStructure
        {
            Tag = "NOTE",
            Xref = "@N1@",
            Value = "a\nb\n\nc",
            Children = { new GedcomStructure { Tag = "SOUR", Value = "@S1@", ValueIsPointer = true } },
        };

        Assert.Equal(
            "2 @N1@ NOTE a\n3 CONT b\n3 CONT\n3 CONT c\n3 SOUR @S1@\n",
            structure.ToGedcomText(GedcomSpecVersion.Gedcom70, 2));
    }

    [Fact]
    public void ToTextWritesEmptyAndNullValuesAlike()
    {
        var withEmpty = new GedcomStructure { Tag = "NOTE", Value = "" };
        var withNull = new GedcomStructure { Tag = "NOTE" };

        Assert.Equal("0 NOTE\n", withNull.ToGedcomText(GedcomSpecVersion.Gedcom70));
        Assert.Equal("0 NOTE\n", withEmpty.ToGedcomText(GedcomSpecVersion.Gedcom70));
    }

    [Fact]
    public void ToTextWritesPlaceholderWithoutTag()
    {
        var plain = new GedcomStructure { Tag = "", Xref = "@X@" };
        var folded = new GedcomStructure { Tag = "", Value = "\nline" };
        var concat = new GedcomStructure { Tag = "", Value = "head\nline" };

        Assert.Equal("1 @X@\n", plain.ToGedcomText(GedcomSpecVersion.Gedcom70, 1));
        Assert.Equal("1\n2 CONT line\n", folded.ToGedcomText(GedcomSpecVersion.Gedcom70, 1));
        Assert.Equal("1\n2 CONC head\n2 CONT line\n", concat.ToGedcomText(GedcomSpecVersion.Gedcom70, 1));
        StructureAssert.Equal(concat, GedcomStructure.ParseGedcomText(concat.ToGedcomText(GedcomSpecVersion.Gedcom70, 1)));
    }

    [Fact]
    public void ToTextDoesNotWriteExtensionUriOrSourceLine()
    {
        var structure = new GedcomStructure { Tag = "_TAG", ExtensionUri = "https://example.com/x", SourceLine = 42 };

        Assert.Equal("0 _TAG\n", structure.ToGedcomText(GedcomSpecVersion.Gedcom70));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public void HonoursBaseLevel(int baseLevel)
    {
        var structure = new GedcomStructure
        {
            Tag = "A",
            Children = { new GedcomStructure { Tag = "B", Children = { new GedcomStructure { Tag = "C", Value = "x\ny" } } } },
        };

        var text = structure.ToGedcomText(GedcomSpecVersion.Gedcom70, baseLevel);

        Assert.Equal(
            $"{Number(baseLevel)} A\n{Number(baseLevel + 1)} B\n{Number(baseLevel + 2)} C x\n{Number(baseLevel + 3)} CONT y\n",
            text);
        var parsed = GedcomStructure.ParseGedcomText(text);
        StructureAssert.Equal(structure, parsed);
        Assert.Equal(1, parsed.SourceLine);
        Assert.Equal(2, parsed.Children[0].SourceLine);
        Assert.Equal(3, parsed.Children[0].Children[0].SourceLine);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void ToTextRejectsBaseLevelOutOfRange(int baseLevel)
    {
        var structure = new GedcomStructure { Tag = "A" };

        Assert.Throws<ArgumentOutOfRangeException>(() => structure.ToGedcomText(GedcomSpecVersion.Gedcom70, baseLevel));
    }

    [Fact]
    public void ParseAppliesTheVersionDecodingSteps()
    {
        var parsed = GedcomStructure.ParseGedcomText("0 _X @@x\n1 _P @I1@\n1 _Q @@I1@\n");

        Assert.Equal("@x", parsed.Value);
        Assert.False(parsed.ValueIsPointer);
        Assert.True(parsed.Children[0].ValueIsPointer);
        Assert.Equal("@I1@", parsed.Children[0].Value);
        Assert.False(parsed.Children[1].ValueIsPointer);
        Assert.Equal("@I1@", parsed.Children[1].Value);
    }

    [Fact]
    public void ParseToleratesBlankLinesLeadingWhitespaceAndCrLf()
    {
        var parsed = GedcomStructure.ParseGedcomText("\r\n  1 NOTE a\r\n\r\n2 CONT b\r\n");

        Assert.Equal("NOTE", parsed.Tag);
        Assert.Equal("a\nb", parsed.Value);
        Assert.Equal(2, parsed.SourceLine);
    }

    [Fact]
    public void ParseRejectsTwoTopLevelStructures()
    {
        Assert.Throws<FormatException>(() => GedcomStructure.ParseGedcomText("0 A\n0 B\n"));
        Assert.Throws<FormatException>(() => GedcomStructure.ParseGedcomText("2 A\n2 B\n"));
        Assert.Throws<FormatException>(() => GedcomStructure.ParseGedcomText("2 A\n1 B\n"));
        Assert.Throws<FormatException>(() => GedcomStructure.ParseGedcomText("2 A\n0 B\n"));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   \n\t\r\n", null)]
    [InlineData("NOTE x\n", "GV1004")]
    [InlineData("0 A\nstray\n", "GV1003")]
    [InlineData("0 A\n2 B\n", "GV1009")]
    [InlineData("1 A\n3 B\n", "GV1009")]
    public void ParseRejectsMalformedText(string text, string? code)
    {
        var exception = Assert.Throws<FormatException>(() => GedcomStructure.ParseGedcomText(text));

        if (code is not null)
        {
            Assert.Contains(code, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ParseErrorNamesTheLineWithinTheText()
    {
        var exception = Assert.Throws<FormatException>(() => GedcomStructure.ParseGedcomText("2 A\n3 B\n5 C\n"));

        Assert.Contains("GV1009", exception.Message, StringComparison.Ordinal);
        Assert.Contains("line 3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseKeepsAContinuationFirstLineAsAStructure()
    {
        var parsed = GedcomStructure.ParseGedcomText("2 CONT x\n");

        Assert.Equal("CONT", parsed.Tag);
        Assert.Equal("x", parsed.Value);
    }

    [Theory]
    [InlineData("a\rb", "a\nb")]
    [InlineData("x\r0 @I9@ INDI", "x\n0 @I9@ INDI")]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\n\rb", "a\n\nb")]
    public void ToTextTreatsCarriageReturnsAsLineBreaks(string value, string expected)
    {
        var structure = new GedcomStructure { Tag = "NOTE", Value = value };

        var parsed = GedcomStructure.ParseGedcomText(structure.ToGedcomText(GedcomSpecVersion.Gedcom70));

        Assert.Equal("NOTE", parsed.Tag);
        Assert.Equal(expected, parsed.Value);
        Assert.Empty(parsed.Children);
        Assert.DoesNotContain('\r', structure.ToGedcomText(GedcomSpecVersion.Gedcom70));
    }

    private static GedcomStructure Chain(int count)
    {
        var root = new GedcomStructure { Tag = "A" };
        var node = root;
        for (var i = 1; i < count; i++)
        {
            var child = new GedcomStructure { Tag = "A" };
            node.Children.Add(child);
            node = child;
        }

        return root;
    }

    [Fact]
    public void ToTextRejectsLevelsAbove999()
    {
        var tooDeep = Chain(1001);
        var exception = Assert.Throws<InvalidOperationException>(() => tooDeep.ToGedcomText(GedcomSpecVersion.Gedcom70));
        Assert.Contains("A.A", exception.Message, StringComparison.Ordinal);

        Assert.Contains("\n999 A\n", Chain(1000).ToGedcomText(GedcomSpecVersion.Gedcom70), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => Chain(1000).ToGedcomText(GedcomSpecVersion.Gedcom70, 1));
        Assert.Throws<InvalidOperationException>(() => Chain(998).ToGedcomText(GedcomSpecVersion.Gedcom70, 3));
        Assert.Contains("\n999 A\n", Chain(997).ToGedcomText(GedcomSpecVersion.Gedcom70, 3), StringComparison.Ordinal);
    }

    [Fact]
    public void ToTextRejectsAContinuationLineAbove999()
    {
        var chain = Chain(1000);
        var last = chain;
        while (last.Children.Count > 0)
        {
            last = last.Children[0];
        }

        last.Value = "a\nb";

        Assert.Throws<InvalidOperationException>(() => chain.ToGedcomText(GedcomSpecVersion.Gedcom70));
    }

    [Theory]
    [InlineData("A B", null)]
    [InlineData("A\nB", null)]
    [InlineData("A\rB", null)]
    [InlineData("NOTE", "")]
    [InlineData("NOTE", "@X 1@")]
    [InlineData("NOTE", "@X\n1@")]
    [InlineData("NOTE", "I1@")]
    [InlineData("NOTE", "I1")]
    [InlineData("@X", null)]
    [InlineData("@", null)]
    public void ToTextRejectsUnsafeTagsAndXrefs(string tag, string? xref)
    {
        var structure = new GedcomStructure { Tag = "INDI", Children = { new GedcomStructure { Tag = tag, Xref = xref, Value = "secret" } } };

        var exception = Assert.Throws<InvalidOperationException>(() => structure.ToGedcomText(GedcomSpecVersion.Gedcom70));

        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
        Assert.Contains("INDI.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToTextExceptionNamesOnlyTheEmptyXrefCase()
    {
        var structure = new GedcomStructure { Tag = "NOTE", Xref = "" };

        var exception = Assert.Throws<InvalidOperationException>(() => structure.ToGedcomText(GedcomSpecVersion.Gedcom70));

        Assert.Contains("its xref is empty", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("tag or xref is empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTripsReaderProducedXrefsWithoutClosingAtAndTagsStartingWithAt()
    {
        var records = Records("0 @I1 INDI\n1 NAME a\n0 @I2@ @X v\n0 @ NOTE n\n0 @I@3@ FAM\n");

        Assert.Contains(records, s => s.Xref == "@I1");
        Assert.Contains(records, s => s.Xref == "@I2@" && s.Tag == "@X");
        Assert.Contains(records, s => s.Xref == "@");
        foreach (var record in records)
        {
            AssertRoundTrips(record);
        }
    }

    [Fact]
    public void RoundTripsTagsAndXrefsWithTabsOrNbsp()
    {
        var records = Records("0 A\tB x\n1 _X Y v\n0 @a\tb@ NOTE v\n");

        Assert.Contains(records.SelectMany(Flatten), s => s.Tag == "A\tB");
        Assert.Contains(records.SelectMany(Flatten), s => s.Tag == "_X Y");
        Assert.Contains(records, s => s.Xref == "@a\tb@");
        foreach (var record in records)
        {
            AssertRoundTrips(record);
        }
    }

    [Fact]
    public void ParseRejectsLinesOverTheLengthCap()
    {
        var text = "0 _X " + new string('a', 1_048_580) + "\n";

        var exception = Assert.Throws<FormatException>(() => GedcomStructure.ParseGedcomText(text));

        Assert.Contains("GV1012", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseAcceptsAFirstLineAtLevelsUpTo999()
    {
        var parsed = GedcomStructure.ParseGedcomText("150 _X v\n151 _Y w\n");

        Assert.Equal("_X", parsed.Tag);
        Assert.Equal("v", parsed.Value);
        Assert.Equal("w", parsed.Children[0].Value);
        Assert.Equal(1, parsed.SourceLine);
        Assert.Equal("_X", GedcomStructure.ParseGedcomText("999 _X v\n").Tag);
    }

    [Fact]
    public void ParseRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => GedcomStructure.ParseGedcomText(null!));
    }
}
