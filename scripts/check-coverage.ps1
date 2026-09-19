#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ResultsDirectory = 'artifacts/coverage',
    [switch]$UpdateBaseline
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Resolve-Path "$PSScriptRoot/.."
$baselinePath = Join-Path $PSScriptRoot 'function-risk-baseline.json'
Import-Module (Join-Path $PSScriptRoot 'CoverageQuality.psm1') -Force
$resultsPath = Resolve-CoverageResultsPath $root $ResultsDirectory
$appProject = Join-Path $root 'src/TickDown.csproj'
$defineConstantsOutput = (& dotnet msbuild $appProject -getProperty:DefineConstants -p:Configuration=Release -p:Platform=x64 -nologo 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($defineConstantsOutput)) {
    throw "Could not resolve the application preprocessor symbols:`n$defineConstantsOutput"
}
$defineConstants = @($defineConstantsOutput -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 })[-1].Trim() -split ';'
$encodedDefineConstants = $defineConstants -join '%3B'
$sourceExclusionViolations = @(Get-CoverageSourceExclusionViolations (Join-Path $root 'src') $defineConstants)
if ($sourceExclusionViolations.Count -gt 0) {
    throw "Coverage exclusion attributes are forbidden in production source:`n- $($sourceExclusionViolations -join "`n- ")"
}
$linkedTypePatterns = @{
    'TickDown.ViewModels.MainViewModel' = 'ViewModels/MainViewModel.cs'
    'TickDown.ViewModels.TimerViewModel' = 'ViewModels/TimerViewModel.cs'
    'TickDown.Services.SettingsService' = 'Services/SettingsService.cs'
}
$partialTypeViolations = @(
    Get-UnlinkedPartialTypeViolations (Join-Path $root 'src') $linkedTypePatterns $defineConstants
)
if ($partialTypeViolations.Count -gt 0) {
    throw "Source-linked partial declarations must stay in the linked top-level glob:`n- $($partialTypeViolations -join "`n- ")"
}

Push-Location $root
try {
    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Recurse -Force }
    $appBuildOutput = (& dotnet build $appProject --configuration Release --no-restore -p:Platform=x64 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Coverage application build failed:`n$appBuildOutput" }
    $appAssemblyOutput = (& dotnet msbuild $appProject -getProperty:TargetPath -p:Configuration=Release -p:Platform=x64 -nologo 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($appAssemblyOutput)) {
        throw "Could not resolve the current Release application assembly:`n$appAssemblyOutput"
    }
    $appAssemblyPath = @($appAssemblyOutput -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 })[-1].Trim()
    $testProject = Join-Path $root 'tests/TickDown.Tests/TickDown.Tests.csproj'
    $testBuildOutput = (& dotnet build $testProject --configuration Release --no-restore "-p:DefineConstants=$encodedDefineConstants" 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Coverage test build failed:`n$testBuildOutput" }
    $testAssemblyOutput = (& dotnet msbuild $testProject -getProperty:TargetPath -p:Configuration=Release -nologo 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($testAssemblyOutput)) {
        throw "Could not resolve the current Release test assembly:`n$testAssemblyOutput"
    }
    $testAssemblyPath = @($testAssemblyOutput -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 })[-1].Trim()
    $testOutputDirectory = Split-Path $testAssemblyPath -Parent
    $coverageAppAssembly = Join-Path $testOutputDirectory 'TickDown.dll'
    $appOutputDirectory = Split-Path $appAssemblyPath -Parent
    Get-ChildItem $appOutputDirectory -File | Where-Object Extension -in '.dll', '.pdb' |
        Copy-Item -Destination $testOutputDirectory -Force
    $sourceLinkedTestTypes = @(
        'TickDown.ViewModels.MainViewModel'
        'TickDown.ViewModels.TimerViewModel'
        'TickDown.Services.SettingsService'
    )
    $previousCoverageAssembly = $env:TICKDOWN_COVERAGE_APP_ASSEMBLY
    try {
        $env:TICKDOWN_COVERAGE_APP_ASSEMBLY = $coverageAppAssembly
        $testOutput = (& dotnet test $testProject --configuration Release --no-restore --no-build `
            "-p:DefineConstants=$encodedDefineConstants" --collect:'XPlat Code Coverage' --settings coverage.runsettings `
            --results-directory $resultsPath 2>&1 | Out-String).Trim()
    }
    finally {
        $env:TICKDOWN_COVERAGE_APP_ASSEMBLY = $previousCoverageAssembly
    }
    if ($LASTEXITCODE -ne 0) { throw "Coverage test run failed:`n$testOutput" }
    $unexpectedLinkedTypes = @(Get-UnexpectedSourceLinkedTypes $testAssemblyPath $sourceLinkedTestTypes)
    if ($unexpectedLinkedTypes.Count -gt 0) {
        throw "Broad collector filters matched unexpected test types:`n- $($unexpectedLinkedTypes -join "`n- ")"
    }

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

    $coveredAssemblies = @(Resolve-CoveredAssemblyPaths $expectedAssemblyNames $testAssemblyPath)
    $trustedGeneratedMembers = @(
        'TickDown.ViewModels.MainViewModel.AddTimerCommand'
        'TickDown.ViewModels.TimerViewModel.StartCommand'
        'TickDown.ViewModels.TimerViewModel.PauseCommand'
        'TickDown.ViewModels.TimerViewModel.StopCommand'
        'TickDown.ViewModels.TimerViewModel.ResetCommand'
        'TickDown.ViewModels.TimerViewModel.RemoveCommand'
        'TickDown.ViewModels.TimerViewModel.DismissCommand'
        'TickDown.ViewModels.TimerViewModel.SetQuickTimeCommand'
        'TickDown.ViewModels.TimerViewModel.SetEndTimeCommand'
    )
    $exclusionViolations = @(
        Get-CoverageExclusionViolations $coveredAssemblies $trustedGeneratedMembers $sourceLinkedTestTypes
    )
    if ($exclusionViolations.Count -gt 0) { throw "Coverage exclusion attributes are forbidden:`n- $($exclusionViolations -join "`n- ")" }
    $stateMachineMap = Get-StateMachineMap $coveredAssemblies
    $genericMethodArities = Get-MethodGenericArities $coveredAssemblies
    $conversionReturnTypes = Get-ConversionReturnTypes $coveredAssemblies
    $functions = @(Get-CoverageFunctions $coverageFiles[0].FullName $stateMachineMap $genericMethodArities $conversionReturnTypes)
    Write-CoverageReports $functions $resultsPath
    Copy-Item $coverageFiles[0].FullName (Join-Path $resultsPath 'coverage.cobertura.xml') -Force

    if ($UpdateBaseline) {
        $updateFailures = @(Test-CoverageBaseline $functions $baselinePath -AllowNewFunctions)
        if ($updateFailures.Count -gt 0) { throw "Coverage baseline update rejected:`n- $($updateFailures -join "`n- ")" }
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
