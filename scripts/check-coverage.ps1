#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ResultsDirectory = 'artifacts/coverage',
    [switch]$UpdateBaseline
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

function Get-ProductionProjectInventory([string]$RootProject) {
    $pending = [Collections.Generic.Queue[string]]::new()
    $pending.Enqueue([IO.Path]::GetFullPath($RootProject))
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $inventory = [Collections.Generic.List[object]]::new()
    while ($pending.Count -gt 0) {
        $project = $pending.Dequeue()
        if (!$seen.Add($project)) { continue }
        $queryOutput = (& dotnet msbuild $project -getProperty:TargetPath -getItem:ProjectReference `
            -p:Configuration=Release -p:Platform=x64 -nologo 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($queryOutput)) {
            throw "Could not resolve production project inventory for $project`:`n$queryOutput"
        }
        try { $query = $queryOutput | ConvertFrom-Json -Depth 20 }
        catch { throw "Invalid MSBuild project inventory for $project`:`n$queryOutput" }
        $targetPath = [string]$query.Properties.TargetPath
        if ([string]::IsNullOrWhiteSpace($targetPath)) { throw "Project did not return TargetPath: $project" }
        $inventory.Add([pscustomobject]@{
                ProjectPath = $project
                TargetPath = [IO.Path]::GetFullPath($targetPath)
                AssemblyName = [IO.Path]::GetFileNameWithoutExtension($targetPath)
            })
        foreach ($reference in @($query.Items.ProjectReference)) {
            if ($null -ne $reference -and ![string]::IsNullOrWhiteSpace([string]$reference.FullPath)) {
                $pending.Enqueue([IO.Path]::GetFullPath([string]$reference.FullPath))
            }
        }
    }
    return $inventory.ToArray()
}
$root = Resolve-Path "$PSScriptRoot/.."
$baselinePath = Join-Path $PSScriptRoot 'function-risk-baseline.json'
Import-Module (Join-Path $PSScriptRoot 'CoverageQuality.psm1') -Force
$resultsPath = Resolve-CoverageResultsPath $root $ResultsDirectory
$appProject = Join-Path $root 'src/TickDown.csproj'
$productionProjects = @(Get-ProductionProjectInventory $appProject)
$productionAssemblyNames = @($productionProjects.AssemblyName)
$duplicateAssemblyNames = @($productionAssemblyNames | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
if ($duplicateAssemblyNames.Count -gt 0) {
    throw "Production projects have duplicate assembly names: $($duplicateAssemblyNames -join ', ')."
}
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
    New-Item $resultsPath -ItemType Directory | Out-Null
    $appBuildOutput = (& dotnet build $appProject --configuration Release --no-restore -p:Platform=x64 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Coverage application build failed:`n$appBuildOutput" }
    $appAssemblyPath = @($productionProjects | Where-Object ProjectPath -eq ([IO.Path]::GetFullPath($appProject)))[0].TargetPath
    if (!(Test-Path $appAssemblyPath -PathType Leaf)) {
        throw "Current Release application assembly was not produced: $appAssemblyPath"
    }
    $testProject = Join-Path $root 'tests/TickDown.Tests/TickDown.Tests.csproj'
    $testBuildOutput = (& dotnet build $testProject --configuration Release --no-restore "-p:DefineConstants=$encodedDefineConstants" 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Coverage test build failed:`n$testBuildOutput" }
    $testAssemblyOutput = (& dotnet msbuild $testProject -getProperty:TargetPath -p:Configuration=Release -nologo 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($testAssemblyOutput)) {
        throw "Could not resolve the current Release test assembly:`n$testAssemblyOutput"
    }
    $testAssemblyPath = @($testAssemblyOutput -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 })[-1].Trim()
    $testOutputDirectory = Split-Path $testAssemblyPath -Parent
    $appOutputDirectory = Split-Path $appAssemblyPath -Parent
    Get-ChildItem $appOutputDirectory -File | Where-Object Extension -in '.dll', '.pdb' |
        Copy-Item -Destination $testOutputDirectory -Force
    $coverageProductionAssemblies = @($productionAssemblyNames | ForEach-Object { Join-Path $testOutputDirectory "$_.dll" })
    $missingStagedAssemblies = @($coverageProductionAssemblies | Where-Object { !(Test-Path $_ -PathType Leaf) })
    if ($missingStagedAssemblies.Count -gt 0) {
        throw "Production assemblies were not staged beside the test candidate: $($missingStagedAssemblies -join ', ')."
    }
    $sourceLinkedTestTypes = @(
        'TickDown.ViewModels.MainViewModel'
        'TickDown.ViewModels.TimerViewModel'
        'TickDown.Services.SettingsService'
        'TickDown.Diagnostics.QualificationDiagnostics'
        'TickDown.Diagnostics.QualificationRuntimeMetrics'
        'TickDown.Diagnostics.QualificationTick'
    )
    [xml]$effectiveSettings = Get-Content (Join-Path $root 'coverage.runsettings') -Raw
    $productionFilters = @($productionAssemblyNames | ForEach-Object { "[$_]*" })
    $linkedTestFilters = @($sourceLinkedTestTypes | ForEach-Object { "[TickDown.Tests]$_*" })
    $effectiveSettings.RunSettings.DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include =
        ($productionFilters + $linkedTestFilters) -join ','
    $effectiveSettingsPath = Join-Path $resultsPath 'effective.runsettings'
    $effectiveSettings.Save($effectiveSettingsPath)

    $previousCoverageAssemblies = $env:TICKDOWN_COVERAGE_ASSEMBLIES
    try {
        $env:TICKDOWN_COVERAGE_ASSEMBLIES = $coverageProductionAssemblies -join [IO.Path]::PathSeparator
        $testOutput = (& dotnet test $testProject --configuration Release --no-restore --no-build `
            "-p:DefineConstants=$encodedDefineConstants" --collect:'XPlat Code Coverage' --settings $effectiveSettingsPath `
            --results-directory $resultsPath 2>&1 | Out-String).Trim()
    }
    finally {
        $env:TICKDOWN_COVERAGE_ASSEMBLIES = $previousCoverageAssemblies
    }
    if ($LASTEXITCODE -ne 0) { throw "Coverage test run failed:`n$testOutput" }
    $unexpectedLinkedTypes = @(Get-UnexpectedSourceLinkedTypes $testAssemblyPath $sourceLinkedTestTypes)
    if ($unexpectedLinkedTypes.Count -gt 0) {
        throw "Broad collector filters matched unexpected test types:`n- $($unexpectedLinkedTypes -join "`n- ")"
    }

    $coverageFiles = @(Get-ChildItem $resultsPath -Filter coverage.cobertura.xml -Recurse)
    if ($coverageFiles.Count -ne 1) { throw "Expected one Cobertura report, found $($coverageFiles.Count)." }
    [xml]$coverageDocument = Get-Content $coverageFiles[0].FullName -Raw
    $expectedAssemblyNames = @(($productionAssemblyNames + 'TickDown.Tests') | Sort-Object -Unique)
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
    $functions = @(
        Get-CoverageFunctions $coverageFiles[0].FullName $stateMachineMap $genericMethodArities $conversionReturnTypes $root
    )
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
