using System.Collections.Generic;

namespace Gedcom.Vector;

/// <summary>
/// Represents a media reference (OBJE record) parsed from a GEDCOM file.
/// </summary>
/// <param name="XrefId">The unique cross-reference identifier (e.g., "@M1@").</param>
/// <param name="Title">The title of the media, if declared.</param>
/// <param name="FilePath">The local file path or URI of the media file, if declared.</param>
/// <param name="Format">The raw format tag (e.g., "jpg", "pdf"), if declared.</param>
/// <param name="MimeType">The resolved MIME type (e.g., "image/jpeg", "application/pdf").</param>
/// <param name="LinkedXrefIds">The identifiers of entities (persons/families) linking to this media.</param>
public record MediaReferenceRecord(
    string XrefId,
    string? Title,
    string? FilePath,
    string? Format,
    string? MimeType,
    IReadOnlyList<string> LinkedXrefIds);
