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

        if (gedcomFile.Length > _options.MaxFileSizeBytes)
        {
            var errResult = new GedcomParseResult();
            errResult.Errors.Add(
                $"GEDCOM file is {gedcomFile.Length} bytes, exceeding the maximum supported size of {_options.MaxFileSizeBytes} bytes.");
            return errResult;
        }

        var encodingResult = GedcomEncodingDetector.Detect(gedcomFile);
        var result = StreamingGedcomParser.Parse(gedcomFile, encodingResult, _logger);

        if (result.Persons.Count == 0 && result.Families.Count == 0 && result.Media.Count == 0)
        {
            result.Errors.Add("No individuals or families were found. The input may not be valid GEDCOM.");
            return result;
        }

        _logger.LogInformation(
            "Parsed GEDCOM input: {PersonCount} persons, {FamilyCount} families, {EventCount} events, {MediaCount} media",
            result.Persons.Count, result.Families.Count, result.Events.Count, result.Media.Count);

        return result;
    }
}
