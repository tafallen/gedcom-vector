using Microsoft.Extensions.Logging;

namespace Gedcom.Vector;

internal static class EventMapper
{
    private static readonly Dictionary<string, FamTreeEventType> SupportedEventTypes = new()
    {
        ["BIRT"] = FamTreeEventType.Birth,
        ["DEAT"] = FamTreeEventType.Death,
        ["CENS"] = FamTreeEventType.Census,
        ["IMMI"] = FamTreeEventType.Immigration,
        ["EMIG"] = FamTreeEventType.Emigration,
        ["RESI"] = FamTreeEventType.Residence,
        ["CHR"] = FamTreeEventType.Christening,
        ["BURI"] = FamTreeEventType.Burial,
        ["BAPM"] = FamTreeEventType.Baptism,
    };

    private static readonly HashSet<string> EventLikeTags = new(SupportedEventTypes.Keys)
    {
        "EVEN", "ADOP", "BAPM", "BARM", "BASM", "BLES", "CHRA", "CONF", "FCOM",
        "ORDN", "NATU", "PROB", "WILL", "GRAD", "RETI",
        "CAST", "DSCR", "EDUC", "IDNO", "NATI", "NCHI", "NMR", "OCCU", "PROP", "RELI", "SSN", "FACT",
    };

    public static void MapEvents(Parsing.GedcomNode individual, List<EventRecord> destination, ILogger logger)
    {
        var children = individual.Children;
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!EventLikeTags.Contains(child.Tag))
            {
                continue;
            }

            if (!SupportedEventTypes.TryGetValue(child.Tag, out var eventType))
            {
                logger.LogDebug(
                    "Skipping unsupported GEDCOM event type {EventType} for individual {XrefId}",
                    child.Tag,
                    individual.XrefId);
                continue;
            }

            destination.Add(new EventRecord(individual.XrefId!, eventType, child.Child("DATE")?.Value, child.Child("PLAC")?.Value));
        }
    }
}
