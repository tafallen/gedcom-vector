using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gedcom.Vector.Gedzip;

/// <summary>
/// Provides support for reading and writing GEDZIP (.gdz) archives containing GEDCOM 7.0 datasets.
/// </summary>
public static class GedzipAdapter
{
    /// <summary>
    /// Extracts and parses the primary GEDCOM manifest (.ged file) from a GEDZIP (.gdz) stream.
    /// </summary>
    public static GedcomParseResult ParseGedzip(Stream gedzipStream, IGedcomImportAdapter importAdapter)
    {
        if (gedzipStream == null) throw new ArgumentNullException(nameof(gedzipStream));
        if (importAdapter == null) throw new ArgumentNullException(nameof(importAdapter));

        using var archive = new ZipArchive(gedzipStream, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry? gedEntry = null;

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".ged", StringComparison.OrdinalIgnoreCase))
            {
                gedEntry = entry;
                break;
            }
        }

        if (gedEntry == null)
        {
            var errResult = new GedcomParseResult();
            errResult.Errors.Add("No .ged manifest file was found inside the GEDZIP archive.");
            return errResult;
        }

        using var entryStream = gedEntry.Open();
        using var ms = new MemoryStream();
        entryStream.CopyTo(ms);
        ms.Position = 0;

        return importAdapter.Parse(ms);
    }

    /// <summary>
    /// Creates a GEDZIP (.gdz) zip archive containing the serialized GEDCOM 7.0 dataset.
    /// </summary>
    public static void CreateGedzip(GedcomParseResult parseResult, IGedcomExportWriter exportWriter, Stream outputStream)
    {
        if (parseResult == null) throw new ArgumentNullException(nameof(parseResult));
        if (exportWriter == null) throw new ArgumentNullException(nameof(exportWriter));
        if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));

        // Enforce 7.0 spec version for GEDZIP packages
        parseResult.SpecVersion = GedcomSpecVersion.Gedcom70;

        using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);
        var entry = archive.CreateEntry("tree.ged", CompressionLevel.Optimal);

        using var entryStream = entry.Open();
        exportWriter.Write(parseResult, entryStream);
    }
}
