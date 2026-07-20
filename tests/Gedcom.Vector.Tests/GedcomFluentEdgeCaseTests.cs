using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gedcom.Vector;
using Gedcom.Vector.Builder;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests;

public class GedcomFluentEdgeCaseTests
{
    [Fact]
    public void FamilyBuilder_EdgeCases_HandledCorrectly()
    {
        var result = new GedcomBuilder()
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithMarriage("12 JUN 1990")
                .WithMarriage(null, "London")
                .WithChild("@I3@")
                .WithChildren("@I4@", null!, "@I5@")
                .WithChildren(null!)
            .AddPerson("@I1@", "Father", "Doe")
            .AddFamily("@F2@")
            .AddMedia("@M1@", "Photo")
            .Build();

        Assert.Equal(2, result.Families.Count);
        var f1 = result.Families.First(f => f.XrefId == "@F1@");
        Assert.Equal("London", f1.MarriagePlace);
        Assert.Contains("@I3@", f1.ChildXrefs);
        Assert.Contains("@I4@", f1.ChildXrefs);
        Assert.Contains("@I5@", f1.ChildXrefs);
    }

    [Fact]
    public void MediaBuilder_MimeTypeResolution_And_EdgeCases()
    {
        var result = new GedcomBuilder()
            .AddMedia("@M1@", "Photo 1", "image.png", "png")
                .LinkTo("@I1@")
                .LinkTo(null!)
            .AddMedia("@M2@")
                .WithTitle("Doc")
                .WithFilePath("file.pdf")
                .WithFormat("pdf")
            .AddMedia("@M3@")
                .WithFormat("gif")
            .AddMedia("@M4@")
                .WithFormat("xyz")
            .AddPerson("@I1@", "John", "Doe")
            .Build();

        Assert.Equal(4, result.Media.Count);
        Assert.Equal("image/png", result.Media[0].MimeType);
        Assert.Single(result.Media[0].LinkedXrefIds);

        Assert.Equal("application/pdf", result.Media[1].MimeType);
        Assert.Equal("image/gif", result.Media[2].MimeType);
        Assert.Equal("xyz", result.Media[3].MimeType);
    }

    [Fact]
    public void PersonBuilder_EdgeCases_And_Events()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
                .WithBirth("1 JAN 1900")
                .WithBirth(null, "New York")
                .WithDeath("1 JAN 1980")
                .WithDeath(null, "Boston")
                .WithSex(PersonSex.Female)
                .WithEvent(FamTreeEventType.Christening, "10 JAN 1900", "Church")
            .AddPerson("@I2@")
                .AddMedia("@M1@", "Doc")
            .Build();

        var person = result.Persons.First(p => p.XrefId == "@I1@");
        Assert.Equal(PersonSex.Female, person.Sex);
        Assert.Equal("New York", person.BirthPlace);
        Assert.Equal("Boston", person.DeathPlace);

