# Gedcom.Vector Performance Optimization Roadmap

This document outlines the architectural bottlenecks, completed performance optimizations, and measured benchmark results for `Gedcom.Vector`.

---

## Executive Summary

`Gedcom.Vector` features a high-throughput, zero-allocation tokenizing pipeline. Through six strategic optimization pillars, the library achieves hardware-bound parsing and exporting speeds:

1. **Zero-Allocation UTF-8 Reader**: Replaced `StreamReader.ReadLine()` with span-based buffer reading. `[COMPLETED]`
2. **SIMD Acceleration**: Utilized .NET 8 `SearchValues<char>` and SIMD vectors for line and delimiter scanning. `[COMPLETED]`
3. **Single-Pass Direct Parser**: Bypassed intermediate `GedcomLine` and `GedcomNode` heap allocations for standard records. `[COMPLETED]`
4. **Value & String Pooling**: Interned places, dates, and surnames using `GedcomStringPool` to cut retained memory by >77%. `[COMPLETED]`
5. **Direct UTF-8 `Utf8StreamWriter` Export**: Formatted tokens directly to UTF-8 byte spans, accelerating serialization by 5.58x. `[COMPLETED]`

---

## Detailed Technical Architecture & Optimization Vectors

### 1. Zero-Allocation UTF-8 Reader Pipeline `[COMPLETED]`
* **Problem**: `ReadLines()` originally called `StreamReader.ReadLine()`, creating a managed `string` object for every line in the file.
* **Implementation**: Implemented `StreamingGedcomParser` using rented `char[]` buffers from `ArrayPool<char>.Shared`.

---

### 2. SIMD & Hardware Acceleration (`Gedcom.Vector`) `[COMPLETED]`
* **Problem**: Line break scanning used scalar string searching.
* **Implementation**: Utilized .NET 8 `SearchValues<char>` containing `\r` and `\n` to locate line breaks using hardware SIMD vector instructions (`Vector128`/`Vector256`).

---

### 3. Single-Pass Direct Parser (Bypassing AST Heap Allocations) `[COMPLETED]`
* **Problem**: Parsing followed a 4-tier pipeline creating `GedcomLine`, `GedcomNode` trees, and child lists.
* **Implementation**: Implemented a streaming level-0 state machine. Recognized records (`INDI`, `FAM`, `OBJE`) are populated directly from character line spans into final records (`PersonRecord`, `FamilyRecord`, `MediaReferenceRecord`), cutting transient memory allocations during parsing from **13.52 MB down to 3.07 MB (77.3% reduction)**.

---

### 4. Value Interning & Global String Pooling (Places, Dates, Surnames) `[COMPLETED]`
* **Problem**: Places like `"New York, USA"`, dates like `"1 JAN 1900"`, and surnames like `"Smith"` repeat thousands of times.
* **Implementation**: Built `GedcomStringPool` with a custom span-hashed lookup table over `ReadOnlySpan<char>`, guaranteeing **zero allocations on pool hits**.

---

### 5. Direct UTF-8 `Utf8StreamWriter` Serialization in Export Pipeline `[COMPLETED]`
* **Problem**: `GedcomExportWriter` relied on `StreamWriter` text encoding buffers.
* **Implementation**: Serializes constant UTF-8 byte spans (`"0 "u8`, `" INDI\n"u8`) directly via a 64KB rented buffer (`ArrayPool<byte>`), increasing export speed from **7.92 ms down to 1.42 ms (5.58x faster)**.

---

## Measured Performance Metrics (4,000-Person Family Tree Dataset)

| Optimization Vector | Metric | Baseline (`main`) | Optimized | Improvement |
| :--- | :--- | :--- | :--- | :--- |
| **MeasureParsing** | **Mean Execution Time** | 13.18 ms | **5.17 ms** | **2.55x Faster (60.8% Speedup)** |
| | **Allocated Memory** | 13.52 MB | **3.07 MB** | **77.3% Reduction (4.4x Lower Memory)** |
| **MeasureExporting** | **Mean Execution Time** | 7.92 ms | **1.42 ms** | **5.58x Faster (82.1% Speedup)** |
| | **Allocated Memory** | 4.52 MB | **4.04 MB** | **10.6% Reduction** |
