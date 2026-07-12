namespace Gedcom.Vector;

public record EventRecord(
    string PersonXrefId,
    FamTreeEventType EventType,
    string? Date,
    string? Place);
