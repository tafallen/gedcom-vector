using System.Text;
using Gedcom.Vector.Diagnostics;
using Gedcom.Vector.Model;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests.Parsing;

public class GedcomStructureReaderTests
{
    private static readonly GedcomEncodingResult Utf8 = new(new UTF8Encoding(false), false, 0);

    private sealed class ReadResult(List<GedcomStructure> records, DiagnosticBag diagnostics, LineAccounting accounting)
    {
        public List<GedcomStructure> Records { get; } = records;

        public IReadOnlyList<GedcomDiagnostic> Diagnostics => diagnostics.Items;

        public LineAccounting Accounting { get; } = accounting;

        public GedcomDiagnostic Single(GedcomDiagnosticCode code)
        {
            GedcomDiagnostic? found = null;
            foreach (var d in Diagnostics)
            {
                if (d.Code == code)
                {
                    Assert.Null(found);
                    found = d;
                }
            }

            Assert.NotNull(found);
            return found;
        }

        public int Count(GedcomDiagnosticCode code)
        {
            var count = 0;
            foreach (var d in Diagnostics)
            {
                if (d.Code == code) count++;
            }

            return count;
        }
    }

    private static ReadResult Read(string text, int? bufferSize = null, int? maxLineLength = null)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var lines = bufferSize is { } size
            ? new GedcomLineReader(stream, Utf8, size, maxLineLength ?? GedcomLineReader.DefaultMaxLineLength)
            : new GedcomLineReader(stream, Utf8);
        var diagnostics = new DiagnosticBag(1000);
        var accounting = new LineAccounting();
        var reader = new GedcomStructureReader(lines, new GedcomStringPool(), diagnostics, accounting);
        var records = new List<GedcomStructure>();
        while (reader.ReadRecord() is { } record)
        {
            records.Add(record);
        }

