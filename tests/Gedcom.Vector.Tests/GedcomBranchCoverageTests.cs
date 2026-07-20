using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gedcom.Vector;
using Gedcom.Vector.Builder;
using Gedcom.Vector.Parsing;
using Xunit;

namespace Gedcom.Vector.Tests;

public class GedcomBranchCoverageTests
{
    [Fact]
    public async Task GedcomExportWriter_NullChecks_And_BranchCoverage()
    {
        var writer = new GedcomExportWriter();
        Assert.Throws<ArgumentNullException>(() => writer.Write(null!));
        Assert.Throws<ArgumentNullException>(() => writer.Write(null!, new MemoryStream()));
        Assert.Throws<ArgumentNullException>(() => writer.Write(new GedcomParseResult(), (Stream)null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => writer.WriteAsync(null!, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => writer.WriteAsync(new GedcomParseResult(), null!));

        // Person with FirstName only (no LastName) and Unknown sex
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "Madonna", null, PersonSex.Unknown)
            .AddPerson("@I2@", null, "SingleSurname", PersonSex.Male)
            .AddFamily("@F1@", null, "@I2@")
            .AddMedia("@M1@", null, null, null)
            .Build();

        string output = writer.Write(result);
        Assert.Contains("1 NAME Madonna", output);
        Assert.Contains("1 NAME /SingleSurname/", output);
        Assert.Contains("0 @M1@ OBJE", output);
    }

    [Fact]
    public void GedcomTreeContext_DeletionAndUnlinking_BranchCoverage()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "Father", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Mother", "Doe", PersonSex.Female)
            .AddPerson("@I3@", "Child", "Doe", PersonSex.Male)
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithChild("@I3@")
            .AddMedia("@M1@", "Photo")
                .LinkTo("@I1@")
                .LinkTo("@F1@")
            .Build();

        var tree = result.ToContext();
        var child = tree.GetPerson("@I3@");
        var mother = tree.GetPerson("@I2@");

        // 1. Delete person with spouse, child, and media links
        tree.DeletePerson("@I1@");
        Assert.Null(tree.GetPerson("@I1@"));
        Assert.DoesNotContain(tree.ParentsOf(child!), p => p.XrefId == "@I1@");

        // 2. Delete person without any links
        tree.DeletePerson("@I2@");
        Assert.Null(tree.GetPerson("@I2@"));

        // 3. Delete family with media link
        tree.DeleteFamily("@F1@");
        Assert.Null(tree.GetFamily("@F1@"));
    }

    [Fact]
    public void StreamingGedcomParser_NameFormats_And_ContinutationBranches()
    {
        // 1. Given name only, Surname only, Unknown sex
        string gedContent =
            "0 HEAD\n" +
            "0 @I1@ INDI\n" +
            "1 NAME Prince\n" +
            "1 SEX U\n" +
            "1 BIRT\n" +
            "2 PLAC BirthCity\n" +
            "3 CONC  Extended\n" +
            "1 DEAT\n" +
            "2 PLAC DeathCity\n" +
            "3 CONC  Extended\n" +
            "0 @I2@ INDI\n" +
            "1 NAME /Cher/\n" +
            "1 SEX F\n" +
            "0 @F1@ FAM\n" +
            "1 HUSB @I1@\n" +
            "1 MARR\n" +
            "2 PLAC MarriageCity\n" +
            "3 CONC  Extended\n" +
            "0 @M1@ OBJE\n" +
            "1 TITL Main Title\n" +
            "2 CONC  Extra Title\n" +
            "2 CONT  New Line Title\n" +
            "0 TRLR\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(gedContent));
        var result = StreamingGedcomParser.Parse(stream, new GedcomEncodingResult(Encoding.UTF8, false, 0));

        Assert.Equal(2, result.Persons.Count);
        Assert.Equal("Prince", result.Persons[0].FirstName);
        Assert.Null(result.Persons[0].LastName);
        Assert.Equal(PersonSex.Unknown, result.Persons[0].Sex);
        Assert.Equal("BirthCity Extended", result.Persons[0].BirthPlace);
        Assert.Equal("DeathCity Extended", result.Persons[0].DeathPlace);

        Assert.Null(result.Persons[1].FirstName);
        Assert.Equal("Cher", result.Persons[1].LastName);

        Assert.Equal("MarriageCity Extended", result.Families[0].MarriagePlace);
        Assert.Equal("Main Title Extra Title\n New Line Title", result.Media[0].Title);
    }

    [Fact]
    public void AnselDecoder_PendingMarksOverflow_BranchCoverage()
    {
        // Add 5 combining diacritics to test PendingMarks overflow list
        byte[] anselBytes = new byte[]
        {
            0xE1, 0xE2, 0xE3, 0xE4, 0xE5, (byte)'a'
        };

        string decoded = AnselDecoder.Decode(anselBytes);
        Assert.NotNull(decoded);

        // Test trailing combining marks without base char
        byte[] trailingMarks = new byte[] { (byte)'a', 0xE1, 0xE2 };
        string decodedTrailing = AnselDecoder.Decode(trailingMarks);
        Assert.NotNull(decodedTrailing);
    }

    [Fact]
    public void Builders_ValidationAndChaining_BranchCoverage()
    {
        Assert.Throws<ArgumentNullException>(() => new PersonBuilder(null!, "@I1@", "John", "Doe", PersonSex.Male));
        Assert.Throws<ArgumentNullException>(() => new FamilyBuilder(null!, "@F1@", "@I1@", "@I2@"));
        Assert.Throws<ArgumentNullException>(() => new MediaBuilder(null!, "@M1@", "Test", null, null));

        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
                .WithBirth("1 JAN 1900")
                .WithDeath("1 JAN 1980")
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithMarriage("1 JUN 1920")
            .AddMedia("@M1@", "Test", null, null)
                .WithTitle("Test")
            .Build();

        Assert.Single(result.Persons);
        Assert.Single(result.Families);
        Assert.Single(result.Media);
    }

    [Fact]
    public void GedcomTreeContext_UnlinkedQueries_BranchCoverage()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "Single", "Person")
            .AddPerson("@I2@", "Other", "Person")
            .AddFamily("@F1@", null, null)
            .Build();

        var tree = result.ToContext();
        var p1 = tree.GetPerson("@I1@");
        var unlinkedPerson = new PersonRecord("@I99@", "Ghost", "User", PersonSex.Unknown, null, null, null, null);

        Assert.Empty(tree.ChildrenOf(p1!));
        Assert.Empty(tree.SpousesOf(p1!));
        Assert.Empty(tree.ParentsOf(p1!));

        Assert.Empty(tree.ChildrenOf(unlinkedPerson));
        Assert.Empty(tree.SpousesOf(unlinkedPerson));
        Assert.Empty(tree.ParentsOf(unlinkedPerson));
        Assert.Empty(tree.MediaFor(unlinkedPerson.XrefId));
    }
}
