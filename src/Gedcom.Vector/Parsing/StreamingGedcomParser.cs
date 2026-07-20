using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// High-performance streaming parser that parses GEDCOM input directly from streams
/// into structured records using zero-allocation span parsing, SIMD line splitting, and string pooling.
/// </summary>
internal static class StreamingGedcomParser
{
    private static readonly SearchValues<char> LineBreaks = SearchValues.Create("\r\n");

    /// <summary>
    /// Parses a GEDCOM stream directly into a <see cref="GedcomParseResult"/>.
    /// </summary>
    public static GedcomParseResult Parse(Stream stream, GedcomEncodingResult encodingResult, ILogger? logger = null)
    {
        var result = new GedcomParseResult();
        var pool = new GedcomStringPool(1024);
        var mediaLinks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var mediaNodes = new List<(string XrefId, string? Title, string? FilePath, string? Format)>();

        // State variables for level-0 entity building
        string? currentXref = null;
        RecordType currentRecordType = RecordType.None;

        // Person state
        string? personFirstName = null;
        string? personLastName = null;
        PersonSex personSex = PersonSex.Unknown;
        string? personBirthDate = null;
        string? personBirthPlace = null;
        string? personDeathDate = null;
        string? personDeathPlace = null;
        SubTag activeSubTag = SubTag.None;

        // Family state
        string? famHusbandXref = null;
        string? famWifeXref = null;
        List<string>? famChildXrefs = null;
        string? famMarriageDate = null;
        string? famMarriagePlace = null;

        // Media state
        string? mediaTitle = null;
        string? mediaFilePath = null;
        string? mediaFormat = null;

        StringBuilder? concBuilder = null;
        ConcTarget concTarget = ConcTarget.None;

        void ApplyConc()
        {
            if (concBuilder == null || concTarget == ConcTarget.None) return;

            string strVal = concBuilder.ToString();
            switch (concTarget)
            {
                case ConcTarget.PersonFirstName:
                    personFirstName = pool.GetOrAdd(strVal);
                    break;
                case ConcTarget.PersonLastName:
                    personLastName = pool.GetOrAdd(strVal);
                    break;
                case ConcTarget.PersonBirthPlace:
                    personBirthPlace = pool.GetOrAdd(strVal);
                    break;
                case ConcTarget.PersonDeathPlace:
                    personDeathPlace = pool.GetOrAdd(strVal);
                    break;
                case ConcTarget.FamMarriagePlace:
                    famMarriagePlace = pool.GetOrAdd(strVal);
                    break;
                case ConcTarget.MediaTitle:
                    mediaTitle = pool.GetOrAdd(strVal);
                    break;
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
                        currentXref,
                        personFirstName,
                        personLastName,
                        personSex,
                        personBirthDate,
                        personBirthPlace,
                        personDeathDate,
                        personDeathPlace));
                    break;

                case RecordType.Family when currentXref != null:
                    result.Families.Add(new FamilyRecord(
                        currentXref,
                        famHusbandXref,
                        famWifeXref,
                        famChildXrefs ?? (IReadOnlyList<string>)Array.Empty<string>(),
                        famMarriageDate,
                        famMarriagePlace));
                    break;

                case RecordType.Media when currentXref != null:
                    mediaNodes.Add((currentXref, mediaTitle, mediaFilePath, mediaFormat));
                    break;
            }

