namespace Gedcom.Vector.Parsing;

public record GedcomLine(int Level, string? XrefId, string Tag, string? Value);
