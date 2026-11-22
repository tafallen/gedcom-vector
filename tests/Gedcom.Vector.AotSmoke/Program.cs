using System.Globalization;
using System.IO.Compression;
using System.Text;

using Gedcom.Vector;
using Gedcom.Vector.Gedzip;

const int ExitUsage = 2;
const int ExitUnreadable = 3;
const int ExitUnknownExtension = 4;

if (args.Length != 2 && args.Length != 4)
{
    return Usage();
}

string input = args[0];
string output = args[1];
GedcomSpecVersion? target = null;

if (args.Length == 4)
{
    if (args[2] != "--target")
    {
        return Usage();
    }

    switch (args[3])
    {
        case "5.5.1":
            target = GedcomSpecVersion.Gedcom551;
            break;
        case "7.0":
            target = GedcomSpecVersion.Gedcom70;
            break;
        default:
            return Usage();
    }
}

string extension = Path.GetExtension(input);
bool isGedzip = string.Equals(extension, ".gdz", StringComparison.OrdinalIgnoreCase);
if (!isGedzip && !string.Equals(extension, ".ged", StringComparison.OrdinalIgnoreCase))
{
    await Console.Error.WriteLineAsync("Unknown input extension '" + extension + "'; expected .ged or .gdz.");
    return ExitUnknownExtension;
}

GedcomParseResult result;
try
{
    var importAdapter = new GedcomImportAdapter();
    using var inputStream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
    result = isGedzip
        ? GedzipAdapter.ParseGedzip(inputStream, importAdapter)
        : importAdapter.Parse(inputStream);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
{
    await Console.Error.WriteLineAsync("Cannot read input '" + input + "': " + ex.Message);
    return ExitUnreadable;
}

if (target is { } version)
{
    result.SpecVersion = version;
}

try
{
    using var outputStream = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None);
    var writer = new GedcomExportWriter();
    if (isGedzip)
    {
        GedzipAdapter.CreateGedzip(result, writer, outputStream);
    }
    else
    {
        writer.Write(result, outputStream);
    }
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    await Console.Error.WriteLineAsync("Cannot write output '" + output + "': " + ex.Message);
    return ExitUsage;
}

var summary = new StringBuilder();
AppendLine(summary, "SpecVersion", result.SpecVersion.ToString());
AppendLine(summary, "Persons", result.Persons.Count.ToString(CultureInfo.InvariantCulture));
AppendLine(summary, "Families", result.Families.Count.ToString(CultureInfo.InvariantCulture));
AppendLine(summary, "Events", result.Events.Count.ToString(CultureInfo.InvariantCulture));
AppendLine(summary, "Media", result.Media.Count.ToString(CultureInfo.InvariantCulture));
AppendLine(summary, "Unparsed", result.UnparsedRecords.Count.ToString(CultureInfo.InvariantCulture));
AppendLine(summary, "Errors", result.Errors.Count.ToString(CultureInfo.InvariantCulture));
foreach (string error in result.Errors)
{
    AppendLine(summary, "Error", error);
}

using (var stdout = Console.OpenStandardOutput())
{
    byte[] bytes = new UTF8Encoding(false).GetBytes(summary.ToString());
    stdout.Write(bytes, 0, bytes.Length);
}

return 0;

static void AppendLine(StringBuilder sb, string key, string value) =>
    sb.Append(key).Append('=').Append(value).Append('\n');

static int Usage()
{
    Console.Error.WriteLine("Usage: Gedcom.Vector.AotSmoke <input> <output> [--target 5.5.1|7.0]");
    return ExitUsage;
}