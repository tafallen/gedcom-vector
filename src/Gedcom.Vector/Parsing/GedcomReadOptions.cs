namespace Gedcom.Vector.Parsing;

/// <summary>Options for <see cref="GedcomDocumentReader"/>.</summary>
internal sealed class GedcomReadOptions
{
    /// <summary>Gets the maximum number of diagnostics kept before a single truncation entry replaces the rest.</summary>
    public int MaxDiagnostics { get; init; } = 10_000;

    /// <summary>Gets a value indicating whether the disposition of every input line is recorded.</summary>
    public bool RecordLineAccounting { get; init; }
}
