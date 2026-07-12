namespace Gedcom.Vector;

public record PersonRecord(
    string XrefId,
    string? FirstName,
    string? LastName,
    PersonSex Sex,
    string? BirthDate,
    string? BirthPlace,
    string? DeathDate,
    string? DeathPlace);
