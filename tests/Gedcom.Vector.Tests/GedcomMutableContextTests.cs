using System.Collections.Generic;
using System.Linq;
using Gedcom.Vector;
using Gedcom.Vector.Builder;
using Xunit;

namespace Gedcom.Vector.Tests;

public class GedcomMutableContextTests
{
    [Fact]
    public void AddPerson_AddsToIndexAndBackingResult()
    {
        var result = new GedcomParseResult();
        var tree = result.ToContext();

        var person = new PersonRecord("@I1@", "John", "Doe", PersonSex.Male, null, null, null, null);
        tree.AddPerson(person);

        Assert.Single(result.Persons);
        Assert.Same(person, result.Persons[0]);
        Assert.Same(person, tree.GetPerson("@I1@"));
    }

    [Fact]
    public void UpdatePerson_UpdatesDetailsAndReferences()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Jane", "Smith", PersonSex.Female)
            .AddPerson("@I3@", "Bobby", "Doe", PersonSex.Male)
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithChild("@I3@")
            .Build();

        var tree = result.ToContext();

        var updatedBobby = new PersonRecord("@I3@", "Robert", "Doe", PersonSex.Male, "1 JAN 2000", "New York", null, null);
        tree.UpdatePerson(updatedBobby);

        // Verify updated in backing result
        var BobbyInList = result.Persons.First(p => p.XrefId == "@I3@");
        Assert.Equal("Robert", BobbyInList.FirstName);
        Assert.Equal("1 JAN 2000", BobbyInList.BirthDate);

        // Verify updated in GetPerson
        var BobbyInGet = tree.GetPerson("@I3@");
        Assert.Same(updatedBobby, BobbyInGet);

        // Verify updated in ChildrenOf query
        var john = tree.GetPerson("@I1@")!;
        var children = tree.ChildrenOf(john).ToList();
        Assert.Single(children);
        Assert.Same(updatedBobby, children[0]);
    }

    [Fact]
    public void DeletePerson_RemovesFromIndexesAndUnlinksSpouseAndChildren()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Jane", "Smith", PersonSex.Female)
            .AddPerson("@I3@", "Bobby", "Doe", PersonSex.Male)
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithChild("@I3@")
            .Build();

        var tree = result.ToContext();

        // Delete John
        tree.DeletePerson("@I1@");

        // Verify removed from context and backing lists
        Assert.Null(tree.GetPerson("@I1@"));
        Assert.DoesNotContain(result.Persons, p => p.XrefId == "@I1@");

        // Verify Jane's spouse link is unlinked
        var jane = tree.GetPerson("@I2@")!;
        Assert.Empty(tree.SpousesOf(jane));

        // Verify the family record husband xref is cleared
        var family = result.Families.First(f => f.XrefId == "@F1@");
        Assert.Null(family.HusbandXref);
        Assert.Equal("@I2@", family.WifeXref);

        // Verify Bobby's parents list only contains Jane now
        var bobby = tree.GetPerson("@I3@")!;
        var parents = tree.ParentsOf(bobby).ToList();
        Assert.Single(parents);
        Assert.Equal("@I2@", parents[0].XrefId);
    }

    [Fact]
    public void AddFamily_HooksUpSpouseAndChildren()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Jane", "Smith", PersonSex.Female)
            .AddPerson("@I3@", "Bobby", "Doe", PersonSex.Male)
            .Build();

        var tree = result.ToContext();

        var family = new FamilyRecord("@F1@", "@I1@", "@I2@", new List<string> { "@I3@" }, "1 JUN 1970", "Boston");
        tree.AddFamily(family);

        // Verify indexed in backing list
        Assert.Single(result.Families);
        Assert.Same(family, result.Families[0]);

        // Verify spouses and children linked
        var john = tree.GetPerson("@I1@")!;
        var spouses = tree.SpousesOf(john).ToList();
        Assert.Single(spouses);
        Assert.Equal("@I2@", spouses[0].XrefId);

        var children = tree.ChildrenOf(john).ToList();
        Assert.Single(children);
        Assert.Equal("@I3@", children[0].XrefId);

        var bobby = tree.GetPerson("@I3@")!;
        Assert.Equal(2, tree.ParentsOf(bobby).Count());
    }

    [Fact]
    public void DeleteFamily_CleansUpAllReferences()
    {
        var result = new GedcomBuilder()
            .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
            .AddPerson("@I2@", "Jane", "Smith", PersonSex.Female)
            .AddPerson("@I3@", "Bobby", "Doe", PersonSex.Male)
            .AddFamily("@F1@", "@I1@", "@I2@")
                .WithChild("@I3@")
            .Build();

        var tree = result.ToContext();

        tree.DeleteFamily("@F1@");

        // Verify family removed
        Assert.Null(tree.GetFamily("@F1@"));
        Assert.Empty(result.Families);

        // Verify relationships unlinked
        var john = tree.GetPerson("@I1@")!;
        Assert.Empty(tree.SpousesOf(john));
        Assert.Empty(tree.ChildrenOf(john));

        var bobby = tree.GetPerson("@I3@")!;
        Assert.Empty(tree.ParentsOf(bobby));
    }
}
