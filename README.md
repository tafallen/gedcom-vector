# Gedcom.Vector

A high-performance, zero-dependency, low-allocation C# library for parsing, building, mutating, and exporting GEDCOM genealogy files.

[![CI](https://github.com/tafallen/gedcom-vector/actions/workflows/ci.yml/badge.svg)](https://github.com/tafallen/gedcom-vector/actions/workflows/ci.yml)
[![License: PolyForm Noncommercial](https://img.shields.io/badge/License-PolyForm%20Noncommercial-blue.svg)](LICENSE)

---

## Features

- **Blazing-Fast Single-Pass Parser**: Zero-allocation SIMD line reader using .NET 8 `SearchValues<char>` line splitting.
- **Span-Based String Pooling**: Deduplicates tags, XrefIds, given names, surnames, dates, and places without heap allocations on pool hits.
- **Direct UTF-8 Exporter**: Formats output tokens directly to UTF-8 byte spans via a 64KB rented buffer, serializing >2.8M records/sec.
- **Fluent Builder API**: Strongly-typed `GedcomBuilder` for programmatically constructing syntactically valid GEDCOM files.
- **$O(1)$ Query & Mutation Context**: `GedcomTreeContext` for constant-time parent, child, spouse, and media relationship lookups and incremental updates.
- **Encoding Auto-Detection**: Auto-detects UTF-8, UTF-16 LE/BE, ANSEL, and Windows-1252. Includes a zero-allocation ANSEL diacritics decoder.
- **Streaming Pipeline**: Single-pass level-0 record parsing capable of processing gigabyte-sized files with low memory consumption.
- **Zero Third-Party Runtime Dependencies**: Clean, portable .NET 8 target.

---

## Quick Start

### Installation

Add `Gedcom.Vector` to your project:

```bash
dotnet add package Gedcom.Vector
```

### Parsing a GEDCOM File

```csharp
using Gedcom.Vector;

using var stream = File.OpenRead("family.ged");

// Initialize import adapter (logging and options optional)
var importAdapter = new GedcomImportAdapter();
GedcomParseResult result = importAdapter.Parse(stream);

Console.WriteLine($"Parsed {result.Persons.Count} individuals and {result.Families.Count} families.");

foreach (var person in result.Persons)
{
    Console.WriteLine($"{person.XrefId}: {person.FirstName} {person.LastName} (Born: {person.BirthDate})");
}
```

### Exporting a GEDCOM File

```csharp
var exportWriter = new GedcomExportWriter();

// Export to string
string gedcomText = exportWriter.Write(result);

// Stream directly to file
using var outputStream = File.Create("output.ged");
exportWriter.Write(result, outputStream);
```

---

## Fluent APIs

`Gedcom.Vector` provides optional, strongly-typed Fluent Builder and Query/Mutation APIs to simplify programmatic tree construction and relationship traversal.

### 1. Fluent Builder (`GedcomBuilder`)

Build a complete, syntactically correct GEDCOM structure in C# without manually handling lists:

```csharp
using Gedcom.Vector.Builder;

GedcomParseResult result = new GedcomBuilder()
    .AddPerson("@I1@", "John", "Doe", PersonSex.Male)
        .WithBirth("1 JAN 1900", "New York, USA")
        .WithDeath("1 JAN 1980", "Boston, USA")
    .AddPerson("@I2@", "Jane", "Smith", PersonSex.Female)
        .WithBirth("1 JUN 1905")
    .AddFamily("@F1@", "@I1@", "@I2@")
        .WithMarriage("1 JUN 1925", "Chicago, USA")
        .WithChild("@I3@")
    .AddPerson("@I3@", "Bobby", "Doe", PersonSex.Male)
    .Build();
```

### 2. High-Performance Query & Mutation Context (`GedcomTreeContext`)

Navigating raw GEDCOM results usually requires writing slow $O(N)$ linear-scanning LINQ queries. The `GedcomTreeContext` indexes the tree upon instantiation, enabling **$O(1)$ relationship queries** and **$O(1)$ incremental updates** (avoiding full recomputations):

```csharp
// Wrap the result in an indexed context
GedcomTreeContext tree = result.ToContext();

// 1. O(1) Traversal Queries
PersonRecord? john = tree.GetPerson("@I1@");
foreach (var child in tree.ChildrenOf(john))
{
    Console.WriteLine($"Child: {child.FirstName} {child.LastName}");
}

// 2. O(1) Incremental Updates (Backing lists are kept in sync automatically)
tree.AddPerson(new PersonRecord("@I4@", "Alice", "Doe", PersonSex.Female, null, null, null, null));
tree.DeletePerson("@I1@"); // John is deleted and unlinked from spouses/children/media automatically!
```

---

## Encoding Detection

`GedcomEncodingDetector` automatically selects the correct decoder:

| Declared `CHAR` tag | Encoding used         |
|---------------------|-----------------------|
| `UTF-8`             | UTF-8                 |
| `UNICODE`           | UTF-16 LE/BE (via BOM)|
| `ANSEL`             | Custom ANSEL decoder  |
| `ANSI`              | Windows-1252          |
| *(absent)*          | UTF-8 (default)       |

BOM detection takes priority over the declared tag.

---

## Configuration

| Option              | Type   | Default    | Description                              |
|---------------------|--------|------------|------------------------------------------|
| `MaxFileSizeBytes`  | `long` | `52428800` | Maximum allowed GEDCOM file size (bytes) |

---

## Performance

The library features a single-pass streaming parser with zero-allocation SIMD line splitting (`SearchValues<char>`) and string pooling. Below are the BenchmarkDotNet performance metrics for parsing and exporting a dataset of **4,000 individuals** (`INDI`) and **2,000 families** (`FAM`):

| Method | Mean | Error | StdDev | Gen0 | Gen1 | Gen2 | Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **MeasureParsing** | **5.17 ms** | 0.32 ms | 0.93 ms | 507.81 | 460.94 | 218.75 | **3.07 MB** |
| **MeasureExporting** | **1.42 ms** | 0.04 ms | 0.10 ms | 736.33 | 722.66 | 634.77 | **4.04 MB** |

### Key Performance Highlights:
* **Parsing Throughput**: Parses 4,000 individuals in **5.17 ms** (**2.55x faster** than baseline).
* **Memory Efficiency**: Cuts transient parsing heap allocations by **77.3%** (from 13.52 MB down to 3.07 MB).
* **Exporting Throughput**: Exports 4,000 records in **1.42 ms** (**5.58x faster** than baseline, serializing >2.8M records/sec).

To execute benchmarks locally:
```bash
dotnet run -c Release --project tests/Gedcom.Vector.Benchmarks -- --filter *
```

---

## Project Structure

```
gedcom-vector/
├── src/
│   └── Gedcom.Vector/
│       ├── Builder/           # GedcomBuilder, PersonBuilder, FamilyBuilder
│       ├── Parsing/           # StreamingGedcomParser, GedcomStringPool, AnselDecoder
│       ├── GedcomImportAdapter.cs
│       ├── GedcomExportWriter.cs
│       ├── GedcomEncodingDetector.cs
│       ├── GedcomTreeContext.cs
│       └── ...
├── tests/
│   ├── Gedcom.Vector.Tests/
│   │   ├── AnselDecoderTests.cs
│   │   ├── GedcomEncodingDetectorTests.cs
│   │   ├── GedcomFluentEdgeCaseTests.cs
│   │   ├── GedcomBranchCoverageTests.cs
│   │   └── ...
│   └── Gedcom.Vector.Benchmarks/
│       └── ParserBenchmarks.cs
├── .github/workflows/
│   ├── ci.yml
│   └── publish.yml
├── LICENSE
└── README.md
```

---

## License

**Free for non-commercial use** under the [PolyForm Noncommercial License 1.0.0](LICENSE).

This covers:
- Personal projects, hobby use, research, and education
- Non-profit and public-sector organizations

**Commercial use requires a separate license.** Please [open an issue](https://github.com/tafallen/gedcom-vector/issues) or contact the author to discuss commercial licensing terms.

---

## Contributing

This library is extracted from the [FAMTree](https://github.com/tafallen/FAMTree) project. Bug reports and suggestions are welcome via GitHub Issues.
