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
using var stream = File.OpenRead("family.ged");
var adapter = new GedcomImportAdapter(
    NullLogger<GedcomImportAdapter>.Instance,
    Options.Create(new GedcomImportOptions { MaxFileSizeBytes = 10_000_000 })
);
var result = adapter.Parse(stream);
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
