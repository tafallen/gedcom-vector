# Documentation Strategy & Improvements Plan

This document outlines the proposed roadmap to make `Gedcom.Vector` the best-documented, easiest-to-use GEDCOM library for .NET. 

---

## 1. Developer Experience & IntelliSense (XML Documentation)

### Current Gap
- While some interfaces (like `IGedcomImportAdapter`) have partial XML comments, most public models (`GedcomParseResult`, `PersonRecord`, `FamilyRecord`, `EventRecord`, `MediaReferenceRecord`, `PersonSex`, `FamTreeEventType`, and `GedcomImportOptions`) and the `IGedcomExportWriter` interface lack XML comments entirely.
- Although `GenerateDocumentationFile` is enabled in the `.csproj` file, compiler warnings for missing public XML comments (`CS1591`) are suppressed.

### Action Plan
- **Implement complete XML comments**: Add triple-slash (`///`) XML documentation to all public types, methods, and properties.
- **Enable compiler checks**: Remove `CS1591` from the `<NoWarn>` property in `Gedcom.Vector.csproj` so the compiler warns us if new public types are added without documentation.
- **Provide clear model mapping guidance**: Document exactly how properties like `XrefId` format, `PersonSex` mapping defaults, and `FamTreeEventType` tags behave.

---

## 2. README Expansion (Comprehensive Reference)

### Current Gap
- The `README.md` provides a fast "Quick Start", but leaves developers guessing about record structures, error handling, or performance characteristics under different encoding scenarios.

### Action Plan
- **Detailed API Usage Section**: Add code snippets showing how to inspect:
  - **Individuals**: Accessing birth/death dates and places, sex, and family associations.
  - **Families**: Resolving husband, wife, and child Xref associations.
  - **Events**: Linking events to individuals via `PersonXrefId` and matching custom event types.
- **Robust Error Handling Guide**: Demonstrate how to check `GedcomParseResult.Errors` and log/handle parsing failures gracefully.
- **Streaming Export Examples**: Document the new `Write` and `WriteAsync` stream-based methods to explain how to serialize large GEDCOM files with low memory consumption.

---

## 3. Library Architecture Guide (`docs/architecture.md`)

### Current Gap
- There is no explanation of the internal parsing pipeline, making it harder for external developers to contribute or debug issues.

### Action Plan
- **Explain the Processing Pipeline**: Document the transition from `Stream` to character lines, the streaming tokenizer (`GedcomLexer`), the hierarchical builder (`GedcomTreeBuilder`), and the lightweight mappers.
- **Visual Diagram**: Include a Mermaid flow diagram showing how bytes flow from the `Stream` to mapped records.

---

## 4. Lightweight Sample Console Application (`samples/`)

### Current Gap
- Developers have to set up their own project from scratch to experiment with the library.

### Action Plan
- Create a simple, self-contained project at `samples/Gedcom.Vector.Sample/` that reads a sample file, prints family trees to the console, and exports a subset of the tree to a new file using the stream APIs.
