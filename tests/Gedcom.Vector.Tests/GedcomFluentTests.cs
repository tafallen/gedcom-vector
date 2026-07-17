using System.Collections.Generic;
using System.Linq;
using Gedcom.Vector;
using Gedcom.Vector.Builder;
using Xunit;

namespace Gedcom.Vector.Tests;

public class GedcomFluentTests
{
    [Fact]
    public void Builder_ConstructsValidGedcomParseResult()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
                .WithBirth("1 JAN 1900", "New York, USA")
                .WithDeath("1 JAN 1980", "Boston, USA")
                .WithEvent(FamTreeEventType.Census, "1920", "Boston, USA")
            .AddPerson("@I2@", "Jane", "Smith", PersonSex.Female)
                .WithBirth("1 JUN 1905")
            .AddPerson("@I3@", "Bobby", "Doe", PersonSex.Male)
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithMarriage("1 JUN 1925", "Chicago, USA")
                .WithChild("@I3@")
            .AddMedia("@M1@", "Family Portrait", "portrait.jpg", "jpg")
                .LinkTo("@F1@")
            .Build();

        Assert.Equal(3, result.Persons.Count);
        Assert.Single(result.Families);
        Assert.Single(result.Events);
        Assert.Single(result.Media);

        var john = result.Persons.FirstOrDefault(p => p.XrefId == "@I1@");
        Assert.NotNull(john);
        Assert.Equal("John", john.FirstName);
        Assert.Equal("Doe", john.LastName);
        Assert.Equal(PersonSex.Male, john.Sex);
        Assert.Equal("1 JAN 1900", john.BirthDate);
        Assert.Equal("New York, USA", john.BirthPlace);
        Assert.Equal("1 JAN 1980", john.DeathDate);

        var censusEvent = result.Events.FirstOrDefault(e => e.EventType == FamTreeEventType.Census);
        Assert.NotNull(censusEvent);
        Assert.Equal("@I1@", censusEvent.PersonXrefId);
        Assert.Equal("1920", censusEvent.Date);

        var family = result.Families.FirstOrDefault(f => f.XrefId == "@F1@");
        Assert.NotNull(family);
        Assert.Equal("@I1@", family.HusbandXref);
        Assert.Equal("@I2@", family.WifeXref);
        Assert.Contains("@I3@", family.ChildXrefs);
        Assert.Equal("1 JUN 1925", family.MarriageDate);

        var media = result.Media.FirstOrDefault(m => m.XrefId == "@M1@");
        Assert.NotNull(media);
        Assert.Equal("Family Portrait", media.Title);
        Assert.Equal("portrait.jpg", media.FilePath);
        Assert.Equal("image/jpeg", media.MimeType);
        Assert.Contains("@F1@", media.LinkedXrefIds);
    }

    [Fact]
    public void TreeContext_QueriesRelationshipsCorrectly()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Jane", "Smith", PersonSex.Female)
            .AddPerson("@I3@", "Bobby", "Doe", PersonSex.Male)
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithChild("@I3@")
            .AddMedia("@M1@", "Family Portrait", "portrait.jpg", "jpg")
                .LinkTo("@I1@")
            .Build();

        var tree = result.ToContext();

        var john = tree.GetPerson("@I1@");
        var jane = tree.GetPerson("@I2@");
        var bobby = tree.GetPerson("@I3@");

        Assert.NotNull(john);
        Assert.NotNull(jane);
        Assert.NotNull(bobby);

        // Test Spouses
        var johnSpouses = tree.SpousesOf(john).ToList();
        Assert.Single(johnSpouses);
        Assert.Equal("@I2@", johnSpouses[0].XrefId);

        var janeSpouses = tree.SpousesOf(jane).ToList();
        Assert.Single(janeSpouses);
        Assert.Equal("@I1@", janeSpouses[0].XrefId);

        // Test Children
        var johnChildren = tree.ChildrenOf(john).ToList();
        Assert.Single(johnChildren);
        Assert.Equal("@I3@", johnChildren[0].XrefId);

        // Test Parents
        var bobbyParents = tree.ParentsOf(bobby).ToList();
        Assert.Equal(2, bobbyParents.Count);
        Assert.Contains(bobbyParents, p => p.XrefId == "@I1@");
        Assert.Contains(bobbyParents, p => p.XrefId == "@I2@");

        // Test Media
        var johnMedia = tree.MediaFor("@I1@").ToList();
        Assert.Single(johnMedia);
        Assert.Equal("@M1@", johnMedia[0].XrefId);
    }
}
