#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts/mutation',
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Resolve-Path "$PSScriptRoot/.."
$testProject = Join-Path $root 'tests/TickDown.Tests'
Import-Module (Join-Path $PSScriptRoot 'MutationQuality.psm1') -Force
$outputPath = Resolve-MutationOutputPath $root $OutputDirectory

Push-Location $root
try {
    $candidate = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the candidate revision.' }
    $dirty = (& git status --porcelain --untracked-files=all | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the candidate working tree.' }
    if (!$AllowDirty -and $dirty) { throw "Mutation results require a clean candidate tree:`n$dirty" }
    if ($env:GITHUB_SHA -and $env:GITHUB_SHA -ne $candidate) {
        throw "Checked-out candidate $candidate does not match GITHUB_SHA $env:GITHUB_SHA."
    }

    $toolOutput = (& dotnet tool restore 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Pinned tool restore failed:`n$toolOutput" }
    if (Test-Path $outputPath) { Remove-Item $outputPath -Recurse -Force }

    $timer = [Diagnostics.Stopwatch]::StartNew()
    Push-Location $testProject
    try {
        $runOutput = (& dotnet tool run dotnet-stryker -- -O $outputPath --skip-version-check 2>&1 | Out-String).Trim()
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
        $timer.Stop()
    }

    New-Item $outputPath -ItemType Directory -Force | Out-Null
    $runOutput | Set-Content (Join-Path $outputPath 'console.log') -Encoding utf8
    $reportFiles = @(Get-ChildItem $outputPath -Filter mutation-report.json -Recurse)
    if ($reportFiles.Count -ne 1) { throw "Expected one mutation JSON report, found $($reportFiles.Count)." }
    if ($runOutput -notmatch 'final mutation score is ([0-9.]+) %') {
        throw 'Stryker output did not contain a final mutation score.'
    }

    $score = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
    $summary = Get-MutationSummary $reportFiles[0].FullName
    Write-MutationSummary $summary (Join-Path $outputPath 'mutation-summary.md') $candidate $score $timer.Elapsed.TotalSeconds
    $reportHash = (Get-FileHash $reportFiles[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    [ordered]@{
        candidate = $candidate
        workingTreeClean = !$dirty
        tool = 'dotnet-stryker'
        toolVersion = '5.0.0'
        mutationScore = $score
        elapsedSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3)
        reportSha256 = $reportHash
        counts = $summary.Counts
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $outputPath 'candidate.json') -Encoding utf8

    if ($exitCode -ne 0) { throw "Stryker failed the configured mutation threshold with exit code $exitCode. Reports remain in $outputPath." }
    "Mutation gate passed at $($score.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture))% for $candidate in $($timer.Elapsed.TotalSeconds.ToString('0.00')) seconds."
    "Reports: $outputPath"
}
finally {
    Pop-Location
}
