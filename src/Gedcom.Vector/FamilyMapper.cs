namespace Gedcom.Vector;

internal static class FamilyMapper
{
    public static FamilyRecord MapFamily(Parsing.GedcomNode family)
    {
        var children = new List<string>();
        var list = family.Children;
        for (int i = 0; i < list.Count; i++)
        {
            var child = list[i];
            if (child.Tag == "CHIL" && child.Value is not null)
            {
                children.Add(child.Value);
            }
        }

        return new FamilyRecord(
            family.XrefId!,
            family.Child("HUSB")?.Value,
            family.Child("WIFE")?.Value,
            children,
            family.Child("MARR")?.Child("DATE")?.Value,
            family.Child("MARR")?.Child("PLAC")?.Value);
    }
}
