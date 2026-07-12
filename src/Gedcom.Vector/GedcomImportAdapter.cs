using Gedcom.Vector.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gedcom.Vector;

public class GedcomImportAdapter : IGedcomImportAdapter
{
    private readonly ILogger<GedcomImportAdapter> _logger;
    private readonly GedcomImportOptions _options;

    public GedcomImportAdapter(ILogger<GedcomImportAdapter> logger, IOptions<GedcomImportOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

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

        foreach (var record in records)
        {
            if (record.Tag == "INDI" && record.XrefId is not null)
            {
                result.Persons.Add(PersonMapper.MapPerson(record));
                result.Events.AddRange(EventMapper.MapEvents(record, _logger));
                ExtractMediaLinks(record, mediaLinks);
            }
            else if (record.Tag == "FAM" && record.XrefId is not null)
            {
                result.Families.Add(FamilyMapper.MapFamily(record));
                ExtractMediaLinks(record, mediaLinks);
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
            mediaLinks.TryGetValue(media.XrefId!, out var linkedXrefs);
            result.Media.Add(MediaMapper.MapMedia(media, linkedXrefs ?? (IReadOnlyList<string>)Array.Empty<string>()));
        }

        _logger.LogInformation(
            "Parsed GEDCOM input: {PersonCount} persons, {FamilyCount} families, {EventCount} events, {MediaCount} media",
            result.Persons.Count, result.Families.Count, result.Events.Count, result.Media.Count);

        return result;
    }

    private static void ExtractMediaLinks(Parsing.GedcomNode entity, Dictionary<string, List<string>> mediaLinks)
    {
        foreach (var obje in entity.ChildrenWithTag("OBJE"))
        {
            if (obje.Value is not null)
            {
                if (!mediaLinks.TryGetValue(obje.Value, out var list))
                {
                    list = new List<string>();
                    mediaLinks[obje.Value] = list;
                }
                list.Add(entity.XrefId!);
            }
        }
    }

    private static IEnumerable<string> ReadLines(Stream stream, GedcomEncodingResult encodingResult)
    {
        if (encodingResult.IsAnsel)
        {
            // Latin1 preserves exactly 1 byte per char without any decoding assumptions,
            // allowing us to read lines while retaining the exact original byte sequence
            // for the ANSEL decoder.
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
