using System.Collections.Generic;

namespace Gedcom.Vector;

/// <summary>
/// Represents a family relationship parsed from a GEDCOM file.
/// </summary>
/// <param name="XrefId">The unique cross-reference identifier (e.g., "@F1@").</param>
/// <param name="HusbandXref">The identifier of the husband (spouse 1), if declared.</param>
/// <param name="WifeXref">The identifier of the wife (spouse 2), if declared.</param>
/// <param name="ChildXrefs">The list of children cross-reference identifiers associated with this family.</param>
/// <param name="MarriageDate">The date of marriage, if declared.</param>
/// <param name="MarriagePlace">The place of marriage, if declared.</param>
public record FamilyRecord(
    string XrefId,
    string? HusbandXref,
    string? WifeXref,
    IReadOnlyList<string> ChildXrefs,
    string? MarriageDate,
    string? MarriagePlace);
