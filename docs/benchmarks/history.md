# Benchmark history

One row per benchmark per checkpoint, appended by `tools/Run-Benchmarks.ps1`. The last column is the change in mean versus the gate reference in `baseline.json` (the Task 5 baseline, except the two export rows, which use the lower of the baseline and the .NET 8 start point). Rows above +10% are recorded, not blocking. Competitor rows are context only.

| Date | Commit | Label | Benchmark | Mean | Allocated | vs gate |
|---|---|---|---|---|---|---|
| 2026-11-21 | 2da3f41 | start: 1.3.2 on .NET 8 | MeasureParsingGedcomVector | 3.05 ms | n/a | n/a |
| 2026-11-21 | 2da3f41 | start: 1.3.2 on .NET 8 | MeasureExporting | 1.54 ms | 4.57 MiB | n/a |
| 2026-11-21 | 2da3f41 | start: 1.3.2 on .NET 8 | MeasureExportingContext | 1.47 ms | 4.57 MiB | n/a |
| 2026-11-21 | 2da3f41 | start: 1.3.2 on .NET 8 | QueryChildrenLinq | 13.5 us | n/a | n/a |
| 2026-11-21 | 2da3f41 | start: 1.3.2 on .NET 8 | QueryChildrenFluent | 28.9 ns | n/a | n/a |
| 2026-11-21 | 2da3f41 | start: 1.3.2 on .NET 8 | CreateTreeContext | 0.97 ms | n/a | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | MeasureParsingGedcomVector | 2.99 ms | n/a | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | MeasureExporting | 2.30 ms | 4.57 MiB | n/a |
| 2026-11-21 | 1a6ad5f | Task 1: net10 retarget | MeasureExportingContext | 2.35 ms | 4.57 MiB | n/a |
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
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | MeasureExporting | 2.077 ms | 4,792,572 B | +34.8% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | MeasureExportingContext | 2.197 ms | 4,792,544 B | +49.8% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | QueryChildrenLinq | 7.469 us | 600 B | 0.0% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | QueryChildrenFluent | 22.63 ns | 64 B | 0.0% |
| 2026-11-22 | 08070d2 | Task 5: .NET 10 baseline (also post-Task-4 checkpoint) | CreateTreeContext | 958.610 us | 1,039,830 B | 0.0% |
| 2026-11-22 | 70917af | Plan 2 Task 4: shared line reader factory | MeasureParsingGedcomVector | 3.297 ms | 2,681,484 B | +1.3% |
| 2026-11-22 | 70917af | Plan 2 Task 4: shared line reader factory | MeasureExporting | 2.454 ms | 4,792,566 B | +59.3% |
| 2026-11-22 | 70917af | Plan 2 Task 4: shared line reader factory | MeasureExportingContext | 1.913 ms | 4,792,946 B | +30.4% |
| 2026-11-22 | 70917af | Plan 2 Task 4: shared line reader factory | QueryChildrenLinq | 7.482 us | 600 B | +0.2% |
| 2026-11-22 | 70917af | Plan 2 Task 4: shared line reader factory | QueryChildrenFluent | 24.86 ns | 64 B | +9.9% |
| 2026-11-22 | 70917af | Plan 2 Task 4: shared line reader factory | CreateTreeContext | 1.235 ms | 1,039,935 B | +28.8% |
| 2026-11-23 | 2e42b15-dirty | Plan 2 Task 7: reading benchmark added | MeasureParsingGedcomVector | 3.122 ms | 2,681,463 B | -4.0% |
| 2026-11-23 | 2e42b15-dirty | Plan 2 Task 7: reading benchmark added | MeasureExporting | 1.558 ms | 4,792,939 B | +1.2% |
| 2026-11-23 | 2e42b15-dirty | Plan 2 Task 7: reading benchmark added | MeasureExportingContext | 1.495 ms | 4,792,984 B | +1.9% |
| 2026-11-23 | 2e42b15-dirty | Plan 2 Task 7: reading benchmark added | QueryChildrenLinq | 7.380 us | 600 B | -1.2% |
| 2026-11-23 | 2e42b15-dirty | Plan 2 Task 7: reading benchmark added | QueryChildrenFluent | 22.22 ns | 64 B | -1.8% |
| 2026-11-23 | 2e42b15-dirty | Plan 2 Task 7: reading benchmark added | CreateTreeContext | 1.227 ms | 1,039,844 B | +28.0% |
| 2026-11-23 | 2e42b15-dirty | Plan 2 Task 7: reading benchmark added | MeasureReadingStructures | 6.611 ms | 8,374,390 B | no gate reference |
| 2026-11-23 | c5b9d64 | Plan 2 Task 8: text round-trip API, shared decode | MeasureParsingGedcomVector | 3.100 ms | 2,681,559 B | -4.7% |
| 2026-11-23 | c5b9d64 | Plan 2 Task 8: text round-trip API, shared decode | MeasureExporting | 1.565 ms | 4,792,899 B | +1.6% |
| 2026-11-23 | c5b9d64 | Plan 2 Task 8: text round-trip API, shared decode | MeasureExportingContext | 1.511 ms | 4,792,852 B | +3.0% |
| 2026-11-23 | c5b9d64 | Plan 2 Task 8: text round-trip API, shared decode | QueryChildrenLinq | 7.403 us | 600 B | -0.9% |
| 2026-11-23 | c5b9d64 | Plan 2 Task 8: text round-trip API, shared decode | QueryChildrenFluent | 22.85 ns | 64 B | +1.0% |
| 2026-11-23 | c5b9d64 | Plan 2 Task 8: text round-trip API, shared decode | CreateTreeContext | 1.229 ms | 1,039,858 B | +28.2% |
| 2026-11-23 | c5b9d64 | Plan 2 Task 8: text round-trip API, shared decode | MeasureReadingStructures | 6.543 ms | 8,374,390 B | -1.0% |
