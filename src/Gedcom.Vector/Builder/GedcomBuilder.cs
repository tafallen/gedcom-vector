using System;
using System.Collections.Generic;

namespace Gedcom.Vector.Builder;

/// <summary>
/// Root builder for programmatically constructing a <see cref="GedcomParseResult"/>.
/// </summary>
public class GedcomBuilder
{
    private readonly List<PersonRecord> _persons = new();
    private readonly List<FamilyRecord> _families = new();
    private readonly List<EventRecord> _events = new();
    private readonly List<MediaReferenceRecord> _media = new();

    /// <summary>
    /// Adds a new person to the builder and returns a <see cref="PersonBuilder"/> to configure it.
    /// </summary>
    public PersonBuilder AddPerson(string xrefId, string? firstName = null, string? lastName = null, PersonSex sex = PersonSex.Unknown)
    {
        var builder = new PersonBuilder(this, xrefId, firstName, lastName, sex);
        return builder;
    }

    /// <summary>
    /// Adds a new family to the builder and returns a <see cref="FamilyBuilder"/> to configure it.
    /// </summary>
    public FamilyBuilder AddFamily(string xrefId, string? husbandXref = null, string? wifeXref = null)
    {
        var builder = new FamilyBuilder(this, xrefId, husbandXref, wifeXref);
        return builder;
    }

    /// <summary>
    /// Adds a new media reference to the builder and returns a <see cref="MediaBuilder"/> to configure it.
    /// </summary>
    public MediaBuilder AddMedia(string xrefId, string? title = null, string? filePath = null, string? format = null)
    {
        var builder = new MediaBuilder(this, xrefId, title, filePath, format);
        return builder;
    }

    internal void AddPersonRecord(PersonRecord person) => _persons.Add(person);
    internal void AddFamilyRecord(FamilyRecord family) => _families.Add(family);
    internal void AddMediaRecord(MediaReferenceRecord media) => _media.Add(media);
    internal void AddEventRecord(EventRecord ev) => _events.Add(ev);

    /// <summary>
    /// Builds the structured <see cref="GedcomParseResult"/> containing all added records.
    /// </summary>
    public GedcomParseResult Build()
    {
        var result = new GedcomParseResult();
        result.Persons.AddRange(_persons);
        result.Families.AddRange(_families);
        result.Events.AddRange(_events);
        result.Media.AddRange(_media);
        return result;
    }
}
