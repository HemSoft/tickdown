#Requires -Version 7.4
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$root = $PSScriptRoot
$project = Join-Path $root 'src/TickDown.csproj'
Import-Module (Join-Path $root 'scripts/RunApp.psm1') -Force

$fingerprint = Get-TickDownRunInputFingerprint $root
$fingerprintPath = Join-Path $root "src/obj/run-$Configuration.sha256"
$executablePath = Find-TickDownRunExecutable $root $Configuration
if (Test-TickDownRunBuildRequired $fingerprintPath $executablePath $fingerprint) {
    dotnet restore $project --locked-mode
    dotnet build $project --configuration $Configuration --no-restore
    $executablePath = Find-TickDownRunExecutable $root $Configuration
    if ([string]::IsNullOrWhiteSpace($executablePath)) {
        throw "The $Configuration build completed without producing TickDown.exe."
    }

    Set-TickDownRunFingerprint $fingerprintPath $fingerprint
}

dotnet run --project $project --configuration $Configuration --no-build --no-restore
