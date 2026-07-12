namespace Gedcom.Vector;

internal static class FamilyMapper
{
    public static FamilyRecord MapFamily(Parsing.GedcomNode family) => new(
        family.XrefId!,
        family.Child("HUSB")?.Value,
        family.Child("WIFE")?.Value,
        family.ChildrenWithTag("CHIL").Select(c => c.Value!).ToList(),
        family.Child("MARR")?.Child("DATE")?.Value,
        family.Child("MARR")?.Child("PLAC")?.Value);
}
