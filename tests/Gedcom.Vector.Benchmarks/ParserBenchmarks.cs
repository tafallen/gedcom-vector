using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private PersonRecord _targetPerson = null!;
    private GedcomTreeContext _context = null!;

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

        // Generate 4000 individuals
        for (int i = 1; i <= 4000; i++)
        {
            sb.AppendLine($"0 @I{i}@ INDI");
            sb.AppendLine($"1 NAME John{i} /Doe/");
            sb.AppendLine("2 GIVN John");
            sb.AppendLine("2 SURN Doe");
            sb.AppendLine("1 SEX M");
            sb.AppendLine("1 BIRT");
            sb.AppendLine("2 DATE 1 JAN 1900");
            sb.AppendLine("2 PLAC New York, USA");
            
            // Individuals 1 to 2000 are spouses in families 1 to 1000
            if (i <= 2000)
            {
                sb.AppendLine($"1 FAMS @F{(i + 1) / 2}@");
            }
            // Individuals 2001 to 4000 are children in families 1 to 2000
            else
            {
                sb.AppendLine($"1 FAMC @F{i - 2000}@");
            }
        }

        // Generate 2000 families
        for (int i = 1; i <= 2000; i++)
        {
            sb.AppendLine($"0 @F{i}@ FAM");
            sb.AppendLine($"1 HUSB @I{i * 2 - 1}@");
            sb.AppendLine($"1 WIFE @I{i * 2}@");
            sb.AppendLine($"1 CHIL @I{i + 2000}@");
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
        _targetPerson = _parsedResult.Persons.First(p => p.XrefId == "@I50@");
        _context = _parsedResult.ToContext();
    }

    [Benchmark(Baseline = true)]
    public GedcomParseResult MeasureParsing_GedcomVector()
    {
        using var stream = new MemoryStream(_gedcomBytes);
        return _importAdapter.Parse(stream);
    }

    [Benchmark]
    public object MeasureParsing_GedcomNetSDK()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllBytes(tempFile, _gedcomBytes);
        try
        {
            using var parser = new Patagames.GedcomNetSdk.Parser(tempFile);
            var trans = new Patagames.GedcomNetSdk.GedcomTransmission();
            trans.Deserialize(parser);
            return trans;
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Benchmark]
    public object MeasureParsing_GeneGenie()
    {
        var parser = new GeneGenie.Gedcom.Parser.GedcomParser();
        string text = Encoding.UTF8.GetString(_gedcomBytes);
        parser.GedcomParse(text);
        return parser;
    }

    [Benchmark]
    public object MeasureParsing_FamilyTreeProject()
    {
        using var stream = new MemoryStream(_gedcomBytes);
        using var reader = FamilyTreeProject.GEDCOM.IO.GEDCOMReader.Create(stream);
        var doc = new FamilyTreeProject.GEDCOM.GEDCOMDocument();
        doc.Load(reader);
        return doc;
    }

    [Benchmark]
    public string MeasureExporting()
    {
        return _exportWriter.Write(_parsedResult);
    }

    [Benchmark]
    public string MeasureExportingContext()
    {
        return _exportWriter.Write(_context);
    }

    [Benchmark]
    public List<PersonRecord> QueryChildrenLinq()
    {
        var targetId = _targetPerson.XrefId;
        return _parsedResult.Families
            .Where(f => f.HusbandXref == targetId || f.WifeXref == targetId)
            .SelectMany(f => f.ChildXrefs)
            .Select(xref => _parsedResult.Persons.FirstOrDefault(p => p.XrefId == xref))
            .Where(p => p is not null)
            .ToList()!;
    }

    [Benchmark]
    public List<PersonRecord> QueryChildrenFluent()
    {
        return _context.ChildrenOf(_targetPerson).ToList();
    }

    [Benchmark]
    public GedcomTreeContext CreateTreeContext()
    {
        return _parsedResult.ToContext();
    }
}
