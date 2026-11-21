# Gedcom.Vector 2.0 Plan 1: Baseline, .NET 10 and Native AOT Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the existing 1.3.2 library to `net10.0` with a production engineering baseline, make it Native AOT and trim compatible with zero `IL2xxx`/`IL3xxx` warnings, prove it with a CI job that compares AOT and JIT output byte for byte, and record the benchmark baseline that GV-07 is measured against. 1.3.2 behaviour is otherwise unchanged.

**Architecture:** Shared build settings move to `Directory.Build.props` and package versions to `Directory.Packages.props`. Options binding switches to the configuration binding source generator (an interceptor, so the call site stays `.Bind(...)`). A small AOT console app under `tests/` exercises parse and export on a hand-authored smoke corpus; a PowerShell 7 script publishes it both ways and diffs every output.

**Tech Stack:** .NET 10 SDK (10.0.404 installed locally), C# latest, xUnit 2.9, BenchmarkDotNet, Microsoft.CodeAnalysis.PublicApiAnalyzers, GitHub Actions, PowerShell 7.

**Spec:** `docs/superpowers/specs/2026-11-21-gedcom-vector-2-0-design.md` (sections 6.1, 6.7 DI paragraph, 6.8, 8 first bullet). Roadmap: `docs/superpowers/plans/2026-11-21-gv2-roadmap.md`.

## Global Constraints

- Target `net10.0` only. No multi-targeting.
- `TreatWarningsAsErrors=true` everywhere; a warning is a bug, never suppressed with `NoWarn` or `#pragma` unless the plan says so.
- `IsAotCompatible=true` on `src/Gedcom.Vector`; zero `IL2xxx`/`IL3xxx` warnings.
- Library dependencies limited to the existing `Microsoft.Extensions.*` packages, moved to the 10.0.x line.
- No reflection or dynamic code in the library.
- No em-dashes in any file, comment, commit message or doc.
- Commits use conventional prefixes (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `ci:`, `build:`) and end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Tests pass and the build is warning-free before every commit. Every regression found gets a test.
- Work happens in the worktree `C:\repos\gedcom-vector-v2` on branch `feature/v2.0`. Do not push.
- The private file `C:\Users\tim\Downloads\Methven Ancestry.ged` may be used for local runs only; never copy it into the repo, never print its content.

## Measured starting point

On `net10.0` with `IsAotCompatible=true`, the 1.3.2 library raises exactly two trim/AOT warnings, both at `GedcomServiceCollectionExtensions.cs(19,9)`: IL2026 and IL3050 from `OptionsBuilderConfigurationExtensions.Bind`. With `AnalysisLevel=latest-recommended` it raises CA1510 (6 files), CA1848 (1), CA1859 (2), CA1873 (1).

## Review Focus

1. **Legacy encodings under AOT:** ANSEL and ANSI (Windows-1252, via `CodePagesEncodingProvider`) files must decode identically under AOT and JIT. Task 4's smoke corpus includes one of each.
2. **Configuration with no `GedcomImport` section:** defaults must survive source-generated binding (`MaxFileSizeBytes == 104857600`). Task 2 pins it.
3. **Non-numeric configuration value** (`GedcomImport:MaxFileSizeBytes = "abc"`): must fail loudly with `InvalidOperationException` on first options access, as the reflection binder did, not silently keep the default. Task 2 pins it.
4. **GEDZIP output determinism:** `CreateGedzip` currently stamps the zip entry with the current time, so two runs differ. AOT versus JIT comparison would fail spuriously and consumers cannot diff exports. Task 4 fixes it with a test.
5. **Smoke app failure paths** (missing input file, unknown extension): both builds must exit with the same non-zero code and message, never an unhandled-exception crash that differs between AOT and JIT. Task 4 pins it.

---

### Task 1: Shared build settings, central packages and the .NET 10 retarget

