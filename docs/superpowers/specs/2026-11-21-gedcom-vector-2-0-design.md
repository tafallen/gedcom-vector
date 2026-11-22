# Gedcom.Vector 2.0.0 Design

Status: approved by the user, 2026-11-21.

User decisions recorded on approval:

- **Validator:** ArmidaleSoftware `GedValidate` is accepted as "the FamilySearch validator" for GV-05.
- **Performance:** GV-07 is priority S. A regression beyond 10% that remains after an optimisation pass is recorded with its measured cost in the release notes and does not block 2.0.0. Only Gedcom.Vector's own benchmarks count.
- **Corpus fallback:** if public 5.5.1 files are not redistributable, synthetic trees exported from real programs (Gramps and free editions of others), committed with their origin recorded, count as real-world files from several programs.
Spec: `tafallen/famtree-desktop` `docs/requirements.md` section 12a, requirements GV-01 to GV-07 (quoted IDs below refer to it).

## 1. Intent

Gedcom.Vector today parses and writes GEDCOM 5.5.1 and 7.0 at the line level, but maps only a lossy subset (one name, birth/death, nine event types with date and place, husband/wife/children/marriage) into a positional model and silently drops everything else inside `INDI` and `FAM`. That loss is why the FAMTree server import is lossy, and it blocks the desktop application's import (IMP-04, IMP-05) and export (EXP-03).

2.0.0 makes the library the single, faithful GEDCOM implementation for both FAMTree products:

- a typed model of everything in GEDCOM 5.5.1 and 7.0 (decided: typed model, not a generic tree);
- parsed dates with sortable ranges;
- conversion between versions with every loss reported;
- verbatim retention of anything unmodelled, at any level, re-emitted in place;
- nothing discarded silently;
- Native AOT and trim compatible, on .NET 10.

The desktop application's SQLite schema is derived from this model, so the model's shape is the most consequential decision here and is specified in detail (section 6).

## 2. Goals, non-goals, anti-goals

**Goals:** GV-01 to GV-07 exactly as worded in the requirements.

**Non-goals:**

- GEDCOM 5.5 (pre-5.5.1), 5.3 and older, GEDCOM X, and the 7.1 working draft. Older 5.x files are read leniently by the 5.5.1 path (most real "5.5" exports are 5.5.1-compatible); constructs that are invalid in 5.5.1 become extensions plus diagnostics, never errors that abort a parse.
- Multi-targeting (`netstandard2.x`, `net8.0`). `design_proposal.md` section 11 decided everything moves to .NET 10; `docs/multi_targeting_architecture.md` discusses netstandard, not .NET 8, so nothing argues for `net8.0;net10.0`. The FAMTree server must retarget to .NET 10 before taking 2.0 (already filed as server tech debt).
- Byte-identical reproduction of arbitrary input files. GV-05 requires import, export, re-import to be identical *as a model*; the writer's own output is required to be stable (export of a re-import is byte-identical to the first export), but `CONC` split points, line endings and whitespace in the original are not preserved.
- A compatibility shim for the 1.x positional records. Release notes give the mapping instead (section 10).
- Publishing to NuGet or pushing; the work stays on branch `feature/v2.0` until the user asks.

**Anti-goals (failure even if it "works"):**

- Any input line that ends up nowhere (GV-03a).
- The private corpus file (`Methven Ancestry.ged`, contains PII) or any of its content reaching the repository, CI logs or test output.
- Reflection or dynamic code anywhere in the library (breaks GV-06).
- A benchmark regression beyond 10% hidden by changing the benchmark (GV-07).

## 3. Constraints

- `net10.0` only, C# latest, nullable enabled, `TreatWarningsAsErrors`, `IsAotCompatible=true` (turns on the trim, AOT and single-file analyzers).
- Dependencies limited to the existing `Microsoft.Extensions.*` abstractions, bumped to 10.0.x.
- Public repository: test data must be redistributable or fetched at CI time with a pinned hash.
- Project rules: no em-dashes anywhere; conventional commit prefixes; commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; build warnings are bugs; tests pass before every commit; every regression gets a test.

## 4. Current state (1.3.2, commit `62bad41`)

