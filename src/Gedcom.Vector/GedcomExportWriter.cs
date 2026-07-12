using System.Text;
using Gedcom.Vector;

namespace Gedcom.Vector;

public class GedcomExportWriter : IGedcomExportWriter
{
    private static readonly Dictionary<FamTreeEventType, string> TagByEventType = new()
    {
        [FamTreeEventType.Birth] = "BIRT",
        [FamTreeEventType.Death] = "DEAT",
        [FamTreeEventType.Census] = "CENS",
        [FamTreeEventType.Immigration] = "IMMI",
        [FamTreeEventType.Emigration] = "EMIG",
        [FamTreeEventType.Residence] = "RESI",
        [FamTreeEventType.Christening] = "CHR",
        [FamTreeEventType.Burial] = "BURI",
    };

    public string Write(GedcomParseResult parseResult)
    {
        var sb = new StringBuilder();

        sb.Append("0 HEAD\n");
        sb.Append("1 GEDC\n");
        sb.Append("2 VERS 5.5.1\n");
        sb.Append("2 FORM LINEAGE-LINKED\n");
        sb.Append("1 CHAR UTF-8\n");

        var eventsByPersonXref = parseResult.Events
            .GroupBy(e => e.PersonXrefId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var familiesAsChild = parseResult.Families
            .SelectMany(f => f.ChildXrefs.Select(childXref => (childXref, f.XrefId)))
            .GroupBy(t => t.childXref)
            .ToDictionary(g => g.Key, g => g.Select(t => t.XrefId).ToList());

        var familiesAsSpouse = parseResult.Families
            .SelectMany(f => new[] { f.HusbandXref, f.WifeXref }.Where(x => x is not null).Select(x => (spouseXref: x!, f.XrefId)))
            .GroupBy(t => t.spouseXref)
            .ToDictionary(g => g.Key, g => g.Select(t => t.XrefId).ToList());

        var mediaByLinkedXref = parseResult.Media
            .SelectMany(m => m.LinkedXrefIds.Select(x => (linkedXref: x, m.XrefId)))
            .GroupBy(t => t.linkedXref)
            .ToDictionary(g => g.Key, g => g.Select(t => t.XrefId).ToList());

        foreach (var person in parseResult.Persons)
        {
            WritePerson(sb, person, eventsByPersonXref, familiesAsChild, familiesAsSpouse, mediaByLinkedXref);
        }

        foreach (var family in parseResult.Families)
        {
            WriteFamily(sb, family, mediaByLinkedXref);
        }

        foreach (var media in parseResult.Media)
        {
            WriteMedia(sb, media);
        }

        sb.Append("0 TRLR");

        return sb.ToString();
    }

    private static void WritePerson(
        StringBuilder sb, PersonRecord person,
        IReadOnlyDictionary<string, List<EventRecord>> eventsByPersonXref,
        IReadOnlyDictionary<string, List<string>> familiesAsChild,
        IReadOnlyDictionary<string, List<string>> familiesAsSpouse,
        IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        sb.Append($"0 {person.XrefId} INDI\n");

        if (person.FirstName is not null || person.LastName is not null)
        {
            var name = person.LastName is not null
                ? $"{(person.FirstName is not null ? person.FirstName + " " : string.Empty)}/{person.LastName}/"
                : person.FirstName!;
            sb.Append($"1 NAME {name}\n");
        }

        if (person.Sex == PersonSex.Male)
        {
            sb.Append("1 SEX M\n");
        }
        else if (person.Sex == PersonSex.Female)
        {
            sb.Append("1 SEX F\n");
        }

        WriteDatedEventBlock(sb, "BIRT", person.BirthDate, person.BirthPlace);
        WriteDatedEventBlock(sb, "DEAT", person.DeathDate, person.DeathPlace);

        if (eventsByPersonXref.TryGetValue(person.XrefId, out var events))
        {
            foreach (var evt in events)
            {
                WriteDatedEventBlock(sb, TagByEventType[evt.EventType], evt.Date, evt.Place);
            }
        }

        if (familiesAsChild.TryGetValue(person.XrefId, out var famcXrefs))
        {
            foreach (var famc in famcXrefs)
            {
                sb.Append($"1 FAMC {famc}\n");
            }
        }

        if (familiesAsSpouse.TryGetValue(person.XrefId, out var famsXrefs))
        {
            foreach (var fams in famsXrefs)
            {
                sb.Append($"1 FAMS {fams}\n");
            }
        }

        WriteObjeLines(sb, person.XrefId, mediaByLinkedXref);
    }

    private static void WriteFamily(StringBuilder sb, FamilyRecord family, IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        sb.Append($"0 {family.XrefId} FAM\n");

        if (family.HusbandXref is not null)
        {
            sb.Append($"1 HUSB {family.HusbandXref}\n");
        }

        if (family.WifeXref is not null)
        {
            sb.Append($"1 WIFE {family.WifeXref}\n");
        }

        foreach (var child in family.ChildXrefs)
        {
            sb.Append($"1 CHIL {child}\n");
        }

        WriteDatedEventBlock(sb, "MARR", family.MarriageDate, family.MarriagePlace);

        WriteObjeLines(sb, family.XrefId, mediaByLinkedXref);
    }

    private static void WriteMedia(StringBuilder sb, MediaReferenceRecord media)
    {
        sb.Append($"0 {media.XrefId} OBJE\n");

        if (media.Format is not null)
        {
            sb.Append($"1 FORM {media.Format}\n");
        }

        if (media.Title is not null)
        {
            sb.Append($"1 TITL {media.Title}\n");
        }

        if (media.FilePath is not null)
        {
            sb.Append($"1 FILE {media.FilePath}\n");
        }
    }

    private static void WriteDatedEventBlock(StringBuilder sb, string tag, string? date, string? place)
    {
        if (date is null && place is null)
        {
            return;
        }

        sb.Append($"1 {tag}\n");

        if (date is not null)
        {
            sb.Append($"2 DATE {date}\n");
        }

        if (place is not null)
        {
            sb.Append($"2 PLAC {place}\n");
        }
    }

    private static void WriteObjeLines(StringBuilder sb, string ownerXref, IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        if (!mediaByLinkedXref.TryGetValue(ownerXref, out var mediaXrefs))
        {
            return;
        }

        foreach (var mediaXref in mediaXrefs)
        {
            sb.Append($"1 OBJE {mediaXref}\n");
        }
    }
}
