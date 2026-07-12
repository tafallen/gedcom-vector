namespace Gedcom.Vector;

public record MediaReferenceRecord(
    string XrefId,
    string? Title,
    string? FilePath,
    string? Format,
    string? MimeType,
    IReadOnlyList<string> LinkedXrefIds);
