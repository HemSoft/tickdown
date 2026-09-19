#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."
Import-Module (Join-Path $root 'scripts/MutationQuality.psm1') -Force
$temp = Join-Path ([IO.Path]::GetTempPath()) "tickdown-mutation-$([Guid]::NewGuid())"
New-Item $temp -ItemType Directory | Out-Null
$passed = 0

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name expected '$Expected', got '$Actual'" }
    $script:passed++
}

try {
    $reportPath = Join-Path $temp 'mutation-report.json'
    @'
{
  "files": {
    "src\\TickDown.Core\\Models\\CountdownTimer.cs": {
      "mutants": [
        { "id": "1", "status": "Killed", "mutatorName": "Equality mutation", "replacement": "false", "location": { "start": { "line": 10 } } },
        { "id": "2", "status": "Survived", "mutatorName": "Block mutation", "replacement": "{}", "location": { "start": { "line": 20 } } },
        { "id": "3", "status": "Ignored", "mutatorName": "String mutation", "replacement": "x", "location": { "start": { "line": 30 } } }
      ]
    }
  }
}
'@ | Set-Content $reportPath -Encoding utf8
    $summary = Get-MutationSummary $reportPath
    Assert-Equal 3 $summary.Total 'Mutant count'
    Assert-Equal 1 $summary.Counts.Killed 'Killed count'
    Assert-Equal 1 $summary.Counts.Survived 'Survived count'
    Assert-Equal 1 $summary.Counts.Ignored 'Ignored count'
    Assert-Equal 20 $summary.Survivors[0].Line 'Survivor line'

    $summaryPath = Join-Path $temp 'summary.md'
    Write-MutationSummary $summary $summaryPath ('a' * 40) 50 12.5
    $markdown = Get-Content $summaryPath -Raw
    Assert-Equal $true ($markdown.Contains('Mutation score: 50.00%')) 'Score publication'
    Assert-Equal $true ($markdown.Contains('Block mutation')) 'Survivor publication'

    $manifest = Get-Content (Join-Path $root 'dotnet-tools.json') -Raw | ConvertFrom-Json
    Assert-Equal '5.0.0' $manifest.tools.'dotnet-stryker'.version 'Pinned Stryker version'
    Assert-Equal $false $manifest.tools.'dotnet-stryker'.rollForward 'Tool roll-forward policy'

    $config = (Get-Content (Join-Path $root 'tests/TickDown.Tests/stryker-config.json') -Raw | ConvertFrom-Json).'stryker-config'
    Assert-Equal '**/Models/CountdownTimer.cs' $config.mutate[0] 'Bounded mutation target'
    Assert-Equal 1 $config.mutate.Count 'Mutation target count'
    Assert-Equal 100 $config.thresholds.high 'Target threshold'
    Assert-Equal 100 $config.thresholds.low 'Low threshold'
    Assert-Equal 100 $config.thresholds.break 'Break threshold'
    Assert-Equal 'html,json,progress' (($config.reporters | Sort-Object) -join ',') 'Required reporters'
    "Passed $passed mutation-quality assertions."
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
