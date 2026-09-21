#Requires -Version 7.4
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repositoryRoot = $PSScriptRoot
$project = Join-Path $repositoryRoot 'src/TickDown.csproj'
$stateDirectory = Join-Path $repositoryRoot 'src/obj/TickDown.Run'
$fingerprintPath = Join-Path $stateDirectory "$Configuration.fingerprint"
$manifestPath = Join-Path $stateDirectory "$Configuration.outputs"
$targetPathCache = Join-Path $stateDirectory "$Configuration.target"
Import-Module (Join-Path $repositoryRoot 'scripts/RunApp.psm1') -Force

$fingerprint = Get-TickDownRunFingerprint $repositoryRoot
$expectedOutputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "src/bin/$Configuration")) + [IO.Path]::DirectorySeparatorChar
$targetPath = $null
if ((Test-Path $fingerprintPath -PathType Leaf) -and
    (Test-Path $targetPathCache -PathType Leaf) -and
    (Get-Content $fingerprintPath -Raw).Trim() -eq $fingerprint) {
    $cachedTargetPath = [IO.Path]::GetFullPath((Get-Content $targetPathCache -Raw).Trim())
    if ($cachedTargetPath.StartsWith($expectedOutputRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $targetPath = $cachedTargetPath
    }
}

if ([string]::IsNullOrWhiteSpace($targetPath)) {
    $targetPathOutput = @(dotnet msbuild $project -nologo -getProperty:TargetPath -p:Configuration=$Configuration)
    $targetPath = $targetPathOutput.Where({ -not [string]::IsNullOrWhiteSpace($_) })[-1].Trim()
}

$outputPaths = Get-TickDownRunOutputPaths $targetPath
if (Test-TickDownRunBuildRequired $fingerprintPath $manifestPath $outputPaths $fingerprint) {
    dotnet restore $project --locked-mode
    dotnet build $project --configuration $Configuration --no-restore

    $missingOutputs = @($outputPaths.Where({ -not (Test-Path $_ -PathType Leaf) }))
    if ($missingOutputs.Count -gt 0) {
        throw "Build completed without required launch output: $($missingOutputs -join ', ')"
    }

    $manifestOutputs = @(Get-TickDownRunOutputManifest $targetPath)
    if ($manifestOutputs.Count -eq 0) {
        throw "Build completed without files in the target output directory."
    }

    New-Item $stateDirectory -ItemType Directory -Force | Out-Null
    Set-Content $manifestPath $manifestOutputs -Encoding utf8NoBOM
    Set-Content $fingerprintPath $fingerprint -Encoding utf8NoBOM
}

New-Item $stateDirectory -ItemType Directory -Force | Out-Null
Set-Content $targetPathCache $targetPath -Encoding utf8NoBOM

dotnet run --project $project --configuration $Configuration --no-build --no-restore