        Assert.Single(result.Events);
        Assert.Equal(FamTreeEventType.Christening, result.Events[0].EventType);
    }

    [Fact]
    public void GedcomTreeContext_ThrowsOnNullAndInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new GedcomTreeContext(null!));

        var result = new GedcomParseResult();
        var tree = result.ToContext();

        Assert.Throws<ArgumentNullException>(() => tree.GetPerson(null!));
        Assert.Throws<ArgumentNullException>(() => tree.GetFamily(null!));
        Assert.Throws<ArgumentNullException>(() => tree.ChildrenOf(null!));
        Assert.Throws<ArgumentNullException>(() => tree.SpousesOf(null!));
        Assert.Throws<ArgumentNullException>(() => tree.ParentsOf(null!));
        Assert.Throws<ArgumentNullException>(() => tree.MediaFor(null!));
        Assert.Throws<ArgumentNullException>(() => tree.AddPerson(null!));
        Assert.Throws<ArgumentNullException>(() => tree.UpdatePerson(null!));
        Assert.Throws<ArgumentNullException>(() => tree.DeletePerson(null!));
        Assert.Throws<ArgumentNullException>(() => tree.AddFamily(null!));
        Assert.Throws<ArgumentNullException>(() => tree.DeleteFamily(null!));

        Assert.Null(tree.GetPerson("@UNKNOWN@"));
        Assert.Null(tree.GetFamily("@UNKNOWN@"));
        Assert.Empty(tree.MediaFor("@UNKNOWN@"));

        // Deleting non-existent does not throw
        tree.DeletePerson("@UNKNOWN@");
        tree.DeleteFamily("@UNKNOWN@");

        // Updating non-existent throws
        Assert.Throws<KeyNotFoundException>(() => tree.UpdatePerson(new PersonRecord("@NO@","A","B", PersonSex.Male, null, null, null, null)));
    }

    [Fact]
    public void GedcomTreeContext_DuplicateAdds_ThrowsArgumentException()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe")
            .AddFamily("@F1@", "@I1@", null)
            .Build();

        var tree = result.ToContext();

        Assert.Throws<ArgumentException>(() => tree.AddPerson(new PersonRecord("@I1@", "Duplicate", "User", PersonSex.Male, null, null, null, null)));
        Assert.Throws<ArgumentException>(() => tree.AddFamily(new FamilyRecord("@F1@", null, null, Array.Empty<string>(), null, null)));
    }

    [Fact]
    public void StreamingGedcomParser_HandlesAllEventsAndMultilineContinuations()
    {
        string gedcomContent =
            "0 HEAD\n" +
            "0 @I1@ INDI\n" +
            "1 NAME John /Doe/\n" +
            "1 CENS\n" +
            "2 DATE 1920\n" +
            "2 PLAC London\n" +
            "1 IMMI\n" +
            "2 DATE 1925\n" +
            "1 EMIG\n" +
            "2 DATE 1924\n" +
            "1 RESI\n" +
            "2 DATE 1930\n" +
            "1 CHR\n" +
            "2 DATE 1901\n" +
            "1 BURI\n" +
            "2 DATE 1980\n" +
            "1 NOTE Multi-line note\n" +
            "2 CONC  continued text\n" +
            "2 CONT  and new line\n" +
            "0 @F1@ FAM\n" +
            "1 HUSB @I1@\n" +
            "1 MARR\n" +
            "2 PLAC Boston\n" +
            "2 CONC , USA\n" +
            "0 TRLR\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(gedcomContent));
        var result = StreamingGedcomParser.Parse(stream, new GedcomEncodingResult(Encoding.UTF8, false, 0));

        Assert.Single(result.Persons);
        Assert.Equal(6, result.Events.Count);
        Assert.Contains(result.Events, e => e.EventType == FamTreeEventType.Census);
        Assert.Contains(result.Events, e => e.EventType == FamTreeEventType.Immigration);
        Assert.Contains(result.Events, e => e.EventType == FamTreeEventType.Emigration);
        Assert.Contains(result.Events, e => e.EventType == FamTreeEventType.Residence);
        Assert.Contains(result.Events, e => e.EventType == FamTreeEventType.Christening);
        Assert.Contains(result.Events, e => e.EventType == FamTreeEventType.Burial);

        var fam = result.Families.First();
        Assert.Equal("Boston, USA", fam.MarriagePlace);
    }

    [Fact]
    public void GedcomStringPool_Resizing_And_NullHandling()
    {
        var pool = new GedcomStringPool(capacity: 4);

        Assert.Null(pool.GetOrAdd((string?)null));
        Assert.Equal(string.Empty, pool.GetOrAdd(string.Empty));
        Assert.Equal(string.Empty, pool.GetOrAdd(ReadOnlySpan<char>.Empty));

        var s1 = pool.GetOrAdd("Test1".AsSpan());
        var s2 = pool.GetOrAdd("Test1".AsSpan());
        Assert.Same(s1, s2);

        // Add enough strings to force pool resize
        for (int i = 0; i < 50; i++)
        {
            var str = pool.GetOrAdd($"Item{i}".AsSpan());
            var repeat = pool.GetOrAdd($"Item{i}");
            Assert.Same(str, repeat);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task ExportWriter_WriteAsync_And_MediaExport_Works()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
                .WithBirth("1 JAN 1900", "New York")
                .WithEvent(FamTreeEventType.Burial, "10 JAN 1980", "Cemetery")
            .AddFamily("@F1@", "@I1@", null)
                .WithMarriage("1 JUN 1925", "Chicago")
            .AddMedia("@M1@", "Photo", "photo.jpg", "jpg")
                .LinkTo("@I1@")
                .LinkTo("@F1@")
            .Build();

        var writer = new GedcomExportWriter();

        // 1. String export
        string text = writer.Write(result);
        Assert.Contains("0 @I1@ INDI", text);
        Assert.Contains("1 OBJE @M1@", text);

        // 2. Async stream export
        using var ms = new MemoryStream();
        await writer.WriteAsync(result, ms);
        Assert.True(ms.Length > 0);
    }

    [Fact]
    public void GedcomTreeContext_FullMutationAndLookup_Coverage()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "Father", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Mother", "Smith", PersonSex.Female)
            .AddPerson("@I3@", "Child", "Doe", PersonSex.Male)
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithChild("@I3@")
            .Build();

        var tree = result.ToContext();

        var father = tree.GetPerson("@I1@");
        var mother = tree.GetPerson("@I2@");
        var child = tree.GetPerson("@I3@");

        Assert.Contains(mother, tree.SpousesOf(father!));
        Assert.Contains(father, tree.SpousesOf(mother!));
        Assert.Contains(father, tree.ParentsOf(child!));
        Assert.Contains(mother, tree.ParentsOf(child!));

        // Test updating person
        var updatedFather = father! with { LastName = "Updated" };
        tree.UpdatePerson(updatedFather);
        Assert.Equal("Updated", tree.GetPerson("@I1@")!.LastName);

        // Test deleting family
        tree.DeleteFamily("@F1@");
        Assert.Null(tree.GetFamily("@F1@"));
        Assert.Empty(tree.ParentsOf(child!));
        Assert.Empty(tree.ChildrenOf(father!));

        // Test adding new person & family
        tree.AddPerson(new PersonRecord("@I4@", "New", "Person", PersonSex.Female, null, null, null, null));
        tree.AddFamily(new FamilyRecord("@F2@", "@I1@", "@I4@", new[] { "@I3@" }, null, null));

        Assert.NotNull(tree.GetPerson("@I4@"));
        Assert.NotNull(tree.GetFamily("@F2@"));
        Assert.Contains(tree.GetPerson("@I4@")!, tree.SpousesOf(father!));
    }

    [Fact]
    public void StreamingGedcomParser_AnselAndUnhandledTags_Coverage()
    {
        // 1. ANSEL input with diacritics via stream parser
        byte[] anselBytes = Encoding.Latin1.GetBytes("0 HEAD\n1 CHAR ANSEL\n0 @I1@ INDI\n1 NAME Mu\xE4" + "n" + "oz /Test/\n0 TRLR\n");
        using var stream = new MemoryStream(anselBytes);
        var result = StreamingGedcomParser.Parse(stream, new GedcomEncodingResult(null, true, 0));
        Assert.Single(result.Persons);
        Assert.Equal("Muñoz", result.Persons[0].FirstName);

        // 2. Unhandled level-0 tag (e.g. 0 @N1@ NOTE)
        string noteContent = "0 HEAD\n0 @N1@ NOTE Some note\n1 CONC text\n0 TRLR\n";
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(noteContent));
        var result2 = StreamingGedcomParser.Parse(stream2, new GedcomEncodingResult(Encoding.UTF8, false, 0));
        Assert.Empty(result2.Persons);
    }

    [Fact]
    public void GedcomTreeContext_MediaFor_Coverage()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
            .AddMedia("@M1@", "Photo", "photo.jpg", "jpg")
                .LinkTo("@I1@")
            .Build();

        var tree = result.ToContext();
        var person = tree.GetPerson("@I1@");
        var mediaList = tree.MediaFor(person!.XrefId);
        Assert.Single(mediaList);
        Assert.Equal("@M1@", mediaList.First().XrefId);
    }

    [Fact]
    public void StreamingGedcomParser_MediaAndComplexFamily_Coverage()
    {
        string content =
            "0 HEAD\n" +
            "0 @I1@ INDI\n" +
            "1 NAME John /Doe/\n" +
            "1 OBJE @M1@\n" +
            "1 FAMS @F1@\n" +
            "1 FAMC @F0@\n" +
            "0 @I2@ INDI\n" +
            "1 NAME Jane /Smith/\n" +
            "1 FAMS @F1@\n" +
            "0 @I3@ INDI\n" +
            "1 NAME Child /Doe/\n" +
            "1 FAMC @F1@\n" +
            "0 @F1@ FAM\n" +
            "1 HUSB @I1@\n" +
            "1 WIFE @I2@\n" +
            "1 CHIL @I3@\n" +
            "1 MARR\n" +
            "2 DATE 1 JAN 1920\n" +
            "2 PLAC London\n" +
            "1 OBJE @M1@\n" +
            "0 @M1@ OBJE\n" +
            "1 TITL Photo\n" +
            "1 FILE photo.jpg\n" +
            "1 FORM JPG\n" +
            "0 TRLR\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = StreamingGedcomParser.Parse(stream, new GedcomEncodingResult(Encoding.UTF8, false, 0));

        Assert.Equal(3, result.Persons.Count);
        Assert.Single(result.Families);
        Assert.Single(result.Media);
        Assert.Contains("@I1@", result.Media[0].LinkedXrefIds);
        Assert.Contains("@F1@", result.Media[0].LinkedXrefIds);

        // Test export of complex tree
        var exporter = new GedcomExportWriter();
        string exportedText = exporter.Write(result);
        Assert.Contains("1 FAMC @F1@", exportedText);
        Assert.Contains("1 FAMS @F1@", exportedText);
        Assert.Contains("1 MARR", exportedText);
    }

    [Fact]
    public void AnselDecoder_MultipleCombiningMarks_Coverage()
    {
        // Decode string with multiple ANSEL combining marks
        string latin1String = "\xE1\xE2\xE8a"; // 3 combining marks before 'a'
        string decoded = AnselDecoder.Decode(latin1String);
        Assert.NotNull(decoded);
    }
}
