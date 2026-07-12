namespace Gedcom.Vector;

internal static class PersonMapper
{
    public static PersonRecord MapPerson(Parsing.GedcomNode individual)
    {
        var (firstName, lastName) = MapName(individual.Child("NAME")?.Value);
        var birth = individual.Child("BIRT");
        var death = individual.Child("DEAT");

        return new PersonRecord(
            individual.XrefId!,
            firstName,
            lastName,
            MapSex(individual.Child("SEX")?.Value),
            birth?.Child("DATE")?.Value,
            birth?.Child("PLAC")?.Value,
            death?.Child("DATE")?.Value,
            death?.Child("PLAC")?.Value);
    }

    private static (string? FirstName, string? LastName) MapName(string? nameValue)
    {
        if (string.IsNullOrEmpty(nameValue))
        {
            return (null, null);
        }

        var slashIndex = nameValue.IndexOf('/');
        if (slashIndex < 0)
        {
            return (NullIfEmpty(nameValue.Trim()), null);
        }

        var given = NullIfEmpty(nameValue[..slashIndex].Trim());
        var closingSlashIndex = nameValue.IndexOf('/', slashIndex + 1);
        var surnameEnd = closingSlashIndex >= 0 ? closingSlashIndex : nameValue.Length;
        var surname = NullIfEmpty(nameValue[(slashIndex + 1)..surnameEnd].Trim());

        return (given, surname);
    }

    private static PersonSex MapSex(string? sexValue) => sexValue switch
    {
        "M" => PersonSex.Male,
        "F" => PersonSex.Female,
        _ => PersonSex.Unknown,
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
