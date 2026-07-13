using Gedcom.Vector.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gedcom.Vector;

/// <inheritdoc />
public class GedcomImportAdapter : IGedcomImportAdapter
{
    private readonly ILogger<GedcomImportAdapter> _logger;
    private readonly GedcomImportOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GedcomImportAdapter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The configuration options.</param>
    public GedcomImportAdapter(ILogger<GedcomImportAdapter> logger, IOptions<GedcomImportOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public GedcomParseResult Parse(Stream gedcomFile)
    {
        if (gedcomFile == null)
        {
            throw new ArgumentNullException(nameof(gedcomFile));
        }

        if (!gedcomFile.CanSeek)
        {
            throw new ArgumentException("The provided stream must be seekable. For network or compressed streams, please buffer the content into a MemoryStream first.", nameof(gedcomFile));
        }

        var result = new GedcomParseResult();

        if (gedcomFile.Length > _options.MaxFileSizeBytes)
        {
            result.Errors.Add(
                $"GEDCOM file is {gedcomFile.Length} bytes, exceeding the maximum supported size of {_options.MaxFileSizeBytes} bytes.");
            return result;
        }

        var encodingResult = GedcomEncodingDetector.Detect(gedcomFile);
        var mediaLinks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var mediaNodes = new List<Parsing.GedcomNode>();

        var lines = GedcomLexer.Tokenize(ReadLines(gedcomFile, encodingResult));
        var records = GedcomTreeBuilder.Build(lines);

        var xrefCache = new Dictionary<string, string>(StringComparer.Ordinal);
        string InternXref(string? x)
        {
            if (x is null) return null!;
            if (xrefCache.TryGetValue(x, out var existing)) return existing;
            xrefCache[x] = x;
            return x;
        }

        foreach (var record in records)
        {
            if (record.Tag == "INDI" && record.XrefId is not null)
            {
                var person = PersonMapper.MapPerson(record);
                var internedXref = InternXref(person.XrefId);
                var internedPerson = person with { XrefId = internedXref };
                result.Persons.Add(internedPerson);

                int startEventCount = result.Events.Count;
                EventMapper.MapEvents(record, result.Events, _logger);
                for (int i = startEventCount; i < result.Events.Count; i++)
                {
                    result.Events[i] = result.Events[i] with { PersonXrefId = internedXref };
                }

                ExtractMediaLinks(record, mediaLinks, xrefCache);
            }
            else if (record.Tag == "FAM" && record.XrefId is not null)
            {
                var family = FamilyMapper.MapFamily(record);
                var internedXref = InternXref(family.XrefId);
                
                var husbandXref = family.HusbandXref is not null ? InternXref(family.HusbandXref) : null;
                var wifeXref = family.WifeXref is not null ? InternXref(family.WifeXref) : null;
                var children = family.ChildXrefs;
                if (children.Count > 0)
                {
                    var internedChildren = new string[children.Count];
                    for (int i = 0; i < children.Count; i++)
                    {
                        internedChildren[i] = InternXref(children[i]);
                    }
                    children = internedChildren;
                }

                var internedFamily = family with { 
                    XrefId = internedXref, 
                    HusbandXref = husbandXref, 
                    WifeXref = wifeXref, 
                    ChildXrefs = children 
                };
                result.Families.Add(internedFamily);

                ExtractMediaLinks(record, mediaLinks, xrefCache);
            }
            else if (record.Tag == "OBJE" && record.XrefId is not null)
            {
                mediaNodes.Add(record);
            }
        }

        if (result.Persons.Count == 0 && result.Families.Count == 0 && mediaNodes.Count == 0)
        {
            result.Errors.Add("No individuals or families were found. The input may not be valid GEDCOM.");
            return result;
        }

        foreach (var media in mediaNodes)
        {
            var mediaXref = InternXref(media.XrefId);
            mediaLinks.TryGetValue(mediaXref, out var linkedXrefs);
            
            IReadOnlyList<string> internedLinks;
            if (linkedXrefs != null && linkedXrefs.Count > 0)
            {
                var arr = new string[linkedXrefs.Count];
                for (int i = 0; i < linkedXrefs.Count; i++)
                {
                    arr[i] = InternXref(linkedXrefs[i]);
                }
                internedLinks = arr;
            }
            else
            {
                internedLinks = Array.Empty<string>();
            }

            var mappedMedia = MediaMapper.MapMedia(media, internedLinks);
            var internedMedia = mappedMedia with { XrefId = mediaXref };
            result.Media.Add(internedMedia);
        }

        _logger.LogInformation(
            "Parsed GEDCOM input: {PersonCount} persons, {FamilyCount} families, {EventCount} events, {MediaCount} media",
            result.Persons.Count, result.Families.Count, result.Events.Count, result.Media.Count);

        return result;
    }

    private static void ExtractMediaLinks(
        Parsing.GedcomNode entity, 
        Dictionary<string, List<string>> mediaLinks,
        Dictionary<string, string> xrefCache)
    {
        string Intern(string x)
        {
            if (xrefCache.TryGetValue(x, out var existing)) return existing;
            xrefCache[x] = x;
            return x;
        }

        var children = entity.Children;
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.Tag == "OBJE" && child.Value is not null)
            {
                var val = Intern(child.Value);
                if (!mediaLinks.TryGetValue(val, out var list))
                {
                    list = new List<string>();
                    mediaLinks[val] = list;
                }
                list.Add(Intern(entity.XrefId!));
            }
        }
    }

    private static IEnumerable<string> ReadLines(Stream stream, GedcomEncodingResult encodingResult)
    {
        if (encodingResult.IsAnsel)
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.Latin1, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);
            string? rawLine;
            while ((rawLine = reader.ReadLine()) is not null)
            {
                yield return AnselDecoder.Decode(rawLine);
            }
        }
        else
        {
            using var reader = new StreamReader(stream, encodingResult.Encoding ?? System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);
            string? rawLine;
            while ((rawLine = reader.ReadLine()) is not null)
            {
                yield return rawLine;
            }
        }
    }
}
