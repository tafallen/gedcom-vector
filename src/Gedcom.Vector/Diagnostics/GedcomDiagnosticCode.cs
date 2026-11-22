namespace Gedcom.Vector.Diagnostics;

/// <summary>Stable identifiers for the diagnostics the reading pipeline can raise.</summary>
public enum GedcomDiagnosticCode
{
    /// <summary>Blank lines were found and ignored (Info).</summary>
    BlankLinesIgnored = 1001,

    /// <summary>A line had leading whitespace, which was ignored (Info).</summary>
    LeadingWhitespace = 1002,

    /// <summary>A line had no level; its text was appended to the previous value (Warning).</summary>
    LineWithoutLevel = 1003,

    /// <summary>A line had no level and there was no previous structure to append to (Error).</summary>
    LineWithoutLevelUnplaced = 1004,

    /// <summary>A line had no tag (Warning).</summary>
    MissingTag = 1005,

    /// <summary>A line had a tag that is not a valid GEDCOM tag (Warning).</summary>
    InvalidTag = 1006,

    /// <summary>A cross-reference identifier was malformed (Warning).</summary>
    MalformedXref = 1007,

    /// <summary>A cross-reference identifier appeared on a substructure rather than a record (Warning).</summary>
    XrefOnSubstructure = 1008,

    /// <summary>A line's level was more than one deeper than its parent (Warning).</summary>
    LevelSkipped = 1009,

    /// <summary>A CONC or CONT appeared at level 0, with an xref, or under a pointer; it was kept as an ordinary structure (Warning).</summary>
    ContinuationMisplaced = 1010,

    /// <summary>A CONC or CONT appeared after other child structures of its parent (Info).</summary>
    ContinuationAfterChildren = 1011,

    /// <summary>The document has no HEAD record (Warning).</summary>
    MissingHeader = 2001,

    /// <summary>The GEDCOM version could not be determined or is not recognised (Warning).</summary>
    UnknownVersion = 2002,

    /// <summary>A GEDCOM 7.0 document declared or used a non-UTF-8 encoding (Error).</summary>
    NonUtf8Encoding = 2003,

    /// <summary>The document has no TRLR record (Warning).</summary>
    MissingTrailer = 2004,

    /// <summary>A record appeared after the TRLR record (Warning).</summary>
    RecordAfterTrailer = 2005,

    /// <summary>The configured maximum number of diagnostics was reached and further ones were suppressed (Warning).</summary>
    DiagnosticsTruncated = 9001,
}
