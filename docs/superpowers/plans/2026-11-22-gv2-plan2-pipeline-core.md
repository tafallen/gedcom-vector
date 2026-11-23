# Gedcom.Vector 2.0 Plan 2: Pipeline Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the new parse pipeline up to, but not including, the typed model: a line reader, a line tokenizer, a structure reader with `CONC`/`CONT` folding and lenient malformed-line handling, diagnostics, the public extension types with their text round-trip API, and a `GedcomDocument` skeleton in which every level-0 record is an `ExtensionRecord`. Every input line is accounted for. The 1.3.2 pipeline is untouched and keeps serving the public API.

**Architecture:** `Stream -> GedcomEncodingDetector -> GedcomLineReader -> GedcomLineTokenizer -> GedcomStructureReader -> GedcomDocumentReader -> GedcomDocument`. The structure reader holds one level-0 record tree at a time and produces raw values. The document reader decides the spec version from `HEAD`, then decodes each record (ANSEL, `@@` escapes), flags pointers, resolves 7.0 `SCHMA` URIs, and wraps each record as an `ExtensionRecord`. The entry point stays `internal` until Plan 6 switches `IGedcomImportAdapter` over; the AOT smoke app reaches it through `InternalsVisibleTo`, so AOT keeps gating the new code.

**Tech Stack:** .NET 10, C# latest, xUnit 2.9, `Xunit.SkippableFact` (new), BenchmarkDotNet 0.15.8, PowerShell 7 scripts.

**Spec:** `docs/superpowers/specs/2026-11-21-gedcom-vector-2-0-design.md`: sections 6.2 steps 1 to 3, 6.3 base types and the `GedcomStructure` text round-trip API, 6.7 (`MaxDiagnostics`), 7 (line accounting, private-corpus rules), 8 (benchmarks). Roadmap: `docs/superpowers/plans/2026-11-21-gv2-roadmap.md` (Plan 2 row and "Carried from Plan 1").

## Global Constraints

- Target `net10.0` only. `TreatWarningsAsErrors=true`; a warning is a bug, never suppressed with `NoWarn` or `#pragma`.
- `IsAotCompatible=true` on the library; zero `IL2xxx`/`IL3xxx`. No reflection or dynamic code in the library. No `Regex` in new code (span code only).
- Library dependencies limited to the existing `Microsoft.Extensions.*` 10.0.x packages.
- Every public API addition goes into `src/Gedcom.Vector/PublicAPI.Unshipped.txt`; `PublicAPI.Shipped.txt` is never edited.
- New public model types: `namespace Gedcom.Vector.Model`, `public sealed class` (or `abstract` for bases), settable properties, `List<T>` collections initialised empty, XML docs on every public member naming the GEDCOM tag where there is one.
- Diagnostics never contain payload values: messages may name tags, xrefs, line numbers and counts only (spec section 7: "never values").
- Do not change 1.3.2 behaviour: `GedcomImportAdapter`, `StreamingGedcomParser`, `GedcomExportWriter` and the existing tests keep passing unchanged, except the shared reader factory extraction in Task 4.
- The private file `C:\Users\tim\Downloads\Methven Ancestry.ged` is used only through the `GEDCOM_VECTOR_PRIVATE_CORPUS` environment variable (a list of file paths since the Plan 2 final fix wave; originally a directory), only locally, never copied, never printed; test failures report xrefs, tag paths and line numbers, never values.
- No em-dashes in any file, comment, commit message or doc.
- Commits use conventional prefixes (`feat:`, `fix:`, `test:`, `build:`, `ci:`, `docs:`, `chore:`) and end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Tests pass and the build is warning-free before every commit. Every regression found gets a test. Test names are PascalCase without underscores (CA1707).
- Benchmarks: after every task that changes code on a measured path, the controller runs `tools/Run-Benchmarks.ps1 -Label "<task>"` on an idle machine and raises any row over +10% in that task's review.

## Review Focus

1. **A text line with no level number** (an embedded newline in a NOTE that lost its `CONT`): its text is appended to the previous structure's value with `\n` and a warning, never dropped. Test in Task 6.
2. **Mixed and lone line endings** (`\r\n`, `\n`, lone `\r`, a final line without a terminator, a line longer than the 64K buffer): every line is read once with the right line number. Test in Task 4.
3. **Whitespace around `CONC`**: 5.5.1 exporters split mid-word and rely on trailing or leading spaces; folding preserves every space exactly. Test in Task 6.
4. **Values that start with `@`**: `@@` escapes decode to one `@`, `@I1@` is a pointer, `@#DJULIAN@ 1700` in 5.5.1 is text, and the text round-trip API re-encodes all three so they parse back identically. Tests in Tasks 7 and 8.
5. **ANSEL diacritics split across a `CONC`** (the combining mark ends one line, its base letter starts the next): decoding happens after folding, so the composed character is correct. Test in Task 7.

