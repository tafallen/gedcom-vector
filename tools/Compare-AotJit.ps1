#Requires -Version 7
<#
.SYNOPSIS
Runs the smoke app as a Native AOT binary and as a JIT binary over a corpus and fails on any difference.

.DESCRIPTION
Publishes tests/Gedcom.Vector.AotSmoke twice: once with PublishAot=true into artifacts/aot (the publish log is
scanned and any "warning ILnnnn" fails the run) and once with PublishAot=false into artifacts/jit. It then runs both
binaries over every selected file in each corpus directory for each export target (none, 5.5.1, 7.0), runs the new
pipeline (--structures) over every .ged file, plus three failure-path checks (a missing input must exit 3, a .txt
input must exit 4, a .gdz input in --structures mode must exit 4). For every run the exit code, the stdout
summary and the bytes of the output file (compared by SHA-256) must be identical between the two binaries. A corpus
file must also exit 0, and a --structures run must print RoundTrip=ok (every record's text re-parses to the same
structure). One line per comparison is printed (file name, target, match or DIFF); file contents are never
printed. stderr must also be identical. The script exits 0 only if everything matches and the AOT publish logged no IL warnings.

.PARAMETER Rid
Runtime identifier to publish for, for example linux-x64 or win-x64.

.PARAMETER Corpus
One or more corpus directories. Defaults to tests/corpus/smoke.

.PARAMETER Include
One or more file name patterns selecting files inside each corpus directory. Defaults to *.ged and *.gdz.

.PARAMETER SkipAot
Local dry run when no native toolchain is installed; CI never passes it. Builds the JIT binary twice, into
artifacts/aot and artifacts/jit, instead of publishing Native AOT, so the rest of the pipeline (corpus enumeration,
targets, failure-path checks, hash/stdout/exit comparison and the publish-log IL scan) can be exercised. A run with
this switch proves nothing about AOT.

.EXAMPLE
pwsh tools/Compare-AotJit.ps1 -Rid linux-x64

.EXAMPLE
pwsh tools/Compare-AotJit.ps1 -Rid win-x64 -Corpus "$HOME/Downloads" -Include "Some Private Tree.ged"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Rid,
    [string[]]$Corpus,
    [string[]]$Include = @('*.ged', '*.gdz'),
    [switch]$SkipAot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests/Gedcom.Vector.AotSmoke'
$artifacts = Join-Path $repoRoot 'artifacts'
$aotDir = Join-Path $artifacts 'aot'
$jitDir = Join-Path $artifacts 'jit'
$outRoot = Join-Path $artifacts 'out'
$exeName = if ($IsWindows) { 'Gedcom.Vector.AotSmoke.exe' } else { 'Gedcom.Vector.AotSmoke' }

if (-not $Corpus) { $Corpus = @(Join-Path $repoRoot 'tests/corpus/smoke') }

function Fail([string]$message) {
    Write-Host "FAIL: $message"
    exit 1
}

function Invoke-Publish([string]$label, [string[]]$extraArgs, [string]$outDir) {
    $publishArgs = @('publish', $project, '-c', 'Release', '-r', $Rid, '-o', $outDir, '-nologo') + $extraArgs
    Write-Host "Publishing $label ($($extraArgs -join ' '))"
    $lines = @(& dotnet @publishArgs 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    $logPath = Join-Path $artifacts "$label-publish.log"
    Set-Content -Path $logPath -Value $lines
    if ($code -ne 0) {
        Write-Host ($lines | Select-Object -Last 15 | Out-String)
        Fail "dotnet publish for the $label binary failed with exit code $code (log: $logPath)."
    }
    $ilWarnings = @($lines | Where-Object { $_ -match 'warning IL\d{4}' } | Sort-Object -Unique)
    if ($ilWarnings.Count -gt 0) {
        Write-Host ($ilWarnings | Out-String)
        Fail "The $label publish logged $($ilWarnings.Count) IL warning(s)."
    }
    Write-Host "  $label publish succeeded with no IL warnings."
}

# A Native AOT publish has no managed entry assembly; a JIT build masquerading as AOT would ship one.
function Assert-NativeBuild([string]$dir) {
    if ($SkipAot) { return }
    $managed = Join-Path $dir 'Gedcom.Vector.AotSmoke.dll'
    if (Test-Path -LiteralPath $managed) {
        Fail "$managed exists: the 'aot' output is a managed build, not Native AOT."
    }
}

function Invoke-Smoke([string]$exe, [string[]]$arguments) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new($exe)
    foreach ($a in $arguments) { $psi.ArgumentList.Add($a) }
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $psi.UseShellExecute = $false
    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
    $stderrTask = $proc.StandardError.ReadToEndAsync()
    $proc.WaitForExit()
    [pscustomobject]@{ Exit = $proc.ExitCode; Stdout = $stdoutTask.GetAwaiter().GetResult(); Stderr = $stderrTask.GetAwaiter().GetResult() }
}

# Clean previous output so stale files can never mask a failure.
if (Test-Path $artifacts) { Remove-Item -Recurse -Force $artifacts }
New-Item -ItemType Directory -Force $artifacts, $outRoot | Out-Null

