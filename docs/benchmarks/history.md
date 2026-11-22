# Benchmark history

One row per benchmark per checkpoint, appended by `tools/Run-Benchmarks.ps1`. The last column is the change in mean versus the gate reference in `baseline.json` (the Task 5 baseline, except the two export rows, which use the lower of the baseline and the .NET 8 start point). Rows above +10% are recorded, not blocking. Competitor rows are context only.

| Date | Commit | Label | Benchmark | Mean | Allocated | vs gate |
|---|---|---|---|---|---|---|
| 2026-11-21 | c362eb6 | start: 1.3.2 on .NET 8 | MeasureParsingGedcomVector | 3.05 ms | n/a | n/a |
| 2026-11-21 | c362eb6 | start: 1.3.2 on .NET 8 | MeasureExporting | 1.54 ms | 4.57 MB | n/a |
| 2026-11-21 | c362eb6 | start: 1.3.2 on .NET 8 | MeasureExportingContext | 1.47 ms | 4.57 MB | n/a |
| 2026-11-21 | c362eb6 | start: 1.3.2 on .NET 8 | QueryChildrenLinq | 13.5 us | n/a | n/a |
| 2026-11-21 | c362eb6 | start: 1.3.2 on .NET 8 | QueryChildrenFluent | 28.9 ns | n/a | n/a |
| 2026-11-21 | c362eb6 | start: 1.3.2 on .NET 8 | CreateTreeContext | 0.97 ms | n/a | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | MeasureParsingGedcomVector | 2.99 ms | n/a | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | MeasureExporting | 2.30 ms | 4.57 MB | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | MeasureExportingContext | 2.35 ms | 4.57 MB | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | QueryChildrenLinq | 7.3 us | n/a | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | QueryChildrenFluent | 21.9 ns | n/a | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | CreateTreeContext | 0.94 ms | n/a | n/a |
| 2026-11-22 | 13f460f | Task 2: AOT binding | MeasureParsingGedcomVector | 3.01 ms | 2,681,534 B | n/a |
| 2026-11-22 | 13f460f | Task 2: AOT binding | MeasureExporting | 2.35 ms | 4,792,730 B | n/a |
| 2026-11-22 | 13f460f | Task 2: AOT binding | MeasureExportingContext | 2.46 ms | 4,792,433 B | n/a |
| 2026-11-22 | 13f460f | Task 2: AOT binding | QueryChildrenLinq | 7.27 us | 600 B | n/a |
| 2026-11-22 | 13f460f | Task 2: AOT binding | QueryChildrenFluent | 22.47 ns | 64 B | n/a |
| 2026-11-22 | 13f460f | Task 2: AOT binding | CreateTreeContext | 0.935 ms | 1,039,811 B | n/a |

| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | MeasureParsingGedcomVector | 3.254 ms | 2,681,454 B | 0.0% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | MeasureParsingGedcomNetSDK | 2.127 s | 2,590,067,576 B | n/a (context only) |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | MeasureParsingGeneGenie | 26.654 ms | 2,822,824 B | n/a (context only) |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | MeasureParsingFamilyTreeProject | 172.905 ms | 96,965,216 B | n/a (context only) |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | MeasureExporting | 2.077 ms | 4,792,572 B | +34.9% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | MeasureExportingContext | 2.197 ms | 4,792,544 B | +49.5% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | QueryChildrenLinq | 7.469 us | 600 B | 0.0% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | QueryChildrenFluent | 22.63 ns | 64 B | 0.0% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | CreateTreeContext | 958.610 us | 1,039,830 B | 0.0% |
