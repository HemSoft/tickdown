#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ResultsDirectory = 'artifacts/coverage',
    [switch]$UpdateBaseline
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Resolve-Path "$PSScriptRoot/.."
$resultsPath = [IO.Path]::GetFullPath((Join-Path $root $ResultsDirectory))
$baselinePath = Join-Path $PSScriptRoot 'function-risk-baseline.json'
Import-Module (Join-Path $PSScriptRoot 'CoverageQuality.psm1') -Force

Push-Location $root
try {
    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Recurse -Force }
    $testOutput = (& dotnet test TickDown.sln --configuration Release --no-restore `
        --collect:'XPlat Code Coverage' --settings coverage.runsettings `
        --results-directory $resultsPath 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Coverage test run failed:`n$testOutput" }

    $coverageFiles = @(Get-ChildItem $resultsPath -Filter coverage.cobertura.xml -Recurse)
    if ($coverageFiles.Count -ne 1) { throw "Expected one Cobertura report, found $($coverageFiles.Count)." }
    $functions = @(Get-CoverageFunctions $coverageFiles[0].FullName)
    Write-CoverageReports $functions $resultsPath
    Copy-Item $coverageFiles[0].FullName (Join-Path $resultsPath 'coverage.cobertura.xml') -Force

    if ($UpdateBaseline) {
        Write-CoverageBaseline $functions $baselinePath
        "Updated coverage baseline with $($functions.Count) functions."
        return
    }

    $failures = @(Test-CoverageBaseline $functions $baselinePath)
    if ($failures.Count -gt 0) { throw "Coverage risk gate failed:`n- $($failures -join "`n- ")" }

    $worst = $functions | Sort-Object Crap -Descending | Select-Object -First 1
    "Coverage risk gate passed for $($functions.Count) functions. Worst: $($worst.Id), CRAP $($worst.Crap)."
    "Reports: $resultsPath/function-risk.md and function-risk.json"
}
finally {
    Pop-Location
}