if ($SkipAot) {
    Write-Host 'WARNING: -SkipAot given. artifacts/aot is a JIT build; this run does NOT test Native AOT.'
    Invoke-Publish 'aot' @('-p:PublishAot=false', '--self-contained', 'false') $aotDir
}
else {
    Invoke-Publish 'aot' @('-p:PublishAot=true') $aotDir
}
Invoke-Publish 'jit' @('-p:PublishAot=false', '--self-contained', 'false') $jitDir

Assert-NativeBuild $aotDir
$aotExe = Join-Path $aotDir $exeName
$jitExe = Join-Path $jitDir $exeName
foreach ($exe in $aotExe, $jitExe) {
    if (-not (Test-Path $exe)) { Fail "Expected binary not found: $exe" }
}

# Build the run list: (display name, input path, target or $null, expected exit code or $null).
$runs = [System.Collections.Generic.List[object]]::new()
$targets = @($null, '5.5.1', '7.0')
foreach ($dir in $Corpus) {
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { Fail "Corpus directory not found: $dir" }
    $files = Get-ChildItem -LiteralPath $dir -File | Where-Object {
        $name = $_.Name
        $Include | Where-Object { $name -like $_ } | Select-Object -First 1
    } | Sort-Object Name
    foreach ($file in $files) {
        foreach ($t in $targets) {
            $runs.Add([pscustomobject]@{ Name = $file.Name; Input = $file.FullName; Target = $t; Structures = $false; Expected = 0 })
        }
        if ($file.Extension -ieq '.ged') {
            $runs.Add([pscustomobject]@{ Name = $file.Name; Input = $file.FullName; Target = $null; Structures = $true; Expected = 0 })
        }
    }
}
if ($runs.Count -eq 0) { Fail 'No corpus files selected.' }

$txtInput = Join-Path $artifacts 'not-a-gedcom.txt'
Set-Content -Path $txtInput -Value 'plain text'
$runs.Add([pscustomobject]@{ Name = '(missing input)'; Input = (Join-Path $artifacts 'does-not-exist.ged'); Target = $null; Structures = $false; Expected = 3 })
$runs.Add([pscustomobject]@{ Name = '(.txt input)'; Input = $txtInput; Target = $null; Structures = $false; Expected = 4 })
$gdzSample = Join-Path $repoRoot 'tests/corpus/smoke/sample-70.gdz'
if (-not (Test-Path -LiteralPath $gdzSample)) { Fail "Smoke corpus file not found: $gdzSample" }
$runs.Add([pscustomobject]@{ Name = '(.gdz structures)'; Input = $gdzSample; Target = $null; Structures = $true; Expected = 4 })

$diffs = 0
$index = 0
foreach ($run in $runs) {
    $index++
    $targetLabel = if ($run.Structures) { 'structures' } elseif ($run.Target) { $run.Target } else { 'none' }
    $results = @{}
    foreach ($build in @(@{ Id = 'aot'; Exe = $aotExe }, @{ Id = 'jit'; Exe = $jitExe })) {
        $outDir = Join-Path $outRoot $build.Id
        New-Item -ItemType Directory -Force $outDir | Out-Null
        $outFile = Join-Path $outDir ("{0:D3}.out" -f $index)
        $arguments = @($run.Input, $outFile)
        if ($run.Target) { $arguments += @('--target', $run.Target) }
        if ($run.Structures) { $arguments += '--structures' }
        $r = Invoke-Smoke $build.Exe $arguments
        $hash = if (Test-Path $outFile) { (Get-FileHash -Algorithm SHA256 -Path $outFile).Hash } else { $null }
        $results[$build.Id] = [pscustomobject]@{ Exit = $r.Exit; Stdout = $r.Stdout; Stderr = $r.Stderr; Hash = $hash }
    }

    $a = $results['aot']
    $j = $results['jit']
    $problems = [System.Collections.Generic.List[string]]::new()
    if ($a.Exit -ne $j.Exit) { $problems.Add("exit codes differ (aot $($a.Exit), jit $($j.Exit))") }
    elseif ($a.Exit -ne $run.Expected) { $problems.Add("unexpected exit code $($a.Exit), wanted $($run.Expected)") }
    if ($a.Stdout -cne $j.Stdout) { $problems.Add('stdout differs') }
    if ($a.Stderr -cne $j.Stderr) { $problems.Add('stderr differs') }
    if ($a.Hash -ne $j.Hash) { $problems.Add('output bytes differ') }
    if ($run.Structures -and $run.Expected -eq 0) {
        foreach ($result in @($a, $j)) {
            if ($result.Stdout -notmatch '(?m)^RoundTrip=ok$') { $problems.Add('structure text did not round-trip') ; break }
        }
    }

    if ($problems.Count -eq 0) {
        Write-Host ("{0,-28} {1,-10} match (exit {2})" -f $run.Name, $targetLabel, $a.Exit)
    }
    else {
        $diffs++
        Write-Host ("{0,-28} {1,-10} DIFF ({2})" -f $run.Name, $targetLabel, ($problems -join '; '))
    }
}

if ($diffs -gt 0) { Fail "$diffs of $($runs.Count) comparisons differ." }
Write-Host "OK: all $($runs.Count) comparisons match and the AOT publish logged no IL warnings."
exit 0
