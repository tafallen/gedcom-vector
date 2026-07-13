namespace Gedcom.Vector;

/// <summary>
/// Represents a specific genealogical event associated with an individual.
/// </summary>
/// <param name="PersonXrefId">The unique identifier of the person this event belongs to.</param>
/// <param name="EventType">The type of the event (e.g., Birth, Death, Census).</param>
/// <param name="Date">The date string associated with the event, if declared.</param>
/// <param name="Place">The place string associated with the event, if declared.</param>
public record EventRecord(
    string PersonXrefId,
    FamTreeEventType EventType,
    string? Date,
    string? Place);
