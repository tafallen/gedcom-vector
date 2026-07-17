# Gedcom.Vector

A portable **.NET 8** GEDCOM 5.5.1 parser, lexer, encoder, and serializer. Zero genealogy-app-specific dependencies — just `Microsoft.Extensions` abstractions.

[![CI](https://github.com/tafallen/gedcom-vector/actions/workflows/ci.yml/badge.svg)](https://github.com/tafallen/gedcom-vector/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Gedcom.Vector)](https://www.nuget.org/packages/Gedcom.Vector)
[![License: PolyForm Noncommercial](https://img.shields.io/badge/license-PolyForm%20Noncommercial-blue)](LICENSE)

---

## Features

- 📄 **Full GEDCOM 5.5.1 support** — parses HEAD, INDI, FAM, OBJE, and TRLR records
- 🔡 **Multi-encoding support** — UTF-8 (with/without BOM), UTF-16 (with BOM), ANSEL, and ANSI/Windows-1252
- ⚡ **Streaming lexer** — low-allocation line-by-line tokeniser backed by `GedcomLexer` and `GedcomTreeBuilder`
- 📦 **NuGet-ready** — structured for `dotnet pack` with symbols (`.snupkg`)
- 💉 **DI-friendly** — integrates with `Microsoft.Extensions.DependencyInjection` via `AddGedcomImport()`
- 🔒 **No app-specific dependencies** — only `Microsoft.Extensions.*` abstractions; no EF Core, no ASP.NET, no databases
- ✅ **Deterministic builds** — reproducible byte-for-byte output

---

## Quick Start

### Install

```bash
dotnet add package Gedcom.Vector
```

### Register with Dependency Injection

```csharp
// Program.cs / Startup.cs
builder.Services.AddGedcomImport(builder.Configuration);
```

This registers `IGedcomImportAdapter` as a singleton, bound to the `GedcomImport` configuration section.

```json
// appsettings.json
{
  "GedcomImport": {
    "MaxFileSizeBytes": 52428800
  }
}
```

### Parse a GEDCOM file

```csharp
public class MyService(IGedcomImportAdapter gedcom)
{
    public GedcomParseResult Import(Stream gedcomStream)
    {
        var result = gedcom.Parse(gedcomStream);

        foreach (var person in result.Persons)
            Console.WriteLine($"{person.FirstName} {person.LastName}");

        foreach (var family in result.Families)
            Console.WriteLine($"Family: {family.HusbandXref} + {family.WifeXref}");

        return result;
    }
}
```

### Export to GEDCOM

```csharp
public class ExportService(IGedcomExportWriter writer)
{
    public async Task ExportAsync(GedcomParseResult data, Stream output)
    {
        await writer.WriteAsync(data, output);
    }
}
```

### Without DI (direct use)

```csharp
using var inputStream = File.OpenRead("family.ged");
var adapter = new GedcomImportAdapter(
    NullLogger<GedcomImportAdapter>.Instance,
    Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 10_000_000 })
);
GedcomParseResult result = adapter.Parse(inputStream);

// Direct export to file
using var outputStream = File.Create("output.ged");
var writer = new GedcomExportWriter();
writer.Write(result, outputStream);
```

---

## Detailed API & Records Reference

### Inspecting Parsed Individuals (`PersonRecord`)
```csharp
foreach (var person in result.Persons)
{
    Console.WriteLine($"ID: {person.XrefId}");
    Console.WriteLine($"Name: {person.FirstName} {person.LastName}");
    Console.WriteLine($"Sex: {person.Sex}"); // PersonSex.Male / PersonSex.Female / PersonSex.Unknown
    
    if (person.BirthDate is not null)
        Console.WriteLine($"Born: {person.BirthDate} at {person.BirthPlace}");
    if (person.DeathDate is not null)
        Console.WriteLine($"Died: {person.DeathDate} at {person.DeathPlace}");
}
```

### Inspecting Family Structures (`FamilyRecord`)
```csharp
foreach (var family in result.Families)
{
    Console.WriteLine($"Family: {family.XrefId}");
    Console.WriteLine($"Spouses: {family.HusbandXref} and {family.WifeXref}");
    Console.WriteLine($"Children IDs: {string.Join(", ", family.ChildXrefs)}");
    
    if (family.MarriageDate is not null)
        Console.WriteLine($"Marriage: {family.MarriageDate} in {family.MarriagePlace}");
}
```

### Inspecting Linked Events (`EventRecord`)
Events are separated from individuals for cleaner data structures, mapped via `PersonXrefId`:
```csharp
foreach (var ev in result.Events)
{
    Console.WriteLine($"Person: {ev.PersonXrefId}");
    Console.WriteLine($"Event: {ev.EventType} (Date: {ev.Date}, Place: {ev.Place})");
}
```

### Handling Media References (`MediaReferenceRecord`)
```csharp
foreach (var media in result.Media)
{
    Console.WriteLine($"Title: {media.Title}");
    Console.WriteLine($"File: {media.FilePath} (MIME: {media.MimeType})");
    Console.WriteLine($"Linked Entities: {string.Join(", ", media.LinkedXrefIds)}");
}
```

### Error and Validation Checking
Check for parsing validation errors or format issues:
```csharp
if (result.Errors.Count > 0)
{
    Console.WriteLine("Import completed with errors/warnings:");
    foreach (var error in result.Errors)
        Console.WriteLine($"- {error}");
}
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

### Benefits and Costs

* **Builder**: Increases readability and reduces reference mapping errors. Negligible overhead.
* **Context Queries**: Running queries via `GedcomTreeContext` is **300x+ faster** than traditional LINQ scans.
* **Context Mutability**: Incremental mutators (like `DeletePerson`) execute in **~200 ns** ($O(1)$), compared to **~1.2 ms** ($O(N)$) for a full tree re-indexing.
* **Break-Even**: Context initialization (indexing) takes **1.19 ms** and allocates **1.15 MB** for a 4,000-person tree. This time cost pays off after **82 relationship queries**.

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

The library features a low-allocation line-by-line streaming tokeniser. Below are the BenchmarkDotNet performance metrics for parsing and exporting a sample dataset consisting of 100 individuals (`INDI`) and 50 families (`FAM`):

| Method           | Mean      | Error    | StdDev   | Gen0    | Gen1    | Allocated |
|----------------- |----------:|---------:|---------:|--------:|--------:|----------:|
| MeasureParsing   |  95.53 us | 1.707 us | 4.345 us | 41.9922 |  7.8125 | 343.92 KB |
| MeasureExporting |  21.85 us | 1.459 us | 4.211 us | 14.6790 |  3.6316 | 120.13 KB |

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
│       ├── Parsing/           # Lexer, tree builder, ANSEL decoder
│       ├── GedcomImportAdapter.cs
│       ├── GedcomExportWriter.cs
│       ├── GedcomEncodingDetector.cs
│       └── ...
├── tests/
│   └── Gedcom.Vector.Tests/
│       ├── AnselDecoderTests.cs
│       ├── GedcomEncodingDetectorTests.cs
│       └── ...
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
