using System.Collections.Generic;

namespace Gedcom.Vector;

/// <summary>
/// Represents an unparsed or custom GEDCOM level-0 record block (e.g. SUBM, SOUR, REPO, _CUSTOM)
/// preserved for 100% lossless round-tripping.
/// </summary>
/// <param name="XrefId">The optional cross-reference ID (e.g. "@SUBM1@").</param>
/// <param name="Tag">The record tag (e.g. "SUBM", "SOUR").</param>
/// <param name="Value">The record value string, if declared.</param>
/// <param name="RawLines">The list of raw child lines preserved verbatim under this record.</param>
public record UnparsedRecord(
    string? XrefId,
    string Tag,
    string? Value,
    IReadOnlyList<string>? RawLines = null);
