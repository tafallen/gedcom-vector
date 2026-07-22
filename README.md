# Gedcom.Vector

A lightening fast, .net 8, high-performance, zero-dependency, low-allocation C# library for parsing, building, mutating, and exporting GEDCOM 5.1.1 and 7.0 genealogy files. Probably the fastest and best gedcom library out there!

Gedcom.Vector is the fastest, most efficient Gedcom library for .net available today. Anywhere. Benchmark it against it's competitors and see for yourself! 

[![CI](https://github.com/tafallen/gedcom-vector/actions/workflows/ci.yml/badge.svg)](https://github.com/tafallen/gedcom-vector/actions/workflows/ci.yml)
[![License: PolyForm Noncommercial](https://img.shields.io/badge/License-PolyForm%20Noncommercial-blue.svg)](LICENSE)
![Target: .NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Dependencies: Zero](https://img.shields.io/badge/Dependencies-Zero-success.svg)

---

## 🚀 Key Capabilities

| Core Engine | Fluent Relationship Context | Specification & Formats |
| :--- | :--- | :--- |
| **SIMD Tokenizer**: .NET 8 `SearchValues<char>` line splitting. | **$O(1)$ Relationship Traversal**: 270x faster than LINQ scans. | **Dual Spec Support**: Full support for GEDCOM 5.5.1 and 7.0 / 7.0.x. |
| **Single-Pass Parser**: Zero-allocation level-0 streaming reader. | **$O(1)$ Incremental Mutability**: Instant add, update, and delete. | **GEDZIP (.gdz) Support**: Read and write zip container archives. |
| **Direct UTF-8 Exporter**: Formats byte spans directly (>2.8M records/sec). | **Span String Pooling**: Deduplicates dates, places, and names. | **Encoding Auto-Detect**: UTF-8, UTF-16, ANSEL, ANSI (Windows-1252). |

---

## 📦 Getting Started

### 1. Installation

```bash
dotnet add package Gedcom.Vector
```

---

### 2. High-Performance Stream Import & Export

Import GEDCOM files directly into memory-optimized record structures (`GedcomParseResult`), or serialize records directly to streams:

```csharp
using Gedcom.Vector;

// --- IMPORT (Automatic 5.5.1 & 7.0 Specification Detection) ---
using var inputStream = File.OpenRead("family.ged");
var importAdapter = new GedcomImportAdapter();
GedcomParseResult result = importAdapter.Parse(inputStream);

Console.WriteLine($"Specification Version: {result.SpecVersion}"); // Gedcom551 or Gedcom70
Console.WriteLine($"Parsed {result.Persons.Count} individuals and {result.Families.Count} families.");

// --- EXPORT (Specify Target Specification) ---
var exportWriter = new GedcomExportWriter();

// Export as GEDCOM 7.0 (Mandatory UTF-8, no CONC/CONT tags)
result.SpecVersion = GedcomSpecVersion.Gedcom70;
using var outputStream7 = File.Create("output_7_0.ged");
exportWriter.Write(result, outputStream7);

// Export as GEDCOM 5.5.1
result.SpecVersion = GedcomSpecVersion.Gedcom551;
using var outputStream5 = File.Create("output_5_5_1.ged");
exportWriter.Write(result, outputStream5);
```

---

### 3. GEDZIP Container Support (`.gdz`)

`Gedcom.Vector` provides full native support for **GEDZIP (`.gdz`)** packages (zipped containers storing a `.ged` manifest along with bundled media files):

```csharp
using Gedcom.Vector.Gedzip;

// 1. Parse a GEDZIP (.gdz) package
using var gdzInput = File.OpenRead("family_tree.gdz");
GedcomParseResult gdzResult = GedzipAdapter.ParseGedzip(gdzInput, importAdapter);

// 2. Export to a GEDZIP (.gdz) package
using var gdzOutput = File.Create("exported_tree.gdz");
GedzipAdapter.CreateGedzip(gdzResult, exportWriter, gdzOutput);
```

---

### 4. $O(1)$ Relationship Navigation & Tree Mutation (`GedcomTreeContext`)

Navigating raw GEDCOM collections usually requires writing slow $O(N)$ LINQ queries. Wrapping the result in `GedcomTreeContext` indexes the tree upon instantiation, enabling **$O(1)$ constant-time relationship lookups** and **$O(1)$ incremental updates**:

```csharp
// Wrap result in an indexed context
GedcomTreeContext tree = result.ToContext();

// 1. O(1) Traversal Queries (53 ns execution time)
PersonRecord? father = tree.GetPerson("@I1@");
foreach (var child in tree.ChildrenOf(father))
{
    Console.WriteLine($"Child: {child.FirstName} {child.LastName}");
}

foreach (var spouse in tree.SpousesOf(father))
{
    Console.WriteLine($"Spouse: {spouse.FirstName} {spouse.LastName}");
}

// 2. O(1) Incremental Tree Mutations (Backing collections automatically kept in sync)
tree.AddPerson(new PersonRecord("@I4@", "Alice", "Doe", PersonSex.Female, null, null, null, null));
tree.DeletePerson("@I1@"); // Father is deleted and unlinked from spouses, children, and media in O(1) time
```

---

### 4. Programmatic Tree Construction (`GedcomBuilder`)

Construct syntactically valid GEDCOM trees programmatically without manually creating collections or managing cross-references:

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

---

## ⚡ Performance & Benchmarks

> [!NOTE]
> **Architecture & Benchmark Interfaces**: 
> - **Core Stream Import/Export (`MeasureParsing` / `MeasureExporting`)**: Benchmarks streaming parsing and serialization between raw streams and `GedcomParseResult` records (non-fluent).
> - **Relationship Traversal (`QueryChildrenFluent`)**: Benchmarks $O(1)$ relationship queries using the optional indexed `GedcomTreeContext`.

BenchmarkDotNet metrics evaluated on a dataset of **4,000 individuals** (`INDI`) and **2,000 families** (`FAM`):

### 1. Core Streaming Import & Export (`GedcomParseResult`)

| Method | Mean Execution Time | Gen0 | Gen1 | Gen2 | Allocated Memory | Throughput |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| **MeasureParsing** | **3.51 ms** | 500.00 | 472.66 | 210.94 | **3.06 MB** | ~1.14M records/sec |
| **MeasureExporting** | **1.11 ms** | 746.09 | 724.61 | 646.48 | **4.03 MB** | **>2.8M records/sec** |

### 2. Relationship Query Interface Benchmark (`LINQ` vs `Fluent GedcomTreeContext`)

| Query Interface Method | Mean Execution Time | StdDev | Gen0 | Allocated Memory | Speedup vs LINQ |
| :--- | ---: | ---: | ---: | ---: | ---: |
| **QueryChildrenLinq** *(Raw LINQ Scan)* | **15,840.57 ns** (15.84 µs) | 257.00 ns | 0.0610 | 664 B | 1.0x *(Baseline)* |
| **QueryChildrenFluent** *(`GedcomTreeContext`)* | **53.19 ns** | 0.50 ns | 0.0181 | 152 B | **298x Faster** |
| **CreateTreeContext** *(One-Time Indexing)* | **1.14 ms** | 52.05 µs | 179.69 | 1.10 MB | *(Pays off after 72 queries)* |

> [!TIP]
> **Context Indexing Pay-Off Math**: Building `GedcomTreeContext` takes **1.14 ms** for a 4,000-person tree. Because fluent queries execute in **53.19 ns** (vs **15.84 µs** for LINQ), context indexing pays off its entire CPU time cost after just **72 relationship queries**.

To execute benchmarks locally:
```bash
dotnet run -c Release --project tests/Gedcom.Vector.Benchmarks -- --filter *
```

---

## 🛠️ Technical Reference

### Character Encoding Detection

`GedcomEncodingDetector` automatically selects the target decoder:

| Declared `CHAR` Tag | Decoder Used | Notes |
| :--- | :--- | :--- |
| `UTF-8` | UTF-8 | Standard default |
| `UNICODE` | UTF-16 LE/BE | Identified via Byte Order Mark (BOM) |
| `ANSEL` | Custom `AnselDecoder` | $O(1)$ zero-allocation combining diacritics decoder |
| `ANSI` | Windows-1252 | Extended Windows Latin-1 |
| *(absent)* | UTF-8 | Fallback default |

### Configuration Options

| Option | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `MaxFileSizeBytes` | `long` | `52428800` (50 MB) | Maximum allowed GEDCOM file size limit in bytes |

---

## 📂 Project Structure & Documentation

```
gedcom-vector/
├── src/
│   └── Gedcom.Vector/
│       ├── Builder/           # GedcomBuilder, PersonBuilder, FamilyBuilder, MediaBuilder
│       ├── Parsing/           # StreamingGedcomParser, GedcomStringPool, AnselDecoder
│       ├── GedcomImportAdapter.cs
│       ├── GedcomExportWriter.cs
│       ├── GedcomEncodingDetector.cs
│       ├── GedcomTreeContext.cs
│       └── ...
├── tests/
│   ├── Gedcom.Vector.Tests/   # Unit tests (90.4% Line Rate, 84.6% Branch Rate)
│   └── Gedcom.Vector.Benchmarks/
├── docs/
│   ├── architecture.md        # Technical Architecture Guide & Pipeline Diagrams
│   └── performance_roadmap.md # Performance Roadmap & Historical Benchmarks
├── LICENSE
└── README.md
```

For detailed architectural diagrams and deep-dive technical specs, see the **[Architecture Guide](docs/architecture.md)** and **[Performance Roadmap](docs/performance_roadmap.md)**.

---

## 📜 License & Support

**Free for non-commercial use** under the [PolyForm Noncommercial License 1.0.0](LICENSE).

* **Allowed**: Personal projects, research, open-source software, non-profit organizations.
* **Commercial Use**: Requires a commercial license. Please [open an issue](https://github.com/tafallen/gedcom-vector/issues) to discuss commercial terms.
