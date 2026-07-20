using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gedcom.Vector;

/// <inheritdoc />
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

    /// <inheritdoc />
    public string Write(GedcomParseResult parseResult)
    {
        if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));

        using var ms = new MemoryStream(4096);
        Write(parseResult, ms);
        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
    }

    /// <inheritdoc />
    public void Write(GedcomParseResult parseResult, Stream output)
    {
        if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
        if (output == null) throw new ArgumentNullException(nameof(output));

        byte[] rentBuffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            var writer = new Utf8StreamWriter(output, rentBuffer);
            WriteInternal(ref writer, parseResult);
            writer.Flush();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentBuffer);
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(GedcomParseResult parseResult, Stream output, CancellationToken cancellationToken = default)
    {
        if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
        if (output == null) throw new ArgumentNullException(nameof(output));

        // MemoryStream serialization buffer for async writing to destination stream
        using var ms = new MemoryStream();
        Write(parseResult, ms);
        ms.Position = 0;
        await ms.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static void WriteInternal(ref Utf8StreamWriter writer, GedcomParseResult parseResult)
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

        writer.WriteUtf8("0 HEAD\n"u8);
        writer.WriteUtf8("1 GEDC\n"u8);
        writer.WriteUtf8("2 VERS 5.5.1\n"u8);
        writer.WriteUtf8("2 FORM LINEAGE-LINKED\n"u8);
        writer.WriteUtf8("1 CHAR UTF-8\n"u8);

        var persons = parseResult.Persons;
        for (int i = 0; i < persons.Count; i++)
        {
            WritePerson(ref writer, persons[i], eventsByPersonXref, familiesAsChild, familiesAsSpouse, mediaByLinkedXref);
        }

        for (int i = 0; i < families.Count; i++)
        {
            WriteFamily(ref writer, families[i], mediaByLinkedXref);
        }

        for (int i = 0; i < mediaList.Count; i++)
        {
            WriteMedia(ref writer, mediaList[i]);
        }

        writer.WriteUtf8("0 TRLR"u8);
    }

    private static void WritePerson(
        ref Utf8StreamWriter writer, PersonRecord person,
        IReadOnlyDictionary<string, List<EventRecord>> eventsByPersonXref,
        IReadOnlyDictionary<string, List<string>> familiesAsChild,
        IReadOnlyDictionary<string, List<string>> familiesAsSpouse,
        IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        writer.WriteUtf8("0 "u8);
        writer.WriteString(person.XrefId);
        writer.WriteUtf8(" INDI\n"u8);

        if (person.FirstName is not null || person.LastName is not null)
        {
            writer.WriteUtf8("1 NAME "u8);
            if (person.LastName is not null)
            {
                if (person.FirstName is not null)
                {
                    writer.WriteString(person.FirstName);
                    writer.WriteByte((byte)' ');
                }
                writer.WriteByte((byte)'/');
                writer.WriteString(person.LastName);
                writer.WriteByte((byte)'/');
            }
            else
            {
                writer.WriteString(person.FirstName);
            }
            writer.WriteByte((byte)'\n');
        }

        if (person.Sex == PersonSex.Male)
        {
            writer.WriteUtf8("1 SEX M\n"u8);
        }
        else if (person.Sex == PersonSex.Female)
        {
            writer.WriteUtf8("1 SEX F\n"u8);
        }

        WriteDatedEventBlock(ref writer, "BIRT"u8, person.BirthDate, person.BirthPlace);
        WriteDatedEventBlock(ref writer, "DEAT"u8, person.DeathDate, person.DeathPlace);

        if (eventsByPersonXref.TryGetValue(person.XrefId, out var events))
        {
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (TagByEventType.TryGetValue(evt.EventType, out var tagStr))
                {
                    WriteDatedEventBlockStringTag(ref writer, tagStr, evt.Date, evt.Place);
                }
            }
        }

        if (familiesAsChild.TryGetValue(person.XrefId, out var famcXrefs))
        {
            for (int i = 0; i < famcXrefs.Count; i++)
            {
                writer.WriteUtf8("1 FAMC "u8);
                writer.WriteString(famcXrefs[i]);
                writer.WriteByte((byte)'\n');
            }
        }

        if (familiesAsSpouse.TryGetValue(person.XrefId, out var famsXrefs))
        {
            for (int i = 0; i < famsXrefs.Count; i++)
            {
                writer.WriteUtf8("1 FAMS "u8);
                writer.WriteString(famsXrefs[i]);
                writer.WriteByte((byte)'\n');
            }
        }

        WriteObjeLines(ref writer, person.XrefId, mediaByLinkedXref);
    }

    private static void WriteFamily(ref Utf8StreamWriter writer, FamilyRecord family, IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        writer.WriteUtf8("0 "u8);
        writer.WriteString(family.XrefId);
        writer.WriteUtf8(" FAM\n"u8);

        if (family.HusbandXref is not null)
        {
            writer.WriteUtf8("1 HUSB "u8);
            writer.WriteString(family.HusbandXref);
            writer.WriteByte((byte)'\n');
        }

        if (family.WifeXref is not null)
        {
            writer.WriteUtf8("1 WIFE "u8);
            writer.WriteString(family.WifeXref);
            writer.WriteByte((byte)'\n');
        }

        var childXrefs = family.ChildXrefs;
        for (int i = 0; i < childXrefs.Count; i++)
        {
            var child = childXrefs[i];
            if (child is not null)
            {
                writer.WriteUtf8("1 CHIL "u8);
                writer.WriteString(child);
                writer.WriteByte((byte)'\n');
            }
        }

        WriteDatedEventBlock(ref writer, "MARR"u8, family.MarriageDate, family.MarriagePlace);
        WriteObjeLines(ref writer, family.XrefId, mediaByLinkedXref);
    }

    private static void WriteMedia(ref Utf8StreamWriter writer, MediaReferenceRecord media)
    {
        writer.WriteUtf8("0 "u8);
        writer.WriteString(media.XrefId);
        writer.WriteUtf8(" OBJE\n"u8);

        if (media.Format is not null)
        {
            writer.WriteUtf8("1 FORM "u8);
            writer.WriteString(media.Format);
            writer.WriteByte((byte)'\n');
        }

        if (media.Title is not null)
        {
            writer.WriteUtf8("1 TITL "u8);
            writer.WriteString(media.Title);
            writer.WriteByte((byte)'\n');
        }

        if (media.FilePath is not null)
        {
            writer.WriteUtf8("1 FILE "u8);
            writer.WriteString(media.FilePath);
            writer.WriteByte((byte)'\n');
        }
    }

    private static void WriteObjeLines(ref Utf8StreamWriter writer, string entityXref, IReadOnlyDictionary<string, List<string>> mediaByLinkedXref)
    {
        if (mediaByLinkedXref.TryGetValue(entityXref, out var mediaXrefs))
        {
            for (int i = 0; i < mediaXrefs.Count; i++)
            {
                writer.WriteUtf8("1 OBJE @"u8);
                writer.WriteString(mediaXrefs[i]);
                writer.WriteUtf8("@\n"u8);
            }
        }
    }

    private static void WriteDatedEventBlock(ref Utf8StreamWriter writer, ReadOnlySpan<byte> tagUtf8, string? date, string? place)
    {
        if (date is null && place is null) return;

        writer.WriteUtf8("1 "u8);
        writer.WriteUtf8(tagUtf8);
        writer.WriteByte((byte)'\n');

        if (date is not null)
        {
            writer.WriteUtf8("2 DATE "u8);
            writer.WriteString(date);
            writer.WriteByte((byte)'\n');
        }

        if (place is not null)
        {
            writer.WriteUtf8("2 PLAC "u8);
            writer.WriteString(place);
            writer.WriteByte((byte)'\n');
        }
    }

    private static void WriteDatedEventBlockStringTag(ref Utf8StreamWriter writer, string tagStr, string? date, string? place)
    {
        if (date is null && place is null) return;

        writer.WriteUtf8("1 "u8);
        writer.WriteString(tagStr);
        writer.WriteByte((byte)'\n');

        if (date is not null)
        {
            writer.WriteUtf8("2 DATE "u8);
            writer.WriteString(date);
            writer.WriteByte((byte)'\n');
        }

        if (place is not null)
        {
            writer.WriteUtf8("2 PLAC "u8);
            writer.WriteString(place);
            writer.WriteByte((byte)'\n');
        }
    }

    private ref struct Utf8StreamWriter
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer;
        private int _position;

        public Utf8StreamWriter(Stream stream, byte[] buffer)
        {
            _stream = stream;
            _buffer = buffer;
            _position = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte b)
        {
            if (_position >= _buffer.Length)
            {
                Flush();
            }
            _buffer[_position++] = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUtf8(ReadOnlySpan<byte> bytes)
        {
            if (_position + bytes.Length > _buffer.Length)
            {
                Flush();
            }
            if (bytes.Length > _buffer.Length)
            {
                _stream.Write(bytes);
                return;
            }
            bytes.CopyTo(_buffer.AsSpan(_position));
            _position += bytes.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(string? str)
        {
            if (string.IsNullOrEmpty(str)) return;
            int maxBytes = Encoding.UTF8.GetMaxByteCount(str.Length);
            if (_position + maxBytes > _buffer.Length)
            {
                Flush();
            }
            int written = Encoding.UTF8.GetBytes(str.AsSpan(), _buffer.AsSpan(_position));
            _position += written;
        }

        public void Flush()
        {
            if (_position > 0)
            {
                _stream.Write(_buffer, 0, _position);
                _position = 0;
            }
        }
    }
}
