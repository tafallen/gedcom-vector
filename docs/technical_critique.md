# Gedcom.Vector Technical Critique and Architectural Review

This document provides a deep technical review and architectural critique of the `Gedcom.Vector` library. It identifies performance bottlenecks, memory allocation hot spots, API inconsistencies, and other technical debt, proposing concrete remedies.

---

## 1. Key Performance & Allocation Bottlenecks

### A. Heavy Linq & Closure Allocations in `GedcomNode` Children Operations
- **Location**: [GedcomNode.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/Parsing/GedcomNode.cs) (Lines 23–25)
- **Problem**: 
  - `Child(string tag)` uses `Children.FirstOrDefault(c => c.Tag == tag)`. The lambda `c => c.Tag == tag` captures the `tag` parameter, creating a transient closure object and a delegate instance on *every* invocation.
  - `ChildrenWithTag(string tag)` uses `Children.Where(c => c.Tag == tag)`, which also allocates a closure, delegate, and an enumerator state machine.
- **Impact**: In a large GEDCOM file with hundreds of thousands of lines, this results in millions of temporary allocations for closures and delegates during entity mapping.

### B. High String Allocation rate in Lexer (`GedcomLexer`)
- **Location**: [GedcomLexer.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/Parsing/GedcomLexer.cs) (Lines 120, 132, 141, 142)
- **Problem**:
  - `TryParseLine` calls `.ToString()` on the slices of the parsed character span to extract `xref`, `tag`, and `value`.
  - GEDCOM tags are highly repetitive (e.g. `INDI`, `NAME`, `SEX`, `BIRT`, `DATE`, `PLAC`, `FAMS`, `FAM`, `HUSB`, `WIFE`, `MARR`, `CHIL`, `OBJE`, `FILE`, `FORM`, `TITL`, `TRLR`). Re-allocating the same tag string for every line is extremely wasteful.
- **Impact**: Generates significant Gen0 GC pressure.

### C. Dictionary Lookup & List Allocations in `AnselDecoder`
- **Location**: [AnselDecoder.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/Parsing/AnselDecoder.cs) (Lines 12, 34, 66)
- **Problem**:
  - The decoder utilizes `Dictionary<byte, char>` lookup tables for `CombiningMarks` and `SpacingCharacters`. While lookup is technically $O(1)$, dictionary hashing and bucket traversal have non-trivial CPU overhead compared to direct array index lookups.
  - The decoder allocates `new List<char>()` for `pendingMarks` on every line that contains diacritics.
- **Impact**: Unnecessary memory allocation and CPU cycles per decoded line.

### D. Extreme Allocation and LINQ Overhead in `GedcomExportWriter`
- **Location**: [GedcomExportWriter.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/GedcomExportWriter.cs) (Lines 30–47)
- **Problem**:
  - The export writer uses nested LINQ chains (`GroupBy`, `SelectMany`, `ToDictionary`, `ToList`, `Select`) to structure the data (events, spouses, children, media) before writing.
  - For `familiesAsSpouse`, it creates a transient array `new[] { f.HusbandXref, f.WifeXref }` for *every* family record.
  - It relies entirely on string interpolation (e.g. `$"0 {person.XrefId} INDI\n"`), creating a massive number of short-lived strings.
- **Impact**: Extremely high memory allocations (172 KB for just 100 people and 50 families), which translates to gigabytes of allocations when exporting large trees.

---

## 2. API & Documentation Inconsistencies

### A. Missing `WriteAsync` and `Stream` / `TextWriter` Export Overloads
- **Location**: [IGedcomExportWriter.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/IGedcomExportWriter.cs) and [GedcomExportWriter.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/GedcomExportWriter.cs)
- **Problem**:
  - The README quick-start suggests:
    ```csharp
    public async Task ExportAsync(GedcomParseResult data, Stream output)
    {
        await writer.WriteAsync(data, output);
    }
    ```
    However, `IGedcomExportWriter` only defines `string Write(GedcomParseResult parseResult)`. The `WriteAsync` method does not exist in the code at all.
  - Restricting export to returning a single `string` forces the entire file to be loaded in memory. For large family trees, this can easily trigger `OutOfMemoryException`.
- **Impact**: Documentation is incorrect; missing streaming capabilities limits usability for large datasets.

---

## 3. Proposed Strategy and Architectural Changes

1. **Implement Direct Array Lookup for Ansel**:
   - Replace the `Dictionary<byte, char>` lookup tables in `AnselDecoder` with flat `char[256]` arrays.
   - Replace `List<char>` with a lightweight, non-allocating struct `PendingMarks` stored on the stack.

2. **Optimize `GedcomNode` Children Access**:
   - Rewrite `Child(string tag)` to use a simple loop over the `Children` list.
   - Refactor consumers (like `FamilyMapper`, `EventMapper`, and `ExtractMediaLinks`) to iterate over the `Children` list directly and match tags, avoiding `ChildrenWithTag` and LINQ allocations.

3. **Pool and Intern Common GEDCOM Strings**:
   - Implement tag interning in `GedcomLexer.TryParseLine`. If the tag matches a known GEDCOM tag (like `INDI`, `NAME`, `BIRT`), return a static string literal instead of calling `ToString()`.
   - Optionally, implement a short-lived `string` deduplication dictionary for `XrefId`s in `GedcomImportAdapter` to ensure references are shared.

4. **Rewrite `GedcomExportWriter` using Loops & Dictionary Cache**:
   - Avoid LINQ structures entirely in the export path. Populate dictionaries using custom loops.
   - Implement `Write` and `WriteAsync` overloads targeting `Stream` (via `StreamWriter`), and make the existing string-returning `Write` call them under the hood to ensure full backward compatibility.
