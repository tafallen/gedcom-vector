using System;
using System.Collections.Generic;

namespace Gedcom.Vector;

/// <summary>
/// A query-optimized context that builds index tables on a <see cref="GedcomParseResult"/>
/// for O(1) family tree relationship traversal.
/// </summary>
public class GedcomTreeContext
{
    private readonly Dictionary<string, PersonRecord> _personsById;
    private readonly Dictionary<string, FamilyRecord> _familiesById;
    private readonly Dictionary<string, List<FamilyRecord>> _familiesAsSpouse;
    private readonly Dictionary<string, FamilyRecord> _familiesAsChild;
    private readonly Dictionary<string, List<PersonRecord>> _childrenByFamilyId;
    private readonly Dictionary<string, List<MediaReferenceRecord>> _mediaByEntityId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GedcomTreeContext"/> class, indexing the tree.
    /// </summary>
    public GedcomTreeContext(GedcomParseResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        // 1. Index persons
        var persons = result.Persons;
        _personsById = new Dictionary<string, PersonRecord>(persons.Count, StringComparer.Ordinal);
        for (int i = 0; i < persons.Count; i++)
        {
            var p = persons[i];
            _personsById[p.XrefId] = p;
        }

        // 2. Index families
        var families = result.Families;
        _familiesById = new Dictionary<string, FamilyRecord>(families.Count, StringComparer.Ordinal);
        _familiesAsSpouse = new Dictionary<string, List<FamilyRecord>>(StringComparer.Ordinal);
        _familiesAsChild = new Dictionary<string, FamilyRecord>(StringComparer.Ordinal);
        _childrenByFamilyId = new Dictionary<string, List<PersonRecord>>(families.Count, StringComparer.Ordinal);

        for (int i = 0; i < families.Count; i++)
        {
            var fam = families[i];
            _familiesById[fam.XrefId] = fam;

            if (fam.HusbandXref is not null)
            {
                if (!_familiesAsSpouse.TryGetValue(fam.HusbandXref, out var list))
                {
                    list = new List<FamilyRecord>();
                    _familiesAsSpouse[fam.HusbandXref] = list;
                }
                list.Add(fam);
            }

            if (fam.WifeXref is not null)
            {
                if (!_familiesAsSpouse.TryGetValue(fam.WifeXref, out var list))
                {
                    list = new List<FamilyRecord>();
                    _familiesAsSpouse[fam.WifeXref] = list;
                }
                list.Add(fam);
            }

            var childrenList = new List<PersonRecord>(fam.ChildXrefs.Count);
            for (int j = 0; j < fam.ChildXrefs.Count; j++)
            {
                var childXref = fam.ChildXrefs[j];
                if (childXref is not null)
                {
                    _familiesAsChild[childXref] = fam;
                    if (_personsById.TryGetValue(childXref, out var childRecord))
                    {
                        childrenList.Add(childRecord);
                    }
                }
            }
            _childrenByFamilyId[fam.XrefId] = childrenList;
        }

        // 3. Index media
        var mediaList = result.Media;
        _mediaByEntityId = new Dictionary<string, List<MediaReferenceRecord>>(StringComparer.Ordinal);
        for (int i = 0; i < mediaList.Count; i++)
        {
            var med = mediaList[i];
            for (int j = 0; j < med.LinkedXrefIds.Count; j++)
            {
                var linkedXref = med.LinkedXrefIds[j];
                if (linkedXref is not null)
                {
                    if (!_mediaByEntityId.TryGetValue(linkedXref, out var list))
                    {
                        list = new List<MediaReferenceRecord>();
                        _mediaByEntityId[linkedXref] = list;
                    }
                    list.Add(med);
                }
            }
        }
    }

    /// <summary>
    /// Gets a person record by their unique cross-reference identifier (e.g., "@I1@").
    /// </summary>
    public PersonRecord? GetPerson(string xref)
    {
        if (xref == null) throw new ArgumentNullException(nameof(xref));
        return _personsById.TryGetValue(xref, out var person) ? person : null;
    }

    /// <summary>
    /// Gets a family record by its unique cross-reference identifier (e.g., "@F1@").
    /// </summary>
    public FamilyRecord? GetFamily(string xref)
    {
        if (xref == null) throw new ArgumentNullException(nameof(xref));
        return _familiesById.TryGetValue(xref, out var family) ? family : null;
    }

    /// <summary>
    /// Gets the children of an individual.
    /// </summary>
    public IEnumerable<PersonRecord> ChildrenOf(PersonRecord person)
    {
        if (person == null) throw new ArgumentNullException(nameof(person));
        if (_familiesAsSpouse.TryGetValue(person.XrefId, out var families))
        {
            var children = new List<PersonRecord>();
            for (int i = 0; i < families.Count; i++)
            {
                var fam = families[i];
                if (_childrenByFamilyId.TryGetValue(fam.XrefId, out var list))
                {
                    children.AddRange(list);
                }
            }
            return children;
        }
        return Array.Empty<PersonRecord>();
    }

    /// <summary>
    /// Gets the spouses of an individual.
    /// </summary>
    public IEnumerable<PersonRecord> SpousesOf(PersonRecord person)
    {
        if (person == null) throw new ArgumentNullException(nameof(person));
        if (_familiesAsSpouse.TryGetValue(person.XrefId, out var families))
        {
            var spouses = new List<PersonRecord>();
            for (int i = 0; i < families.Count; i++)
            {
                var fam = families[i];
                string? spouseId = fam.HusbandXref == person.XrefId ? fam.WifeXref : fam.HusbandXref;
                if (spouseId is not null && _personsById.TryGetValue(spouseId, out var spouse))
                {
                    spouses.Add(spouse);
                }
            }
            return spouses;
        }
        return Array.Empty<PersonRecord>();
    }

    /// <summary>
    /// Gets the parents of an individual.
    /// </summary>
    public IEnumerable<PersonRecord> ParentsOf(PersonRecord person)
    {
        if (person == null) throw new ArgumentNullException(nameof(person));
        if (_familiesAsChild.TryGetValue(person.XrefId, out var fam))
        {
            var parents = new List<PersonRecord>(2);
            if (fam.HusbandXref is not null && _personsById.TryGetValue(fam.HusbandXref, out var dad))
            {
                parents.Add(dad);
            }
            if (fam.WifeXref is not null && _personsById.TryGetValue(fam.WifeXref, out var mom))
            {
                parents.Add(mom);
            }
            return parents;
        }
        return Array.Empty<PersonRecord>();
    }

    /// <summary>
    /// Gets the media reference records linked to a specific person or family.
    /// </summary>
    public IEnumerable<MediaReferenceRecord> MediaFor(string entityXref)
    {
        if (entityXref == null) throw new ArgumentNullException(nameof(entityXref));
        return _mediaByEntityId.TryGetValue(entityXref, out var list) ? list : Array.Empty<MediaReferenceRecord>();
    }
}
