using System;
using System.Collections.Generic;

namespace Gedcom.Vector.Builder;

/// <summary>
/// Fluent builder for constructing a <see cref="FamilyRecord"/>.
/// </summary>
public class FamilyBuilder
{
    private readonly GedcomBuilder _root;
    private readonly string _xrefId;
    private readonly string? _husbandXref;
    private readonly string? _wifeXref;
    private readonly List<string> _childXrefs = new();
    private string? _marriageDate;
    private string? _marriagePlace;

    internal FamilyBuilder(GedcomBuilder root, string xrefId, string? husbandXref, string? wifeXref)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _xrefId = xrefId ?? throw new ArgumentNullException(nameof(xrefId));
        _husbandXref = husbandXref;
        _wifeXref = wifeXref;
    }

    /// <summary>
    /// Configures the marriage event for this family.
    /// </summary>
    public FamilyBuilder WithMarriage(string? date, string? place = null)
    {
        _marriageDate = date;
        if (place is not null)
        {
            _marriagePlace = place;
        }
        return this;
    }

    /// <summary>
    /// Adds a child to this family.
    /// </summary>
    public FamilyBuilder WithChild(string childXref)
    {
        if (childXref is not null)
        {
            _childXrefs.Add(childXref);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple children to this family.
    /// </summary>
    public FamilyBuilder WithChildren(params string[] childXrefs)
    {
        if (childXrefs is not null)
        {
            foreach (var child in childXrefs)
            {
                if (child is not null)
                {
                    _childXrefs.Add(child);
                }
            }
        }
        return this;
    }

    private FamilyRecord GetRecord()
    {
        return new FamilyRecord(_xrefId, _husbandXref, _wifeXref, _childXrefs, _marriageDate, _marriagePlace);
    }

    /// <summary>
    /// Adds a person to the GEDCOM builder.
    /// </summary>
    public PersonBuilder AddPerson(string xrefId, string? firstName = null, string? lastName = null, PersonSex sex = PersonSex.Unknown)
    {
        _root.AddFamilyRecord(GetRecord());
        return _root.AddPerson(xrefId, firstName, lastName, sex);
    }

    /// <summary>
    /// Adds another family to the GEDCOM builder.
    /// </summary>
    public FamilyBuilder AddFamily(string xrefId, string? husbandXref = null, string? wifeXref = null)
    {
        _root.AddFamilyRecord(GetRecord());
        return _root.AddFamily(xrefId, husbandXref, wifeXref);
    }

    /// <summary>
    /// Adds a media reference to the GEDCOM builder.
    /// </summary>
    public MediaBuilder AddMedia(string xrefId, string? title = null, string? filePath = null, string? format = null)
    {
        _root.AddFamilyRecord(GetRecord());
        return _root.AddMedia(xrefId, title, filePath, format);
    }

    /// <summary>
    /// Builds the final <see cref="GedcomParseResult"/>.
    /// </summary>
    public GedcomParseResult Build()
    {
        _root.AddFamilyRecord(GetRecord());
        return _root.Build();
    }
}
