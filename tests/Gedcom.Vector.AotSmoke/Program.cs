using System.Globalization;
using System.IO.Compression;
using System.Text;

using Gedcom.Vector;
using Gedcom.Vector.Gedzip;
using Gedcom.Vector.Model;
using Gedcom.Vector.Parsing;

const int ExitUsage = 2;
const int ExitUnreadable = 3;
const int ExitUnknownExtension = 4;

if (args.Length < 2 || args.Length > 4 || (args.Length == 3 && args[2] != "--structures"))
{
    return Usage();
}

string input = args[0];
string output = args[1];
GedcomSpecVersion? target = null;
bool structuresMode = args.Length == 3;

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

if (structuresMode)
{
    if (isGedzip)
    {
        await Console.Error.WriteLineAsync("--structures reads .ged input only.");
        return ExitUnknownExtension;
    }

    return await RunStructuresAsync(input, output);
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

static async Task<int> RunStructuresAsync(string input, string output)
{
    GedcomDocument document;
    try
    {
        using var inputStream = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
        document = GedcomDocumentReader.Read(inputStream).Document;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        await Console.Error.WriteLineAsync("Cannot read input '" + input + "': " + ex.Message);
        return ExitUnreadable;
    }

    var text = new StringBuilder();
    long structures = 0;
    var pending = new Stack<GedcomStructure>();
    foreach (var record in document.ExtensionRecords)
    {
        text.Append(record.Structure.ToGedcomText(document.SourceVersion));
        pending.Push(record.Structure);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            structures++;
            foreach (var child in node.Children)
            {
                pending.Push(child);
            }
        }
    }

    try
    {
        File.WriteAllBytes(output, new UTF8Encoding(false).GetBytes(text.ToString()));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        await Console.Error.WriteLineAsync("Cannot write output '" + output + "': " + ex.Message);
        return ExitUsage;
    }

    var summary = new StringBuilder();
    AppendLine(summary, "SourceVersion", document.SourceVersion.ToString());
    AppendLine(summary, "Records", document.ExtensionRecords.Count.ToString(CultureInfo.InvariantCulture));
    AppendLine(summary, "Structures", structures.ToString(CultureInfo.InvariantCulture));
    AppendLine(summary, "Diagnostics", document.Diagnostics.Count.ToString(CultureInfo.InvariantCulture));
    foreach (var diagnostic in document.Diagnostics)
    {
        string line = diagnostic.Line is { } n ? "line " + n.ToString(CultureInfo.InvariantCulture) : "line -";
        AppendLine(summary, "Diagnostic", "GV" + ((int)diagnostic.Code).ToString(CultureInfo.InvariantCulture) + " " + line);
    }

    using var stdout = Console.OpenStandardOutput();
    byte[] bytes = new UTF8Encoding(false).GetBytes(summary.ToString());
    stdout.Write(bytes, 0, bytes.Length);
    return 0;
}

static int Usage()
{
    Console.Error.WriteLine("Usage: Gedcom.Vector.AotSmoke <input> <output> [--target 5.5.1|7.0 | --structures]");
    return ExitUsage;
}
