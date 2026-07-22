using System;
using System.Collections.Generic;
using System.Linq;

namespace Gedcom.Vector;

/// <summary>
/// A query-optimized context that builds index tables on a <see cref="GedcomParseResult"/>
/// for O(1) family tree relationship traversal, supporting incremental mutable updates.
/// </summary>
public class GedcomTreeContext
{
    private readonly GedcomParseResult _backingResult;
    private readonly Dictionary<string, PersonRecord> _personsById;
    private readonly Dictionary<string, FamilyRecord> _familiesById;
    private readonly Dictionary<string, List<FamilyRecord>> _familiesAsSpouse;
    private readonly Dictionary<string, FamilyRecord> _familiesAsChild;
    private readonly Dictionary<string, List<PersonRecord>> _childrenByFamilyId;
    private readonly Dictionary<string, List<MediaReferenceRecord>> _mediaByEntityId;

    /// <summary>
    /// Gets the backing <see cref="GedcomParseResult"/> instance.
    /// </summary>
    public GedcomParseResult BackingResult => _backingResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="GedcomTreeContext"/> class, indexing the tree.
    /// </summary>
    public GedcomTreeContext(GedcomParseResult result)
    {
        _backingResult = result ?? throw new ArgumentNullException(nameof(result));

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
        _familiesAsSpouse = new Dictionary<string, List<FamilyRecord>>(persons.Count / 2, StringComparer.Ordinal);
        _familiesAsChild = new Dictionary<string, FamilyRecord>(persons.Count, StringComparer.Ordinal);
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
        if (!_familiesAsSpouse.TryGetValue(person.XrefId, out var families) || families.Count == 0)
        {
            return Array.Empty<PersonRecord>();
        }

        if (families.Count == 1)
        {
            return _childrenByFamilyId.TryGetValue(families[0].XrefId, out var list) ? list : Array.Empty<PersonRecord>();
        }

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

    /// <summary>
    /// Gets the spouses of an individual.
    /// </summary>
    public IEnumerable<PersonRecord> SpousesOf(PersonRecord person)
    {
        if (person == null) throw new ArgumentNullException(nameof(person));
        if (!_familiesAsSpouse.TryGetValue(person.XrefId, out var families) || families.Count == 0)
        {
            return Array.Empty<PersonRecord>();
        }

        var spouses = new List<PersonRecord>(families.Count);
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

    /// <summary>
    /// Adds a new person to both the index dictionaries and the backing parsed result.
    /// </summary>
    /// <param name="person">The person record to add.</param>
    public void AddPerson(PersonRecord person)
    {
        if (person == null) throw new ArgumentNullException(nameof(person));
        if (_personsById.ContainsKey(person.XrefId))
        {
            throw new ArgumentException($"Person with ID '{person.XrefId}' already exists.", nameof(person));
        }

        _personsById[person.XrefId] = person;
        _backingResult.Persons.Add(person);
    }

    /// <summary>
    /// Updates an existing person's details in the index dictionaries and the backing parsed result.
    /// </summary>
    /// <param name="person">The updated person record.</param>
    public void UpdatePerson(PersonRecord person)
    {
        if (person == null) throw new ArgumentNullException(nameof(person));
        if (!_personsById.TryGetValue(person.XrefId, out var existing))
        {
            throw new KeyNotFoundException($"Person with ID '{person.XrefId}' not found.");
        }

        // 1. Replace in dictionary
        _personsById[person.XrefId] = person;

        // 2. Replace in backing list
        int idx = _backingResult.Persons.IndexOf(existing);
        if (idx >= 0)
        {
            _backingResult.Persons[idx] = person;
        }

        // 3. Update in children lookup reference lists
        if (_familiesAsChild.TryGetValue(person.XrefId, out var childFam))
        {
            if (_childrenByFamilyId.TryGetValue(childFam.XrefId, out var childrenList))
            {
                for (int i = 0; i < childrenList.Count; i++)
                {
                    if (childrenList[i].XrefId == person.XrefId)
                    {
                        childrenList[i] = person;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Removes a person from the index dictionaries and the backing result, unlinking them from relations.
    /// </summary>
    /// <param name="xref">The ID of the person to delete.</param>
    public void DeletePerson(string xref)
    {
        if (xref == null) throw new ArgumentNullException(nameof(xref));
        if (!_personsById.TryGetValue(xref, out var person)) return;

        // 1. Remove from primary index and backing list
        _personsById.Remove(xref);
        _backingResult.Persons.Remove(person);

        // 2. Unlink from families where this person is a child
        if (_familiesAsChild.TryGetValue(xref, out var childFam))
        {
            if (_childrenByFamilyId.TryGetValue(childFam.XrefId, out var children))
            {
                children.RemoveAll(c => c.XrefId == xref);
            }
            _familiesAsChild.Remove(xref);

            var updatedChildren = childFam.ChildXrefs.Where(id => id != xref).ToList();
            var updatedFam = childFam with { ChildXrefs = updatedChildren };
            ReplaceFamilyInBacking(childFam, updatedFam);
        }

        // 3. Unlink from families where this person is a spouse
        if (_familiesAsSpouse.TryGetValue(xref, out var spouseFamilies))
        {
            for (int i = 0; i < spouseFamilies.Count; i++)
            {
                var fam = spouseFamilies[i];
                if (fam.HusbandXref == xref)
                {
                    var updated = fam with { HusbandXref = null };
                    ReplaceFamilyInBacking(fam, updated);
                }
                else if (fam.WifeXref == xref)
                {
                    var updated = fam with { WifeXref = null };
                    ReplaceFamilyInBacking(fam, updated);
                }
            }
            _familiesAsSpouse.Remove(xref);
        }

        // 4. Clean up media references
        UnlinkEntityFromMedia(xref);
    }

    /// <summary>
    /// Adds a new family to both the index dictionaries and the backing parsed result.
    /// </summary>
    /// <param name="family">The family record to add.</param>
    public void AddFamily(FamilyRecord family)
    {
        if (family == null) throw new ArgumentNullException(nameof(family));
        if (_familiesById.ContainsKey(family.XrefId))
        {
            throw new ArgumentException($"Family with ID '{family.XrefId}' already exists.", nameof(family));
        }

        _familiesById[family.XrefId] = family;
        _backingResult.Families.Add(family);

        // Map spouse lookups
        if (family.HusbandXref is not null)
        {
            if (!_familiesAsSpouse.TryGetValue(family.HusbandXref, out var list))
            {
                list = new List<FamilyRecord>();
                _familiesAsSpouse[family.HusbandXref] = list;
            }
            list.Add(family);
        }

        if (family.WifeXref is not null)
        {
            if (!_familiesAsSpouse.TryGetValue(family.WifeXref, out var list))
            {
                list = new List<FamilyRecord>();
                _familiesAsSpouse[family.WifeXref] = list;
            }
            list.Add(family);
        }

        // Map children lookups
        var childrenList = new List<PersonRecord>(family.ChildXrefs.Count);
        for (int i = 0; i < family.ChildXrefs.Count; i++)
        {
            var childXref = family.ChildXrefs[i];
            if (childXref is not null)
            {
                _familiesAsChild[childXref] = family;
                if (_personsById.TryGetValue(childXref, out var child))
                {
                    childrenList.Add(child);
                }
            }
        }
        _childrenByFamilyId[family.XrefId] = childrenList;
    }

    /// <summary>
    /// Removes a family from the index dictionaries and the backing result, unlinking all relations.
    /// </summary>
    /// <param name="xref">The ID of the family to delete.</param>
    public void DeleteFamily(string xref)
    {
        if (xref == null) throw new ArgumentNullException(nameof(xref));
        if (!_familiesById.TryGetValue(xref, out var family)) return;

        _familiesById.Remove(xref);
        _backingResult.Families.Remove(family);

        // Remove from spouse lookups
        if (family.HusbandXref is not null && _familiesAsSpouse.TryGetValue(family.HusbandXref, out var husbList))
        {
            husbList.Remove(family);
        }
        if (family.WifeXref is not null && _familiesAsSpouse.TryGetValue(family.WifeXref, out var wifeList))
        {
            wifeList.Remove(family);
        }

        // Remove children parent maps
        for (int i = 0; i < family.ChildXrefs.Count; i++)
        {
            var childXref = family.ChildXrefs[i];
            if (childXref is not null)
            {
                _familiesAsChild.Remove(childXref);
            }
        }
        _childrenByFamilyId.Remove(xref);

        // Clean up media references
        UnlinkEntityFromMedia(xref);
    }

    private void UnlinkEntityFromMedia(string xref)
    {
        if (_mediaByEntityId.TryGetValue(xref, out var linkedMediaList))
        {
            var mediaToUpdate = linkedMediaList.ToList();
            _mediaByEntityId.Remove(xref);

            for (int i = 0; i < mediaToUpdate.Count; i++)
            {
                var med = mediaToUpdate[i];
                var updatedLinked = med.LinkedXrefIds.Where(id => id != xref).ToList();
                var updatedMed = med with { LinkedXrefIds = updatedLinked };

                int bIdx = _backingResult.Media.IndexOf(med);
                if (bIdx >= 0)
                {
                    _backingResult.Media[bIdx] = updatedMed;
                }

                foreach (var entityId in updatedLinked)
                {
                    if (_mediaByEntityId.TryGetValue(entityId, out var mediaList))
                    {
                        for (int j = 0; j < mediaList.Count; j++)
                        {
                            if (mediaList[j].XrefId == med.XrefId)
                            {
                                mediaList[j] = updatedMed;
                            }
                        }
                    }
                }
            }
        }
    }

    private void ReplaceFamilyInBacking(FamilyRecord oldFam, FamilyRecord newFam)
    {
        int idx = _backingResult.Families.IndexOf(oldFam);
        if (idx >= 0)
        {
            _backingResult.Families[idx] = newFam;
        }
        _familiesById[oldFam.XrefId] = newFam;

        if (oldFam.HusbandXref is not null && _familiesAsSpouse.TryGetValue(oldFam.HusbandXref, out var husbList))
        {
            int hIdx = husbList.IndexOf(oldFam);
            if (hIdx >= 0) husbList[hIdx] = newFam;
        }
        if (oldFam.WifeXref is not null && _familiesAsSpouse.TryGetValue(oldFam.WifeXref, out var wifeList))
        {
            int wIdx = wifeList.IndexOf(oldFam);
            if (wIdx >= 0) wifeList[wIdx] = newFam;
        }

        for (int i = 0; i < oldFam.ChildXrefs.Count; i++)
        {
            var childXref = oldFam.ChildXrefs[i];
            if (childXref is not null)
            {
                _familiesAsChild[childXref] = newFam;
            }
        }
    }
}
