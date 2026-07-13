namespace Gedcom.Vector;

/// <summary>
/// Represents an individual parsed from a GEDCOM file.
/// </summary>
/// <param name="XrefId">The unique cross-reference identifier (e.g., "@I1@").</param>
/// <param name="FirstName">The first (given) name of the individual, if declared.</param>
/// <param name="LastName">The last (surname) name of the individual, if declared.</param>
/// <param name="Sex">The parsed biological sex of the individual.</param>
/// <param name="BirthDate">The birth date string, if declared.</param>
/// <param name="BirthPlace">The birth place string, if declared.</param>
/// <param name="DeathDate">The death date string, if declared.</param>
/// <param name="DeathPlace">The death place string, if declared.</param>
public record PersonRecord(
    string XrefId,
    string? FirstName,
    string? LastName,
    PersonSex Sex,
    string? BirthDate,
    string? BirthPlace,
    string? DeathDate,
    string? DeathPlace);
