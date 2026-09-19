#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Resolve-Path "$PSScriptRoot/../.."
$passed = 0

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name expected '$Expected', got '$Actual'" }
    $script:passed++
}

Push-Location $root
try {
    $sdkPolicy = Get-Content global.json -Raw | ConvertFrom-Json
    Assert-Equal '10.0.108' $sdkPolicy.sdk.version 'Pinned SDK'
    Assert-Equal 'disable' $sdkPolicy.sdk.rollForward 'SDK roll-forward policy'
    Assert-Equal $false $sdkPolicy.sdk.allowPrerelease 'Prerelease SDK policy'
    Assert-Equal '10.0.108' (& dotnet --version) 'Resolved SDK'

    [xml]$buildProps = Get-Content Directory.Build.props -Raw
    Assert-Equal '10-recommended' $buildProps.Project.PropertyGroup.AnalysisLevel 'Analyzer policy'
    Assert-Equal 'true' $buildProps.Project.PropertyGroup.RestorePackagesWithLockFile 'Lock generation policy'

    $projects = @(Get-ChildItem src, tests -Filter *.csproj -Recurse |
        Where-Object FullName -NotMatch '[\\/]obj[\\/]')
    Assert-Equal 4 $projects.Count 'Tracked project count'
    foreach ($project in $projects) {
        $lockPath = Join-Path $project.DirectoryName 'packages.lock.json'
        if (!(Test-Path $lockPath)) { throw "Missing lock file for $($project.FullName)" }
        $lock = Get-Content $lockPath -Raw | ConvertFrom-Json -AsHashtable
        Assert-Equal 1 $lock.version "$($project.Name) lock format"
        if ($lock.dependencies.Count -eq 0) { throw "$($project.Name) lock has no target frameworks" }
        $passed++

    }

    $packageManifests = @((Get-Item Directory.Build.props)) + $projects
    foreach ($manifest in $packageManifests) {
        [xml]$xml = Get-Content $manifest.FullName -Raw
        $references = @($xml.Project.ItemGroup.PackageReference) | Where-Object { $null -ne $_ }
        foreach ($reference in $references) {
            $version = [string]$reference.Version
            if ($version -notmatch '^\[[^,\[\]()]+\]$') {
                throw "$($manifest.Name) must use an exact closed version for $($reference.Include): $version"
            }
            $passed++
        }
    }

    $restoreOutput = (& dotnet restore TickDown.sln --locked-mode --no-cache 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Locked restore failed:`n$restoreOutput" }
    $passed++
    "Passed $passed dependency-resolution assertions."
}
finally {
    Pop-Location
}