---

### Task 1: Harden `tools/Run-Benchmarks.ps1` (carried from Plan 1)

**Files:**
- Modify: `tools/Run-Benchmarks.ps1`

**Interfaces:**
- Produces: a new `-Commit <sha>` parameter; history rows stamp the measured commit and mark a dirty tree.

- [ ] **Step 1: Formatting**: every `-f` and `ToString` that writes a number uses `[cultureinfo]::InvariantCulture`.
- [ ] **Step 2: Commit stamping**: the row's commit is `-Commit` when given; otherwise, for a live run, `git rev-parse --short HEAD`, suffixed `-dirty` when `git status --porcelain` lists tracked changes; with `-ResultsOnly` and no `-Commit`, the script fails with a message asking for `-Commit`.
- [ ] **Step 3: Guards**: `-Job` without `-NoHistory` fails before running anything ("short jobs are too noisy for history.md; add -NoHistory"). A benchmark without a `Memory` section writes `n/a`, not `0 B`. The catch block writes to `[Console]::Error` and then `exit 1` (no terminating `Write-Error`).
- [ ] **Step 4: Verify** with `-ResultsOnly` against a results directory from a short live run (`-NoHistory -Filter '*QueryChildrenFluent' -Job short`), plus one run of each guard, recording the output. No row may be added to `docs/benchmarks/history.md`.
- [ ] **Step 5: Commit** `build: harden Run-Benchmarks commit stamping, culture and guards`

---

### Task 2: Diagnostics

**Files:**
- Create: `src/Gedcom.Vector/Diagnostics/GedcomDiagnostic.cs`, `src/Gedcom.Vector/Diagnostics/GedcomDiagnosticCode.cs`, `src/Gedcom.Vector/Diagnostics/GedcomDiagnosticSeverity.cs`, `src/Gedcom.Vector/Diagnostics/DiagnosticBag.cs`, `src/Gedcom.Vector/Diagnostics/DiagnosticMessages.cs`
- Modify: `src/Gedcom.Vector/PublicAPI.Unshipped.txt`
- Test: `tests/Gedcom.Vector.Tests/Diagnostics/DiagnosticBagTests.cs`

**Interfaces:**
- Produces (public, `namespace Gedcom.Vector.Diagnostics`):
  - `enum GedcomDiagnosticSeverity { Info, Warning, Error }`
  - `enum GedcomDiagnosticCode` with these explicit values: `BlankLinesIgnored = 1001` (Info), `LeadingWhitespace = 1002` (Info), `LineWithoutLevel = 1003` (Warning: text appended to the previous value), `LineWithoutLevelUnplaced = 1004` (Error: no previous structure to append to), `MissingTag = 1005` (Warning), `InvalidTag = 1006` (Warning), `MalformedXref = 1007` (Warning), `XrefOnSubstructure = 1008` (Warning), `LevelSkipped = 1009` (Warning), `ContinuationMisplaced = 1010` (Warning: `CONC`/`CONT` at level 0, with an xref, or under a pointer; kept as an ordinary structure), `ContinuationAfterChildren = 1011` (Info), `MissingHeader = 2001` (Warning), `UnknownVersion = 2002` (Warning), `NonUtf8Encoding = 2003` (Error), `MissingTrailer = 2004` (Warning), `RecordAfterTrailer = 2005` (Warning), `DiagnosticsTruncated = 9001` (Warning).
  - `sealed class GedcomDiagnostic` with constructor `GedcomDiagnostic(GedcomDiagnosticCode code, GedcomDiagnosticSeverity severity, string message, int? line = null, string? recordXref = null, string? tagPath = null)` and get-only properties `Code`, `Severity`, `Message`, `Line`, `RecordXref`, `TagPath` (dot-separated tags from the record, e.g. `INDI.BIRT.DATE`), plus `ToString()` returning `"{Severity} GV{(int)Code} line {Line}: {Message}"` (omit the line part when null).
