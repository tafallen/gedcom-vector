using System.Collections.Generic;

namespace Gedcom.Vector;

/// <summary>
/// Represents the structured result of parsing a GEDCOM file.
/// </summary>
public class GedcomParseResult
{
    /// <summary>
    /// Gets or sets the GEDCOM specification version detected during parsing.
    /// </summary>
    public GedcomSpecVersion SpecVersion { get; set; } = GedcomSpecVersion.Gedcom551;

    /// <summary>
    /// Gets the list of parsed individuals (INDI records).
    /// </summary>
    public List<PersonRecord> Persons { get; } = new();

    /// <summary>
    /// Gets the list of parsed family relationships (FAM records).
    /// </summary>
    public List<FamilyRecord> Families { get; } = new();

    /// <summary>
    /// Gets the list of parsed individual events (e.g. birth, death, census).
    /// </summary>
    public List<EventRecord> Events { get; } = new();

    /// <summary>
    /// Gets the list of parsed media references (OBJE records).
    /// </summary>
    public List<MediaReferenceRecord> Media { get; } = new();

    /// <summary>
    /// Gets the list of unparsed or custom level-0 records preserved for lossless round-tripping.
    /// </summary>
    public List<UnparsedRecord> UnparsedRecords { get; } = new();

    /// <summary>
    /// Gets the list of warning or error messages encountered during parsing.
    /// </summary>
    public List<string> Errors { get; } = new();
}
