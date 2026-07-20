# Gedcom.Vector Technical Debt Audit & Architectural Critique

This document provides a comprehensive technical audit of the `Gedcom.Vector` codebase, categorizing identified technical debt, performance edge cases, architectural trade-offs, and recommended future refactoring strategies.

---

## Executive Summary

The codebase has undergone significant performance optimization (2.55x faster parsing, 5.58x faster exporting, zero-allocation SIMD tokenization, 95.7% line test coverage). However, several areas of **technical debt** exist—primarily around record immutability vs container mutability, linear media unlinking in `GedcomTreeContext`, lossy preservation of custom GEDCOM tags, and string pool capacity limits.

---

## 1. Architectural & Data Model Debt

### 1.1 Hybrid Immutability Model (`record` vs `List<T>`)
* **Issue**: Entity models (`PersonRecord`, `FamilyRecord`, `MediaReferenceRecord`, `EventRecord`) are implemented as immutable C# positional `record` types. However, `GedcomParseResult` exposes mutable `List<T>` properties (`Persons`, `Families`, `Media`, `Events`).
* **Impact**:
  * Mutating an entity inside `GedcomTreeContext` requires using C# `with { ... }` expressions and replacing element indices inside `List<T>`.
  * External callers modifying `result.Persons.Add(...)` directly can desynchronize an active `GedcomTreeContext` instance without its knowledge.
* **Recommendation**:
  * Expose `IReadOnlyList<T>` on `GedcomParseResult` and require all structural modifications to go through `GedcomTreeContext`.

---

### 1.2 $O(N)$ Media Search During `GedcomTreeContext` Entity Deletion
* **Issue**: In `GedcomTreeContext.DeletePerson(xref)` and `DeleteFamily(xref)`, cleaning up media links iterates through `_backingResult.Media` in an $O(N)$ linear loop:
  ```csharp
  for (int i = 0; i < _backingResult.Media.Count; i++) {
      var med = _backingResult.Media[i];
      if (med.LinkedXrefIds.Contains(xref)) { ... }
  }
  ```
* **Impact**: While relationship queries are $O(1)$, deleting an entity in a dataset with tens of thousands of media records degrades to $O(N)$ linear scans.
* **Recommendation**:
  * Use the existing `_mediaByEntityId` index dictionary to directly find and update only the media items linked to the deleted entity in $O(1)$ time.

---

### 1.3 Lossy Custom Tag & Submitter (`SUBM`/`SOUR`) Parsing
* **Issue**: `StreamingGedcomParser` streams and recognizes core genealogical entities (`INDI`, `FAM`, `OBJE`). Unrecognized level-0 records (like `SUBM` submitter or `SOUR` repository sources) and custom level-1 vendor extension tags (`_CUSTOM`) are skipped.
* **Impact**:
  * Applications requiring full loss-less round-tripping of custom vendor tags will lose unparsed metadata during re-export.
* **Recommendation**:
  * Introduce an optional `UnparsedNodes` list on `GedcomParseResult` to preserve unrecognized level-0 blocks for full round-trip fidelity.

---

## 2. Memory & Performance Edge-Case Debt

### 2.1 Unbounded `GedcomStringPool` Memory Growth
* **Issue**: `GedcomStringPool` deduplicates strings during parsing. However, the pool uses a growing `List<string>` and `ArrayPool<int>` without an upper bound or clearing policy.
* **Impact**: Parsing thousands of distinct GEDCOM files in a long-running service worker could lead to monotonic memory growth.
* **Recommendation**:
  * Pass a `maxEntries` threshold or implement an explicit `.Clear()` method after each import run to return pooled buffers.

---

### 2.2 Re-allocation of Lookup Maps in `GedcomExportWriter`
* **Issue**: On every call to `GedcomExportWriter.Write()`, temporary lookup dictionaries (`Dictionary<string, List<EventRecord>>` and `Dictionary<string, List<MediaReferenceRecord>>`) are allocated and populated.
* **Impact**: Generates transient heap allocations during high-frequency export calls.
* **Recommendation**:
  * Cache lookup maps or serialize directly when input comes from an indexed `GedcomTreeContext`.

---

## 3. Prioritized Action Plan

| ID | Component | Debt Type | Severity | Effort | Target Fix |
| :--- | :--- | :--- | :---: | :---: | :--- |
| **TD-01** | `GedcomTreeContext` | $O(N)$ Media Cleanup in Deletion | **Medium** | Low | Optimize deletion to use `_mediaByEntityId` index. |
| **TD-02** | `GedcomStringPool` | Unbounded Pool Growth | **Medium** | Low | Add `.Clear()` and max capacity guards. |
| **TD-03** | `GedcomParseResult` | Mutable `List<T>` Desync Risk | **Low** | Medium | Expose `IReadOnlyList<T>` properties. |
| **TD-04** | `StreamingGedcomParser` | Unparsed Vendor Tag Loss | **Low** | Medium | Add optional `CustomNodes` preservation bucket. |