- Produces (internal): `DiagnosticBag(int maxDiagnostics)` with `GedcomDiagnostic Add(GedcomDiagnosticCode code, int? line = null, string? recordXref = null, string? tagPath = null, string? tag = null)` (severity and message come from `DiagnosticMessages` by code; `tag` may be interpolated into the message, nothing else may), `IReadOnlyList<GedcomDiagnostic> Items`, `int SuppressedCount`. At the cap it appends one `DiagnosticsTruncated` entry and from then on returns that entry without storing new ones.
- `DiagnosticMessages`: one constant template per code, the only place message text lives. The 2003 text is exactly `GEDCOM 7.0 requires UTF-8 encoding. Non-UTF-8 encoding detected.` (the 1.3.2 wording).

- [ ] **Step 1: Write the failing tests**: `AddUsesSeverityAndMessageForCode` (every enum value has a template and a severity, checked by iterating `Enum.GetValues<GedcomDiagnosticCode>()`), `AddStopsAtCapAndAppendsOneTruncationEntry` (cap 3, add 5: `Items.Count == 4`, last is `DiagnosticsTruncated`, `SuppressedCount == 2`), `AddReturnsTruncationEntryAfterCap`, `ToStringOmitsLineWhenNull`.
- [ ] **Step 2: Run** `dotnet test --filter FullyQualifiedName~DiagnosticBagTests`. Expected: compile failure.
- [ ] **Step 3: Implement** the five files and add the public members to `PublicAPI.Unshipped.txt`.
- [ ] **Step 4: Run** the tests, then `dotnet build -c Release`. Expected: PASS, zero warnings.
- [ ] **Step 5: Commit** `feat: add GEDCOM diagnostics with codes, severities and a capped bag`

---

### Task 3: Extension model types and the document skeleton

**Files:**
- Create: `src/Gedcom.Vector/Model/GedcomElement.cs`, `GedcomExtension.cs`, `GedcomStructure.cs`, `ExtensionRecord.cs`, `GedcomDocument.cs` (all under `src/Gedcom.Vector/Model/`)
- Modify: `src/Gedcom.Vector/PublicAPI.Unshipped.txt`
- Test: `tests/Gedcom.Vector.Tests/Model/ModelSkeletonTests.cs`, helper `tests/Gedcom.Vector.Tests/Model/StructureAssert.cs`

**Interfaces:**
- Produces (public, `namespace Gedcom.Vector.Model`), exactly as design 6.3:
  - `abstract class GedcomElement { int? SourceOrder { get; set; }; List<GedcomExtension> Extensions { get; } }`
  - `sealed class GedcomExtension { int? SourceOrder { get; set; }; GedcomStructure Structure { get; set; } }`
  - `sealed class GedcomStructure { string Tag; string? Xref; string? Value; bool ValueIsPointer; string? ExtensionUri; int? SourceLine; List<GedcomStructure> Children { get; } }` (the text methods arrive in Task 8)
  - `sealed class ExtensionRecord : GedcomElement { GedcomStructure Structure { get; set; } }` (level-0 record kept verbatim; `SourceOrder` is its index among level-0 records)
  - `sealed class GedcomDocument { GedcomSpecVersion SourceVersion { get; set; }; List<ExtensionRecord> ExtensionRecords { get; }; List<GedcomDiagnostic> Diagnostics { get; } }`. Plan 3 adds `Header` and the typed record lists; do not add them now.
- Produces (test helper): `StructureAssert.Equal(GedcomStructure expected, GedcomStructure actual, bool ignoreSourceLine = true)` comparing Tag, Xref, Value, ValueIsPointer, ExtensionUri and children recursively, failing with the tag path and line, never values.

- [ ] **Step 1: Write the failing tests**: `CollectionsStartEmpty` (every list on every type is non-null and empty), `GedcomDocumentDefaultsToGedcom551`, `StructureAssertReportsTagPathNotValue` (two structures differing only in a sentinel value: the failure message contains `INDI.NOTE` and not the sentinel).
- [ ] **Step 2: Run** them. Expected: compile failure.
- [ ] **Step 3: Implement** the types with XML docs and the PublicAPI entries.
- [ ] **Step 4: Run** tests and Release build. Expected: PASS, zero warnings.
- [ ] **Step 5: Commit** `feat: add the extension model types and the GedcomDocument skeleton`

---

### Task 4: Line reader

**Files:**
- Create: `src/Gedcom.Vector/Parsing/GedcomLineReader.cs`, `src/Gedcom.Vector/Parsing/GedcomTextReaderFactory.cs`
- Modify: `src/Gedcom.Vector/Parsing/StreamingGedcomParser.cs` (replace its private `CreateReader` with the factory; no other change)
- Test: `tests/Gedcom.Vector.Tests/Parsing/GedcomLineReaderTests.cs`

