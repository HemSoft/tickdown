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
    [xml]$coverageDocument = Get-Content $coverageFiles[0].FullName -Raw
    [xml]$runsettings = Get-Content (Join-Path $root 'coverage.runsettings') -Raw
    $includeFilter = [string]$runsettings.RunSettings.DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include
    $expectedAssemblyNames = @(
        [regex]::Matches($includeFilter, '\[([^]]+)\]') |
            ForEach-Object { $_.Groups[1].Value } |
            Sort-Object -Unique
    )
    $reportedAssemblyNames = @($coverageDocument.coverage.packages.package.name | Sort-Object -Unique)
    $missingAssemblies = @($expectedAssemblyNames | Where-Object { $_ -notin $reportedAssemblyNames })
    if ($missingAssemblies.Count -gt 0) { throw "Covered assemblies missing from Cobertura: $($missingAssemblies -join ', ')." }

    $assemblySearchRoot = Join-Path $root 'tests/TickDown.Tests/bin/Release'
    $coveredAssemblies = @(
        $expectedAssemblyNames | ForEach-Object {
            $assemblyName = $_
            $matches = @(
                Get-ChildItem $assemblySearchRoot -Filter "$assemblyName.dll" -File -Recurse |
                    Where-Object FullName -NotMatch '[\\/]ref[\\/]'
            )
            if ($matches.Count -ne 1) { throw "Expected one built assembly for $assemblyName, found $($matches.Count)." }
            $matches[0].FullName
        }
    )
    $exclusionViolations = @(Get-CoverageExclusionViolations $coveredAssemblies)
    if ($exclusionViolations.Count -gt 0) { throw "Coverage exclusion attributes are forbidden:`n- $($exclusionViolations -join "`n- ")" }
    $stateMachineMap = Get-StateMachineMap $coveredAssemblies
    $functions = @(Get-CoverageFunctions $coverageFiles[0].FullName $stateMachineMap)
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
