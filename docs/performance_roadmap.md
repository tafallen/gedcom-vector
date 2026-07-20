# Gedcom.Vector Performance Optimization Roadmap

This document outlines the architectural bottlenecks and technical opportunities to further increase the parsing, serialization, and memory efficiency of `Gedcom.Vector`.

---

## Executive Summary

`Gedcom.Vector` is already significantly faster and lower-allocation than traditional .NET GEDCOM parsers. However, several high-impact optimization vectors remain to push performance to hardware limits:

1. **Zero-Allocation UTF-8 Reader**: Replace `StreamReader.ReadLine()` with span-based buffer reading.
2. **SIMD Acceleration**: Utilize .NET 8 `SearchValues<byte>/<char>` and SIMD vectors for line and delimiter scanning.
3. **Single-Pass Direct Parser**: Bypass intermediate `GedcomLine` and `GedcomNode` heap allocations for standard records.
4. **Value & String Pooling**: Intern places, dates, and surnames to cut retained memory by 50–70%.
5. **Parallel Block Parsing**: Partition level-0 records across CPU cores for large files.
6. **UTF-8 `IBufferWriter<byte>` Export**: Eliminate text encoding buffers in serialization.

---

## Detailed Technical Critique & Optimization Vectors

### 1. Zero-Allocation UTF-8 Reader Pipeline
* **Current Bottleneck**: `ReadLines()` calls `StreamReader.ReadLine()`, creating a new managed `string` object for every line in the file. For a 100,000-line GEDCOM file, 100,000 transient string instances are allocated before tokenization even starts.
* **Proposed Architecture**: Use `ReadOnlySequence<byte>` / `PipeReader` / memory-mapped buffer scanning to slice lines directly in UTF-8 byte spans without allocating intermediate line strings.

---

### 2. SIMD & Hardware Acceleration (`Gedcom.Vector`)
* **Current Bottleneck**: `GedcomLexer` uses scalar `ReadOnlySpan<char>.IndexOf(' ')` and character loops.
* **Proposed Architecture**: Leverage .NET 8 `SearchValues<char>` / `SearchValues<byte>` and SIMD intrinsics (`Vector128`/`Vector256`) to locate space delimiters, `@` cross-reference boundaries, and line breaks across 16–32 byte vectors in a single CPU instruction cycle.

---

### 3. Single-Pass Direct Parser (Bypassing AST Heap Allocations)
* **Current Bottleneck**: Parsing currently follows a 4-tier pipeline:
  `StreamReader.ReadLine()` → `GedcomLine` → `GedcomNode` tree with `List<GedcomNode>` → Record Mappers.
  This creates multiple transient heap objects per line.
* **Proposed Architecture**: Implement a single-pass streaming state-machine parser. Recognized level-0 records (`INDI`, `FAM`, `OBJE`) are parsed directly from byte/char line spans into final records without instantiating intermediate `GedcomLine`, `GedcomNode`, or child `List<GedcomNode>` objects. This eliminates up to 80% of transient heap allocations during parsing.

---

### 4. Value Interning & Global String Pooling (Places, Dates, Surnames)
* **Current Bottleneck**: Currently, only GEDCOM tags (`INDI`, `NAME`) and `XrefId` strings are interned. Places like `"New York, USA"`, dates like `"1 JAN 1900"`, and surnames like `"Smith"` repeat thousands of times across large family trees.
* **Proposed Architecture**: Introduce a lightweight span-based `StringPool` (e.g. using a fast hash table over `ReadOnlySpan<char>`) to deduplicate places, dates, given names, and surnames during parsing. This will cut the retained heap memory footprint of `GedcomParseResult` by 50–70%.

---

### 5. Parallel Block Parsing for Large GEDCOM Files (>10MB / 100k+ Records)
* **Current Bottleneck**: Parsing currently runs synchronously on a single CPU thread.
* **Proposed Architecture**: GEDCOM files naturally partition at `\n0 @` boundaries into independent level-0 record blocks. For large files, chunk the file by scanning for level-0 block offsets and parse blocks concurrently across multi-core CPUs using `Parallel.ForEach`, scaling parsing throughput linearly with available CPU cores.

---

### 6. Direct UTF-8 `IBufferWriter<byte>` Serialization in Export Pipeline
* **Current Bottleneck**: `GedcomExportWriter` relies on `StreamWriter` / `TextWriter` character encoding logic.
* **Proposed Architecture**: Serialize raw UTF-8 byte spans directly to `IBufferWriter<byte>` (such as `PipeWriter` or `ArrayBufferWriter<byte>`) using `Utf8.TryWrite`, completely eliminating character encoding buffers and transient string formatting.

---

## Projected Performance Impact

| Optimization Vector | Projected Execution Speedup | Projected Memory Reduction |
| :--- | :--- | :--- |
| **Zero-Allocation UTF-8 Reader** | 2.0x – 3.0x faster | 40% – 50% fewer allocations |
| **SIMD Delimiter Scanning** | 1.5x – 2.0x faster | Zero extra allocations |
| **Single-Pass Direct Parser** | 2.5x – 4.0x faster | 75% – 85% fewer allocations |
| **String Pooling (Places/Dates)** | 1.1x – 1.2x faster | 50% – 70% lower retained RAM |
| **Parallel Block Parsing** | 3.0x – 8.0x faster (multi-core) | N/A (Throughput focus) |
| **Direct UTF-8 `IBufferWriter` Export** | 2.0x – 3.0x faster | 60% – 80% fewer allocations |