**Interfaces:**
- Produces (internal):
  - `static class GedcomTextReaderFactory { static StreamReader Create(Stream stream, GedcomEncodingResult encoding) }`: same behaviour as the old `CreateReader` (ANSEL is read as Latin-1 characters and decoded later).
  - `sealed class GedcomLineReader : IDisposable { GedcomLineReader(Stream stream, GedcomEncodingResult encoding); bool TryReadLine(out ReadOnlySpan<char> line, out int lineNumber) }`: `line` excludes its terminator and is valid until the next call; `lineNumber` is 1-based; terminators are `\r\n`, `\n` and a lone `\r`; a final line without a terminator is returned; an empty file returns false at once; the buffer is rented from `ArrayPool<char>` and grows for long lines.

- [ ] **Step 1: Write the failing tests**: `ReadsLfCrlfAndLoneCrAsSeparateLines` (`"a\r\nb\nc\rd"` gives a, b, c, d with numbers 1 to 4), `ReturnsFinalLineWithoutTerminator`, `CountsBlankLines` (`"a\n\nb"` gives a, empty, b), `ReadsLineLongerThanBuffer` (a 200,000-character line), `CrAtBufferBoundaryFollowedByLfIsOneTerminator` (place `\r` as the last char of the first buffer fill), `EmptyStreamReturnsNoLines`, `AnselIsReturnedUndecoded` (byte `0xE2` arrives as `'\u00E2'`).
- [ ] **Step 2: Run** them. Expected: compile failure.
- [ ] **Step 3: Implement**, reusing the old parser's split loop (`IndexOfAny` over `\r` and `\n`, carry the partial line, grow when full). Swap `StreamingGedcomParser.CreateReader` for the factory.
- [ ] **Step 4: Run** the new tests and the whole suite. Expected: PASS (all 1.3.2 tests unchanged).
- [ ] **Step 5: Commit** `feat: add a span line reader shared with the 1.3.2 parser`

---

### Task 5: Line tokenizer

**Files:**
- Create: `src/Gedcom.Vector/Parsing/GedcomLineTokenizer.cs`
- Test: `tests/Gedcom.Vector.Tests/Parsing/GedcomLineTokenizerTests.cs`

**Interfaces:**
- Produces (internal):
  - `enum GedcomLineKind { Structure, Blank, NoLevel, NoTag }`
  - `readonly ref struct GedcomLine { GedcomLineKind Kind; int Level; ReadOnlySpan<char> Xref; ReadOnlySpan<char> Tag; ReadOnlySpan<char> Value; bool HasValue; bool HadLeadingWhitespace; bool XrefMalformed; bool TagInvalid; ReadOnlySpan<char> Text }` (`Text` is the whole line, used for `NoLevel`)
  - `static class GedcomLineTokenizer { static GedcomLine Tokenize(ReadOnlySpan<char> line) }`

Rules (the tests pin each one):
- Leading spaces and tabs are skipped and set `HadLeadingWhitespace`. A line that is empty or only whitespace is `Blank`.
- The level is one or more ASCII digits followed by a space; anything else (including digits followed by a letter, or more than 3 digits) is `NoLevel`.
- After the level, a token starting with `@` is the xref; it ends at the next space. It is malformed unless it is at least 3 characters, ends with `@`, and has no other `@` inside. A malformed xref is still returned verbatim.
- The next token is the tag; if there is none, the line is `NoTag`. `TagInvalid` when the tag has a character outside `A-Z a-z 0-9 _`.
- The value is everything after exactly one space following the tag, verbatim, including further leading and trailing spaces. `HasValue` is false when nothing follows the tag and when only the single delimiter space follows it.

