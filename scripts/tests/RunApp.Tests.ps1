#Requires -Version 7.4
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."
Import-Module (Join-Path $root 'scripts/RunApp.psm1') -Force
$temp = Join-Path ([IO.Path]::GetTempPath()) "tickdown-run-app-$([Guid]::NewGuid())"
$passed = 0

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name expected '$Expected', got '$Actual'" }
    $script:passed++
}

try {
    $null = New-Item (Join-Path $temp 'src/Assets') -ItemType Directory -Force
    $null = New-Item (Join-Path $temp 'src/bin/Debug/net10.0') -ItemType Directory -Force
    $null = New-Item (Join-Path $temp 'src/obj') -ItemType Directory -Force
    Set-Content (Join-Path $temp 'src/App.xaml.cs') 'first source' -Encoding utf8
    Set-Content (Join-Path $temp 'src/Assets/app.ico') 'first asset' -Encoding utf8
    Set-Content (Join-Path $temp 'src/bin/ignored.txt') 'first output' -Encoding utf8
    Set-Content (Join-Path $temp 'src/obj/ignored.txt') 'first intermediate' -Encoding utf8
    Set-Content (Join-Path $temp 'Directory.Build.props') '<Project />' -Encoding utf8
    Set-Content (Join-Path $temp 'global.json') '{}' -Encoding utf8

    $initial = Get-TickDownRunInputFingerprint $temp
    Assert-Equal $initial (Get-TickDownRunInputFingerprint $temp) 'Stable input fingerprint'
    Set-Content (Join-Path $temp 'src/bin/ignored.txt') 'changed output' -Encoding utf8
    Set-Content (Join-Path $temp 'src/obj/ignored.txt') 'changed intermediate' -Encoding utf8
    Assert-Equal $initial (Get-TickDownRunInputFingerprint $temp) 'Generated output exclusion'

    Set-Content (Join-Path $temp 'src/App.xaml.cs') 'changed source' -Encoding utf8
    $sourceChanged = Get-TickDownRunInputFingerprint $temp
    Assert-Equal $true ($sourceChanged -cne $initial) 'Source invalidation'
    Set-Content (Join-Path $temp 'src/Assets/app.ico') 'changed asset' -Encoding utf8
    $assetChanged = Get-TickDownRunInputFingerprint $temp
    Assert-Equal $true ($assetChanged -cne $sourceChanged) 'Asset invalidation'
    Set-Content (Join-Path $temp 'Directory.Build.props') '<Project><PropertyGroup /></Project>' -Encoding utf8
    $buildChanged = Get-TickDownRunInputFingerprint $temp
    Assert-Equal $true ($buildChanged -cne $assetChanged) 'Build policy invalidation'

    $fingerprintPath = Join-Path $temp 'src/obj/run-Debug.sha256'
    $executablePath = Join-Path $temp 'src/bin/Debug/net10.0/TickDown.exe'
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $null $buildChanged) 'Undiscovered output build requirement'
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $executablePath $buildChanged) 'Missing output build requirement'
    Set-Content $executablePath '' -Encoding utf8
    Set-TickDownRunFingerprint $fingerprintPath $buildChanged
    Assert-Equal $false (Test-TickDownRunBuildRequired $fingerprintPath $executablePath $buildChanged) 'Current output fast path'
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $executablePath 'different') 'Stale fingerprint build requirement'
    Remove-Item $executablePath
    Assert-Equal $true (Test-TickDownRunBuildRequired $fingerprintPath $executablePath $buildChanged) 'Deleted output build requirement'

    Set-Content $executablePath '' -Encoding utf8
    Assert-Equal $executablePath (Find-TickDownRunExecutable $temp 'Debug') 'Executable discovery'
    $runScript = Get-Content (Join-Path $root 'run.ps1') -Raw
    Assert-Equal $true $runScript.Contains('dotnet restore $project --locked-mode') 'Locked cold restore'
    Assert-Equal $true $runScript.Contains('--no-build --no-restore') 'Warm launch without build or restore'
    Assert-Equal $true $runScript.Contains('$PSNativeCommandUseErrorActionPreference = $true') 'Native failure propagation'
    "Passed $passed run-app assertions."
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
