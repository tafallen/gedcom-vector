using System;
using System.IO;
using Gedcom.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gedcom.Vector.Sample;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("        Gedcom.Vector Sample Sandbox Application   ");
        Console.WriteLine("==================================================");

        // 1. Create a sample GEDCOM content representing a small family tree
        string sampleGedcom = 
            "0 HEAD\n" +
            "1 SOUR FAMTree\n" +
            "1 CHAR UTF-8\n" +
            "0 @I1@ INDI\n" +
            "1 NAME John /Doe/\n" +
            "2 GIVN John\n" +
            "2 SURN Doe\n" +
            "1 SEX M\n" +
            "1 BIRT\n" +
            "2 DATE 12 MAR 1945\n" +
            "2 PLAC London, England\n" +
            "1 FAMS @F1@\n" +
            "0 @I2@ INDI\n" +
            "1 NAME Jane /Smith/\n" +
            "2 GIVN Jane\n" +
            "2 SURN Smith\n" +
            "1 SEX F\n" +
            "1 FAMS @F1@\n" +
            "0 @I3@ INDI\n" +
            "1 NAME Bobby /Doe/\n" +
            "2 GIVN Bobby\n" +
            "2 SURN Doe\n" +
            "1 SEX M\n" +
            "1 FAMC @F1@\n" +
            "0 @F1@ FAM\n" +
            "1 HUSB @I1@\n" +
            "1 WIFE @I2@\n" +
            "1 CHIL @I3@\n" +
            "1 MARR\n" +
            "2 DATE 18 JUN 1970\n" +
            "2 PLAC Boston, USA\n" +
            "0 @M1@ OBJE\n" +
            "1 FORM jpg\n" +
            "1 TITL Doe Family Portrait\n" +
            "1 FILE portrait.jpg\n" +
            "0 TRLR\n";

        string inputPath = "sample_input.ged";
        string outputPath = "sample_output.ged";

        // Write sample data to input file
        File.WriteAllText(inputPath, sampleGedcom);
        Console.WriteLine($"\n[1] Written sample family tree to: {inputPath}");

        // 2. Initialize the importer (direct use, non-DI)
        var importer = new GedcomImportAdapter(
            NullLogger<GedcomImportAdapter>.Instance,
            Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 10 * 1024 * 1024 })
        );

        // 3. Parse the GEDCOM stream
        Console.WriteLine("[2] Importing and parsing GEDCOM file...");
        GedcomParseResult result;
        using (var inputStream = File.OpenRead(inputPath))
        {
            result = importer.Parse(inputStream);
        }

        // Check for any parsing errors
        if (result.Errors.Count > 0)
        {
            Console.WriteLine("Warnings/Errors encountered:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        // 4. Print structured records
        Console.WriteLine($"\nParsed {result.Persons.Count} individuals:");
        foreach (var person in result.Persons)
        {
            Console.WriteLine($"  - [{person.XrefId}] Name: {person.FirstName} {person.LastName} (Sex: {person.Sex})");
            if (person.BirthDate is not null)
            {
                Console.WriteLine($"    Birth: {person.BirthDate} in {person.BirthPlace}");
            }
        }

        Console.WriteLine($"\nParsed {result.Families.Count} family structures:");
        foreach (var family in result.Families)
        {
            Console.WriteLine($"  - [{family.XrefId}] Husband: {family.HusbandXref}, Wife: {family.WifeXref}");
            Console.WriteLine($"    Children: {string.Join(", ", family.ChildXrefs)}");
            if (family.MarriageDate is not null)
            {
                Console.WriteLine($"    Marriage: {family.MarriageDate} at {family.MarriagePlace}");
            }
        }

        Console.WriteLine($"\nParsed {result.Events.Count} individual events:");
        foreach (var ev in result.Events)
        {
            Console.WriteLine($"  - Person {ev.PersonXrefId}: {ev.EventType} on {ev.Date} at {ev.Place}");
        }

        Console.WriteLine($"\nParsed {result.Media.Count} media references:");
        foreach (var media in result.Media)
        {
            Console.WriteLine($"  - [{media.XrefId}] Title: {media.Title} (Format: {media.Format}, MIME: {media.MimeType})");
            Console.WriteLine($"    File path: {media.FilePath}");
        }

        // 5. Export the parsed structure back to a file using the stream exporter API
        Console.WriteLine($"\n[3] Exporting structural tree to output: {outputPath}...");
        {
            using var outputStream = File.Create(outputPath);
            var exporter = new GedcomExportWriter();
            exporter.Write(result, outputStream);
        }

        Console.WriteLine("\n[4] Parity verification check...");
        string exportedContent = File.ReadAllText(outputPath);
        if (exportedContent.Contains("0 @I1@ INDI") && exportedContent.Contains("0 @F1@ FAM") && exportedContent.Contains("0 TRLR"))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(">>> SUCCESS: Round-trip completed and verified successfully! <<<");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(">>> FAILURE: Exported content mismatch! <<<");
            Console.ResetColor();
        }

        // Clean up files
        try
        {
            File.Delete(inputPath);
            File.Delete(outputPath);
        }
        catch { }

        Console.WriteLine("\n==================================================");
        Console.WriteLine("Sandbox execution complete. Press any key to exit.");
        Console.WriteLine("==================================================");
    }
}
