# Gedcom.Vector Future Performance & Efficiency Opportunities

This document presents an architectural critique and performance roadmap detailing potential future efficiency improvements for `Gedcom.Vector`.

---

## Executive Summary

While `Gedcom.Vector 1.2.1` achieves industry-leading performance (**4.49 ms parsing** for 4,000 records, **1.33 ms exporting**, and **55 ns $O(1)$ fluent queries**), four advanced optimization vectors remain for ultra-high-throughput environments:

1. **Direct UTF-8 Byte Span Tokenizer (`Utf8GedcomParser`)**: Bypasses UTF-16 character decoding (`StreamReader`) using `SearchValues<byte>` SIMD splitting.
2. **Compact Micro-Collections in `GedcomTreeContext`**: Replaces single/double element `List<T>` allocations with compact struct slots.
3. **Direct Export from `GedcomTreeContext`**: Bypasses dictionary re-mapping during serialization when exporting from an indexed context.
4. **Parallel Batch Import (`ParseParallel`)**: Enables multi-core linear scaling for multi-file batch workloads.

---

## 1. Deep-Dive Optimization Vectors

### 1.1 Direct UTF-8 Byte Span Tokenizer (`Utf8GedcomParser`)
* **Current Bottleneck**: `StreamingGedcomParser` decodes UTF-8 byte streams into a `char[]` buffer via `StreamReader`, then operates on `ReadOnlySpan<char>`.
* **Proposed Architecture**:
  * Implement a direct UTF-8 byte parser (`ReadOnlySpan<byte>`) using .NET 8 `SearchValues<byte>` (`\r`, `\n`) for SIMD line splitting.
  * Utilize UTF-8 string interning (`Utf8StringPool`) to hash UTF-8 byte spans directly.
* **Expected Impact**:
  * **15–25% faster parsing throughput**.
  * **Zero intermediate character buffer copying**.

---

### 1.2 Compact Micro-Collections in `GedcomTreeContext`
* **Current Bottleneck**: `_familiesAsSpouse` and `_mediaByEntityId` instantiate standard `new List<T>()` objects (costing 40+ bytes of heap allocation per list) even when an entity has only 1 spouse or 1 media item.
* **Proposed Architecture**:
  * In genealogy datasets, >85% of individuals have 0, 1, or 2 spouses and media items.
  * Implement a compact `ValueList<T>` struct or single-element slot representation for 1-element and 2-element relationships.
* **Expected Impact**:
  * **25–35% reduction in `GedcomTreeContext` memory allocation** (from 1.10 MB down to ~780 KB for 4,000 records).
  * **Faster context initialization** (from 1.21 ms down to ~0.92 ms).

---

### 1.3 Direct Context Serialization in `GedcomExportWriter`
* **Current Bottleneck**: `GedcomExportWriter.Write(GedcomParseResult)` builds temporary `eventsByPersonXref`, `familiesAsChild`, and `familiesAsSpouse` dictionaries on every call.
* **Proposed Architecture**:
  * Provide an overload `GedcomExportWriter.Write(GedcomTreeContext)` that reads relationship links directly from the pre-indexed context tables without allocating temporary dictionaries.
* **Expected Impact**:
  * **Eliminates ~1.2 MB of transient export allocations**.
  * **15–20% faster export speed** (from 1.33 ms down to ~1.05 ms).

---

### 1.4 Multi-Core Parallel Batch Import (`ParseParallel`)
* **Current Bottleneck**: Single file import operates on a single CPU thread.
* **Proposed Architecture**:
  * Expose `GedcomImportAdapter.ParseParallel(IEnumerable<Stream>)` utilizing `Parallel.ForEachAsync` with thread-local `GedcomStringPool` instances.
* **Expected Impact**:
  * Linear throughput scaling across CPU cores (e.g. 4x–8x throughput when processing batch archives of 100+ GEDCOM files).

---

## 2. Priority & Impact Matrix

| Opportunity | Target Area | Effort | Expected Speedup | Expected Memory Reduction | Priority |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **Direct UTF-8 Byte Span Parser** | Import Pipeline | Medium | **+20% Faster Parsing** | -15% Allocations | High |
| **Direct Context Serialization** | Export Pipeline | Low | **+18% Faster Exporting** | -30% Transient Allocations | High |
| **Compact Micro-Collections** | Tree Context | Medium | **+25% Faster Indexing** | -30% Memory Footprint | Medium |
| **Parallel Batch Import** | Batch Workloads | Low | **4x-8x Core Scaling** | 0% | Medium |
