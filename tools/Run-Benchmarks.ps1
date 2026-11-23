#Requires -Version 7
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
  Writing history rows in this mode requires -Commit, because the results were measured at a commit this script
  cannot know. With -NoHistory nothing is written, so -Commit is optional.

.PARAMETER NoHistory
  Print the comparison without appending to history.md.

.PARAMETER Commit
  The commit the results were measured at, recorded in the history row. Without it, a live run records the short
  SHA of HEAD, suffixed -dirty when tracked files have uncommitted changes (untracked files are ignored).

.PARAMETER Job
  Passes --job to BenchmarkDotNet (for example short). Short jobs are too noisy for history.md, so this requires
  -NoHistory.
#>
[CmdletBinding()]
param(
    [string]$Label = '',
    [switch]$All,
    [string]$ResultsOnly = '',
    [switch]$NoHistory,
    [string]$BaselineFile = '',
    [string[]]$Filter = @(),
    [string]$Job = '',
    [string]$Commit = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$baselineFile = if ($BaselineFile) { $BaselineFile } else { Join-Path $repo 'docs/benchmarks/baseline.json' }
$historyFile = Join-Path $repo 'docs/benchmarks/history.md'
$project = Join-Path $repo 'tests/Gedcom.Vector.Benchmarks'
$gatedFilters = @('*MeasureParsingGedcomVector', '*MeasureExporting*', '*QueryChildren*', '*CreateTreeContext', '*MeasureReadingStructures')
$competitors = @('MeasureParsingGedcomNetSDK', 'MeasureParsingGeneGenie', 'MeasureParsingFamilyTreeProject')
$threshold = 10.0
$inv = [cultureinfo]::InvariantCulture

function Format-Time([double]$ns) {
    if ($ns -ge 1e9) { return [string]::Format($inv, '{0:N3} s', $ns / 1e9) }
    if ($ns -ge 1e6) { return [string]::Format($inv, '{0:N3} ms', $ns / 1e6) }
    if ($ns -ge 1e3) { return [string]::Format($inv, '{0:N3} us', $ns / 1e3) }
    return [string]::Format($inv, '{0:N2} ns', $ns)
}

# Guards run before anything is measured.
if ($Job -and -not $NoHistory) {
    [Console]::Error.WriteLine('Short jobs are too noisy for history.md; add -NoHistory.')
    exit 1
}
if ($ResultsOnly -and -not $NoHistory -and -not $Commit) {
    [Console]::Error.WriteLine('-ResultsOnly needs -Commit <sha>: the results were measured at a commit this script cannot know.')
    exit 1
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
        $filters = @(if ($Filter.Count) { $Filter } elseif ($All) { '*' } else { $gatedFilters })
        $extra = @(if ($Job) { '--job'; $Job })
        Write-Host ('Benchmark filters: ' + ($filters -join ' ') + ' | extra: ' + ($extra -join ' '))
        Push-Location $repo
        try {
            & dotnet run -c Release --project $project -- --filter @filters --exporters json csv github @extra
            if ($LASTEXITCODE -ne 0) { throw "dotnet run failed with exit code $LASTEXITCODE" }
        }
        finally { Pop-Location }
    }

    $jsonFiles = @(Get-ChildItem $resultsDir -Filter '*-report*.json' -ErrorAction SilentlyContinue)
    if (-not $jsonFiles.Count) { throw "No BenchmarkDotNet JSON report found in $resultsDir" }
    # One report per benchmark class, possibly in several JSON flavours: read all, de-duplicate by class and method.
    $byKey = [ordered]@{}
    foreach ($f in $jsonFiles) {
        $rep = Get-Content $f.FullName -Raw | ConvertFrom-Json
        if (-not $rep.Benchmarks) { throw "No benchmarks in $($f.FullName)" }
        foreach ($bm in $rep.Benchmarks) { $byKey["$($bm.Type).$($bm.Method)"] = $bm }
    }
    $benchmarks = @($byKey.Values)

    if ($Commit) { $commit = $Commit }
    elseif ($ResultsOnly) { $commit = 'n/a' }   # only reachable with -NoHistory; nothing is written
    else {
        $commit = (& git -C $repo rev-parse --short HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or -not $commit) { throw 'git rev-parse failed' }
        $dirty = @(& git -C $repo status --porcelain --untracked-files=no)
        if ($LASTEXITCODE -ne 0) { throw 'git status failed' }
        if ($dirty.Count) { $commit += '-dirty' }
    }
    $date = (Get-Date).ToString('yyyy-MM-dd', $inv)
    $rows = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]

    $seen = @{}
    foreach ($b in $benchmarks) {
        $name = $b.Method
        $seen[$name] = $true
        if (-not $All -and $competitors -contains $name) { continue }
        $mean = [double]$b.Statistics.Mean
        $allocBytes = if ($b.Memory -and $null -ne $b.Memory.BytesAllocatedPerOperation) { [long]$b.Memory.BytesAllocatedPerOperation } else { $null }
        $allocText = if ($null -eq $allocBytes) { 'n/a' } else { [string]::Format($inv, '{0:N0} B', $allocBytes) }
        $ref = $baseline.benchmarks.$name
        if ($ref) {
            $pct = ($mean - [double]$ref.gateReferenceNs) / [double]$ref.gateReferenceNs * 100.0
            $pctText = [string]::Format($inv, '{0:+0.0;-0.0;0.0}%', $pct)
            if ($pct -gt $threshold) { $warnings.Add("$name is $pctText versus its gate reference ($(Format-Time $mean) against $(Format-Time $ref.gateReferenceNs))") }
        }
        elseif ($competitors -contains $name) { $pctText = 'n/a (context only)' }
        else {
            $pctText = 'no gate reference'
            $warnings.Add("$name has no entry in baseline.json (no gate reference)")
        }
        $rows.Add("| $date | $commit | $Label | $name | $(Format-Time $mean) | $allocText | $pctText |")
    }

    foreach ($gated in $baseline.benchmarks.PSObject.Properties.Name) {
        if (-not $seen.ContainsKey($gated)) { $warnings.Add("$gated is in baseline.json but missing from the results") }
    }

    $rows | ForEach-Object { Write-Host $_ }
    $warnings | ForEach-Object { Write-Warning $_ }
    if (-not $warnings.Count) { Write-Host "No row is over +$threshold% versus its gate reference." }

    if (-not $NoHistory) {
        if (-not (Test-Path $historyFile)) {
            $header = @('# Benchmark history', '', '| Date | Commit | Label | Benchmark | Mean | Allocated | vs gate |', '|---|---|---|---|---|---|---|')
            Set-Content -Path $historyFile -Value $header -Encoding utf8
        }
        Add-Content -Path $historyFile -Value $rows -Encoding utf8
        Write-Host "Appended $($rows.Count) rows to $historyFile"
    }
    exit 0
}
catch {
    [Console]::Error.WriteLine($_)
    exit 1
}
