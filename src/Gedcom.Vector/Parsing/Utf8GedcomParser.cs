using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// A high-performance direct UTF-8 byte span parser that tokenizes UTF-8 streams using SIMD byte searching
/// without intermediate StreamReader character decoding overhead.
/// </summary>
public static class Utf8GedcomParser
{
    private static readonly SearchValues<byte> Utf8LineBreaks = SearchValues.Create(new byte[] { (byte)'\r', (byte)'\n' });

    /// <summary>
    /// Parses a UTF-8 encoded GEDCOM input stream directly using SIMD byte line splitting.
    /// </summary>
    public static GedcomParseResult Parse(Stream stream, ILogger? logger = null)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        var result = new GedcomParseResult();
        var pool = new GedcomStringPool(4096);
        var mediaLinks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var mediaNodes = new List<(string XrefId, string? Title, string? FilePath, string? Format)>();

        string? currentXref = null;
        RecordType currentRecordType = RecordType.None;

        string? personFirstName = null;
        string? personLastName = null;
        PersonSex personSex = PersonSex.Unknown;
        string? personBirthDate = null;
        string? personBirthPlace = null;
        string? personDeathDate = null;
        string? personDeathPlace = null;
        SubTag activeSubTag = SubTag.None;

        string? famHusbandXref = null;
        string? famWifeXref = null;
        List<string>? famChildXrefs = null;
        string? famMarriageDate = null;
        string? famMarriagePlace = null;

        string? mediaTitle = null;
        string? mediaFilePath = null;
        string? mediaFormat = null;

        string? unparsedTag = null;
        string? unparsedValue = null;
        List<string>? unparsedRawLines = null;

        StringBuilder? concBuilder = null;
        ConcTarget concTarget = ConcTarget.None;

        void ApplyConc()
        {
            if (concBuilder == null || concTarget == ConcTarget.None) return;
            string strVal = concBuilder.ToString();
            switch (concTarget)
            {
                case ConcTarget.PersonFirstName: personFirstName = pool.GetOrAdd(strVal); break;
                case ConcTarget.PersonLastName: personLastName = pool.GetOrAdd(strVal); break;
                case ConcTarget.PersonBirthPlace: personBirthPlace = pool.GetOrAdd(strVal); break;
                case ConcTarget.PersonDeathPlace: personDeathPlace = pool.GetOrAdd(strVal); break;
                case ConcTarget.FamMarriagePlace: famMarriagePlace = pool.GetOrAdd(strVal); break;
                case ConcTarget.MediaTitle: mediaTitle = pool.GetOrAdd(strVal); break;
            }
            concBuilder = null;
            concTarget = ConcTarget.None;
        }

        void FlushCurrentRecord()
        {
            ApplyConc();
            switch (currentRecordType)
            {
                case RecordType.Person when currentXref != null:
                    result.Persons.Add(new PersonRecord(
                        currentXref, personFirstName, personLastName, personSex,
                        personBirthDate, personBirthPlace, personDeathDate, personDeathPlace));
                    break;

                case RecordType.Family when currentXref != null:
                    result.Families.Add(new FamilyRecord(
                        currentXref, famHusbandXref, famWifeXref,
                        famChildXrefs ?? (IReadOnlyList<string>)Array.Empty<string>(),
                        famMarriageDate, famMarriagePlace));
                    break;

                case RecordType.Media when currentXref != null:
                    mediaNodes.Add((currentXref, mediaTitle, mediaFilePath, mediaFormat));
                    break;

                case RecordType.Unparsed when unparsedTag != null:
                    result.UnparsedRecords.Add(new UnparsedRecord(currentXref, unparsedTag, unparsedValue, unparsedRawLines));
                    break;
            }

            currentXref = null;
            currentRecordType = RecordType.None;
            personFirstName = null;
            personLastName = null;
            personSex = PersonSex.Unknown;
            personBirthDate = null;
            personBirthPlace = null;
            personDeathDate = null;
            personDeathPlace = null;
            activeSubTag = SubTag.None;
            famHusbandXref = null;
            famWifeXref = null;
            famChildXrefs = null;
            famMarriageDate = null;
            famMarriagePlace = null;
            mediaTitle = null;
            mediaFilePath = null;
            mediaFormat = null;
            unparsedTag = null;
            unparsedValue = null;
            unparsedRawLines = null;
        }