- `GedcomImportAdapter` detects encoding (`GedcomEncodingDetector`, ANSEL via `AnselDecoder`), then calls `StreamingGedcomParser`, a flat char-level state machine. `Utf8GedcomParser` exists but nothing calls it (dead code). There is no node tree.
- Model: positional records `PersonRecord`, `FamilyRecord`, `EventRecord` (+ `FamTreeEventType`, a FAMTree-specific enum), `MediaReferenceRecord`, `UnparsedRecord` (top-level only), collected in `GedcomParseResult` with `List<string> Errors`.
- `GedcomExportWriter` writes 5.5.1 or 7.0 from that model; `GedcomTreeContext` indexes and mutates it; `Builder/` offers a fluent builder; `Gedzip/GedzipAdapter` reads and writes `.gdz`.
- `GedcomServiceCollectionExtensions.AddGedcomImport` uses reflection-based `Bind`.
- Benchmarks: `tests/Gedcom.Vector.Benchmarks/ParserBenchmarks.cs` (synthetic 4,000 people, 2,000 families).
- CI: build and test on ubuntu, .NET 8.

## 5. Approaches considered

| Approach | Trade-off | Outcome |
|---|---|---|
| **A. Typed model** of every record and structure, each node carrying its own unmodelled extensions in place | Most code; maps directly onto SQLite tables and typed server code | **Chosen by the user** |
| B. Generic structure tree as source of truth, typed views generated over it | Lossless by construction, least code, but consumers and the desktop schema work through views and the schema leans generic | Rejected: the desktop schema needs typed tables |
| C. Typed core (people, families, events) plus generic tree for the rest | Less code than A | Rejected: fails GV-02, which requires every structure modelled |

Within A, two further calls (mine, stated to the user):

- **One version-neutral model**, using 7.0's vocabulary and structure, plus explicit members for 5.5.1-only constructs. The writer converts at export. Two per-version models would double the surface and push conversion onto every consumer.
- **Mutable sealed classes** with settable properties and `List<T>` collections, not positional records. Consumers (the desktop app filling it from SQLite, `GedcomTreeContext`) build and edit incrementally; immutable records would make every edit a clone.

## 6. Design

### 6.1 Solution layout and engineering baseline

```
Directory.Build.props        shared: net10.0, nullable, warnings as errors, analyzers, deterministic, SourceLink
Directory.Packages.props     central package management
src/Gedcom.Vector/
  Model/                     typed model (section 6.3)
  Dates/                     GedcomDate parser and calendars (section 6.4)
  Parsing/                   tokenizer, encoding, structure reader, mapper (section 6.2)
  Writing/                   writer, line folding (section 6.5)
  Conversion/                version converter and report (section 6.6)
  Diagnostics/               GedcomDiagnostic and codes
  Context/                   GedcomTreeContext (ported)
  Builder/                   fluent builder (ported)
  Gedzip/                    GEDZIP (ported)
  PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt
tests/Gedcom.Vector.Tests/          unit + spec-coverage + corpus round-trip
tests/Gedcom.Vector.Benchmarks/     existing benchmarks, unchanged workload
tests/Gedcom.Vector.AotSmoke/       Native AOT console app for the CI job
tests/corpus/public/                redistributable corpus files (section 7)
tools/                              CI scripts (AOT compare, validator runner)
```

- `Microsoft.CodeAnalysis.PublicApiAnalyzers`: the public surface is checked in, so every API change is visible in review and GV-04's breaking-change list is derived from the diff, not memory.
- Built-in .NET analyzers at `latest-recommended`; XML docs required on public members (already on).
- `Microsoft.SourceLink.GitHub`, deterministic and CI builds.
- Tests: xUnit, test-first for every unit of behaviour. `Xunit.SkippableFact` for the private-corpus tests so a skipped run shows as Skipped, not Passed.
- Dead `Utf8GedcomParser` and the old `StreamingGedcomParser` are removed once the new pipeline replaces them; their useful techniques (SIMD line splitting, string pool, ASCII fast paths) move into the new tokenizer.

### 6.2 Parse pipeline

```
Stream -> EncodingDetector -> LineTokenizer -> StructureReader -> ModelMapper -> GedcomDocument
                                                     \-> Diagnostics
```

