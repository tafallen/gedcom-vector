using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gedcom.Vector.Tests;

/// <summary>
/// Pins the export allocation defect found on 2026-11-22: the 1.3.2 writer allocates about 3.8 times its
/// output, mostly on the large object heap, costing one Gen2 collection per call and about 40% under
/// background GC on .NET 10. See docs/benchmarks/2026-11-22-export-gc-investigation.md and design section 8.
/// </summary>
public class ExportAllocationBudgetTests
{
    private const string SkipReason =
        "Fails on the 1.3.2 writer by design; enabled when the Plan 5 writer lands. " +
        "See docs/benchmarks/2026-11-22-export-gc-investigation.md.";

    [Fact(Skip = SkipReason)]
    public void WriteToStringAllocatesAtMostOneAndAHalfTimesTheReturnedString()
    {
        var parsed = ParseBenchmarkShapedTree();
        var writer = new GedcomExportWriter();
        _ = writer.Write(parsed); // warm-up: JIT and static initialisation are not part of the budget

        long before = GC.GetAllocatedBytesForCurrentThread();
        string output = writer.Write(parsed);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        long stringBytes = (long)output.Length * sizeof(char);
        Assert.True(
            allocated <= stringBytes * 3 / 2,
            $"Write allocated {allocated:N0} bytes for a {stringBytes:N0}-byte string ({(double)allocated / stringBytes:F2}x); budget is 1.5x.");
    }

    // Similar shape to ParserBenchmarks (4000 individuals, 2000 families).
    private static GedcomParseResult ParseBenchmarkShapedTree()
    {
        var sb = new StringBuilder();
        sb.Append("0 HEAD\n1 GEDC\n2 VERS 5.5.1\n2 FORM LINEAGE-LINKED\n1 CHAR UTF-8\n");
        for (int i = 1; i <= 4000; i++)
        {
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"0 @I{i}@ INDI\n1 NAME John{i} /Doe/\n1 SEX M\n1 BIRT\n2 DATE 1 JAN 1900\n2 PLAC New York, USA\n");
            if (i <= 2000)
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"1 FAMS @F{(i + 1) / 2}@\n");
            else
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"1 FAMC @F{i - 2000}@\n");
        }
        for (int i = 1; i <= 2000; i++)
        {
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"0 @F{i}@ FAM\n1 HUSB @I{i * 2 - 1}@\n1 WIFE @I{i * 2}@\n1 CHIL @I{i + 2000}@\n1 MARR\n2 DATE 1 JUN 1925\n");
        }
        sb.Append("0 TRLR\n");

        var importer = new GedcomImportAdapter(
            NullLogger<GedcomImportAdapter>.Instance,
            Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 10_000_000 }));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        return importer.Parse(stream);
    }
}