            // Reset state
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
        }

        // Buffer reading loop using rented ArrayPool char buffer
        int bufferSize = 65536;
        char[] rentBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
        try
        {
            using var reader = CreateReader(stream, encodingResult);
            int bufferCount = 0;
            int read;

            while ((read = reader.Read(rentBuffer, bufferCount, rentBuffer.Length - bufferCount)) > 0)
            {
                bufferCount += read;
                var bufferSpan = rentBuffer.AsSpan(0, bufferCount);
                int position = 0;

                while (position < bufferSpan.Length)
                {
                    var slice = bufferSpan.Slice(position);
                    int breakIndex = slice.IndexOfAny(LineBreaks);
                    if (breakIndex < 0)
                    {
                        break;
                    }

                    var lineSpan = slice.Slice(0, breakIndex);
                    position += breakIndex + 1;

                    // Consume matching \n if previous was \r
                    if (slice[breakIndex] == '\r' && position < bufferSpan.Length && bufferSpan[position] == '\n')
                    {
                        position++;
                    }

                    if (encodingResult.IsAnsel)
                    {
                        var decoded = AnselDecoder.Decode(lineSpan);
                        ProcessLine(decoded.AsSpan());
                    }
                    else
                    {
                        ProcessLine(lineSpan);
                    }
                }

                // Move remaining partial line to start of buffer
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

                // If buffer is full and no newline was found, grow buffer
                if (bufferCount == rentBuffer.Length)
                {
                    char[] newBuffer = ArrayPool<char>.Shared.Rent(rentBuffer.Length * 2);
                    Array.Copy(rentBuffer, newBuffer, bufferCount);
                    ArrayPool<char>.Shared.Return(rentBuffer);
                    rentBuffer = newBuffer;
                }
            }

            if (bufferCount > 0)
            {
                var lineSpan = rentBuffer.AsSpan(0, bufferCount);
                if (encodingResult.IsAnsel)
                {
                    var decoded = AnselDecoder.Decode(lineSpan);
                    ProcessLine(decoded.AsSpan());
                }
                else
                {
                    ProcessLine(lineSpan);
                }
            }

            FlushCurrentRecord();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rentBuffer);
        }

        // Map media references
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

        void ProcessLine(ReadOnlySpan<char> rawLine)
        {
            var line = rawLine.TrimEnd();
            if (line.IsEmpty) return;

            if (!TryParseLineSpans(line, out int level, out var xrefSpan, out var tagSpan, out var valueSpan))
            {
                return;
            }

            if (tagSpan.SequenceEqual("CONC") || tagSpan.SequenceEqual("CONT"))
            {
                if (concBuilder != null)
                {
                    if (tagSpan.SequenceEqual("CONT"))
                    {
                        concBuilder.Append('\n');
                    }
                    if (!valueSpan.IsEmpty)
                    {
                        concBuilder.Append(valueSpan);
                    }
                }
                return;
            }

            ApplyConc();

            if (level == 0)
            {
                FlushCurrentRecord();

                if (tagSpan.SequenceEqual("INDI"))
                {
                    currentRecordType = RecordType.Person;
                    currentXref = pool.GetOrAdd(xrefSpan);
                }
                else if (tagSpan.SequenceEqual("FAM"))
                {
                    currentRecordType = RecordType.Family;
                    currentXref = pool.GetOrAdd(xrefSpan);
                }
                else if (tagSpan.SequenceEqual("OBJE"))
                {
                    currentRecordType = RecordType.Media;
                    currentXref = pool.GetOrAdd(xrefSpan);
                }
                return;
            }

            if (currentRecordType == RecordType.Person)
            {
                if (level == 1)
                {
                    if (tagSpan.SequenceEqual("NAME"))
                    {
                        ParseName(valueSpan, pool, out personFirstName, out personLastName);
                        concBuilder = new StringBuilder(personLastName ?? personFirstName ?? string.Empty);
                        concTarget = personLastName != null ? ConcTarget.PersonLastName : ConcTarget.PersonFirstName;
                        activeSubTag = SubTag.None;
                    }
                    else if (tagSpan.SequenceEqual("SEX"))
                    {
                        personSex = valueSpan.Equals("M".AsSpan(), StringComparison.Ordinal) ? PersonSex.Male :
                                    valueSpan.Equals("F".AsSpan(), StringComparison.Ordinal) ? PersonSex.Female : PersonSex.Unknown;
                        activeSubTag = SubTag.None;
                    }
                    else if (tagSpan.SequenceEqual("BIRT"))
                    {
                        activeSubTag = SubTag.Birth;
                    }
                    else if (tagSpan.SequenceEqual("DEAT"))
                    {
                        activeSubTag = SubTag.Death;
                    }
                    else if (tagSpan.SequenceEqual("OBJE"))
                    {
                        AddMediaLink(mediaLinks, pool, valueSpan, currentXref!);
                        activeSubTag = SubTag.None;
                    }
                    else if (TryGetEventType(tagSpan, out var evType))
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
                    if (tagSpan.SequenceEqual("DATE"))
                    {
                        var dateVal = pool.GetOrAdd(valueSpan);
                        if (activeSubTag == SubTag.Birth) personBirthDate = dateVal;
                        else if (activeSubTag == SubTag.Death) personDeathDate = dateVal;
                        else if (activeSubTag == SubTag.OtherEvent && result.Events.Count > 0)
                        {
                            var lastEv = result.Events[result.Events.Count - 1];
                            result.Events[result.Events.Count - 1] = lastEv with { Date = dateVal };
                        }
                    }
                    else if (tagSpan.SequenceEqual("PLAC"))
                    {
                        var placeVal = pool.GetOrAdd(valueSpan);
                        if (activeSubTag == SubTag.Birth)
                        {
                            personBirthPlace = placeVal;
                            concBuilder = new StringBuilder(placeVal);
                            concTarget = ConcTarget.PersonBirthPlace;
                        }
                        else if (activeSubTag == SubTag.Death)
                        {
                            personDeathPlace = placeVal;
                            concBuilder = new StringBuilder(placeVal);
                            concTarget = ConcTarget.PersonDeathPlace;
                        }
                        else if (activeSubTag == SubTag.OtherEvent && result.Events.Count > 0)
                        {
                            var lastEv = result.Events[result.Events.Count - 1];
                            result.Events[result.Events.Count - 1] = lastEv with { Place = placeVal };
                        }
                    }
                }
            }
            else if (currentRecordType == RecordType.Family)
            {
                if (level == 1)
                {
                    if (tagSpan.SequenceEqual("HUSB"))
                    {
                        famHusbandXref = pool.GetOrAdd(valueSpan);
                        activeSubTag = SubTag.None;
                    }
                    else if (tagSpan.SequenceEqual("WIFE"))
                    {
                        famWifeXref = pool.GetOrAdd(valueSpan);
                        activeSubTag = SubTag.None;
                    }
                    else if (tagSpan.SequenceEqual("CHIL"))
                    {
                        famChildXrefs ??= new List<string>();
                        famChildXrefs.Add(pool.GetOrAdd(valueSpan));
                        activeSubTag = SubTag.None;
                    }
                    else if (tagSpan.SequenceEqual("MARR"))
                    {
                        activeSubTag = SubTag.Marriage;
                    }
                    else if (tagSpan.SequenceEqual("OBJE"))
                    {
                        AddMediaLink(mediaLinks, pool, valueSpan, currentXref!);
                        activeSubTag = SubTag.None;
                    }
                }
                else if (level == 2)
                {
                    if (tagSpan.SequenceEqual("DATE") && activeSubTag == SubTag.Marriage)
                    {
                        famMarriageDate = pool.GetOrAdd(valueSpan);
                    }
                    else if (tagSpan.SequenceEqual("PLAC") && activeSubTag == SubTag.Marriage)
                    {
                        famMarriagePlace = pool.GetOrAdd(valueSpan);
                        concBuilder = new StringBuilder(famMarriagePlace);
                        concTarget = ConcTarget.FamMarriagePlace;
                    }
                }
            }
            else if (currentRecordType == RecordType.Media)
            {
                if (level == 1)
                {
                    if (tagSpan.SequenceEqual("TITL"))
                    {
                        mediaTitle = pool.GetOrAdd(valueSpan);
                        concBuilder = new StringBuilder(mediaTitle ?? string.Empty);
                        concTarget = ConcTarget.MediaTitle;
                    }
                    else if (tagSpan.SequenceEqual("FILE")) mediaFilePath = pool.GetOrAdd(valueSpan);
                    else if (tagSpan.SequenceEqual("FORM")) mediaFormat = pool.GetOrAdd(valueSpan);
                }
            }
        }
    }

    private static bool TryParseLineSpans(
        ReadOnlySpan<char> span,
        out int level,
        out ReadOnlySpan<char> xrefSpan,
        out ReadOnlySpan<char> tagSpan,
        out ReadOnlySpan<char> valueSpan)
    {
        level = 0;
        xrefSpan = default;
        tagSpan = default;
        valueSpan = default;

        int space1 = span.IndexOf(' ');
        if (space1 < 0) return false;

        if (!int.TryParse(span.Slice(0, space1), out level)) return false;

        var rest = span.Slice(space1 + 1);
        if (rest.IsEmpty) return false;

        if (rest[0] == '@')
        {
            int nextAt = rest.Slice(1).IndexOf('@');
            if (nextAt < 0) return false;
            int xrefLen = nextAt + 2;
            if (rest.Length <= xrefLen || rest[xrefLen] != ' ') return false;

            xrefSpan = rest.Slice(0, xrefLen);
            rest = rest.Slice(xrefLen + 1);
        }

        int space2 = rest.IndexOf(' ');
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

    private static void ParseName(ReadOnlySpan<char> nameValue, GedcomStringPool pool, out string? firstName, out string? lastName)
    {
        firstName = null;
        lastName = null;
        if (nameValue.IsEmpty) return;

        int slash1 = nameValue.IndexOf('/');
        if (slash1 < 0)
        {
            firstName = pool.GetOrAdd(nameValue.Trim());
            return;
        }

        var given = nameValue.Slice(0, slash1).Trim();
        if (!given.IsEmpty) firstName = pool.GetOrAdd(given);

        int slash2 = nameValue.Slice(slash1 + 1).IndexOf('/');
        int surnameLen = slash2 >= 0 ? slash2 : nameValue.Length - (slash1 + 1);
        var surname = nameValue.Slice(slash1 + 1, surnameLen).Trim();
        if (!surname.IsEmpty) lastName = pool.GetOrAdd(surname);
    }

    private static void AddMediaLink(Dictionary<string, List<string>> mediaLinks, GedcomStringPool pool, ReadOnlySpan<char> mediaValue, string entityXref)
    {
        if (mediaValue.IsEmpty) return;
        var mediaXref = pool.GetOrAdd(mediaValue);
        if (!mediaLinks.TryGetValue(mediaXref, out var list))
        {
            list = new List<string>();
            mediaLinks[mediaXref] = list;
        }
        list.Add(entityXref);
    }

    private static bool TryGetEventType(ReadOnlySpan<char> tag, out FamTreeEventType eventType)
    {
        if (tag.SequenceEqual("BIRT")) { eventType = FamTreeEventType.Birth; return true; }
        if (tag.SequenceEqual("DEAT")) { eventType = FamTreeEventType.Death; return true; }
        if (tag.SequenceEqual("CENS")) { eventType = FamTreeEventType.Census; return true; }
        if (tag.SequenceEqual("IMMI")) { eventType = FamTreeEventType.Immigration; return true; }
        if (tag.SequenceEqual("EMIG")) { eventType = FamTreeEventType.Emigration; return true; }
        if (tag.SequenceEqual("RESI")) { eventType = FamTreeEventType.Residence; return true; }
        if (tag.SequenceEqual("CHR") || tag.SequenceEqual("BAP")) { eventType = FamTreeEventType.Christening; return true; }
        if (tag.SequenceEqual("BURI")) { eventType = FamTreeEventType.Burial; return true; }

        eventType = FamTreeEventType.Birth;
        return false;
    }

    private static TextReader CreateReader(Stream stream, GedcomEncodingResult encodingResult)
    {
        if (encodingResult.IsAnsel)
        {
            return new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);
        }
        return new StreamReader(stream, encodingResult.Encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);
    }

    private enum RecordType { None, Person, Family, Media, Other }
    private enum SubTag { None, Birth, Death, Marriage, OtherEvent }
    private enum ConcTarget { None, PersonFirstName, PersonLastName, PersonBirthPlace, PersonDeathPlace, FamMarriagePlace, MediaTitle }
}