1. **Encoding** (existing `GedcomEncodingDetector`, `AnselDecoder`): UTF-8, UTF-16 by BOM, ANSEL, ANSI (Windows-1252), fallback UTF-8. Unchanged behaviour, retained tests.
2. **LineTokenizer**: span-based, splits lines, parses `level [@xref@] TAG [value]`, tracks line numbers, handles 7.0 `@@` escapes and 5.5.1 leading-`@` rules. Malformed lines become diagnostics plus an extension structure so the line is not lost (GV-03a). Strings interned through the existing `GedcomStringPool`.
3. **StructureReader**: maintains a level stack and folds `CONC`/`CONT` into the parent value (`CONT` as `\n`). Emits a stream of complete `GedcomStructure` subtrees per level-0 record. Only one record's tree is held at a time, so memory stays proportional to the largest record, not the file.
4. **ModelMapper**: maps each record tree into typed model objects using hand-written per-structure mappers (one class per structure family: `IndividualMapper`, `EventMapper`, `PlaceMapper`, `CitationMapper`, ...). For each child tag: if the mapper models it under this parent, it maps it; otherwise the child subtree is attached verbatim to the parent's `Extensions` with its sibling index. A modelled tag that appears more often than the spec's cardinality allows is mapped where the model can hold it (lists) or kept as an extension with a warning (single-valued properties); it is never overwritten.
5. **Pointer resolution**: after all records are read, every pointer is checked against the record index; dangling pointers produce a warning and are kept as written. `@VOID@` is a recognised null pointer.

The mapper is the only place that knows tag-to-member mappings; the writer uses the same mapping tables in reverse (one table per structure, shared), so read and write cannot drift.

### 6.3 Model

All model types are `public sealed class` in namespace `Gedcom.Vector.Model`, with settable properties, `List<T>` collections initialised empty (never null), and XML docs naming the GEDCOM tag and both specs' structure names. Base types:

```csharp
public abstract class GedcomElement
{
    // Position among the parent's children when read; null for elements created in code.
    public int? SourceOrder { get; set; }

    // Unmodelled substructures of this element, verbatim.
    public List<GedcomExtension> Extensions { get; } = [];
}

public sealed class GedcomExtension
{
    public int? SourceOrder { get; set; }        // same ordering key as modelled children
    public GedcomStructure Structure { get; set; } = null!;
}

public sealed class GedcomStructure            // generic node, used only for extensions
{
    public string Tag { get; set; } = "";
    public string? Xref { get; set; }
    public string? Value { get; set; }           // CONC/CONT already folded
    public bool ValueIsPointer { get; set; }
    public string? ExtensionUri { get; set; }    // resolved from HEAD.SCHMA for 7.0 _TAGs
    public int? SourceLine { get; set; }
    public List<GedcomStructure> Children { get; } = [];
}

public abstract class GedcomRecord : GedcomElement
{
    public string Xref { get; set; } = "";       // verbatim, including @ delimiters
    public List<Identifier> Identifiers { get; } = [];   // REFN, UID, EXID, RIN, AFN, RFN
    public ChangeDate? Change { get; set; }      // CHAN
    public CreationDate? Creation { get; set; }  // CREA (7.0)
    public List<NoteStructure> Notes { get; } = [];
}
```

