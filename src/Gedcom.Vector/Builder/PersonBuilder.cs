using System;

namespace Gedcom.Vector.Builder;

/// <summary>
/// Fluent builder for constructing a <see cref="PersonRecord"/>.
/// </summary>
public class PersonBuilder
{
    private readonly GedcomBuilder _root;
    private readonly string _xrefId;
    private readonly string? _firstName;
    private readonly string? _lastName;
    private PersonSex _sex;
    private string? _birthDate;
    private string? _birthPlace;
    private string? _deathDate;
    private string? _deathPlace;

    internal PersonBuilder(GedcomBuilder root, string xrefId, string? firstName, string? lastName, PersonSex sex)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _xrefId = xrefId ?? throw new ArgumentNullException(nameof(xrefId));
        _firstName = firstName;
        _lastName = lastName;
        _sex = sex;
    }

    /// <summary>
    /// Configures the birth event for this person.
    /// </summary>
    public PersonBuilder WithBirth(string? date, string? place = null)
    {
        _birthDate = date;
        if (place is not null)
        {
            _birthPlace = place;
        }
        return this;
    }

    /// <summary>
    /// Configures the death event for this person.
    /// </summary>
    public PersonBuilder WithDeath(string? date, string? place = null)
    {
        _deathDate = date;
        if (place is not null)
        {
            _deathPlace = place;
        }
        return this;
    }

    /// <summary>
    /// Configures the sex of this person.
    /// </summary>
    public PersonBuilder WithSex(PersonSex sex)
    {
        _sex = sex;
        return this;
    }

    /// <summary>
    /// Adds a genealogical event to this person.
    /// </summary>
    public PersonBuilder WithEvent(FamTreeEventType eventType, string? date, string? place = null)
    {
        _root.AddEventRecord(new EventRecord(_xrefId, eventType, date, place));
        return this;
    }

    private PersonRecord GetRecord()
    {
        return new PersonRecord(_xrefId, _firstName, _lastName, _sex, _birthDate, _birthPlace, _deathDate, _deathPlace);
    }

    /// <summary>
    /// Adds another person to the GEDCOM builder.
    /// </summary>
    public PersonBuilder AddPerson(string xrefId, string? firstName = null, string? lastName = null, PersonSex sex = PersonSex.Unknown)
    {
        _root.AddPersonRecord(GetRecord());
        return _root.AddPerson(xrefId, firstName, lastName, sex);
    }

    /// <summary>
    /// Adds a family to the GEDCOM builder.
    /// </summary>
    public FamilyBuilder AddFamily(string xrefId, string? husbandXref = null, string? wifeXref = null)
    {
        _root.AddPersonRecord(GetRecord());
        return _root.AddFamily(xrefId, husbandXref, wifeXref);
    }

    /// <summary>
    /// Adds a media reference to the GEDCOM builder.
    /// </summary>
    public MediaBuilder AddMedia(string xrefId, string? title = null, string? filePath = null, string? format = null)
    {
        _root.AddPersonRecord(GetRecord());
        return _root.AddMedia(xrefId, title, filePath, format);
    }

    /// <summary>
    /// Builds the final <see cref="GedcomParseResult"/>.
    /// </summary>
    public GedcomParseResult Build()
    {
        _root.AddPersonRecord(GetRecord());
        return _root.Build();
    }
}