        int bufferSize = 65536;
        byte[] rentBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int bufferCount = 0;
            int read;

            while ((read = stream.Read(rentBuffer, bufferCount, rentBuffer.Length - bufferCount)) > 0)
            {
                bufferCount += read;
                var bufferSpan = rentBuffer.AsSpan(0, bufferCount);
                int position = 0;

                while (position < bufferSpan.Length)
                {
                    var slice = bufferSpan.Slice(position);
                    int breakIndex = slice.IndexOfAny(Utf8LineBreaks);
                    if (breakIndex < 0) break;

                    var lineSpan = slice.Slice(0, breakIndex);
                    position += breakIndex + 1;

                    if (slice[breakIndex] == (byte)'\r' && position < bufferSpan.Length && bufferSpan[position] == (byte)'\n')
                    {
                        position++;
                    }

                    ProcessByteLine(lineSpan);
                }

                if (position < bufferCount)
                {
                    int remaining = bufferCount - position;
                    bufferSpan.Slice(position, remaining).CopyTo(rentBuffer);
                    bufferCount = remaining;
                }
                else
                {
                    bufferCount = 0;
                }

                if (bufferCount == rentBuffer.Length)
                {
                    byte[] newBuffer = ArrayPool<byte>.Shared.Rent(rentBuffer.Length * 2);
                    Array.Copy(rentBuffer, newBuffer, bufferCount);
                    ArrayPool<byte>.Shared.Return(rentBuffer);
                    rentBuffer = newBuffer;
                }
            }

            if (bufferCount > 0)
            {
                ProcessByteLine(rentBuffer.AsSpan(0, bufferCount));
            }

