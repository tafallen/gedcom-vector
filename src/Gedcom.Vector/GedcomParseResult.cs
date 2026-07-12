namespace Gedcom.Vector;

public class GedcomParseResult
{
    public List<PersonRecord> Persons { get; } = new();

    public List<FamilyRecord> Families { get; } = new();

    public List<EventRecord> Events { get; } = new();

    public List<MediaReferenceRecord> Media { get; } = new();

    public List<string> Errors { get; } = new();
}
