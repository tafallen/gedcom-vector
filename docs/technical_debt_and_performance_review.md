# Technical Debt and Performance Review

This document outlines key technical debt areas and performance enhancement opportunities in the `Gedcom.Vector` library.

---

## 1. Technical Debt

### Stream Seekability Assumptions
- **Location**: [GedcomImportAdapter.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/GedcomImportAdapter.cs) (Line 22) and [GedcomEncodingDetector.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/GedcomEncodingDetector.cs) (Line 27)
- **Issue**: The library assumes the incoming `Stream` is seekable. It accesses `gedcomFile.Length` and sets `stream.Position = 0`.
- **Impact**: If a client passes a non-seekable stream (e.g., `NetworkStream` from an HTTP upload or `GZipStream`), the parser will crash with a `NotSupportedException`.
- **Remediation**: Check `stream.CanSeek`. If false, wrap the stream in a `MemoryStream` or buffer the initial segment before reading.

### Dead Code
- **Location**: [GedcomLexer.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/Parsing/GedcomLexer.cs) (Lines 61–71)
- **Issue**: The `AppendContinuation` method is defined but never called anywhere in the codebase.
- **Remediation**: Delete the unused helper method to clean up the code.

### Inefficient Ansel Read Path
- **Location**: [GedcomImportAdapter.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/GedcomImportAdapter.cs) (Lines 97–103)
- **Issue**: When processing ANSEL files, the adapter reads a line using `Encoding.Latin1` (bytes to string), immediately encodes it back to bytes using `Encoding.Latin1.GetBytes(rawLine)`, and then decodes it using `AnselDecoder.Decode(bytes)`.
- **Impact**: Double memory allocations and unnecessary conversions (byte -> string -> byte -> string) on every single line of the file.
- **Remediation**: Read raw bytes directly from the stream using a byte-oriented reader or `ReadOnlySpan<byte>` split-by-line helper, then pass the byte slice directly to `AnselDecoder.Decode`.

---

## 2. Performance Enhancement Opportunities

### Replace Regex Line Parsing with Span-Based Parsing
- **Location**: [GedcomLexer.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/Parsing/GedcomLexer.cs) (Lines 7–9)
- **Opportunity**: The lexer uses a compiled regex (`LinePattern`) for every single line of the GEDCOM file.
- **Impact**: Regex execution allocates a significant number of internal matching/group objects, which represents a large portion of the `1.32 MB` allocated during parsing in the benchmarks.
- **Remediation**: Since GEDCOM lines have a highly structured format (`LEVEL [XREF] TAG [VALUE]`), they can be parsed without regex using `ReadOnlySpan<char>` and index slicing. This will drop allocations close to zero for the lexing step.

### Optimizing `AnselDecoder` List Allocation
- **Location**: [AnselDecoder.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/Parsing/AnselDecoder.cs) (Line 66)
- **Opportunity**: The decoder allocates `new List<char>()` for `pendingMarks` on every call to `Decode()`.
- **Impact**: Constant list instantiation per line.
- **Remediation**: Replace the list with a simple `char` variable (since multiple combining characters are extremely rare in practice) or utilize a pooled array / `stackalloc` span.

### Avoid String Concatenation for `CONC` and `CONT`
- **Location**: [GedcomLexer.cs](file:///c:/repos/gedcom-vector/src/Gedcom.Vector/Parsing/GedcomLexer.cs) (Line 42)
- **Opportunity**: Text continuation tags (`CONC` and `CONT`) use string concatenation (`previous.Value + separator + value`).
- **Impact**: If a long description or note span multiple lines, it creates numerous temporary string objects.
- **Remediation**: Store the pieces in a list of strings or use a pooled `StringBuilder` internally, and only combine them into the final string at the end of the node processing step.
