#Requires -Version 5
<#
.SYNOPSIS
    Per-class coverage gate: fails the pipeline if any instrumented class in a Cobertura
    coverage report falls below the given line and/or branch coverage threshold.

.DESCRIPTION
    Committed identically (byte-for-byte) in both plugin repos' tools\ directories
    (KPPasskeyChecker, KP2FAChecker) — analogous to tools\run-selfcheck.ps1. Each repo is a
    standalone git checkout, so this cannot live in a shared monorepo-only location; a
    committed release.ps1 must not depend on anything outside its own repo. Invoked
    identically from each repo's own release.ps1.

    Does not duplicate any coverlet <Exclude> filter logic: the caller is expected to have
    produced ReportPath with
    "dotnet test --settings coverage.runsettings --collect:\"XPlat Code Coverage\"" first, so
    the already-excluded classes (structural excludes + the documented
    TestCoverageExemptions.Entries exemptions, see coverage.runsettings) never appear as
    <class> elements in the report at all — every class this script sees is in scope and
    must clear both thresholds.

    Every <class> element present in the report is checked independently; a report with zero
    classes (empty/malformed) is treated as an error, not a silent pass.

.PARAMETER ReportPath
    Path to a Cobertura coverage.cobertura.xml report.

.PARAMETER LineThreshold
    Minimum required line-coverage percentage (0-100) for every class.

.PARAMETER BranchThreshold
    Minimum required branch-coverage percentage (0-100) for every class.

.EXAMPLE
    .\coverage-gate.ps1 -ReportPath .\TestResults\<guid>\coverage.cobertura.xml -LineThreshold 55 -BranchThreshold 60
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [Parameter(Mandatory = $true)][double]$LineThreshold,
    [Parameter(Mandatory = $true)][double]$BranchThreshold
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ReportPath)) {
    throw ("Coverage report not found: {0}`n" -f $ReportPath) +
        "Run 'dotnet test --settings coverage.runsettings --collect:`"XPlat Code Coverage`"' " +
        "first and pass the resulting coverage.cobertura.xml path."
}

[xml]$xml = $null
try {
    [xml]$xml = Get-Content $ReportPath -Raw
} catch {
    throw "Coverage report at $ReportPath is not valid XML: $($_.Exception.Message)"
}

$classes = @()
foreach ($pkg in $xml.coverage.packages.package) {
    foreach ($cls in $pkg.classes.class) {
        $classes += $cls
    }
}

if ($classes.Count -eq 0) {
    throw "No <class> elements found in $ReportPath -- the report looks empty or malformed."
}

$failures = @()
foreach ($cls in $classes) {
    $linePct = [double]$cls.'line-rate' * 100
    $branchPct = [double]$cls.'branch-rate' * 100
    if ($linePct -lt $LineThreshold -or $branchPct -lt $BranchThreshold) {
        $failures += [pscustomobject]@{
            Class     = $cls.name
            LinePct   = [math]::Round($linePct, 2)
            BranchPct = [math]::Round($branchPct, 2)
        }
    }
}

Write-Host ("coverage-gate: checked {0} class(es) against line >= {1}% / branch >= {2}%." -f `
    $classes.Count, $LineThreshold, $BranchThreshold)

if ($failures.Count -gt 0) {
    Write-Host ("coverage-gate FAILED -- {0} class(es) below threshold:" -f $failures.Count) -ForegroundColor Red
    foreach ($f in ($failures | Sort-Object Class)) {
        Write-Host ("  {0}: line {1}% / branch {2}%" -f $f.Class, $f.LinePct, $f.BranchPct) -ForegroundColor Yellow
    }
    exit 1
}

Write-Host "coverage-gate OK -- every instrumented class meets the threshold." -ForegroundColor Green
exit 0
