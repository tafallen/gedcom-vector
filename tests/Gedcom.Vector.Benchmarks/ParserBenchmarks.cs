using System.Text;
using BenchmarkDotNet.Attributes;
using Gedcom.Vector;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gedcom.Vector.Benchmarks;

[MemoryDiagnoser]
public class ParserBenchmarks
{
    private byte[] _gedcomBytes = null!;
    private GedcomImportAdapter _importAdapter = null!;
    private GedcomParseResult _parsedResult = null!;
    private GedcomExportWriter _exportWriter = null!;

    [GlobalSetup]
    public void Setup()
    {
        var sb = new StringBuilder();
        sb.AppendLine("0 HEAD");
        sb.AppendLine("1 SOUR FAMTree");
        sb.AppendLine("1 GEDC");
        sb.AppendLine("2 VERS 5.5.1");
        sb.AppendLine("2 FORM LINEAGE-LINKED");
        sb.AppendLine("1 CHAR UTF-8");

        // Generate 100 individuals
        for (int i = 1; i <= 100; i++)
        {
            sb.AppendLine($"0 @I{i}@ INDI");
            sb.AppendLine($"1 NAME John{i} /Doe/");
            sb.AppendLine("2 GIVN John");
            sb.AppendLine("2 SURN Doe");
            sb.AppendLine("1 SEX M");
            sb.AppendLine("1 BIRT");
            sb.AppendLine("2 DATE 1 JAN 1900");
            sb.AppendLine("2 PLAC New York, USA");
            sb.AppendLine($"1 FAMS @F{i}@");
        }

        // Generate 50 families
        for (int i = 1; i <= 50; i++)
        {
            sb.AppendLine($"0 @F{i}@ FAM");
            sb.AppendLine($"1 HUSB @I{i * 2 - 1}@");
            sb.AppendLine($"1 WIFE @I{i * 2}@");
            sb.AppendLine("1 MARR");
            sb.AppendLine("2 DATE 1 JUN 1925");
        }

        sb.AppendLine("0 TRLR");

        _gedcomBytes = Encoding.UTF8.GetBytes(sb.ToString());

        _importAdapter = new GedcomImportAdapter(
            NullLogger<GedcomImportAdapter>.Instance,
            Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 10_000_000 })
        );

        using var stream = new MemoryStream(_gedcomBytes);
        _parsedResult = _importAdapter.Parse(stream);
        _exportWriter = new GedcomExportWriter();
    }

    [Benchmark]
    public GedcomParseResult MeasureParsing()
    {
        using var stream = new MemoryStream(_gedcomBytes);
        return _importAdapter.Parse(stream);
    }

    [Benchmark]
    public string MeasureExporting()
    {
        return _exportWriter.Write(_parsedResult);
    }
}
