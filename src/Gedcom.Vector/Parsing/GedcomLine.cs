namespace Gedcom.Vector.Parsing;

/// <summary>
/// Represents a single parsed line from a GEDCOM file.
/// </summary>
/// <param name="Level">The indentation level of the line (e.g., 0, 1, 2).</param>
/// <param name="XrefId">The optional cross-reference identifier (e.g., "@I1@").</param>
/// <param name="Tag">The GEDCOM tag name (e.g., "INDI", "NAME").</param>
/// <param name="Value">The optional raw value associated with the line, including any combined CONC/CONT values.</param>
public record GedcomLine(int Level, string? XrefId, string Tag, string? Value);
