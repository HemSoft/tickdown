#Requires -Version 7.4

function Get-TickDownRunInputFingerprint {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $sourceRoot = Join-Path $root 'src'
    $sourceFiles = @(Get-ChildItem $sourceRoot -File -Recurse | Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
        })
    $rootInputs = @(
        '.editorconfig'
        'Directory.Build.props'
        'Directory.Build.targets'
        'global.json'
        'SonarLint.xml'
        'stylecop.json'
        'TickDown.sln'
    ) | ForEach-Object { Get-Item (Join-Path $root $_) -ErrorAction SilentlyContinue }
    $entries = @($sourceFiles + $rootInputs | Sort-Object FullName | ForEach-Object {
            $relativePath = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
            $contentHash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
            "$relativePath`0$contentHash"
        })
    if ($entries.Count -eq 0) {
        throw "No TickDown build inputs were found under $root."
    }

    $payload = [Text.Encoding]::UTF8.GetBytes($entries -join "`n")
    return [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($payload))
}

function Test-TickDownRunBuildRequired {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FingerprintPath,
        [Parameter(Mandatory)][AllowNull()][AllowEmptyString()][string]$ExecutablePath,
        [Parameter(Mandatory)][string]$InputFingerprint
    )

    if ([string]::IsNullOrWhiteSpace($ExecutablePath) -or
        !(Test-Path $ExecutablePath -PathType Leaf) -or
        !(Test-Path $FingerprintPath -PathType Leaf)) {
        return $true
    }

    $savedFingerprint = (Get-Content $FingerprintPath -Raw).Trim()
    return $savedFingerprint -cne $InputFingerprint
}

function Set-TickDownRunFingerprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FingerprintPath,
        [Parameter(Mandatory)][string]$InputFingerprint
    )

    $null = New-Item (Split-Path $FingerprintPath) -ItemType Directory -Force
    Set-Content $FingerprintPath $InputFingerprint -Encoding ascii -NoNewline
}

function Find-TickDownRunExecutable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$Configuration
    )

    $outputRoot = Join-Path $RepositoryRoot "src/bin/$Configuration"
    return Get-ChildItem $outputRoot -Filter TickDown.exe -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

Export-ModuleMember -Function Get-TickDownRunInputFingerprint, Test-TickDownRunBuildRequired, Set-TickDownRunFingerprint, Find-TickDownRunExecutable