**Files:**
- Create: `Directory.Build.props`, `Directory.Packages.props`, `global.json`
- Modify: `src/Gedcom.Vector/Gedcom.Vector.csproj`, `tests/Gedcom.Vector.Tests/Gedcom.Vector.Tests.csproj`, `tests/Gedcom.Vector.Benchmarks/Gedcom.Vector.Benchmarks.csproj`, `samples/Gedcom.Vector.Sample/Gedcom.Vector.Sample.csproj`, `.github/workflows/ci.yml`, `.github/workflows/publish.yml`
- Test: existing suite in `tests/Gedcom.Vector.Tests`

**Interfaces:**
- Produces: every project inherits `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`, `Deterministic=true`, `ContinuousIntegrationBuild=true` when `$(GITHUB_ACTIONS)=='true'`, `AnalysisLevel=latest-recommended`. Projects declare `PackageReference` without `Version`.

- [ ] **Step 1: Create `global.json`** pinning SDK `10.0.404` with `rollForward: latestFeature`.
- [ ] **Step 2: Create `Directory.Build.props`** with the properties above. Remove the duplicated properties from each `.csproj`. SourceLink needs no package (built into the .NET 8+ SDK for GitHub remotes); keep `IncludeSymbols`/`snupkg` in the library csproj.
- [ ] **Step 3: Create `Directory.Packages.props`** with `ManagePackageVersionsCentrally=true`. Library packages at the latest 10.0.x stable: `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Options.ConfigurationExtensions`; test-only `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Configuration` at the same line; existing test packages at their latest stable (`xunit` 2.9.x, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`, `Moq`); `BenchmarkDotNet` at the latest stable that supports .NET 10 (0.15.x or later; 0.13.12 predates it); competitor benchmark packages at their current versions.
- [ ] **Step 4: Remove the `InternalsVisibleTo FAMTree.Api.Tests` item** from the library csproj (design 6.7). Keep `Gedcom.Vector.Tests`.
- [ ] **Step 5: Fix the recommended-analyzer findings** in the library: CA1510 (use `ArgumentNullException.ThrowIfNull`), CA1848 and CA1873 (convert the logging call to a `[LoggerMessage]` source-generated method on a `partial` class), CA1859 (use the concrete type). Fix any new findings the test, benchmark or sample projects raise the same way.
- [ ] **Step 6: Update both workflows** to `dotnet-version: 10.0.x`.
- [ ] **Step 7: Build and test**

Run: `dotnet build Gedcom.Vector.sln -c Release` then `dotnet test Gedcom.Vector.sln -c Release`
Expected: `0 Warning(s)`, `0 Error(s)`; all existing tests pass (same count as on `main` under .NET 8).

- [ ] **Step 8: Commit** `build: retarget to net10.0 with shared build settings and central package management`

---

### Task 2: AOT-compatible options binding and the plain-code overload

**Files:**
- Modify: `src/Gedcom.Vector/Gedcom.Vector.csproj`, `src/Gedcom.Vector/GedcomServiceCollectionExtensions.cs`
- Test: `tests/Gedcom.Vector.Tests/GedcomServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Produces: `public static IServiceCollection AddGedcomImport(this IServiceCollection services, IConfiguration configuration)` (unchanged signature) and new `public static IServiceCollection AddGedcomImport(this IServiceCollection services, Action<GedcomImportOptions> configure)`. Both register the same validation (`MaxFileSizeBytes > 0`, message `GedcomImport:MaxFileSizeBytes must be a positive number of bytes.`), `ValidateOnStart()`, and the two singletons.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void AddGedcomImport_NoSection_KeepsDefaults()
    // empty configuration -> options.MaxFileSizeBytes == 100 * 1024 * 1024
[Fact] public void AddGedcomImport_NonNumericValue_ThrowsOnAccess()
    // "GedcomImport:MaxFileSizeBytes" = "abc" -> Assert.Throws<InvalidOperationException>(() => options.Value)
[Fact] public void AddGedcomImport_Action_SetsOptions()
    // services.AddGedcomImport(o => o.MaxFileSizeBytes = 42) -> options.MaxFileSizeBytes == 42, adapter resolves as singleton
[Fact] public void AddGedcomImport_Action_ZeroSize_FailsValidation()
    // o => o.MaxFileSizeBytes = 0 -> Assert.Throws<OptionsValidationException>(() => options.Value)
```

- [ ] **Step 2: Run** `dotnet test --filter FullyQualifiedName~GedcomServiceCollectionExtensionsTests`. Expected: the two `Action` tests fail to compile (overload missing). The two configuration tests may already pass; they pin behaviour across the switch.
- [ ] **Step 3: Set `IsAotCompatible=true` and `EnableConfigurationBindingGenerator=true`** in the library csproj and build. Expected: IL2026/IL3050 gone (the generator intercepts `.Bind`). If either remains, replace `.Bind(section)` with `.Configure(o => { var v = section[nameof(GedcomImportOptions.MaxFileSizeBytes)]; if (v is not null) o.MaxFileSizeBytes = long.Parse(v, CultureInfo.InvariantCulture); })` and make the non-numeric case throw `InvalidOperationException` to keep the test green.
- [ ] **Step 4: Add the `Action<GedcomImportOptions>` overload**, sharing a private helper with the configuration overload for the validation and registrations (no duplicated rules). Keep the existing lambda `.Validate(...)`: it is plain code and AOT-safe, so the `[OptionsValidator]` generator mentioned in design 6.7 is not needed (YAGNI; noted in the release notes later).
- [ ] **Step 5: Run** `dotnet build -c Release` and `dotnet test -c Release`. Expected: zero warnings, all tests pass.
- [ ] **Step 6: Commit** `feat: make options binding AOT compatible and add an Action-based AddGedcomImport overload`

---

### Task 3: Public API tracking

**Files:**
- Create: `src/Gedcom.Vector/PublicAPI.Shipped.txt`, `src/Gedcom.Vector/PublicAPI.Unshipped.txt`
- Modify: `Directory.Packages.props`, `src/Gedcom.Vector/Gedcom.Vector.csproj`

**Interfaces:**
- Produces: RS0016/RS0017 enforced as errors, so every later public API change must edit `PublicAPI.Unshipped.txt`. Plan 8 derives the release notes' breaking-change list from the diff of these two files against this commit.

- [ ] **Step 1: Add `Microsoft.CodeAnalysis.PublicApiAnalyzers`** (latest stable, `PrivateAssets=all`) to the library only, plus the two files as `AdditionalFiles`. Both files start with `#nullable enable`.
- [ ] **Step 2: Build** and confirm RS0016 errors appear for every public member.
- [ ] **Step 3: Populate `PublicAPI.Shipped.txt`** with the 1.3.2 surface (everything except the Task 2 overload) using `dotnet format analyzers src/Gedcom.Vector/Gedcom.Vector.csproj --diagnostics RS0016` and then moving the generated lines from Unshipped to Shipped. The Task 2 `Action` overload line stays in `PublicAPI.Unshipped.txt`.
- [ ] **Step 4: Build and test.** Expected: zero warnings, all tests pass.
- [ ] **Step 5: Commit** `build: track the public API surface with PublicApiAnalyzers`

---

### Task 4: AOT smoke app, smoke corpus, deterministic GEDZIP and the CI job

**Files:**
- Create: `tests/Gedcom.Vector.AotSmoke/Gedcom.Vector.AotSmoke.csproj`, `tests/Gedcom.Vector.AotSmoke/Program.cs`, `tests/corpus/smoke/ansel-551.ged`, `tests/corpus/smoke/ansi-551.ged`, `tests/corpus/smoke/utf16le-551.ged`, `tests/corpus/smoke/utf8-70.ged`, `tests/corpus/smoke/sample-70.gdz`, `tests/corpus/smoke/README.md`, `tools/Compare-AotJit.ps1`
- Modify: `src/Gedcom.Vector/Gedzip/GedzipAdapter.cs`, `Gedcom.Vector.sln`, `.github/workflows/ci.yml`, `.gitignore`
- Test: `tests/Gedcom.Vector.Tests/Gedcom70AndGedzipTests.cs`

**Interfaces:**
- Consumes: 1.3.2 public API (`GedcomImportAdapter`, `GedcomExportWriter`, `GedzipAdapter`, `GedcomParseResult`).
- Produces: `Gedcom.Vector.AotSmoke <input> <output> [--target 5.5.1|7.0]`. Exit 0 on success, 2 on usage error, 3 on unreadable input, 4 on unknown extension; messages to stderr. On success writes the export to `<output>` (`.gdz` input produces `.gdz` output via `GedzipAdapter.CreateGedzip`) and a summary to stdout, one `key=value` per line in this order: `SpecVersion`, `Persons`, `Families`, `Events`, `Media`, `Unparsed`, `Errors`, then each error as `Error=<text>`. `tools/Compare-AotJit.ps1 -Rid <rid> [-Corpus <dir>...]` exits 0 only if every output file, summary and exit code matches between builds and the AOT publish logged no `IL` warnings. Plan 6 moves the app to the 2.0 model; Plan 7 adds the public corpus directory to its default inputs.

- [ ] **Step 1: Write the failing test** in `Gedcom70AndGedzipTests.cs`

```csharp
[Fact] public void CreateGedzip_IsDeterministic()
    // write the same parse result twice; assert the two byte arrays are equal
    // and the entry's LastWriteTime == new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero)
```

- [ ] **Step 2: Run it.** Expected: FAIL (entry timestamp is the current time).
- [ ] **Step 3: In `GedzipAdapter.CreateGedzip`, set `entry.LastWriteTime` to 1980-01-01T00:00:00Z** (the earliest zip/DOS timestamp) before opening the entry. Do not change the entry name here; the `gedcom.ged` fix belongs to Plan 6.
- [ ] **Step 4: Run the test.** Expected: PASS.
- [ ] **Step 5: Author the smoke corpus.** Hand-written, no third-party content, each with a header, 3 to 5 individuals, one family with marriage, one OBJE record, one `_CUSTOM` level-0 record, one SOUR record:
  - `ansel-551.ged`: `1 CHAR ANSEL`, names containing ANSEL combining diacritics (bytes `0xE2 0x65` for e-acute, `0xE8 0x75` for u-umlaut) and `0xA2` (O-slash).
  - `ansi-551.ged`: `1 CHAR ANSI`, Windows-1252 bytes `0xE9`, `0xFC`, `0x80` (euro sign) in names and places.
  - `utf16le-551.ged`: UTF-16LE with BOM, `1 CHAR UNICODE`, including a non-BMP character in a NOTE.
  - `utf8-70.ged`: `2 VERS 7.0`, UTF-8 without BOM, `1 SCHMA` with one `_TAG`.
  - `sample-70.gdz`: zip containing `gedcom.ged` (the 7.0 content) plus one small `media/photo.jpg` placeholder.
  - `README.md`: states the files are hand-authored for this repo, fictional, and what each one exercises.
  Add `.gitattributes` entries marking `tests/corpus/**` as `binary` so line endings and encodings are never normalised.
- [ ] **Step 6: Create the smoke app**: `OutputType=Exe`, `PublishAot=true`, `InvariantGlobalization=true`, `IsPackable=false`, references the library. Constructs `GedcomImportAdapter` and `GedcomExportWriter` directly (no DI host). `--target` sets `GedcomParseResult.SpecVersion` before export. Add it to the solution.
- [ ] **Step 7: Write `tools/Compare-AotJit.ps1`** (PowerShell 7, cross-platform): `dotnet publish tests/Gedcom.Vector.AotSmoke -c Release -r <rid> -p:PublishAot=true -o artifacts/aot` capturing the log; fail on any line matching `warning IL\d{4}`; `dotnet publish ... -p:PublishAot=false --self-contained false -o artifacts/jit`; for every file in each corpus directory and each target (`none`, `5.5.1`, `7.0`) run both binaries into separate output folders; compare exit codes, stdout and output bytes (`Get-FileHash`). Print one line per comparison (file name, target, `match`/`DIFF`), never file contents. Add `artifacts/` to `.gitignore`.
- [ ] **Step 8: Write the failure-path checks** into the script's run list: a non-existent input path (expect exit 3 from both) and a `.txt` input (expect exit 4 from both).
- [ ] **Step 9: Run locally**

Run: `pwsh tools/Compare-AotJit.ps1 -Rid win-x64`
Expected: every line `match`, exit code 0, no `IL` warnings. Then once, locally only: `pwsh tools/Compare-AotJit.ps1 -Rid win-x64 -Corpus "C:\Users\tim\Downloads"` restricted to the private file via a `-Include "Methven Ancestry.ged"` parameter. Expected: `match` for all three targets. Do not commit any output.

- [ ] **Step 10: Add the `aot` job to `ci.yml`**: matrix `os: [ubuntu-latest, windows-latest]` with `rid: linux-x64` / `win-x64`, setup .NET 10.0.x, run `pwsh tools/Compare-AotJit.ps1 -Rid ${{ matrix.rid }}`. Upload `artifacts/` as a build artifact on failure.
- [ ] **Step 11: Build and test the whole solution.** Expected: zero warnings, all tests pass.
- [ ] **Step 12: Commit** `ci: add Native AOT smoke app and AOT-versus-JIT comparison job` (the GEDZIP fix as its own earlier commit: `fix: make GEDZIP output deterministic by fixing the entry timestamp`).

---

### Task 5: Benchmark baseline on .NET 10

**Files:**
- Create: `docs/benchmarks/2026-11-21-baseline-1.3.2-net10.md`
- Modify: `tests/Gedcom.Vector.Benchmarks/*` only if BenchmarkDotNet 0.15 requires API changes

**Interfaces:**
- Produces: the GV-07 baseline Plan 8 compares against: mean, error, standard deviation and allocation for `MeasureParsing_GedcomVector`, `MeasureExporting`, `MeasureExportingContext`, `QueryChildrenLinq`, `QueryChildrenFluent`, `CreateTreeContext`, plus machine, OS, SDK and runtime lines from the BenchmarkDotNet header. Competitor rows recorded for context only.

- [ ] **Step 1: Run the benchmarks** in the background (they take several minutes): `dotnet run -c Release --project tests/Gedcom.Vector.Benchmarks -- --filter "*"` on a quiet machine, on AC power.
- [ ] **Step 2: Write the baseline doc**: the BenchmarkDotNet GitHub-markdown table copied from `BenchmarkDotNet.Artifacts/results/*-report-github.md`, the commit SHA measured, and one paragraph stating that Plan 8 reruns the identical workload on the same machine and that only Gedcom.Vector rows are gated (user decision: a residual regression over 10% is recorded, not blocking).
- [ ] **Step 3: Commit** `docs: record the .NET 10 benchmark baseline for 1.3.2`

---

### Task 6: Plan 1 close-out

- [ ] **Step 1: Full verification**: `dotnet build -c Release` (0 warnings), `dotnet test -c Release` (all pass), `pwsh tools/Compare-AotJit.ps1 -Rid win-x64` (all match).
- [ ] **Step 2: Grep the diff for em-dashes**: `git diff main --name-only | % { Select-String -Path $_ -Pattern ([char]0x2014) }` returns nothing.
- [ ] **Step 3: Whole-plan code review** (superpowers:requesting-code-review) against this plan and the design; fix findings with tests.
- [ ] **Step 4: Mark Plan 1 done in the roadmap** and commit `docs: mark plan 1 complete`.
