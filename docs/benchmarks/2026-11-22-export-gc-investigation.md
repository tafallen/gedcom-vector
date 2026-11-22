# Export slowdown on .NET 10: investigation (2026-11-22)

## Symptom

After the Task 1 retarget to `net10.0` (commit `296ee8b`), `MeasureExporting` went from 1.54 ms to 2.2 to 2.5 ms and `MeasureExportingContext` from 1.47 ms to 2.2 to 2.4 ms, with a standard deviation of about 20%. Allocation was unchanged at 4.57 MB per call. Every other Gedcom.Vector benchmark was flat or faster.

## Method

All runs on the same machine (AMD Ryzen AI 7 350, Windows 11, SDK 10.0.404, runtimes 8.0.25 and 10.0.15), BenchmarkDotNet 0.15.8, both runtimes in one BenchmarkDotNet session (`--runtimes net8.0 net10.0`) so the machine state is shared. The library source at `1a6ad5f` was compiled into a scratch probe targeting both `net8.0` and `net10.0`.

1. **Primitives.** A copy of the writer's output shape (about 1 MB of ASCII through a 64 KB pooled buffer) timed in stages: buffer writes only, writes into a growing `MemoryStream(4096)`, `Encoding.UTF8.GetString`, allocating a 1 MB string. Every stage was 8 to 27% faster on .NET 10, with identical GC counts. The runtime's primitives are not slower.
2. **Real writer, small live heap.** `GedcomExportWriter.Write(GedcomParseResult)` with only the parse result kept alive: .NET 8 1.52 ms, .NET 10 1.60 ms, within noise. The dictionary-building block alone (events by person, families as child and as spouse) takes about 1.6 ms of LOH-heavy work on both.
3. **Real writer, repository benchmark setup.** A copy of `ParserBenchmarks.Setup` that also keeps the input bytes and the `GedcomTreeContext` alive:

| Setup | GC | .NET 8 | .NET 10 | Change |
|---|---|---:|---:|---:|
| Parse result only | default (background GC on) | 1.80 ms ±0.04 | 1.70 ms ±0.05 | -6% |
| Parse result, bytes and context | default (background GC on) | 1.73 ms ±0.21 | 2.42 ms ±0.50 | +40% |
| Parse result only | `DOTNET_gcConcurrent=0` | 1.83 ms ±0.05 | 1.71 ms ±0.05 | -6% |
| Parse result, bytes and context | `DOTNET_gcConcurrent=0` | 2.04 ms ±0.01 | 1.90 ms ±0.00 | -6% |

## Root cause

The 1.3.2 writer allocates 4.57 MB to produce about 1.2 MB of string: the output `MemoryStream` grows from 4 KB by doubling, the three lookup dictionaries and their lists grow from empty, and the final string is decoded from the stream. Most of that lands on the large object heap, which triggers about one Gen2 collection per call. With background GC on and a larger live heap, .NET 10 schedules those background Gen2 collections differently from .NET 8, which costs about 40% and produces bimodal timings. With background GC off, .NET 10 is 6% faster than .NET 8 in every setup.

The runtime change exposed the defect; the defect is ours. Real callers keep more live data than the benchmark, so they are more exposed, not less.

## Decision (user, 2026-11-22)

- The 1.3.2 writer is not patched; Plan 6 removes it.
- The 2.0 writer (Plan 5) pre-sizes its lookups from record counts, writes to pooled buffers rather than a growing `MemoryStream`, and carries a deterministic allocation-budget test: `Write` to a string allocates at most 1.5 times the returned string's size. The test is written against the 1.3.2 writer first and fails there.
- GV-07 gates the two export benchmarks against the lower of the .NET 8 start point and the .NET 10 baseline (design section 8), so the .NET 10 baseline cannot hide this.
- Baselines and checkpoints record allocated bytes alongside the mean.

## Reproducing

Run the export benchmarks with and without `DOTNET_gcConcurrent=0` set in the environment; the gap between the two runs is the background-GC cost.

```powershell
$env:DOTNET_gcConcurrent = '0'; dotnet run -c Release --project tests/Gedcom.Vector.Benchmarks -- --filter '*Export*'
```