            FlushCurrentRecord();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentBuffer);
        }

        for (int i = 0; i < mediaNodes.Count; i++)
        {
            var m = mediaNodes[i];
            mediaLinks.TryGetValue(m.XrefId, out var linkedXrefs);
            IReadOnlyList<string> internedLinks;
            if (linkedXrefs != null && linkedXrefs.Count > 0)
            {
                var arr = new string[linkedXrefs.Count];
                for (int j = 0; j < linkedXrefs.Count; j++)
                {
                    arr[j] = pool.GetOrAdd(linkedXrefs[j])!;
                }
                internedLinks = arr;
            }
            else
            {
                internedLinks = Array.Empty<string>();
            }

            string? mimeType = m.Format?.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "png" => "image/png",
                "gif" => "image/gif",
                "pdf" => "application/pdf",
                _ => m.Format
            };

            result.Media.Add(new MediaReferenceRecord(m.XrefId, m.Title, m.FilePath, m.Format, mimeType, internedLinks));
        }

        return result;

        void ProcessByteLine(ReadOnlySpan<byte> rawLine)
        {
            while (!rawLine.IsEmpty && (rawLine[rawLine.Length - 1] == (byte)' ' || rawLine[rawLine.Length - 1] == (byte)'\r' || rawLine[rawLine.Length - 1] == (byte)'\t'))
            {
                rawLine = rawLine.Slice(0, rawLine.Length - 1);
            }
            if (rawLine.IsEmpty) return;

            if (!TryParseByteLineSpans(rawLine, out int level, out var xrefByteSpan, out var tagByteSpan, out var valueByteSpan))
            {
                return;
            }

            if (tagByteSpan.SequenceEqual("CONC"u8) || tagByteSpan.SequenceEqual("CONT"u8))
            {
                if (concTarget != ConcTarget.None)
                {
                    if (concBuilder == null)
                    {
                        string existing = concTarget switch
                        {
                            ConcTarget.PersonFirstName => personFirstName ?? string.Empty,
                            ConcTarget.PersonLastName => personLastName ?? string.Empty,
                            ConcTarget.PersonBirthPlace => personBirthPlace ?? string.Empty,
                            ConcTarget.PersonDeathPlace => personDeathPlace ?? string.Empty,
                            ConcTarget.FamMarriagePlace => famMarriagePlace ?? string.Empty,
                            ConcTarget.MediaTitle => mediaTitle ?? string.Empty,
                            _ => string.Empty
                        };
                        concBuilder = new StringBuilder(existing);
                    }
                    if (tagByteSpan.SequenceEqual("CONT"u8))
                    {
                        concBuilder.Append('\n');
                    }
                    if (!valueByteSpan.IsEmpty)
                    {
                        concBuilder.Append(Encoding.UTF8.GetString(valueByteSpan));
                    }
                }
                return;
            }

            ApplyConc();

            if (level == 0)
            {
                FlushCurrentRecord();

                if (tagByteSpan.SequenceEqual("INDI"u8))
                {
                    currentRecordType = RecordType.Person;
                    currentXref = pool.GetOrAdd(Encoding.UTF8.GetString(xrefByteSpan));
                }
                else if (tagByteSpan.SequenceEqual("FAM"u8))
                {
                    currentRecordType = RecordType.Family;
                    currentXref = pool.GetOrAdd(Encoding.UTF8.GetString(xrefByteSpan));
                }
                else if (tagByteSpan.SequenceEqual("OBJE"u8))
                {
                    currentRecordType = RecordType.Media;
                    currentXref = pool.GetOrAdd(Encoding.UTF8.GetString(xrefByteSpan));
                }
                else if (tagByteSpan.SequenceEqual("HEAD"u8))
                {
                    currentRecordType = RecordType.Header;
                }
                else if (!tagByteSpan.SequenceEqual("TRLR"u8))
                {
                    currentRecordType = RecordType.Unparsed;
                    currentXref = xrefByteSpan.IsEmpty ? null : pool.GetOrAdd(Encoding.UTF8.GetString(xrefByteSpan));
                    unparsedTag = pool.GetOrAdd(Encoding.UTF8.GetString(tagByteSpan));
                    unparsedValue = valueByteSpan.IsEmpty ? null : Encoding.UTF8.GetString(valueByteSpan);
                    unparsedRawLines = new List<string>();
                }
                return;
            }

            if (currentRecordType == RecordType.Header)
            {
                if (level == 2 && tagByteSpan.SequenceEqual("VERS"u8))
                {
                    if (valueByteSpan.StartsWith("7."u8))
                    {
                        result.SpecVersion = GedcomSpecVersion.Gedcom70;
                    }
                }
                return;
            }

            if (currentRecordType == RecordType.Unparsed)
            {
                unparsedRawLines?.Add(Encoding.UTF8.GetString(rawLine));
                return;
            }

            if (currentRecordType == RecordType.Person)
            {
                if (level == 1)
                {
                    if (tagByteSpan.SequenceEqual("NAME"u8))
                    {
                        ParseNameByte(valueByteSpan, pool, out personFirstName, out personLastName);
                        concBuilder = null;
                        concTarget = personLastName != null ? ConcTarget.PersonLastName : ConcTarget.PersonFirstName;
                        activeSubTag = SubTag.None;
                    }
                    else if (tagByteSpan.SequenceEqual("SEX"u8))
                    {
                        personSex = valueByteSpan.SequenceEqual("M"u8) ? PersonSex.Male :
                                    valueByteSpan.SequenceEqual("F"u8) ? PersonSex.Female : PersonSex.Unknown;
                        activeSubTag = SubTag.None;
                    }
                    else if (tagByteSpan.SequenceEqual("BIRT"u8))
                    {
                        activeSubTag = SubTag.Birth;
                        result.Events.Add(new EventRecord(currentXref!, FamTreeEventType.Birth, null, null));
                    }
                    else if (tagByteSpan.SequenceEqual("DEAT"u8))
                    {
                        activeSubTag = SubTag.Death;
                        result.Events.Add(new EventRecord(currentXref!, FamTreeEventType.Death, null, null));
                    }
                    else if (tagByteSpan.SequenceEqual("OBJE"u8))
                    {
                        AddMediaLinkByte(mediaLinks, pool, valueByteSpan, currentXref!);
                        activeSubTag = SubTag.None;
                    }
                    else if (TryGetEventTypeByte(tagByteSpan, out var evType))
                    {
                        activeSubTag = SubTag.OtherEvent;
                        result.Events.Add(new EventRecord(currentXref!, evType, null, null));
                    }
                    else
                    {
                        activeSubTag = SubTag.None;
                    }
                }
                else if (level == 2)
                {
                    if (tagByteSpan.SequenceEqual("DATE"u8))
                    {
                        var dateVal = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                        if (activeSubTag == SubTag.Birth) personBirthDate = dateVal;
                        else if (activeSubTag == SubTag.Death) personDeathDate = dateVal;

                        if (result.Events.Count > 0)
                        {
                            var lastEv = result.Events[result.Events.Count - 1];
                            if (lastEv.PersonXrefId == currentXref)
                            {
                                result.Events[result.Events.Count - 1] = lastEv with { Date = dateVal };
                            }
                        }
                    }
                    else if (tagByteSpan.SequenceEqual("PLAC"u8))
                    {
                        var placeVal = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                        if (activeSubTag == SubTag.Birth)
                        {
                            personBirthPlace = placeVal;
                            concBuilder = null;
                            concTarget = ConcTarget.PersonBirthPlace;
                        }
                        else if (activeSubTag == SubTag.Death)
                        {
                            personDeathPlace = placeVal;
                            concBuilder = null;
                            concTarget = ConcTarget.PersonDeathPlace;
                        }

                        if (result.Events.Count > 0)
                        {
                            var lastEv = result.Events[result.Events.Count - 1];
                            if (lastEv.PersonXrefId == currentXref)
                            {
                                result.Events[result.Events.Count - 1] = lastEv with { Place = placeVal };
                            }
                        }
                    }
                }
            }
            else if (currentRecordType == RecordType.Family)
            {
                if (level == 1)
                {
                    if (tagByteSpan.SequenceEqual("HUSB"u8))
                    {
                        famHusbandXref = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                        activeSubTag = SubTag.None;
                    }
                    else if (tagByteSpan.SequenceEqual("WIFE"u8))
                    {
                        famWifeXref = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                        activeSubTag = SubTag.None;
                    }
                    else if (tagByteSpan.SequenceEqual("CHIL"u8))
                    {
                        famChildXrefs ??= new List<string>();
                        famChildXrefs.Add(pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan))!);
                        activeSubTag = SubTag.None;
                    }
                    else if (tagByteSpan.SequenceEqual("MARR"u8))
                    {
                        activeSubTag = SubTag.Marriage;
                    }
                    else
                    {
                        activeSubTag = SubTag.None;
                    }
                }
                else if (level == 2)
                {
                    if (tagByteSpan.SequenceEqual("DATE"u8))
                    {
                        if (activeSubTag == SubTag.Marriage) famMarriageDate = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                    }
                    else if (tagByteSpan.SequenceEqual("PLAC"u8))
                    {
                        if (activeSubTag == SubTag.Marriage)
                        {
                            famMarriagePlace = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                            concBuilder = null;
                            concTarget = ConcTarget.FamMarriagePlace;
                        }
                    }
                }
            }
            else if (currentRecordType == RecordType.Media)
            {
                if (level == 1)
                {
                    if (tagByteSpan.SequenceEqual("TITL"u8))
                    {
                        mediaTitle = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                        concBuilder = null;
                        concTarget = ConcTarget.MediaTitle;
                    }
                    else if (tagByteSpan.SequenceEqual("FILE"u8)) mediaFilePath = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                    else if (tagByteSpan.SequenceEqual("FORM"u8)) mediaFormat = pool.GetOrAdd(Encoding.UTF8.GetString(valueByteSpan));
                }
            }
        }
    }

    private static bool TryParseByteLineSpans(
        ReadOnlySpan<byte> span,
        out int level,
        out ReadOnlySpan<byte> xrefSpan,
        out ReadOnlySpan<byte> tagSpan,
        out ReadOnlySpan<byte> valueSpan)
    {
        level = 0;
        xrefSpan = default;
        tagSpan = default;
        valueSpan = default;

        int space1 = span.IndexOf((byte)' ');
        if (space1 < 0) return false;

        if (space1 == 1 && (uint)(span[0] - (byte)'0') <= 9)
        {
            level = span[0] - (byte)'0';
        }
        else
        {
            return false;
        }

        var rest = span.Slice(space1 + 1);
        if (rest.IsEmpty) return false;

        if (rest[0] == (byte)'@')
        {
            int nextAt = rest.Slice(1).IndexOf((byte)'@');
            if (nextAt < 0) return false;
            int xrefLen = nextAt + 2;
            if (rest.Length <= xrefLen || rest[xrefLen] != (byte)' ') return false;

            xrefSpan = rest.Slice(0, xrefLen);
            rest = rest.Slice(xrefLen + 1);
        }

        int space2 = rest.IndexOf((byte)' ');
        if (space2 < 0)
        {
            tagSpan = rest;
        }
        else
        {
            tagSpan = rest.Slice(0, space2);
            valueSpan = rest.Slice(space2 + 1);
        }

        return true;
    }

    private static ReadOnlySpan<byte> TrimByteSpan(ReadOnlySpan<byte> span)
    {
        while (!span.IsEmpty && (span[0] == (byte)' ' || span[0] == (byte)'\t'))
        {
            span = span.Slice(1);
        }
        while (!span.IsEmpty && (span[span.Length - 1] == (byte)' ' || span[span.Length - 1] == (byte)'\t'))
        {
            span = span.Slice(0, span.Length - 1);
        }
        return span;
    }

    private static void ParseNameByte(ReadOnlySpan<byte> nameValue, GedcomStringPool pool, out string? firstName, out string? lastName)
    {
        firstName = null;
        lastName = null;
        if (nameValue.IsEmpty) return;

        int slash1 = nameValue.IndexOf((byte)'/');
        if (slash1 < 0)
        {
            firstName = pool.GetOrAdd(Encoding.UTF8.GetString(nameValue).Trim());
            return;
        }

        var given = TrimByteSpan(nameValue.Slice(0, slash1));
        if (!given.IsEmpty) firstName = pool.GetOrAdd(Encoding.UTF8.GetString(given));

        int slash2 = nameValue.Slice(slash1 + 1).IndexOf((byte)'/');
        ReadOnlySpan<byte> surname;
        if (slash2 < 0) surname = TrimByteSpan(nameValue.Slice(slash1 + 1));
        else surname = TrimByteSpan(nameValue.Slice(slash1 + 1, slash2));

        if (!surname.IsEmpty) lastName = pool.GetOrAdd(Encoding.UTF8.GetString(surname));
    }

    private static void AddMediaLinkByte(Dictionary<string, List<string>> mediaLinks, GedcomStringPool pool, ReadOnlySpan<byte> mediaValue, string entityXref)
    {
        if (mediaValue.IsEmpty) return;
        var mediaXref = pool.GetOrAdd(Encoding.UTF8.GetString(mediaValue));
        if (mediaXref == null) return;
        if (!mediaLinks.TryGetValue(mediaXref, out var list))
        {
            list = new List<string>();
            mediaLinks[mediaXref] = list;
        }
        list.Add(entityXref);
    }

    private static bool TryGetEventTypeByte(ReadOnlySpan<byte> tag, out FamTreeEventType eventType)
    {
        if (tag.SequenceEqual("BIRT"u8)) { eventType = FamTreeEventType.Birth; return true; }
        if (tag.SequenceEqual("DEAT"u8)) { eventType = FamTreeEventType.Death; return true; }
        if (tag.SequenceEqual("CENS"u8)) { eventType = FamTreeEventType.Census; return true; }
        if (tag.SequenceEqual("IMMI"u8)) { eventType = FamTreeEventType.Immigration; return true; }
        if (tag.SequenceEqual("EMIG"u8)) { eventType = FamTreeEventType.Emigration; return true; }
        if (tag.SequenceEqual("RESI"u8)) { eventType = FamTreeEventType.Residence; return true; }
        if (tag.SequenceEqual("CHR"u8)) { eventType = FamTreeEventType.Christening; return true; }
        if (tag.SequenceEqual("BAPM"u8) || tag.SequenceEqual("BAP"u8)) { eventType = FamTreeEventType.Baptism; return true; }
        if (tag.SequenceEqual("BURI"u8)) { eventType = FamTreeEventType.Burial; return true; }

        eventType = FamTreeEventType.Birth;
        return false;
    }

    private enum RecordType { None, Header, Person, Family, Media, Unparsed, Other }
    private enum SubTag { None, Birth, Death, Marriage, OtherEvent }
    private enum ConcTarget { None, PersonFirstName, PersonLastName, PersonBirthPlace, PersonDeathPlace, FamMarriagePlace, MediaTitle }
}