        return new ReadResult(records, diagnostics, accounting);
    }

    private static GedcomStructure FindFirst(IEnumerable<GedcomStructure> structures, string tag)
    {
        foreach (var s in structures)
        {
            if (s.Tag == tag) return s;
            if (TryFind(s.Children, tag) is { } child) return child;
        }

        throw new InvalidOperationException("Tag not found: " + tag);
    }

    private static GedcomStructure? TryFind(List<GedcomStructure> structures, string tag)
    {
        foreach (var s in structures)
        {
            if (s.Tag == tag) return s;
            if (TryFind(s.Children, tag) is { } child) return child;
        }

        return null;
    }

    [Fact]
    public void BuildsNestedRecordTrees()
    {
        var result = Read("0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME John /Doe/\n2 GIVN John\n1 BIRT\n2 DATE 1900\n0 TRLR\n");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(3, result.Records.Count);
        Assert.Equal(["HEAD", "INDI", "TRLR"], result.Records.ConvertAll(r => r.Tag));

        var indi = result.Records[1];
        Assert.Equal("@I1@", indi.Xref);
        Assert.Null(indi.Value);
        Assert.Equal(3, indi.SourceLine);
        Assert.Equal(2, indi.Children.Count);

        var name = indi.Children[0];
        Assert.Equal("NAME", name.Tag);
        Assert.Equal("John /Doe/", name.Value);
        Assert.Equal(4, name.SourceLine);
        Assert.False(name.ValueIsPointer);
        var givn = Assert.Single(name.Children);
        Assert.Equal("GIVN", givn.Tag);
        Assert.Equal("John", givn.Value);

        var birt = indi.Children[1];
        Assert.Equal("BIRT", birt.Tag);
        Assert.Null(birt.Value);
        Assert.Equal("1900", Assert.Single(birt.Children).Value);
        Assert.Equal(7, birt.Children[0].SourceLine);

        Assert.Empty(result.Records[2].Children);
        Assert.Null(result.Records[2].Xref);
    }

    [Fact]
    public void InternsTagsAndXrefs()
    {
        var pool = new GedcomStringPool();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("0 @I1@ INDI\n1 FAMS @F1@\n0 @F1@ FAM\n1 HUSB @I1@\n0 @I2@ INDI\n"));
        using var lines = new GedcomLineReader(stream, Utf8);
        var reader = new GedcomStructureReader(lines, pool, new DiagnosticBag(10), null);

        var first = reader.ReadRecord()!;
        var second = reader.ReadRecord()!;
        var third = reader.ReadRecord()!;

        Assert.Null(reader.ReadRecord());
        Assert.Same(first.Tag, third.Tag);
        Assert.Same(pool.GetOrAdd("@F1@".AsSpan()), second.Xref);
        Assert.Equal("@I1@", second.Children[0].Value);
        Assert.False(second.Children[0].ValueIsPointer);
    }

    [Fact]
    public void FoldsContAndConcPreservingSpaces()
    {
        var result = Read("0 @I1@ INDI\n1 NOTE abc\n2 CONC def \n2 CONC  ghi\n2 CONT\n2 CONT x\n");

        Assert.Empty(result.Diagnostics);
        var note = Assert.Single(result.Records[0].Children);
        Assert.Equal("abcdef  ghi\n\nx", note.Value);
        Assert.Empty(note.Children);
        for (var line = 3; line <= 6; line++)
        {
            Assert.Equal(new LineDisposition(LineDispositionKind.Folded, note, null), result.Accounting[line]);
        }
    }

    [Fact]
    public void FoldsIntoStructureWithoutValue()
    {
        var result = Read("0 @N1@ NOTE\n1 CONC\n1 CONT\n1 CONC a\n0 @N2@ NOTE\n1 CONC\n");

        Assert.Equal("\na", result.Records[0].Value);
        Assert.Null(result.Records[1].Value);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("0 @N1@ NOTE a\n1 SOUR @S1@\n1 CONT b\n", "NOTE", "a\nb", 3, "NOTE.CONT")]
    [InlineData("0 @I1@ INDI\n1 NOTE a\n2 SOUR @S1@\n3 PAGE p\n2 CONC b\n1 BIRT\n", "NOTE", "ab", 5, "INDI.NOTE.CONC")]
    public void FoldsContAfterChildrenWithInfo(string text, string targetTag, string expectedValue, int continuationLine, string tagPath)
    {
        var result = Read(text);

        var target = FindFirst(result.Records, targetTag);
        Assert.Equal(expectedValue, target.Value);
        Assert.Equal("SOUR", Assert.Single(target.Children).Tag);
        var info = result.Single(GedcomDiagnosticCode.ContinuationAfterChildren);
        Assert.Equal(GedcomDiagnosticSeverity.Info, info.Severity);
        Assert.Equal(continuationLine, info.Line);
        Assert.Equal(tagPath, info.TagPath);
        Assert.Single(result.Diagnostics);
        Assert.Equal(new LineDisposition(LineDispositionKind.Folded, target, null), result.Accounting[continuationLine]);
    }

    [Theory]
    [InlineData("0 HEAD\n0 CONC x\n", 2, "CONC", "CONC", null)]
    [InlineData("0 HEAD\n1 NOTE a\n2 @X1@ CONT b\n", 3, "CONT", "HEAD.NOTE.CONT", "a")]
    [InlineData("0 @I1@ INDI\n1 FAMC @F1@\n2 CONC x\n", 3, "CONC", "INDI.FAMC.CONC", "@F1@")]
    public void KeepsMisplacedContinuationAsStructure(string text, int line, string tag, string tagPath, string? parentValue)
    {
        var result = Read(text);

        var misplaced = result.Single(GedcomDiagnosticCode.ContinuationMisplaced);
        Assert.Equal(line, misplaced.Line);
        Assert.Equal(tagPath, misplaced.TagPath);
        Assert.Equal(GedcomDiagnosticSeverity.Warning, misplaced.Severity);

        var disposition = result.Accounting[line];
        Assert.NotNull(disposition);
        Assert.Equal(LineDispositionKind.Structure, disposition.Value.Kind);
        var structure = disposition.Value.Structure!;
        Assert.Equal(tag, structure.Tag);
        Assert.Equal(line, structure.SourceLine);
        Assert.Same(structure, FindFirst(result.Records, tag));

        // The structure the continuation would have folded into keeps its own value.
        if (parentValue is null)
        {
            Assert.Same(structure, result.Records[^1]);
            Assert.Null(result.Records[0].Value);
        }
        else
        {
            var parent = result.Records[0].Children[0];
            Assert.Same(structure, Assert.Single(parent.Children));
            Assert.Equal(parentValue, parent.Value);
        }
    }

    [Fact]
    public void ReparentsSkippedLevel()
    {
        var result = Read("0 @I1@ INDI\n1 BIRT\n3 DATE 1900\n4 TIME 12:00\n1 DEAT\n");

        var indi = Assert.Single(result.Records);
        Assert.Equal(["BIRT", "DEAT"], indi.Children.ConvertAll(c => c.Tag));
        var date = Assert.Single(indi.Children[0].Children);
        Assert.Equal("DATE", date.Tag);
        Assert.Equal("TIME", Assert.Single(date.Children).Tag);

        Assert.Equal(2, result.Count(GedcomDiagnosticCode.LevelSkipped));
        Assert.Equal(3, result.Diagnostics[0].Line);
        Assert.Equal("INDI.BIRT.DATE", result.Diagnostics[0].TagPath);
        Assert.Equal(4, result.Diagnostics[1].Line);
        Assert.Equal(GedcomDiagnosticSeverity.Warning, result.Diagnostics[0].Severity);
    }

    [Fact]
    public void FoldsSkippedLevelContinuationIntoDeepest()
    {
        var result = Read("0 @I1@ INDI\n1 NOTE a\n3 CONT b\n");

        var note = Assert.Single(result.Records[0].Children);
        Assert.Equal("a\nb", note.Value);
        Assert.Empty(note.Children);
        var skipped = result.Single(GedcomDiagnosticCode.LevelSkipped);
        Assert.Equal(3, skipped.Line);
        Assert.Single(result.Diagnostics);
        Assert.Equal(new LineDisposition(LineDispositionKind.Folded, note, null), result.Accounting[3]);
    }

    [Fact]
    public void StartsRecordWhenFirstLineIsNotLevelZero()
    {
        var result = Read("1 HEAD\n0 TRLR\n");

        Assert.Equal(["HEAD", "TRLR"], result.Records.ConvertAll(r => r.Tag));
        var skipped = Assert.Single(result.Diagnostics);
        Assert.Equal(GedcomDiagnosticCode.LevelSkipped, skipped.Code);
        Assert.Equal(1, skipped.Line);
        Assert.Equal("HEAD", skipped.TagPath);
    }

    [Theory]
    [InlineData("0 @N1@ NOTE first\nsecond line\n", "NOTE", "first\nsecond line", 1)]
    [InlineData("0 @N1@ NOTE first\nsecond line\n1 CONT third\nfourth\n", "NOTE", "first\nsecond line\nthird\nfourth", 2)]
    [InlineData("0 @I1@ INDI\n1 NOTE a\nb\n1 BIRT\n", "NOTE", "a\nb", 1)]
    [InlineData("0 @I1@ INDI\n1 NOTE\nb\n", "NOTE", "\nb", 1)]
    [InlineData("0 @I1@ INDI\n1 NOTE a\n\n   b c \n", "NOTE", "a\n\n   b c ", 1)]
    [InlineData("0 @I1@ INDI\n1 NOTE a\n\nb\n", "NOTE", "a\n\nb", 1)]
    [InlineData("0 @I1@ INDI\n1 NOTE a\n\n \n\t\n  b\n\n1 BIRT\n", "NOTE", "a\n\n\n\n  b", 1)]
    [InlineData("0 @I1@ INDI\n1 NOTE a\n\n1 BIRT\n\nb\n", "BIRT", "\n\nb", 1)]
    [InlineData("0 @I1@ INDI\n1 NOTE a\n\tb\n  c\n", "NOTE", "a\n\tb\n  c", 2)]
    [InlineData("0 @I1@ INDI\n1 NOTE a\n2 SOUR s\n2 CONT b\nlost\n", "NOTE", "a\nb\nlost", 1)]
    public void AppendsLineWithoutLevelToPreviousValue(string text, string tag, string expected, int expectedWarnings)
    {
        var result = Read(text);

        var target = FindFirst(result.Records, tag);
        Assert.Equal(expected, target.Value);
        Assert.Equal(expectedWarnings, result.Count(GedcomDiagnosticCode.LineWithoutLevel));
        foreach (var warning in result.Diagnostics)
        {
            if (warning.Code != GedcomDiagnosticCode.LineWithoutLevel) continue;
            Assert.Equal(GedcomDiagnosticSeverity.Warning, warning.Severity);
            Assert.Equal(new LineDisposition(LineDispositionKind.Folded, target, null), result.Accounting[warning.Line!.Value]);
        }
    }

    [Fact]
    public void KeepsUnplacedLineWithoutLevelAsPlaceholderRecord()
    {
        var result = Read("stray text\n0 HEAD\n");

        Assert.Equal(["", "HEAD"], result.Records.ConvertAll(r => r.Tag));
        var placeholder = result.Records[0];
        Assert.Equal("stray text", placeholder.Value);
        Assert.Null(placeholder.Xref);
        Assert.Equal(1, placeholder.SourceLine);
        Assert.Empty(placeholder.Children);
        Assert.Null(result.Records[1].Value);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(GedcomDiagnosticCode.LineWithoutLevelUnplaced, error.Code);
        Assert.Equal(GedcomDiagnosticSeverity.Error, error.Severity);
        Assert.Equal(1, error.Line);
        Assert.Null(error.RecordXref);
        Assert.Null(error.TagPath);
        Assert.Equal(new LineDisposition(LineDispositionKind.Structure, placeholder, null), result.Accounting[1]);
    }

    [Fact]
    public void UnplacedPlaceholderKeepsIndentationAndCollectsFollowingText()
    {
        var result = Read("\n  stray\n\n more\n1 NOTE n\n0 HEAD\n");

        Assert.Equal(["", "HEAD"], result.Records.ConvertAll(r => r.Tag));
        var placeholder = result.Records[0];
        Assert.Equal("  stray\n\n more", placeholder.Value);
        Assert.Equal(2, placeholder.SourceLine);
        Assert.Equal("NOTE", Assert.Single(placeholder.Children).Tag);
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.LineWithoutLevelUnplaced));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.LineWithoutLevel));
        Assert.Equal(0, result.Count(GedcomDiagnosticCode.LeadingWhitespace));
        Assert.Equal(LineDispositionKind.Diagnostic, result.Accounting[1]!.Value.Kind);
        Assert.Equal(new LineDisposition(LineDispositionKind.Structure, placeholder, null), result.Accounting[2]);
        Assert.Equal(LineDispositionKind.Diagnostic, result.Accounting[3]!.Value.Kind);
        Assert.Equal(new LineDisposition(LineDispositionKind.Folded, placeholder, null), result.Accounting[4]);
    }

    [Fact]
    public void AggregatesBlankLinesIntoOneDiagnostic()
    {
        var result = Read("0 HEAD\n\n1 CHAR UTF-8\n   \n\t\n0 TRLR\n\n");

        var blank = Assert.Single(result.Diagnostics);
        Assert.Equal(GedcomDiagnosticCode.BlankLinesIgnored, blank.Code);
        Assert.Equal(2, blank.Line);
        foreach (var line in new[] { 2, 4, 5, 7 })
        {
            var disposition = result.Accounting[line];
            Assert.NotNull(disposition);
            Assert.Equal(LineDispositionKind.Diagnostic, disposition.Value.Kind);
            Assert.Same(blank, disposition.Value.Diagnostic);
        }

        Assert.Equal("UTF-8", result.Records[0].Children[0].Value);
    }

    [Fact]
    public void KeepsTaglessLineAsPlaceholderStructure()
    {
        var result = Read("0 @I1@ INDI\n1 NOTE a\n2\n1 @X1@\n1 BIRT\n");

        var indi = Assert.Single(result.Records);
        Assert.Equal(["NOTE", "", "BIRT"], indi.Children.ConvertAll(c => c.Tag));
        var note = indi.Children[0];
        Assert.Equal("a", note.Value);
        var underNote = Assert.Single(note.Children);
        Assert.Equal("", underNote.Tag);
        Assert.Null(underNote.Xref);
        Assert.Null(underNote.Value);
        Assert.Equal(3, underNote.SourceLine);
        var withXref = indi.Children[1];
        Assert.Equal("@X1@", withXref.Xref);
        Assert.Null(withXref.Value);
        Assert.Equal(4, withXref.SourceLine);
        Assert.Empty(withXref.Children);

        Assert.Equal(new LineDisposition(LineDispositionKind.Structure, underNote, null), result.Accounting[3]);
        Assert.Equal(new LineDisposition(LineDispositionKind.Structure, withXref, null), result.Accounting[4]);
        Assert.Equal(2, result.Count(GedcomDiagnosticCode.MissingTag));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.XrefOnSubstructure));
        Assert.Equal(3, result.Diagnostics.Count);
        AssertDiagnostic(result.Diagnostics[0], GedcomDiagnosticCode.MissingTag, 3, "@I1@", "INDI.NOTE.?");
        AssertDiagnostic(result.Diagnostics[1], GedcomDiagnosticCode.MissingTag, 4, "@I1@", "INDI.?");
        Assert.Equal(GedcomDiagnosticSeverity.Warning, result.Diagnostics[0].Severity);
    }

    [Fact]
    public void ChildrenAfterTaglessLineStayUnderIt()
    {
        var result = Read(
            "0 @I1@ INDI\n1 NAME A /x/\n0 @I9@\n1 NAME B /y/\n1 BIRT\n2 DATE 1800\n2\n3 PLAC p\n2 DATE 1900\nstray\n0 TRLR\n");

        Assert.Equal(["INDI", "", "TRLR"], result.Records.ConvertAll(r => r.Tag));
        var first = result.Records[0];
        Assert.Equal("NAME", Assert.Single(first.Children).Tag);
        Assert.Equal("A /x/", first.Children[0].Value);

        var placeholder = result.Records[1];
        Assert.Equal("@I9@", placeholder.Xref);
        Assert.Null(placeholder.Value);
        Assert.Equal(3, placeholder.SourceLine);
        Assert.Equal(["NAME", "BIRT"], placeholder.Children.ConvertAll(c => c.Tag));
        Assert.Equal("B /y/", placeholder.Children[0].Value);

        var birt = placeholder.Children[1];
        Assert.Equal(["DATE", "", "DATE"], birt.Children.ConvertAll(c => c.Tag));
        Assert.Equal("1800", birt.Children[0].Value);
        Assert.Empty(birt.Children[0].Children);
        var taglessUnderBirt = birt.Children[1];
        Assert.Equal("PLAC", Assert.Single(taglessUnderBirt.Children).Tag);
        Assert.Equal("1900\nstray", birt.Children[2].Value);

        var missing = result.Diagnostics[0];
        AssertDiagnostic(missing, GedcomDiagnosticCode.MissingTag, 3, "@I9@", "?");
        AssertDiagnostic(result.Diagnostics[1], GedcomDiagnosticCode.MissingTag, 7, "@I9@", "?.BIRT.?");
        Assert.Equal(GedcomDiagnosticCode.LineWithoutLevel, result.Diagnostics[2].Code);
        Assert.Equal(3, result.Diagnostics.Count);
        Assert.Equal(new LineDisposition(LineDispositionKind.Structure, placeholder, null), result.Accounting[3]);
    }

    [Fact]
    public void LineWithoutLevelAfterTaglessLineAppendsToPlaceholder()
    {
        var result = Read("0 @I1@ INDI\n1 NOTE a\n2\nstray\n");

        var note = Assert.Single(result.Records[0].Children);
        Assert.Equal("a", note.Value);
        var placeholder = Assert.Single(note.Children);
        Assert.Equal("\nstray", placeholder.Value);
        Assert.Equal(new LineDisposition(LineDispositionKind.Folded, placeholder, null), result.Accounting[4]);
        AssertDiagnostic(result.Single(GedcomDiagnosticCode.LineWithoutLevel), GedcomDiagnosticCode.LineWithoutLevel, 4, "@I1@", "INDI.NOTE.?");
    }

    [Fact]
    public void LineWithoutLevelAfterPointerAppendsWithWarning()
    {
        var result = Read("0 @I1@ INDI\n1 FAMC @F1@\nstray\n");

        var famc = Assert.Single(result.Records[0].Children);
        Assert.Equal("@F1@\nstray", famc.Value);
        var warning = Assert.Single(result.Diagnostics);
        AssertDiagnostic(warning, GedcomDiagnosticCode.LineWithoutLevel, 3, "@I1@", "INDI.FAMC");
        Assert.Equal(GedcomDiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal(new LineDisposition(LineDispositionKind.Folded, famc, null), result.Accounting[3]);
    }

    [Fact]
    public void ReadsRunsOfSpacesBeforeTheTagAsOneDelimiter()
    {
        var result = Read("0 @I1@  INDI\n1  NAME John /Smith/\n2   CONC  x\n1 NOTE  two\n");

        var indi = Assert.Single(result.Records);
        Assert.Equal("INDI", indi.Tag);
        Assert.Equal("@I1@", indi.Xref);
        Assert.Equal(["NAME", "NOTE"], indi.Children.ConvertAll(c => c.Tag));
        Assert.Equal("John /Smith/ x", indi.Children[0].Value);
        Assert.Equal(" two", indi.Children[1].Value);
        Assert.Equal(new LineDisposition(LineDispositionKind.Structure, indi, null), result.Accounting[1]);
        Assert.Equal(new LineDisposition(LineDispositionKind.Structure, indi.Children[0], null), result.Accounting[2]);
        Assert.Equal(new LineDisposition(LineDispositionKind.Folded, indi.Children[0], null), result.Accounting[3]);

        Assert.Equal(3, result.Diagnostics.Count);
        AssertDiagnostic(result.Diagnostics[0], GedcomDiagnosticCode.ExtraDelimiter, 1, "@I1@", "INDI");
        AssertDiagnostic(result.Diagnostics[1], GedcomDiagnosticCode.ExtraDelimiter, 2, "@I1@", "INDI.NAME");
        AssertDiagnostic(result.Diagnostics[2], GedcomDiagnosticCode.ExtraDelimiter, 3, "@I1@", "INDI.NAME.CONC");
        Assert.All(result.Diagnostics, d => Assert.Equal(GedcomDiagnosticSeverity.Info, d.Severity));
    }

    [Fact]
    public void ReportsOtherFlagsAndKeepsStructures()
    {
        var result = Read(" 0 @I1@ INDI\n  1 @X1@ NAME a\n1 _B!D x\n0 @bad FAM\n");

        Assert.Equal(1, result.Count(GedcomDiagnosticCode.LeadingWhitespace));
        Assert.Equal(1, result.Single(GedcomDiagnosticCode.LeadingWhitespace).Line);
        var onSub = result.Single(GedcomDiagnosticCode.XrefOnSubstructure);
        Assert.Equal(2, onSub.Line);
        Assert.Equal("INDI.NAME", onSub.TagPath);
        var invalid = result.Single(GedcomDiagnosticCode.InvalidTag);
        Assert.Equal(3, invalid.Line);
        Assert.Equal("INDI.?", invalid.TagPath);
        var malformed = result.Single(GedcomDiagnosticCode.MalformedXref);
        Assert.Equal(4, malformed.Line);
        Assert.Null(malformed.RecordXref);
        Assert.Equal("FAM", malformed.TagPath);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal("@X1@", result.Records[0].Children[0].Xref);
        Assert.Equal("_B!D", result.Records[0].Children[1].Tag);
        Assert.Equal("@bad", result.Records[1].Xref);
        Assert.Equal(4, result.Diagnostics.Count);
    }

    [Fact]
    public void ReportsLineTooLongAndKeepsTruncatedLine()
    {
        var result = Read("0 @N1@ NOTE " + new string('x', 40) + "\n1 CONT y\n", bufferSize: 16, maxLineLength: 32);

        var tooLong = result.Single(GedcomDiagnosticCode.LineTooLong);
        Assert.Equal(GedcomDiagnosticSeverity.Error, tooLong.Severity);
        Assert.Equal(1, tooLong.Line);
        Assert.Equal("@N1@", tooLong.RecordXref);
        Assert.Equal("NOTE", tooLong.TagPath);
        Assert.Single(result.Diagnostics);
        var note = Assert.Single(result.Records);
        Assert.Equal(new string('x', 20) + "\ny", note.Value);
        Assert.Equal(LineDispositionKind.Structure, result.Accounting[1]!.Value.Kind);
    }

    [Fact]
    public void RecordsEachLineExactlyOnce()
    {
        string[] lines =
        [
            "stray",                // 1 no level, unplaced: placeholder record
            "1 HEAD",               // 2 under the placeholder
            "1 GEDC",               // 3
            "2 VERS 5.5.1",         // 4
            "0 @I1@ INDI",          // 5
            "1 NAME Joh",           // 6
            "2 CONC n",             // 7 folded
            "  1 NOTE a",           // 8 leading whitespace
            "2 SOUR @S1@",          // 9
            "2 CONT b",             // 10 continuation after children
            "lost line",            // 11 no level, appended
            "",                     // 12 blank
            "2",                    // 13 missing tag, placeholder structure
            "1 FAMC @F1@",          // 14
            "2 CONC x",             // 15 misplaced under a pointer
            "1 BIRT",               // 16
            "3 DATE 1900",          // 17 level skipped
            "1 @X1@ _BAD!",         // 18 xref on substructure, invalid tag
            "0 @bad NOTE",          // 19 malformed xref
            "",                     // 20 blank
        ];

        var result = Read(string.Join('\n', lines) + "\n");

        Assert.Equal(20, result.Accounting.LineCount);
        Assert.Null(result.Accounting[21]);
        LineDispositionKind[] expected =
        [
            LineDispositionKind.Structure, LineDispositionKind.Structure, LineDispositionKind.Structure,
            LineDispositionKind.Structure, LineDispositionKind.Structure, LineDispositionKind.Structure,
            LineDispositionKind.Folded, LineDispositionKind.Structure, LineDispositionKind.Structure,
            LineDispositionKind.Folded, LineDispositionKind.Folded, LineDispositionKind.Diagnostic,
            LineDispositionKind.Structure, LineDispositionKind.Structure, LineDispositionKind.Structure,
            LineDispositionKind.Structure, LineDispositionKind.Structure, LineDispositionKind.Structure,
            LineDispositionKind.Structure, LineDispositionKind.Diagnostic,
        ];
        for (var line = 1; line <= 20; line++)
        {
            var disposition = result.Accounting[line];
            Assert.NotNull(disposition);
            Assert.Equal(expected[line - 1], disposition.Value.Kind);
            if (disposition.Value.Kind == LineDispositionKind.Diagnostic)
            {
                Assert.NotNull(disposition.Value.Diagnostic);
                Assert.Null(disposition.Value.Structure);
            }
            else
            {
                Assert.NotNull(disposition.Value.Structure);
                Assert.Null(disposition.Value.Diagnostic);
            }

            if (disposition.Value.Kind == LineDispositionKind.Structure)
            {
                Assert.Equal(line, disposition.Value.Structure!.SourceLine);
            }
        }

        Assert.Equal(3, result.Records.Count);
        Assert.Equal("stray", result.Records[0].Value);
        Assert.Equal(["HEAD", "GEDC"], result.Records[0].Children.ConvertAll(c => c.Tag));
        var name = FindFirst(result.Records, "NAME");
        var note = FindFirst(result.Records, "NOTE");
        Assert.Equal("John", name.Value);
        Assert.Equal("a\nb\nlost line", note.Value);
        Assert.Same(name, result.Accounting[7]!.Value.Structure);
        Assert.Same(note, result.Accounting[10]!.Value.Structure);
        Assert.Same(note, result.Accounting[11]!.Value.Structure);
        Assert.Same(result.Accounting[12]!.Value.Diagnostic, result.Accounting[20]!.Value.Diagnostic);
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.LineWithoutLevelUnplaced));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.LineWithoutLevel));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.BlankLinesIgnored));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.MissingTag));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.LeadingWhitespace));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.ContinuationAfterChildren));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.ContinuationMisplaced));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.LevelSkipped));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.XrefOnSubstructure));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.InvalidTag));
        Assert.Equal(1, result.Count(GedcomDiagnosticCode.MalformedXref));
    }

    [Fact]
    public void LineAccountingRejectsDoubleRecord()
    {
        var accounting = new LineAccounting();
        var structure = new GedcomStructure { Tag = "HEAD" };

        Assert.Equal(0, accounting.LineCount);
        Assert.Null(accounting[1]);
        accounting.Record(1, new LineDisposition(LineDispositionKind.Structure, structure, null));
        accounting.Record(5000, new LineDisposition(LineDispositionKind.Folded, structure, null));

        Assert.Throws<InvalidOperationException>(() => accounting.Record(1, new LineDisposition(LineDispositionKind.Folded, structure, null)));
        Assert.Throws<ArgumentOutOfRangeException>(() => accounting.Record(0, new LineDisposition(LineDispositionKind.Folded, structure, null)));
        Assert.Equal(5000, accounting.LineCount);
        Assert.Null(accounting[2]);
        Assert.Null(accounting[0]);
        Assert.Equal(LineDispositionKind.Structure, accounting[1]!.Value.Kind);
        Assert.Equal(LineDispositionKind.Folded, accounting[5000]!.Value.Kind);
    }

    [Fact]
    public void DiagnosticsCarryLineXrefAndTagPath()
    {
        var result = Read("0 @I1@ INDI\n1 BIRT\n3 DATE 1900\n2 _B!D x\n1 NOTE a\n2 SOUR s\n2 CONT b\nlost\n0 @F1@ FAM\n1 HUSB @I1@\n2 CONT x\n");

        Assert.Equal(5, result.Diagnostics.Count);
        AssertDiagnostic(result.Diagnostics[0], GedcomDiagnosticCode.LevelSkipped, 3, "@I1@", "INDI.BIRT.DATE");
        AssertDiagnostic(result.Diagnostics[1], GedcomDiagnosticCode.InvalidTag, 4, "@I1@", "INDI.BIRT.?");
        AssertDiagnostic(result.Diagnostics[2], GedcomDiagnosticCode.ContinuationAfterChildren, 7, "@I1@", "INDI.NOTE.CONT");
        AssertDiagnostic(result.Diagnostics[3], GedcomDiagnosticCode.LineWithoutLevel, 8, "@I1@", "INDI.NOTE");
        AssertDiagnostic(result.Diagnostics[4], GedcomDiagnosticCode.ContinuationMisplaced, 11, "@F1@", "FAM.HUSB.CONT");
    }

    [Theory]
    [InlineData("0 @I1@ INDI\n1 SECRET-PAYLOAD x\n", "INDI.?")]
    [InlineData("0 @I1@ INDI\n1 ABCDEFGHIJKLMNOPQRSTUVWXYZ012345 x\n2 @X1@ NOTE y\n", "INDI.?.NOTE")]
    [InlineData("0 @SECRET PAYLOAD@ INDI\n1 @X1@ NOTE y\n", "?.NOTE")]
    public void DiagnosticArgumentsCarryNoPayload(string text, string tagPath)
    {
        var result = Read(text);

        var diagnostic = result.Diagnostics[^1];
        Assert.Equal(tagPath, diagnostic.TagPath);
        Assert.DoesNotContain("SECRET", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PAYLOAD", diagnostic.Message, StringComparison.Ordinal);
        Assert.True(diagnostic.RecordXref is null or "@I1@");
        Assert.DoesNotContain("ABCDEFGHIJKLMNOPQRSTUVWXYZ012345", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("INDI", true)]
    [InlineData("_UID", true)]
    [InlineData("a1_", true)]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ01234", true)]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ012345", false)]
    [InlineData("", false)]
    [InlineData("B!D", false)]
    [InlineData("NAMEé", false)]
    public void SafeTagAcceptsOnlyShortTagCharacters(string tag, bool safe)
    {
        Assert.Equal(safe ? tag : null, DiagnosticArguments.SafeTag(tag));
    }

    [Theory]
    [InlineData("@I1@", true)]
    [InlineData("@x@", true)]
    [InlineData("@ABCDEFGHIJKLMNOPQRST@", true)]
    [InlineData("@ABCDEFGHIJKLMNOPQRSTU@", false)]
    [InlineData("@@", false)]
    [InlineData("@I1", false)]
    [InlineData("@I@1@", false)]
    [InlineData("@I 1@", false)]
    [InlineData("@I\t1@", false)]
    [InlineData("", false)]
    public void SafeXrefAcceptsOnlyWellFormedXrefs(string xref, bool safe)
    {
        Assert.Equal(safe ? xref : null, DiagnosticArguments.SafeXref(xref));
    }

    private static void AssertDiagnostic(GedcomDiagnostic diagnostic, GedcomDiagnosticCode code, int line, string? recordXref, string tagPath)
    {
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal(line, diagnostic.Line);
        Assert.Equal(recordXref, diagnostic.RecordXref);
        Assert.Equal(tagPath, diagnostic.TagPath);
    }
}
