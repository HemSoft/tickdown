# Copyright © 2025 HemSoft

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $repositoryRoot 'scripts/RunApp.psm1') -Force
$script:assertionCount = 0

function Assert-Equal {
    param($Expected, $Actual, [string] $Name)

    $script:assertionCount++
    if ($Expected -ne $Actual) {
        throw "$Name expected '$Expected' but found '$Actual'."
    }
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('tickdown-run-tests-' + [guid]::NewGuid())
try {
    New-Item (Join-Path $tempRoot 'src/bin/Debug/net10.0-windows') -ItemType Directory -Force | Out-Null
    Set-Content (Join-Path $tempRoot 'Directory.Build.props') '<Project />'
    Set-Content (Join-Path $tempRoot 'src/App.xaml') '<Application />'
    Set-Content (Join-Path $tempRoot 'src/packages.lock.json') '{}'

    $fingerprint = Get-TickDownRunFingerprint $tempRoot
    $fingerprintPath = Join-Path $tempRoot '.run/Debug.fingerprint'
    $manifestPath = Join-Path $tempRoot '.run/Debug.outputs'
    $targetPath = Join-Path $tempRoot 'src/bin/Debug/net10.0-windows/TickDown.dll'
    $outputPaths = @(Get-TickDownRunOutputPaths $targetPath)
    Assert-Equal 4 $outputPaths.Count 'Required output count'
    Assert-Equal ([IO.Path]::GetFullPath($targetPath)) $outputPaths[1] 'Managed target path'
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $fingerprint) 'Missing outputs require build'

    foreach ($path in $outputPaths) {
        New-Item ([IO.Path]::GetDirectoryName($path)) -ItemType Directory -Force | Out-Null
        Set-Content $path 'output'
    }

    New-Item ([IO.Path]::GetDirectoryName($fingerprintPath)) -ItemType Directory -Force | Out-Null
    $manifestOutputs = @(Get-TickDownRunOutputManifest $targetPath)
    Assert-Equal 4 $manifestOutputs.Count 'Output manifest count'
    Set-Content $manifestPath $manifestOutputs
    Set-Content $fingerprintPath $fingerprint
    Assert-Equal $false (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $fingerprint) 'Matching fingerprint skips build'

    Remove-Item $targetPath
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $fingerprint) 'Missing managed assembly requires build'
    Set-Content $targetPath 'output'

    Remove-Item $outputPaths[0]
    $decoyPath = Join-Path $tempRoot 'src/bin/Debug/net10.0-windows/win-x64/TickDown.exe'
    New-Item ([IO.Path]::GetDirectoryName($decoyPath)) -ItemType Directory -Force | Out-Null
    Set-Content $decoyPath 'stale RID output'
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $fingerprint) 'RID decoy does not satisfy exact output'
    Set-Content $outputPaths[0] 'output'

    $dependencyOutput = Join-Path ([IO.Path]::GetDirectoryName($targetPath)) 'TickDown.Core.dll'
    Set-Content $dependencyOutput 'dependency output'
    Set-Content $manifestPath @($outputPaths + $dependencyOutput)
    Remove-Item $dependencyOutput
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $fingerprint) 'Missing manifest output requires build'
    Set-Content $dependencyOutput 'dependency output'

    Set-Content (Join-Path $tempRoot 'src/App.xaml') '<Application RequestedTheme="Dark" />'
    $changedFingerprint = Get-TickDownRunFingerprint $tempRoot
    Assert-Equal $false ($fingerprint -eq $changedFingerprint) 'Source edit changes fingerprint'
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $changedFingerprint) 'Source edit requires build'

    Set-Content $fingerprintPath $changedFingerprint
    Set-Content (Join-Path $tempRoot 'src/packages.lock.json') '{"changed":true}'
    $dependencyFingerprint = Get-TickDownRunFingerprint $tempRoot
    Assert-Equal $false ($changedFingerprint -eq $dependencyFingerprint) 'Dependency edit changes fingerprint'
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $dependencyFingerprint) 'Dependency edit requires build'

    New-Item (Join-Path $tempRoot 'src/obj') -ItemType Directory -Force | Out-Null
    Set-Content (Join-Path $tempRoot 'src/obj/generated.cs') '// generated'
    Assert-Equal $dependencyFingerprint (Get-TickDownRunFingerprint $tempRoot) 'Generated files are ignored'

    Set-Content $fingerprintPath $dependencyFingerprint
    Set-Content (Join-Path $tempRoot 'README.md') '# Documentation'
    Assert-Equal $dependencyFingerprint (Get-TickDownRunFingerprint $tempRoot) 'Documentation is ignored'
    Assert-Equal $false (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $dependencyFingerprint) 'Unrelated edit skips build'

    $runScript = Get-Content (Join-Path $repositoryRoot 'run.ps1') -Raw
    Assert-Equal $true $runScript.Contains('dotnet restore $project --locked-mode') 'Locked cold restore'
    Assert-Equal $true $runScript.Contains('--no-build --no-restore') 'Warm launch without build or restore'
    Assert-Equal $true $runScript.Contains('$PSNativeCommandUseErrorActionPreference = $true') 'Native failure propagation'
    Assert-Equal $true $runScript.Contains('-getProperty:TargetPath') 'Exact target resolution'

    Write-Host "Passed $script:assertionCount run-app assertions."
}
finally {
    Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}