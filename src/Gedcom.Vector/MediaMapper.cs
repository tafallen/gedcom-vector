namespace Gedcom.Vector;

internal static class MediaMapper
{
    private static readonly Dictionary<string, string> MimeTypesByFormat = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["pdf"] = "application/pdf",
    };

    public static MediaReferenceRecord MapMedia(
        Parsing.GedcomNode media, IReadOnlyList<string> linkedXrefs)
    {
        var format = media.Child("FORM")?.Value;

        return new MediaReferenceRecord(
            media.XrefId!,
            NullIfEmpty(media.Child("TITL")?.Value),
            media.Child("FILE")?.Value,
            format,
            MapMimeType(format),
            linkedXrefs);
    }

    private static string? MapMimeType(string? format) =>
        format is not null && MimeTypesByFormat.TryGetValue(format, out var mime) ? mime : format;

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
