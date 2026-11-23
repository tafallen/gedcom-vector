namespace Gedcom.Vector.Diagnostics;

/// <summary>The only place diagnostic message text lives. Templates are fixed text; only the tag placeholder is substituted.</summary>
internal static class DiagnosticMessages
{
    internal const string TagPlaceholder = "{tag}";

    internal static (GedcomDiagnosticSeverity Severity, string Template) For(GedcomDiagnosticCode code) => code switch
    {
        GedcomDiagnosticCode.BlankLinesIgnored => (GedcomDiagnosticSeverity.Info, "Blank lines were ignored."),
        GedcomDiagnosticCode.LeadingWhitespace => (GedcomDiagnosticSeverity.Info, "Leading whitespace on a line was ignored."),
        GedcomDiagnosticCode.LineWithoutLevel => (GedcomDiagnosticSeverity.Warning, "A line without a level was appended to the previous value."),
        GedcomDiagnosticCode.LineWithoutLevelUnplaced => (GedcomDiagnosticSeverity.Error, "A line without a level had no previous structure to append to and was kept as a record without a tag."),
        GedcomDiagnosticCode.MissingTag => (GedcomDiagnosticSeverity.Warning, "A line has no tag."),
        GedcomDiagnosticCode.InvalidTag => (GedcomDiagnosticSeverity.Warning, "The tag " + TagPlaceholder + " is not a valid GEDCOM tag."),
        GedcomDiagnosticCode.MalformedXref => (GedcomDiagnosticSeverity.Warning, "A cross-reference identifier is malformed."),
        GedcomDiagnosticCode.XrefOnSubstructure => (GedcomDiagnosticSeverity.Warning, "A cross-reference identifier on the substructure " + TagPlaceholder + " is only valid on records."),
        GedcomDiagnosticCode.LevelSkipped => (GedcomDiagnosticSeverity.Warning, "A line level is more than one deeper than its parent."),
        GedcomDiagnosticCode.ContinuationMisplaced => (GedcomDiagnosticSeverity.Warning, "The continuation tag " + TagPlaceholder + " is misplaced and was kept as an ordinary structure."),
        GedcomDiagnosticCode.ContinuationAfterChildren => (GedcomDiagnosticSeverity.Info, "The continuation tag " + TagPlaceholder + " follows other child structures."),
        GedcomDiagnosticCode.LineTooLong => (GedcomDiagnosticSeverity.Error, "A line exceeded the maximum line length and its excess was discarded."),
        GedcomDiagnosticCode.ExtraDelimiter => (GedcomDiagnosticSeverity.Info, "Extra spaces between the level, cross-reference identifier and tag of a line were read as one delimiter."),
        GedcomDiagnosticCode.MissingHeader => (GedcomDiagnosticSeverity.Warning, "The document has no HEAD record."),
        GedcomDiagnosticCode.UnknownVersion => (GedcomDiagnosticSeverity.Warning, "The GEDCOM version is missing or not recognised."),
        GedcomDiagnosticCode.NonUtf8Encoding => (GedcomDiagnosticSeverity.Error, "GEDCOM 7.0 requires UTF-8 encoding. Non-UTF-8 encoding detected."),
        GedcomDiagnosticCode.MissingTrailer => (GedcomDiagnosticSeverity.Warning, "The document has no TRLR record."),
        GedcomDiagnosticCode.RecordAfterTrailer => (GedcomDiagnosticSeverity.Warning, "A record appears after the TRLR record."),
        GedcomDiagnosticCode.DiagnosticsTruncated => (GedcomDiagnosticSeverity.Warning, "Further diagnostics were suppressed after the configured maximum was reached."),
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "No message template for this diagnostic code."),
    };

    internal static string Format(string template, string? tag) =>
        template.Replace(TagPlaceholder, tag ?? "?", StringComparison.Ordinal);
}