- [ ] **Step 1: Write the failing tests**: one `[Theory]` per rule group: `TokenizesLevelXrefTagValue` (`"0 @I1@ INDI"`, `"1 NAME John /Doe/"`, `"2 CONC  two leading"` keeps `" two leading"`, `"1 NOTE trailing  "` keeps both trailing spaces), `TreatsWhitespaceOnlyAsBlank`, `SkipsLeadingWhitespace`, `ReportsNoLevel` (`"Smith family notes"`, `"12abc X"`, `"1234 TAG"`), `ReportsNoTag` (`"1"`, `"1 "`, `"0 @I1@"`), `FlagsMalformedXref` (`"0 @I1 INDI"`, `"0 @@ INDI"`), `FlagsInvalidTag` (`"1 BIRT-X"`), `EmptyValueAfterDelimiterHasNoValue` (`"1 BIRT "`).
- [ ] **Step 2: Run** them. Expected: compile failure.
- [ ] **Step 3: Implement** with span operations only (no `Regex`, no allocation).
- [ ] **Step 4: Run** tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add a span GEDCOM line tokenizer`

---

### Task 6: Structure reader with folding, leniency and line accounting

**Files:**
- Create: `src/Gedcom.Vector/Parsing/GedcomStructureReader.cs`, `src/Gedcom.Vector/Parsing/LineAccounting.cs`, `src/Gedcom.Vector/Parsing/GedcomValueCodec.cs` (`IsPointer` only; Task 7 adds `Decode` and `Encode`)
- Test: `tests/Gedcom.Vector.Tests/Parsing/GedcomStructureReaderTests.cs`, `tests/Gedcom.Vector.Tests/Parsing/GedcomValueCodecTests.cs`

**Interfaces:**
- Consumes: `GedcomLineReader`, `GedcomLineTokenizer`, `DiagnosticBag`, `GedcomStringPool` (existing, for tags and xrefs).
- Produces (internal):
  - `enum LineDispositionKind { Structure, Folded, Diagnostic }`; `readonly record struct LineDisposition(LineDispositionKind Kind, GedcomStructure? Structure, GedcomDiagnostic? Diagnostic)`
  - `sealed class LineAccounting { void Record(int lineNumber, LineDisposition disposition); LineDisposition? this[int lineNumber]; int LineCount }`: `Record` throws `InvalidOperationException` if the line already has a disposition (a line must be accounted exactly once).
  - `sealed class GedcomStructureReader { GedcomStructureReader(GedcomLineReader lines, GedcomStringPool pool, DiagnosticBag diagnostics, LineAccounting? accounting); GedcomStructure? ReadRecord() }`: returns the next complete level-0 tree with raw (undecoded) values and `SourceLine` set, or null at the end.

Rules (each pinned by a test):
- **Folding:** `CONT` appends `\n` plus its value (empty when none); `CONC` appends its value; spaces are kept exactly. The target is the open structure at the `CONC`/`CONT` line's level minus one. If that structure already has children, fold anyway and add `ContinuationAfterChildren`. The line's disposition is `Folded` with the target structure.
- **Misplaced continuation:** `CONC`/`CONT` at level 0, with an xref, or whose target's raw value satisfies `GedcomValueCodec.IsPointer` is kept as an ordinary structure with `ContinuationMisplaced`.
- **`GedcomValueCodec.IsPointer(ReadOnlySpan<char> raw)`:** true when the value starts and ends with `@`, is at least 3 characters, its second character is neither `@` nor `#`, and it contains no other `@` and no whitespace (`@I1@` and `@VOID@` yes; `@I 1@`, `@#DJULIAN@`, `@@x` no). Test: `IsPointerRecognisesPointers` `[Theory]` over those rows.
- **Level skip:** a level more than one deeper than the deepest open structure is treated as one deeper, with `LevelSkipped`. A file whose first line is not level 0 starts a record at level 0 the same way.
- **No level:** the whole line text is appended with `\n` to the value of the structure that received the previous line (the line's own structure, or the fold target), with `LineWithoutLevel`, disposition `Folded`. Before any structure exists: `LineWithoutLevelUnplaced`, disposition `Diagnostic`.
- **Blank:** the first blank line adds one `BlankLinesIgnored`; every blank line's disposition is `Diagnostic` pointing at that single entry.
- **No tag:** `MissingTag`, disposition `Diagnostic` (the line carries no data beyond its level and xref).
- **Other flags:** `HadLeadingWhitespace` adds `LeadingWhitespace` (once per file); `XrefMalformed` adds `MalformedXref`; `TagInvalid` adds `InvalidTag`; an xref on a line above level 0 adds `XrefOnSubstructure`. These lines still become structures.
- Every diagnostic carries the line number, the current record's xref and the tag path.

- [ ] **Step 1: Write the failing tests**: `BuildsNestedRecordTrees`, `FoldsContAndConcPreservingSpaces` (5.5.1 mid-word split `"1 NOTE abc"`/`"2 CONC def "`/`"2 CONC  ghi"`/`"2 CONT"`/`"2 CONT x"` gives `"abcdef  ghi\n\nx"`), `FoldsContAfterChildrenWithInfo`, `KeepsMisplacedContinuationAsStructure` (level 0, with xref, under a pointer), `ReparentsSkippedLevel`, `StartsRecordWhenFirstLineIsNotLevelZero`, `AppendsLineWithoutLevelToPreviousValue` (Review Focus 1), `ReportsUnplacedLineWithoutLevel`, `AggregatesBlankLinesIntoOneDiagnostic`, `RecordsEachLineExactlyOnce` (accounting over a 20-line sample covering every rule: every line 1..20 has exactly one disposition), `LineAccountingRejectsDoubleRecord`, `DiagnosticsCarryLineXrefAndTagPath`.
- [ ] **Step 2: Run** them. Expected: compile failure.
- [ ] **Step 3: Implement** with a level stack (`List<GedcomStructure>` indexed by level). Pre-size child lists only when the count is known; no LINQ.
- [ ] **Step 4: Run** tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add the structure reader with CONC/CONT folding and line accounting`

---

### Task 7: Document reader, decoding and the reading benchmark

**Files:**
- Create: `src/Gedcom.Vector/Parsing/GedcomDocumentReader.cs`, `src/Gedcom.Vector/Parsing/GedcomReadOptions.cs`
- Modify: `src/Gedcom.Vector/Parsing/GedcomValueCodec.cs` (add `Decode`, `Encode`), `src/Gedcom.Vector/Gedcom.Vector.csproj` (`InternalsVisibleTo Gedcom.Vector.Benchmarks`), `tests/Gedcom.Vector.Benchmarks/ParserBenchmarks.cs` (add one method only), `docs/benchmarks/baseline.json`
- Test: `tests/Gedcom.Vector.Tests/Parsing/GedcomDocumentReaderTests.cs`, `tests/Gedcom.Vector.Tests/Parsing/GedcomValueCodecTests.cs`

**Interfaces:**
- Produces (internal):
  - `sealed class GedcomReadOptions { int MaxDiagnostics { get; init; } = 10_000; bool RecordLineAccounting { get; init; } }`
  - `sealed record GedcomReadResult(GedcomDocument Document, LineAccounting? Accounting)`
  - `static class GedcomDocumentReader { static GedcomReadResult Read(Stream stream, GedcomReadOptions? options = null) }`: requires a seekable stream (`ArgumentException`, same message as `GedcomImportAdapter.Parse`); calls `GedcomEncodingDetector.Detect`.
  - `GedcomValueCodec` gains `static string? Decode(string? raw, GedcomSpecVersion version)` and `static string? Encode(string? value, bool isPointer, GedcomSpecVersion version)` (Task 8 uses `Encode`).
- Produces (benchmark): `[Benchmark] public GedcomDocument MeasureReadingStructures()` reading `_gedcomBytes` through `GedcomDocumentReader`.

Rules:
- **Version:** from `HEAD.GEDC.VERS`: a value starting `7.` is `Gedcom70`; any value starting `5.` is `Gedcom551`; missing or other values give `Gedcom551` plus `UnknownVersion`. A first record that is not `HEAD` gives `MissingHeader` and `Gedcom551`.
- **Encoding check:** for 7.0, `IsAnsel || DeclaredUnrecognized || Encoding is not UTF8Encoding` adds `NonUtf8Encoding` (same rule as `StreamingGedcomParser`).
- **Decoding order per record:** ANSEL decode of each value first (so a diacritic split by `CONC` composes correctly), then escapes, then pointer flags. Tags and xrefs are ASCII and are not decoded.
- **Pointer:** values for which `GedcomValueCodec.IsPointer` holds are stored verbatim with `ValueIsPointer = true`.
- **Escapes (both versions):** a non-pointer value starting with `@@` loses its first `@`. Nothing else is rewritten: mid-value `@@` and 5.5.1 escape sequences such as `@#DJULIAN@` stay verbatim. `Encode` is the inverse: a non-pointer value starting with `@` gains a leading `@`, except a 5.5.1 value starting `@#`.
- **SCHMA:** for 7.0, every `HEAD.SCHMA.TAG` value `"_TAG uri"` maps that tag to its URI; every structure in the document whose tag starts with `_` and is in the map gets `ExtensionUri`.
- **Records:** every level-0 tree becomes an `ExtensionRecord` with `SourceOrder` = its index. No `TRLR` gives `MissingTrailer`; a record after `TRLR` gives `RecordAfterTrailer` and is kept.
- Diagnostics are copied into `GedcomDocument.Diagnostics` in the order raised.

- [ ] **Step 1: Write the failing tests**: codec `[Theory]` rows `DecodesEscapes` (Review Focus 4: `@@x` to `@x`; `a@@b` unchanged; `@#DJULIAN@ 1700` unchanged in 5.5.1; pointers `@I1@` and `@VOID@` unchanged), `EncodeIsInverseOfDecode` over the same rows for both versions. Reader: `DetectsVersionFromHead` (`7.0`, `7.0.18`, `5.5.1`, `5.5`, missing), `ReportsMissingHeaderAndTrailer`, `KeepsRecordAfterTrailer`, `ReportsNonUtf8For70` (ANSI and unrecognised `CHAR`), `DecodesAnselAfterFolding` (Review Focus 5: bytes `0xE2` at the end of one line and `e` after `CONC` on the next give `"é"`), `ResolvesSchmaExtensionUris`, `KeepsEveryLevelZeroRecordInOrder`, `ReadsEverySmokeCorpusFile` (each `tests/corpus/smoke/*.ged`: zero Error diagnostics, and the record count equals the number of level-0 lines in the file's decoded text, located like `SmokeCorpusTests`), `RejectsNonSeekableStream`, `CapsDiagnostics` (`MaxDiagnostics = 5` over 20 bad lines).
- [ ] **Step 2: Run** them. Expected: compile failure.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the whole suite and the Release build. Expected: PASS, zero warnings.
- [ ] **Step 5: Add `MeasureReadingStructures`** to `ParserBenchmarks` (no change to any existing method or to `Setup`), run `pwsh tools/Run-Benchmarks.ps1 -Label "Plan 2 Task 7: reading benchmark added"` on an idle machine, and add `MeasureReadingStructures` to `baseline.json` with that run's mean as `meanNs` and `gateReferenceNs` (it is not a GV-07 gate; it guards the new pipeline within 2.0 work), keeping the existing entries unchanged.
- [ ] **Step 6: Commit** `feat: add the document reader with version detection, decoding and SCHMA` then `test: add the structure reading benchmark and its reference`

---

### Task 8: `GedcomStructure` text round-trip API

**Files:**
- Modify: `src/Gedcom.Vector/Model/GedcomStructure.cs`, `src/Gedcom.Vector/PublicAPI.Unshipped.txt`
- Test: `tests/Gedcom.Vector.Tests/Model/GedcomStructureTextTests.cs`

**Interfaces:**
- Consumes: `GedcomValueCodec.Encode`, `GedcomLineReader`, `GedcomStructureReader`.
- Produces (public, design 6.3):
  - `string ToGedcomText(GedcomSpecVersion version, int baseLevel = 0)`: one line per structure, each ending `\n`, `CONT` for every `\n` in a value, no `CONC` folding (line length is the writer's concern, Plan 5), values encoded with `GedcomValueCodec.Encode`. `ExtensionUri` and `SourceLine` are not written.
  - `static GedcomStructure ParseGedcomText(string text)` (7.0 rules) and `static GedcomStructure ParseGedcomText(string text, GedcomSpecVersion version)`: strict: the text must hold exactly one top-level structure at its first line's level; any Warning or Error diagnostic throws `FormatException` naming the code and line. `SourceLine` counts from 1 within the text.
- Contract (the test): for any structure `s` produced by `GedcomDocumentReader`, `ParseGedcomText(s.ToGedcomText(v, b), v)` equals `s` under `StructureAssert.Equal` ignoring `SourceLine` and `ExtensionUri`, for both versions and base levels 0 and 3.

- [ ] **Step 1: Write the failing tests**: `RoundTripsEverySmokeCorpusRecord` (every record of every smoke file, both versions where the file's own version is used for decoding), `RoundTripsMultilineValues` (values with `\n`, trailing spaces, leading spaces), `RoundTripsAtSignValues` (Review Focus 4 rows), `HonoursBaseLevel`, `ParseRejectsTwoTopLevelStructures`, `ParseRejectsMalformedText`.
- [ ] **Step 2: Run** them. Expected: FAIL (methods missing).
- [ ] **Step 3: Implement**, reusing the reader classes over a `StringReader`-backed stream path or a char-span overload of `GedcomLineReader` (add an internal constructor taking `TextReader` if needed).
- [ ] **Step 4: Run** tests and the Release build. Expected: PASS, zero warnings.
- [ ] **Step 5: Commit** `feat: add the GedcomStructure text round-trip API`

---

### Task 9: Line accounting and privacy across real files

**Files:**
- Modify: `Directory.Packages.props` (add `Xunit.SkippableFact`, latest stable), `tests/Gedcom.Vector.Tests/Gedcom.Vector.Tests.csproj`, `.gitignore`
- Create: `tests/Gedcom.Vector.Tests/Corpus/LineAccountingTests.cs`, `tests/Gedcom.Vector.Tests/Corpus/PrivateCorpus.cs`, `tests/Gedcom.Vector.Tests/Diagnostics/DiagnosticPrivacyTests.cs`

**Interfaces:**
- Produces (test helper): `PrivateCorpus.Files()` returns the `.ged` files in the directory named by `GEDCOM_VECTOR_PRIVATE_CORPUS`, or calls `Skip.If(true, ...)` when the variable is unset or empty. Failure messages from every corpus test name file name, line number, record xref and tag path only.

- [ ] **Step 1: Write the tests**: `AccountsForEverySmokeLine` (`RecordLineAccounting = true`: every line 1..LineCount has exactly one disposition; `Structure` and `Folded` dispositions point at a structure reachable from the document; `Diagnostic` dispositions point at a diagnostic in the document), `[SkippableFact] AccountsForEveryPrivateCorpusLine` (same assertions, plus zero `Error` diagnostics; on failure report at most the first 20 offending line numbers with their disposition kind), `DiagnosticsNeverContainValues` (a file whose every value is the sentinel `PRIVATE-SENTINEL-7f3a` and that triggers every line-level code: no diagnostic `Message`, `RecordXref` or `TagPath` contains the sentinel).
- [ ] **Step 2: Run** with the variable unset: the private test shows as Skipped. Then locally, once, set `GEDCOM_VECTOR_PRIVATE_CORPUS=C:\Users\tim\Downloads` and run only that test; record pass/fail and counts only. Fix any defect found with its own failing test first.
- [ ] **Step 3: Add** `tests/corpus/private/` and `*.private.ged` to `.gitignore`.
- [ ] **Step 4: Run** the suite and the Release build. Expected: PASS (one more Skipped in CI), zero warnings.
- [ ] **Step 5: Commit** `test: account for every input line across the smoke and private corpora`

---

### Task 10: AOT gate for the new pipeline

**Files:**
- Modify: `src/Gedcom.Vector/Gedcom.Vector.csproj` (`InternalsVisibleTo Gedcom.Vector.AotSmoke`), `tests/Gedcom.Vector.AotSmoke/Program.cs`, `tools/Compare-AotJit.ps1`

**Interfaces:**
- Produces: `Gedcom.Vector.AotSmoke <input> <output> --structures`: reads `.ged` input through `GedcomDocumentReader` (`.gdz` gives exit 4 in this mode), writes every record's `ToGedcomText(document.SourceVersion)` to `<output>`, and prints `SourceVersion`, `Records`, `Structures` (total node count), `Diagnostics`, then one `Diagnostic=GV<code> line <n>` per diagnostic, in that order. Existing modes are unchanged.
- `Compare-AotJit.ps1` adds the `--structures` run for every `.ged` corpus file (so 5 more comparisons on the smoke corpus).

- [ ] **Step 1: Implement** the mode and the script change.
- [ ] **Step 2: Run** `pwsh tools/Compare-AotJit.ps1 -Rid win-x64` (with the Visual Studio Installer directory on PATH). Expected: all comparisons match, no IL warnings. Then once with `-Corpus "C:\Users\tim\Downloads" -Include "Methven Ancestry.ged"`. Expected: match. Do not commit output.
- [ ] **Step 3: Build and test** the solution. Expected: zero warnings, all pass.
- [ ] **Step 4: Commit** `ci: gate the new reading pipeline in the AOT comparison`

---

### Task 11: Plan 2 close-out

- [ ] **Step 1: Full verification**: `dotnet build -c Release` (0 warnings), `dotnet test -c Release` (all pass), `pwsh tools/Compare-AotJit.ps1 -Rid win-x64` (all match), `pwsh tools/Run-Benchmarks.ps1 -Label "Plan 2 close-out"` (no existing row over +10% that was not already over before Plan 2).
- [ ] **Step 2: Grep the branch diff for em-dashes**: `git diff 2f9fbe6 --name-only | % { Select-String -Path $_ -Pattern ([char]0x2014) }` returns nothing.
- [ ] **Step 3: Whole-plan code review** (superpowers:requesting-code-review) against this plan and the design; fix findings with tests.
- [ ] **Step 4: Append** any Plan 2 behaviour change visible to 1.3.2 users to `docs/release-notes/2.0.0.md` (expected: none, since the new pipeline is internal), mark Plan 2 done in the roadmap, and commit `docs: mark plan 2 complete`.
