namespace Gedcom.Vector;

public record FamilyRecord(
    string XrefId,
    string? HusbandXref,
    string? WifeXref,
    IReadOnlyList<string> ChildXrefs,
    string? MarriageDate,
    string? MarriagePlace);
