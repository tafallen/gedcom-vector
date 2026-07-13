using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));

        var sb = new StringBuilder();
        using (var writer = new StringWriter(sb))
        {
            WriteInternal(writer, parseResult);
        }
        return sb.ToString();
    }

    public void Write(GedcomParseResult parseResult, Stream output)
    {
        if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
        if (output == null) throw new ArgumentNullException(nameof(output));

        using var writer = new StreamWriter(output, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
        WriteInternal(writer, parseResult);
    }

    public async Task WriteAsync(GedcomParseResult parseResult, Stream output, CancellationToken cancellationToken = default)
    {
        if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
        if (output == null) throw new ArgumentNullException(nameof(output));

        using var writer = new StreamWriter(output, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
        await WriteInternalAsync(writer, parseResult, cancellationToken);
    }

    private static void WriteInternal(TextWriter writer, GedcomParseResult parseResult)
    {
        var eventsByPersonXref = new Dictionary<string, List<EventRecord>>(StringComparer.Ordinal);
        var events = parseResult.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.PersonXrefId is null) continue;
            if (!eventsByPersonXref.TryGetValue(ev.PersonXrefId, out var list))
            {
                list = new List<EventRecord>();
                eventsByPersonXref[ev.PersonXrefId] = list;
            }
            list.Add(ev);
        }

        var familiesAsChild = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var familiesAsSpouse = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var families = parseResult.Families;
        for (int i = 0; i < families.Count; i++)
        {
            var fam = families[i];
            var childXrefs = fam.ChildXrefs;
            for (int j = 0; j < childXrefs.Count; j++)
            {
                var childXref = childXrefs[j];
                if (childXref is null) continue;
                if (!familiesAsChild.TryGetValue(childXref, out var list))
                {
                    list = new List<string>();
                    familiesAsChild[childXref] = list;
                }
                list.Add(fam.XrefId);
            }

            if (fam.HusbandXref is not null)
            {
                if (!familiesAsSpouse.TryGetValue(fam.HusbandXref, out var list))
                {
                    list = new List<string>();
                    familiesAsSpouse[fam.HusbandXref] = list;
                }
                list.Add(fam.XrefId);
            }

            if (fam.WifeXref is not null)
            {
                if (!familiesAsSpouse.TryGetValue(fam.WifeXref, out var list))
                {
                    list = new List<string>();
                    familiesAsSpouse[fam.WifeXref] = list;
                }
                list.Add(fam.XrefId);
            }
        }

        var mediaByLinkedXref = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var mediaList = parseResult.Media;
        for (int i = 0; i < mediaList.Count; i++)
        {
            var med = mediaList[i];
            var linkedXrefIds = med.LinkedXrefIds;
            for (int j = 0; j < linkedXrefIds.Count; j++)
            {
                var linkedXref = linkedXrefIds[j];
                if (linkedXref is null) continue;
                if (!mediaByLinkedXref.TryGetValue(linkedXref, out var list))
                {
                    list = new List<string>();
                    mediaByLinkedXref[linkedXref] = list;
                }
                list.Add(med.XrefId);
            }
        }

        writer.Write("0 HEAD\n");
        writer.Write("1 GEDC\n");
        writer.Write("2 VERS 5.5.1\n");
        writer.Write("2 FORM LINEAGE-LINKED\n");
        writer.Write("1 CHAR UTF-8\n");

        var persons = parseResult.Persons;
        for (int i = 0; i < persons.Count; i++)
        {
            WritePerson(writer, persons[i], eventsByPersonXref, familiesAsChild, familiesAsSpouse, mediaByLinkedXref);
        }

        for (int i = 0; i < families.Count; i++)
        {
            WriteFamily(writer, families[i], mediaByLinkedXref);
        }

        for (int i = 0; i < mediaList.Count; i++)
        {
            WriteMedia(writer, mediaList[i]);
        }

        writer.Write("0 TRLR");
    }

    private static void WritePerson(
        TextWriter writer, PersonRecord person,
        IReadOnlyDictionary<string, List<EventRecord>> eventsByPersonXref,
        IReadOnlyDictionary<string, List<string>> familiesAsChild,
        IReadOnlyDictionary<string, List<string>> familiesAsSpouse,
        IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        writer.Write("0 ");
        writer.Write(person.XrefId);
        writer.Write(" INDI\n");

        if (person.FirstName is not null || person.LastName is not null)
        {
            writer.Write("1 NAME ");
            if (person.LastName is not null)
            {
                if (person.FirstName is not null)
                {
                    writer.Write(person.FirstName);
                    writer.Write(' ');
                }
                writer.Write('/');
                writer.Write(person.LastName);
                writer.Write('/');
            }
            else
            {
                writer.Write(person.FirstName!);
            }
            writer.Write('\n');
        }

        if (person.Sex == PersonSex.Male)
        {
            writer.Write("1 SEX M\n");
        }
        else if (person.Sex == PersonSex.Female)
        {
            writer.Write("1 SEX F\n");
        }

        WriteDatedEventBlock(writer, "BIRT", person.BirthDate, person.BirthPlace);
        WriteDatedEventBlock(writer, "DEAT", person.DeathDate, person.DeathPlace);

        if (eventsByPersonXref.TryGetValue(person.XrefId, out var events))
        {
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                WriteDatedEventBlock(writer, TagByEventType[evt.EventType], evt.Date, evt.Place);
            }
        }

        if (familiesAsChild.TryGetValue(person.XrefId, out var famcXrefs))
        {
            for (int i = 0; i < famcXrefs.Count; i++)
            {
                writer.Write("1 FAMC ");
                writer.Write(famcXrefs[i]);
                writer.Write('\n');
            }
        }

        if (familiesAsSpouse.TryGetValue(person.XrefId, out var famsXrefs))
        {
            for (int i = 0; i < famsXrefs.Count; i++)
            {
                writer.Write("1 FAMS ");
                writer.Write(famsXrefs[i]);
                writer.Write('\n');
            }
        }

        WriteObjeLines(writer, person.XrefId, mediaByLinkedXref);
    }

    private static void WriteFamily(TextWriter writer, FamilyRecord family, IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        writer.Write("0 ");
        writer.Write(family.XrefId);
        writer.Write(" FAM\n");

        if (family.HusbandXref is not null)
        {
            writer.Write("1 HUSB ");
            writer.Write(family.HusbandXref);
            writer.Write('\n');
        }

        if (family.WifeXref is not null)
        {
            writer.Write("1 WIFE ");
            writer.Write(family.WifeXref);
            writer.Write('\n');
        }

        var childXrefs = family.ChildXrefs;
        for (int i = 0; i < childXrefs.Count; i++)
        {
            writer.Write("1 CHIL ");
            writer.Write(childXrefs[i]);
            writer.Write('\n');
        }

        WriteDatedEventBlock(writer, "MARR", family.MarriageDate, family.MarriagePlace);

        WriteObjeLines(writer, family.XrefId, mediaByLinkedXref);
    }

    private static void WriteMedia(TextWriter writer, MediaReferenceRecord media)
    {
        writer.Write("0 ");
        writer.Write(media.XrefId);
        writer.Write(" OBJE\n");

        if (media.Format is not null)
        {
            writer.Write("1 FORM ");
            writer.Write(media.Format);
            writer.Write('\n');
        }

        if (media.Title is not null)
        {
            writer.Write("1 TITL ");
            writer.Write(media.Title);
            writer.Write('\n');
        }

        if (media.FilePath is not null)
        {
            writer.Write("1 FILE ");
            writer.Write(media.FilePath);
            writer.Write('\n');
        }
    }

    private static void WriteDatedEventBlock(TextWriter writer, string tag, string? date, string? place)
    {
        if (date is null && place is null)
        {
            return;
        }

        writer.Write("1 ");
        writer.Write(tag);
        writer.Write('\n');

        if (date is not null)
        {
            writer.Write("2 DATE ");
            writer.Write(date);
            writer.Write('\n');
        }

        if (place is not null)
        {
            writer.Write("2 PLAC ");
            writer.Write(place);
            writer.Write('\n');
        }
    }

    private static void WriteObjeLines(TextWriter writer, string ownerXref, IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        if (!mediaByLinkedXref.TryGetValue(ownerXref, out var mediaXrefs))
        {
            return;
        }

        for (int i = 0; i < mediaXrefs.Count; i++)
        {
            writer.Write("1 OBJE ");
            writer.Write(mediaXrefs[i]);
            writer.Write('\n');
        }
    }

    // Async equivalents
    private static async Task WriteInternalAsync(TextWriter writer, GedcomParseResult parseResult, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var eventsByPersonXref = new Dictionary<string, List<EventRecord>>(StringComparer.Ordinal);
        var events = parseResult.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.PersonXrefId is null) continue;
            if (!eventsByPersonXref.TryGetValue(ev.PersonXrefId, out var list))
            {
                list = new List<EventRecord>();
                eventsByPersonXref[ev.PersonXrefId] = list;
            }
            list.Add(ev);
        }

        var familiesAsChild = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var familiesAsSpouse = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var families = parseResult.Families;
        for (int i = 0; i < families.Count; i++)
        {
            var fam = families[i];
            var childXrefs = fam.ChildXrefs;
            for (int j = 0; j < childXrefs.Count; j++)
            {
                var childXref = childXrefs[j];
                if (childXref is null) continue;
                if (!familiesAsChild.TryGetValue(childXref, out var list))
                {
                    list = new List<string>();
                    familiesAsChild[childXref] = list;
                }
                list.Add(fam.XrefId);
            }

            if (fam.HusbandXref is not null)
            {
                if (!familiesAsSpouse.TryGetValue(fam.HusbandXref, out var list))
                {
                    list = new List<string>();
                    familiesAsSpouse[fam.HusbandXref] = list;
                }
                list.Add(fam.XrefId);
            }

            if (fam.WifeXref is not null)
            {
                if (!familiesAsSpouse.TryGetValue(fam.WifeXref, out var list))
                {
                    list = new List<string>();
                    familiesAsSpouse[fam.WifeXref] = list;
                }
                list.Add(fam.XrefId);
            }
        }

        var mediaByLinkedXref = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var mediaList = parseResult.Media;
        for (int i = 0; i < mediaList.Count; i++)
        {
            var med = mediaList[i];
            var linkedXrefIds = med.LinkedXrefIds;
            for (int j = 0; j < linkedXrefIds.Count; j++)
            {
                var linkedXref = linkedXrefIds[j];
                if (linkedXref is null) continue;
                if (!mediaByLinkedXref.TryGetValue(linkedXref, out var list))
                {
                    list = new List<string>();
                    mediaByLinkedXref[linkedXref] = list;
                }
                list.Add(med.XrefId);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        await writer.WriteAsync("0 HEAD\n");
        await writer.WriteAsync("1 GEDC\n");
        await writer.WriteAsync("2 VERS 5.5.1\n");
        await writer.WriteAsync("2 FORM LINEAGE-LINKED\n");
        await writer.WriteAsync("1 CHAR UTF-8\n");

        var persons = parseResult.Persons;
        for (int i = 0; i < persons.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WritePersonAsync(writer, persons[i], eventsByPersonXref, familiesAsChild, familiesAsSpouse, mediaByLinkedXref);
        }

        for (int i = 0; i < families.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteFamilyAsync(writer, families[i], mediaByLinkedXref);
        }

        for (int i = 0; i < mediaList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteMediaAsync(writer, mediaList[i]);
        }

        await writer.WriteAsync("0 TRLR");
    }

    private static async Task WritePersonAsync(
        TextWriter writer, PersonRecord person,
        IReadOnlyDictionary<string, List<EventRecord>> eventsByPersonXref,
        IReadOnlyDictionary<string, List<string>> familiesAsChild,
        IReadOnlyDictionary<string, List<string>> familiesAsSpouse,
        IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        await writer.WriteAsync("0 ");
        await writer.WriteAsync(person.XrefId);
        await writer.WriteAsync(" INDI\n");

        if (person.FirstName is not null || person.LastName is not null)
        {
            await writer.WriteAsync("1 NAME ");
            if (person.LastName is not null)
            {
                if (person.FirstName is not null)
                {
                    await writer.WriteAsync(person.FirstName);
                    await writer.WriteAsync(' ');
                }
                await writer.WriteAsync('/');
                await writer.WriteAsync(person.LastName);
                await writer.WriteAsync('/');
            }
            else
            {
                await writer.WriteAsync(person.FirstName!);
            }
            await writer.WriteAsync('\n');
        }

        if (person.Sex == PersonSex.Male)
        {
            await writer.WriteAsync("1 SEX M\n");
        }
        else if (person.Sex == PersonSex.Female)
        {
            await writer.WriteAsync("1 SEX F\n");
        }

        await WriteDatedEventBlockAsync(writer, "BIRT", person.BirthDate, person.BirthPlace);
        await WriteDatedEventBlockAsync(writer, "DEAT", person.DeathDate, person.DeathPlace);

        if (eventsByPersonXref.TryGetValue(person.XrefId, out var events))
        {
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                await WriteDatedEventBlockAsync(writer, TagByEventType[evt.EventType], evt.Date, evt.Place);
            }
        }

        if (familiesAsChild.TryGetValue(person.XrefId, out var famcXrefs))
        {
            for (int i = 0; i < famcXrefs.Count; i++)
            {
                await writer.WriteAsync("1 FAMC ");
                await writer.WriteAsync(famcXrefs[i]);
                await writer.WriteAsync('\n');
            }
        }

        if (familiesAsSpouse.TryGetValue(person.XrefId, out var famsXrefs))
        {
            for (int i = 0; i < famsXrefs.Count; i++)
            {
                await writer.WriteAsync("1 FAMS ");
                await writer.WriteAsync(famsXrefs[i]);
                await writer.WriteAsync('\n');
            }
        }

        await WriteObjeLinesAsync(writer, person.XrefId, mediaByLinkedXref);
    }

    private static async Task WriteFamilyAsync(TextWriter writer, FamilyRecord family, IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        await writer.WriteAsync("0 ");
        await writer.WriteAsync(family.XrefId);
        await writer.WriteAsync(" FAM\n");

        if (family.HusbandXref is not null)
        {
            await writer.WriteAsync("1 HUSB ");
            await writer.WriteAsync(family.HusbandXref);
            await writer.WriteAsync('\n');
        }

        if (family.WifeXref is not null)
        {
            await writer.WriteAsync("1 WIFE ");
            await writer.WriteAsync(family.WifeXref);
            await writer.WriteAsync('\n');
        }

        var childXrefs = family.ChildXrefs;
        for (int i = 0; i < childXrefs.Count; i++)
        {
            await writer.WriteAsync("1 CHIL ");
            await writer.WriteAsync(childXrefs[i]);
            await writer.WriteAsync('\n');
        }

        await WriteDatedEventBlockAsync(writer, "MARR", family.MarriageDate, family.MarriagePlace);

        await WriteObjeLinesAsync(writer, family.XrefId, mediaByLinkedXref);
    }

    private static async Task WriteMediaAsync(TextWriter writer, MediaReferenceRecord media)
    {
        await writer.WriteAsync("0 ");
        await writer.WriteAsync(media.XrefId);
        await writer.WriteAsync(" OBJE\n");

        if (media.Format is not null)
        {
            await writer.WriteAsync("1 FORM ");
            await writer.WriteAsync(media.Format);
            await writer.WriteAsync('\n');
        }

        if (media.Title is not null)
        {
            await writer.WriteAsync("1 TITL ");
            await writer.WriteAsync(media.Title);
            await writer.WriteAsync('\n');
        }

        if (media.FilePath is not null)
        {
            await writer.WriteAsync("1 FILE ");
            await writer.WriteAsync(media.FilePath);
            await writer.WriteAsync('\n');
        }
    }

    private static async Task WriteDatedEventBlockAsync(TextWriter writer, string tag, string? date, string? place)
    {
        if (date is null && place is null)
        {
            return;
        }

        await writer.WriteAsync("1 ");
        await writer.WriteAsync(tag);
        await writer.WriteAsync('\n');

        if (date is not null)
        {
            await writer.WriteAsync("2 DATE ");
            await writer.WriteAsync(date);
            await writer.WriteAsync('\n');
        }

        if (place is not null)
        {
            await writer.WriteAsync("2 PLAC ");
            await writer.WriteAsync(place);
            await writer.WriteAsync('\n');
        }
    }

    private static async Task WriteObjeLinesAsync(TextWriter writer, string ownerXref, IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        if (!mediaByLinkedXref.TryGetValue(ownerXref, out var mediaXrefs))
        {
            return;
        }

        for (int i = 0; i < mediaXrefs.Count; i++)
        {
            await writer.WriteAsync("1 OBJE ");
            await writer.WriteAsync(mediaXrefs[i]);
            await writer.WriteAsync('\n');
        }
    }
}