**Original order is preserved (builder check, designer's call).** GEDCOM defines no order across different tags, and real files interleave events and attributes (BIRT, OCCU, RESI, DEAT). Every modelled child element and every extension records `SourceOrder`, its index among its parent's children when read. The writer gathers all of a parent's children (modelled members across all lists, plus extensions), sorts those with a `SourceOrder` by it, and emits children without one (created in code) after them in the target spec's documented order (the order the specification lists substructures in). So an extension is re-emitted between the same neighbours, under the same parent, and a file's own ordering survives export. The desktop schema needs one integer sort column per child row to keep this.

`GedcomDocumentComparer` compares, per parent, the *sequence* of children (modelled and extension, by kind and content) in emitted order, not raw `SourceOrder` numbers, which legitimately renumber when CONC/CONT folding changes line counts. Mis-placed extensions therefore fail round-trip test 1.

**Persisting extensions separately (builder check, designer's call).** The desktop app stores extensions in SQLite and rebuilds them for export. `GedcomStructure` is public and offers `string ToGedcomText(GedcomSpecVersion, int baseLevel = 0)` and `static GedcomStructure ParseGedcomText(string)`, round-trip exact for any structure the parser produced, so consumers persist an extension as text plus its `SourceOrder` without writing their own serialiser.

**Document root** (replaces `GedcomParseResult`):

```csharp
public sealed class GedcomDocument
{
    public GedcomSpecVersion SourceVersion { get; set; }
    public Header Header { get; set; } = new();
    public List<Individual> Individuals { get; } = [];
    public List<Family> Families { get; } = [];
    public List<SourceRecord> Sources { get; } = [];
    public List<RepositoryRecord> Repositories { get; } = [];
    public List<SharedNoteRecord> SharedNotes { get; } = [];      // 5.5.1 NOTE record, 7.0 SNOTE
    public List<MultimediaRecord> Multimedia { get; } = [];        // OBJE record
    public List<SubmitterRecord> Submitters { get; } = [];
    public SubmissionRecord? Submission { get; set; }               // 5.5.1 SUBN
    public List<ExtensionRecord> ExtensionRecords { get; } = [];   // unknown and _ level-0 records, verbatim
    public List<GedcomDiagnostic> Diagnostics { get; } = [];
}
```

**Records and their members** (every member below is a modelled structure; anything else lands in `Extensions`):

| Type | Members |
|---|---|
| `Header` | `SourceSystem` (SOUR: id, VERS, NAME, CORP with address/contact, DATA with date/copyright), `Destination`, `TransmissionDate` (DATE + TIME), `SubmitterXref`, `SubmissionXref` (5.5.1), `FileName` (5.5.1 FILE), `Copyright`, `GedcomVersion` + `GedcomForm` (GEDC), `CharacterSet` (5.5.1 CHAR + VERS), `Language`, `PlaceForm` (PLAC.FORM), `Note`, `Schema` (7.0 SCHMA: ordered tag-to-URI list) |
| `Individual` | `Restriction`, `Names`, `Sex`, `Events`, `Attributes`, `NonEvents` (7.0 NO), `LdsOrdinances`, `ChildOfFamilies` (FAMC with PEDI, STAT, notes), `SpouseOfFamilies` (FAMS with notes), `SubmitterXrefs`, `Associations`, `AliasXrefs` (ALIA), `AncestorInterestXrefs`/`DescendantInterestXrefs` (5.5.1 ANCI/DESI), `Citations`, `Media` |
| `Family` | `Restriction`, `Events`, `Attributes` (7.0 NCHI, RESI, FACT), `NonEvents`, `Husband`/`Wife` (pointer + PHRASE), `Children` (pointer + PHRASE), `ChildCount` (5.5.1 NCHI), `Associations`, `SubmitterXrefs`, `LdsSpouseSealings` (SLGS), `Citations`, `Media` |
| `SourceRecord` | `Data` (EVEN list with date period, place; AGNC; notes), `Author`, `Title`, `Abbreviation`, `Publication`, `Text` (with MIME/LANG), `RepositoryCitations` (pointer, CALN list with MEDI), `Media` |
| `RepositoryRecord` | `Name`, `Address`, `Contact` (PHON/EMAIL/FAX/WWW lists) |
| `SharedNoteRecord` | `Text`, `MimeType`, `Language`, `Translations`, `Citations` |
| `MultimediaRecord` | `Restriction`, `Files` (FILE with FORM/MEDI, TITL, TRAN), `Citations` |
| `SubmitterRecord` | `Name`, `Address`, `Contact`, `Media`, `Languages` |
| `SubmissionRecord` | 5.5.1 SUBN fields: submitter, family file name, temple code, generations of ancestors/descendants, ordinance flag |

**Shared structures:**

- `PersonalName`: `Value` (the `Given /Surname/` text), `Type` (enum: AKA, BIRTH, IMMIGRANT, MAIDEN, MARRIED, PROFESSIONAL, OTHER, with PHRASE), `Pieces` (`NamePieces`: lists of Prefixes, Givens, Nicknames, SurnamePrefixes, Surnames, Suffixes, since 7.0 allows repeats), `Translations` (7.0 TRAN with LANG; 5.5.1 FONE/ROMN with TYPE, held as `NameVariant { Kind = Phonetic | Romanised | Translation, Method, Language, Value, Pieces }`), `Notes`, `Citations`. Convenience read-only `GivenName`/`Surname` helpers derive from pieces or value.
- `EventDetail` (shared by every event and attribute): `Type` (TYPE text), `Date` (`GedcomDate`), `Place` (`Place`), `Address`, `Contact`, `Agency`, `ReligiousAffiliation`, `Cause`, `Restriction`, `SortDate` (7.0 SDATE), `Associations`, `Notes`, `Citations`, `Media`, `Uids`.
- `IndividualEvent`: `Kind` (enum covering ADOP, BAPM, BARM, BASM, BIRT, BLES, BURI, CENS, CHR, CHRA, CONF, CREM, DEAT, EMIG, FCOM, GRAD, IMMI, NATU, ORDN, PROB, RETI, WILL, EVEN), `Occurred` (the `Y` flag), `Detail`, `Age` (`GedcomAge`), `FamilyXref` + `AdoptedBy` (for BIRT/CHR/ADOP FAMC).
- `IndividualAttribute`: `Kind` (CAST, DSCR, EDUC, IDNO, NATI, NCHI, NMR, OCCU, PROP, RELI, RESI, SSN, TITL, FACT), `Value`, `Detail`, `Age`.
- `FamilyEvent`: `Kind` (ANUL, CENS, DIV, DIVF, ENGA, MARB, MARC, MARL, MARR, MARS, RESI, EVEN), `Occurred`, `Detail`, `HusbandAge`, `WifeAge` (with PHRASE).
- `NonEvent` (7.0 NO): `Kind`, `Date` (period), `Notes`, `Citations`.
- `LdsOrdinance`: `Kind` (BAPL, CONL, ENDL, INIL, SLGC, SLGS), `Date`, `Temple`, `Place`, `Status` (+ status date), `FamilyXref` (SLGC), `Notes`, `Citations`.
- `Place`: `Name`, `Jurisdictions` (read-only split of `Name` by comma), `Form`, `Language`, `Translations`, `Coordinates` (LATI/LONG parsed to decimal degrees and kept as text), `ExternalIds`, `Notes`, plus 5.5.1 FONE/ROMN variants.
- `Address`: `Text` (full ADDR value), `Line1..Line3`, `City`, `State`, `PostalCode`, `Country`. `Contact`: `Phones`, `Emails`, `Faxes`, `WebPages` (5.5.1 places these beside ADDR; 7.0 too; one type for both).
- `SourceCitation`: `SourceXref` (or `@VOID@`), `InlineDescription` (5.5.1 SOUR without pointer, with TEXT), `Page`, `Data` (DATE, TEXT list), `EventCited` (EVEN + ROLE + PHRASE), `Quality` (0 to 3), `Notes`, `Media`.
- `NoteStructure`: either inline `Text` (+ MIME, LANG, TRAN, citations) or `SharedNoteXref` (5.5.1 NOTE @N@ and 7.0 SNOTE). One type, `IsShared` derived.
- `MultimediaLink`: `MultimediaXref`, or `Embedded` (5.5.1 inline OBJE with FILE/FORM/TITL), `Crop` (7.0 CROP), `Title`.
- `Association`: `IndividualXref` (+ PHRASE), `Role` (enum: CHIL, CLERGY, FATH, FRIEND, GODP, HUSB, MOTH, MULTIPLE, NGHBR, OFFICIATOR, PARENT, SPOU, WIFE, WITN, OTHER) with `RolePhrase`, `Relationship` (5.5.1 RELA free text), `Notes`, `Citations`.
- `Identifier`: `Kind` (REFN, UID, EXID, RIN, AFN, RFN), `Value`, `Type` (REFN TYPE or EXID TYPE URI).
- `ChangeDate` / `CreationDate`: `Date`, `Time`, `Notes`.
- `Restriction`: `[Flags]` enum CONFIDENTIAL, LOCKED, PRIVACY; unknown tokens kept as text with a warning.
- `GedcomAge`: raw text plus parsed qualifier (`<`, `>`), years, months, weeks, days, and PHRASE.

**Enumerations with open values:** GEDCOM enumerations can carry extension values (7.0 `_FOO`) and PHRASEs. Each is modelled as `GedcomEnum<TEnum>` (a `readonly record struct` of `TEnum? Known`, `string Raw`), so an unrecognised value round-trips exactly and a recognised one is type-safe. Values with a C# enum: SEX, PEDI, STAT (FAMC and ordinance), RESN, NAME TYPE, ROLE, MEDI, QUAY, and the event/attribute kinds above.

### 6.4 Dates (GV-02a)

`GedcomDate` (immutable `sealed record`, as it is a value, not an entity):

- `Raw` (exact text as read; the writer emits `Raw` unchanged when the target version accepts it, so no date text is ever rewritten by accident), `Phrase` (7.0 PHRASE or 5.5.1 `INT ... (phrase)` / `(phrase)`).
- `Kind`: `Exact`, `About`, `Calculated`, `Estimated`, `Before`, `After`, `Between`, `From`, `To`, `FromTo`, `Interpreted`, `PhraseOnly`, `Unparsed`.
- `Start`, `End`: `DateValue { Calendar, Year, Month?, Day?, IsBce, DualYear? }`.
- `Calendar`: Gregorian, Julian, Hebrew, FrenchRepublican, plus `Extension(string)` for 7.0 `_` calendars and 5.5.1 `@#DUNKNOWN@`.
- `SortRange`: `(long EarliestJdn, long LatestJdn)?`, Julian Day Numbers, so dates in different calendars sort together. A partial date spans its whole month or year. `BEF x` is `(long.MinValue, latest(x))`, `AFT x` is `(earliest(x), long.MaxValue)`, `BET`/`FROM...TO` span both ends. `ABT`/`CAL`/`EST` use the exact range of the date given; no invented fuzz (callers can widen). Dual years (`1699/00`) use the later year, the Gregorian reading. Unknown calendars yield `null` plus an info diagnostic.
- Calendar conversion to JDN: Gregorian and Julian (proleptic, standard algorithms), Hebrew (molad-based, with leap-year month ADS/ADR handling), French Republican (Romme rule, years 1 to 14 per spec usage). Each tested against published reference tables.
- Unparseable text keeps `Raw`, `Kind = Unparsed`, a warning diagnostic, and round-trips unchanged.

`GedcomTime` likewise for TIME (hour, minute, second, fraction, UTC flag).

### 6.5 Writing

`IGedcomExportWriter.Write(GedcomDocument, Stream, GedcomWriteOptions)` returns a `GedcomWriteResult` (the `ConversionReport` plus diagnostics). Options: `TargetVersion` (default: the document's `SourceVersion`), `LineEnding` (default CRLF), `MaxLineLength` for 5.5.1 `CONC` folding (default 255, splitting never at a space boundary that would lose whitespace), `Encoding` (UTF-8 default; 5.5.1 may also write UTF-16; ANSEL/ANSI write is not offered and is reported if requested). Synchronous, `Stream`, and async overloads as today, plus `GedcomTreeContext` overloads.

Writer rules: canonical sibling order per the target spec; extensions re-emitted per section 6.3; 7.0 output declares a `SCHMA` entry for every `_` tag whose URI is known; 7.0 `@` escaping; header `GEDC.VERS` written as `5.5.1` or `7.0` (current patch, 7.0.18 at time of writing, is not required in the header by the spec).

### 6.6 Version conversion (GV-02b)

`GedcomVersionConverter.Convert(GedcomDocument, GedcomSpecVersion target)` returns a new document and a `ConversionReport`; the writer calls it when `TargetVersion != SourceVersion`. Report entries: `Path` (record xref plus tag path, never values, so reports of private data are safe to log), `Code`, `Message`, `Action` (`Transformed`, `Dropped`, `KeptAsExtension`).

Mapping follows FamilySearch's published 5.5.1-to-7.0 guidance. Examples (the full table is a plan deliverable and lives in code as data with a test per row):

| 5.5.1 to 7.0 | 7.0 to 5.5.1 |
|---|---|
| NOTE record and pointer to SNOTE | SNOTE to NOTE record |
| Embedded OBJE to new OBJE record + pointer | CROP, TRAN on FILE: dropped, reported |
| Inline SOUR (no pointer) to new SOUR record + citation | `@VOID@` pointers: dropped, reported |
| AFN, RFN to EXID with registered type URI | EXID, UID: kept as `_EXID`/`_UID` extensions, reported |
| CHAR, FILE, SUBN, FONE/ROMN: transformed or dropped, reported | PHRASE, SDATE, NO, INIL, CREA, MIME, LANG on notes: dropped, reported |
| RELA to ROLE OTHER + PHRASE | ROLE + PHRASE to RELA text |
| `@#DUNKNOWN@` and date-phrase forms to 7.0 PHRASE | Extension calendars: date kept as phrase, reported |

Option `DowngradeStrategy`: `ReportAndDrop` (default, clean standard output) or `ReportAndKeepAsExtension` (emits a `_`-prefixed equivalent). Either way, every loss is in the report (EXP-06a).

### 6.7 Public API and DI

- `IGedcomImportAdapter` and `IGedcomExportWriter` keep their names (server DI registrations stay valid) but take and return `GedcomDocument`. `Parse(Stream)` and `ParseParallelAsync` stay, plus `ParseAsync(Stream, CancellationToken)`. The seekable-stream requirement stays (encoding detection rewinds).
- `GedcomImportOptions` gains `MaxDiagnostics` (cap, default 10,000, with a final "truncated" entry).
- `AddGedcomImport(IServiceCollection, IConfiguration)` keeps its signature; binding uses the configuration binding source generator (`EnableConfigurationBindingGenerator=true`), validation uses the `[OptionsValidator]` source generator. A second overload `AddGedcomImport(IServiceCollection, Action<GedcomImportOptions>)` binds in plain code for consumers without configuration.
- `GedcomTreeContext`, `Builder/` and `GedzipAdapter` are ported to the new model with equivalent operations (`GetIndividual`, `ParentsOf`, `ChildrenOf`, `SpousesOf`, `MediaFor`, add/update/delete).
- Removed: `PersonRecord`, `FamilyRecord`, `EventRecord`, `FamTreeEventType`, `MediaReferenceRecord`, `UnparsedRecord`, `GedcomParseResult`, `PersonSex` (replaced by `GedcomEnum<Sex>`). Mapping in the release notes.
- `InternalsVisibleTo FAMTree.Api.Tests` is removed (the server consumes the package).

### 6.8 AOT (GV-06)

- Library: `IsAotCompatible=true`, zero `IL2xxx`/`IL3xxx` (enforced by warnings as errors).
- `tests/Gedcom.Vector.AotSmoke`: console app, `PublishAot=true`, args `<in> <out> [--target 5.5.1|7.0]`; reads `.ged` or `.gdz`, writes the export plus a deterministic text summary (counts, diagnostics codes) to stdout.
- CI job `aot` (ubuntu-latest and windows-latest): `dotnet publish -c Release -r <rid> -p:PublishAot=true -warnaserror`, publish the same app JIT (`-p:PublishAot=false`), run both over the whole public corpus and both target versions, and fail unless every output file and summary is byte-identical between AOT and JIT.
- Phase 1 delivers this job against the 1.3.2 model first, so AOT regressions are caught for the entire rewrite.

## 7. Corpus and round-trip testing (GV-05)

**Public corpus** (`tests/corpus/public/`, plus `manifest.json` listing source URL, SHA-256, license and origin program for each file):

- FamilySearch 7.0 test files (24: 22 `.ged`, 2 `.gdz`, from `gedcom.io/testfiles/gedcom70/`). License is unverified (repo is Apache-2.0; the files' page says only "(c) Intellectual Reserve"). Rule: a file is vendored only if its license clearly permits redistribution; otherwise CI downloads it at the pinned SHA-256 and fails if the hash changes.
- 5.5.1 real-world exports from several programs: candidates are the GEDCOM 5.5 torture-test suite (TGC55), Gramps example files, and ArmidaleSoftware's MIT sample files. Same licensing rule. The plan's first corpus task is a license check with a recorded verdict per file.

**Private corpus:** `Methven Ancestry.ged` (Ancestry.com export, 128,475 lines, 4,338 individuals, heavy `_APID`/`_OID` extensions and citations; real PII).

- Never in the repo: located by environment variable `GEDCOM_VECTOR_PRIVATE_CORPUS` (a directory). `.gitignore` adds `tests/corpus/private/`, `*.private.ged`, and a guard test fails the build if any file under `tests/corpus/` is not listed in the public manifest.
- Tests run locally only, as `[SkippableFact]`, skipped (visibly) when the variable is unset, so CI never sees it.
- Test failures report record xrefs, tag paths and line numbers, never values. The diff helper is built that way for every corpus file, so the rule cannot be forgotten per test.

**Round-trip tests**, run for every corpus file and both target versions:

1. Parse, export to the source version, re-parse: documents are equal under `GedcomDocumentComparer` (deep structural equality, ignoring `SourceLine`); then exporting the re-parse is byte-identical to the first export.
2. Accounting (GV-03a): every input line is accounted for by exactly one of: a modelled member, an extension structure, or a diagnostic. The mapper records a per-line disposition in a debug-only accounting mode used by this test.
3. Cross-version: 5.5.1 to 7.0 to 5.5.1 reproduces the original modulo entries listed in the two conversion reports. Records the upgrade created from inline structures (inline SOUR, embedded OBJE) are not re-inlined on downgrade (that would guess intent); the comparer applies a documented normalisation for this test only, treating a record referenced exactly once and marked `Transformed` in the report as equal to its inline form (designer's call).
4. 7.0 output passes ArmidaleSoftware `GedValidate` (MIT, C#, built from source at a pinned commit in CI, run headless on ubuntu) with zero errors. Warnings the validator raises for content present in the input are compared against the input's own validation and must not increase.

**Spec coverage test:** FamilySearch's machine-readable spec (`extracted-files/substructures.tsv`, `enumerations.tsv`, `payloads.tsv` at a pinned commit, Apache-2.0) is vendored under `tests/spec/`; a test asserts that every 7.0 structure type and enumeration value maps to a model member or enum value. There is no machine-readable 5.5.1 spec, so 5.5.1 coverage is a hand-written table test derived from the 5.5.1 specification's grammar, one row per structure.

## 8. Performance (GV-07)

- Baseline: the existing `ParserBenchmarks` run on the 1.3.2 code **after** its Phase 1 retarget to .NET 10, on the same machine, saved under `docs/benchmarks/`. This isolates the model change from the runtime change.
- The benchmark workload stays unchanged. Target: every existing Gedcom.Vector benchmark's mean within 10% of baseline (the competitor-library benchmarks in `ParserBenchmarks` are reported for context and not gated).
- Export exception (user decision 2026-11-22, see `docs/benchmarks/2026-11-22-export-gc-investigation.md`): `MeasureExporting` and `MeasureExportingContext` are gated against the lower of the .NET 8 start point (1.54 ms and 1.47 ms) and the .NET 10 baseline. The 1.3.2 writer allocates about 3.8 times its output, mostly on the large object heap, which costs one Gen2 collection per call; on .NET 10 those background collections make the export about 40% slower and very noisy, so the .NET 10 baseline would hide a defect in our code.
- Writer allocation budget: the 2.0 writer's `Write` to a string allocates at most 1.5 times the size of the returned string (measured with `GC.GetAllocatedBytesForCurrentThread`), and `Write` to a caller's stream allocates no large-object-heap buffers. Lookup dictionaries are pre-sized from record counts and output goes to pooled buffers, never a growing `MemoryStream`. This is the one allocation that is gated.
- Derived values that a parse need not pay for up front (`GedcomDate`'s parsed form and `SortRange`, `Place.Jurisdictions`, coordinates) are computed lazily on first access and cached, so the parse cost of a date is storing its raw text. Allocation is reported but not gated (a fuller model necessarily allocates more objects).
- Design levers: one pass, span tokenizer over bytes for UTF-8 input, string pooling, per-record trees only (never whole-file), mapping via `switch` on interned tag strings, no LINQ in hot paths, lists pre-sized from cardinality hints.
- A local-only benchmark over the private file is added (skipped without the env var) to watch real-world cost, not gated.
- Measured at the end of each model phase, not just at the end, so a regression is found while its cause is fresh.

## 9. Delivery order

1. **Engineering baseline + .NET 10 + AOT** on the existing code: `Directory.Build.props`, central packages, `net10.0`, `IsAotCompatible`, binding source generator, PublicAPI files, AOT smoke app and CI job, benchmark baseline. Shippable on its own.
2. **Pipeline core:** tokenizer, structure reader, diagnostics, extensions, accounting mode.
3. **Model and mapper**, by area: header and records skeleton; individuals and names; events, attributes, places, addresses; citations, notes, media; families, associations, LDS; sources, repositories, submitters, submission, shared notes.
4. **Dates and ages.**
5. **Writer** (same-version), round-trip tests over the public corpus.
6. **Version conversion** and its report.
7. **Port** `GedcomTreeContext`, builder, GEDZIP, DI; remove 1.x types.
8. **Corpus completion, validator job, spec coverage test, private corpus tests.**
9. **Performance pass** against the baseline.
10. **Release:** version 2.0.0, README, `docs/` updates, release notes (`CHANGELOG.md` and `docs/release-notes/2.0.0.md`) listing every public API change from the PublicAPI diff with a migration path, including a 1.x-to-2.0 mapping table (`PersonRecord.FirstName` to `Individual.Names[0].GivenName`, `EventRecord` with `FamTreeEventType` to `Individual.Events[i].Kind`, `UnparsedRecord` to `ExtensionRecord`, `Errors` to `Diagnostics`, and so on).

Each step ends with all tests green, zero warnings, the AOT job green, and a code review before moving on.

## 10. Calls made by the designer (for the user to check)

- One version-neutral model in 7.0 vocabulary; mutable sealed classes; `GedcomEnum<T>` for open enumerations.
- Interface names `IGedcomImportAdapter`/`IGedcomExportWriter` kept, signatures changed; no 1.x compatibility shim.
- The file's own child order is preserved via `SourceOrder` on every element and extension; "in place" means same parent, same neighbours.
- `GedcomStructure` text round-trip API offered so consumers can persist extensions.
- Cross-version round-trip test normalises converter-created single-use records rather than re-inlining them.
- Derived date and place values are lazy.
- Byte-identity is required only for writer stability and AOT versus JIT, not against the original file.
- `ABT`/`CAL`/`EST` sort ranges are not widened.
- Downgrade default is report-and-drop, with an option to keep as `_` extensions.
- ANSEL and ANSI are read-only encodings.
- ArmidaleSoftware `GedValidate` as "the FamilySearch validator": FamilySearch does not publish an official one; its tools page lists this and others. This one is C#, MIT, headless on Linux.

## 11. Left to the builder

- Exact class names of mappers and internal helpers; internal data structures for mapping tables.
- The full conversion table rows and the 5.5.1 coverage table rows (derived from the specs).
- Calendar algorithm implementations, provided they pass the reference-table tests.
- CI matrix details beyond those stated.
