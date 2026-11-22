<#
.SYNOPSIS
  Runs the Gedcom.Vector benchmarks and appends one row per benchmark to docs/benchmarks/history.md.

.DESCRIPTION
  Results are read from BenchmarkDotNet's JSON exporter output (BenchmarkDotNet.Artifacts/results/*-report*.json),
  never from console text. All times are normalised to nanoseconds for comparison. The gate reference for each
  benchmark is read from docs/benchmarks/baseline.json (gateReferenceNs). A row more than 10 percent over its
  gate reference is printed as a warning; it does not change the exit code. The exit code is non-zero only when
  the run or the parse fails.

.PARAMETER Label
  Free text recorded in the history row (for example "Plan 3 checkpoint").

.PARAMETER All
  Also run the competitor benchmarks (GedcomNetSDK, GeneGenie, FamilyTreeProject). They are context only.

.PARAMETER ResultsOnly
  Skip the run and parse an existing results directory (BenchmarkDotNet.Artifacts or its results subfolder).

.PARAMETER NoHistory
  Print the comparison without appending to history.md.
#>
[CmdletBinding()]
param(
    [string]$Label = '',
    [switch]$All,
    [string]$ResultsOnly = '',
    [switch]$NoHistory
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$baselineFile = Join-Path $repo 'docs/benchmarks/baseline.json'
$historyFile = Join-Path $repo 'docs/benchmarks/history.md'
$project = Join-Path $repo 'tests/Gedcom.Vector.Benchmarks'
$gatedFilters = @('*MeasureParsingGedcomVector', '*MeasureExporting*', '*QueryChildren*', '*CreateTreeContext')
$competitors = @('MeasureParsingGedcomNetSDK', 'MeasureParsingGeneGenie', 'MeasureParsingFamilyTreeProject')
$threshold = 10.0

function Format-Time([double]$ns) {
    if ($ns -ge 1e9) { return ('{0:N3} s' -f ($ns / 1e9)) }
    if ($ns -ge 1e6) { return ('{0:N3} ms' -f ($ns / 1e6)) }
    if ($ns -ge 1e3) { return ('{0:N3} us' -f ($ns / 1e3)) }
    return ('{0:N2} ns' -f $ns)
}

try {
    if (-not (Test-Path $baselineFile)) { throw "Missing gate reference file: $baselineFile" }
    $baseline = Get-Content $baselineFile -Raw | ConvertFrom-Json

    if ($ResultsOnly) {
        $resultsDir = $ResultsOnly
        if (Test-Path (Join-Path $ResultsOnly 'results')) { $resultsDir = Join-Path $ResultsOnly 'results' }
    }
    else {
        $resultsDir = Join-Path $repo 'BenchmarkDotNet.Artifacts/results'
        if (Test-Path $resultsDir) { Remove-Item (Join-Path $resultsDir '*') -Force }
        $filters = if ($All) { @('*') } else { $gatedFilters }
        Push-Location $repo
        try {
            & dotnet run -c Release --project $project -- --filter @filters --exporters json csv github
            if ($LASTEXITCODE -ne 0) { throw "dotnet run failed with exit code $LASTEXITCODE" }
        }
        finally { Pop-Location }
    }

    $json = Get-ChildItem $resultsDir -Filter '*-report*.json' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $json) { throw "No BenchmarkDotNet JSON report found in $resultsDir" }
    $report = Get-Content $json.FullName -Raw | ConvertFrom-Json
    if (-not $report.Benchmarks) { throw "No benchmarks in $($json.FullName)" }

    $commit = (& git -C $repo rev-parse --short HEAD).Trim()
    $date = Get-Date -Format 'yyyy-MM-dd'
    $rows = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]

    foreach ($b in $report.Benchmarks) {
        $name = $b.Method
        if (-not $All -and $competitors -contains $name) { continue }
        $mean = [double]$b.Statistics.Mean
        $alloc = [long]$b.Memory.BytesAllocatedPerOperation
        $ref = $baseline.benchmarks.$name
        if ($ref) {
            $pct = ($mean - [double]$ref.gateReferenceNs) / [double]$ref.gateReferenceNs * 100.0
            $pctText = ('{0:+0.0;-0.0;0.0}%' -f $pct)
            if ($pct -gt $threshold) { $warnings.Add("$name is $pctText versus its gate reference ($(Format-Time $mean) against $(Format-Time $ref.gateReferenceNs))") }
        }
        else { $pctText = 'n/a (context only)' }
        $rows.Add(('| {0} | {1} | {2} | {3} | {4} | {5:N0} B | {6} |' -f $date, $commit, $Label, $name, (Format-Time $mean), $alloc, $pctText))
    }

    $rows | ForEach-Object { Write-Host $_ }
    $warnings | ForEach-Object { Write-Warning $_ }
    if (-not $warnings.Count) { Write-Host "No row is over +$threshold% versus its gate reference." }

    if (-not $NoHistory) {
        if (-not (Test-Path $historyFile)) { throw "Missing $historyFile" }
        Add-Content -Path $historyFile -Value $rows -Encoding utf8
        Write-Host "Appended $($rows.Count) rows to $historyFile"
    }
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
