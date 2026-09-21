# Copyright © 2025 HemSoft

Set-StrictMode -Version Latest

function Get-TickDownRunFingerprint {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot
    )

    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $inputs = [Collections.Generic.List[IO.FileInfo]]::new()
    $rootInputPaths = @(
        '.editorconfig',
        'Directory.Build.props',
        'Directory.Build.targets',
        'Directory.Packages.props',
        'global.json',
        'SonarLint.xml',
        'stylecop.json',
        'TickDown.sln'
    )
    foreach ($relativePath in $rootInputPaths) {
        $path = Join-Path $root $relativePath
        if (Test-Path $path -PathType Leaf) {
            $inputs.Add((Get-Item $path))
        }
    }

    $sourceRoot = Join-Path $root 'src'
    if (Test-Path $sourceRoot -PathType Container) {
        foreach ($file in Get-ChildItem $sourceRoot -File -Recurse) {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]') {
                continue
            }

            $inputs.Add($file)
        }
    }

    $builder = [Text.StringBuilder]::new()
    foreach ($file in $inputs | Sort-Object FullName -Unique) {
        $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        [void]$builder.Append($relativePath)
        [void]$builder.Append("`n")
        [void]$builder.Append((Get-FileHash $file.FullName -Algorithm SHA256).Hash)
        [void]$builder.Append("`n")
    }

    $bytes = [Text.Encoding]::UTF8.GetBytes($builder.ToString())
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash)
}

function Get-TickDownRunOutputPaths {
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)]
        [string] $TargetPath
    )

    $resolvedTargetPath = [IO.Path]::GetFullPath($TargetPath)
    $targetDirectory = [IO.Path]::GetDirectoryName($resolvedTargetPath)
    $targetName = [IO.Path]::GetFileNameWithoutExtension($resolvedTargetPath)
    $targetStem = Join-Path $targetDirectory $targetName
    return @(
        "$targetStem.exe",
        $resolvedTargetPath,
        "$targetStem.deps.json",
        "$targetStem.runtimeconfig.json"
    )
}

function Get-TickDownRunOutputManifest {
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)]
        [string] $TargetPath
    )

    $targetDirectory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($TargetPath))
    return @(Get-ChildItem $targetDirectory -File -Recurse | ForEach-Object FullName | Sort-Object -Unique)
}

function Test-TickDownRunBuildRequired {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string] $FingerprintPath,

        [Parameter(Mandatory)]
        [string] $ManifestPath,

        [Parameter(Mandatory)]
        [string[]] $OutputPaths,

        [Parameter(Mandatory)]
        [string] $CurrentFingerprint
    )

    foreach ($outputPath in $OutputPaths) {
        if (-not (Test-Path $outputPath -PathType Leaf)) {
            return $true
        }
    }

    if (-not (Test-Path $FingerprintPath -PathType Leaf) -or
        -not (Test-Path $ManifestPath -PathType Leaf)) {
        return $true
    }

    if ((Get-Content $FingerprintPath -Raw).Trim() -ne $CurrentFingerprint) {
        return $true
    }

    $manifestOutputs = @(Get-Content $ManifestPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($manifestOutputs.Count -eq 0) {
        return $true
    }

    foreach ($manifestOutput in $manifestOutputs) {
        if (-not (Test-Path $manifestOutput -PathType Leaf)) {
            return $true
        }
    }

    return $false
}

Export-ModuleMember -Function Get-TickDownRunFingerprint, Get-TickDownRunOutputManifest, Get-TickDownRunOutputPaths, Test-TickDownRunBuildRequired