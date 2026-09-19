Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-MutationOutputPath([string]$RepoRoot, [string]$OutputDirectory) {
    $ownedRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts/mutation'))
    $candidate = [IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputDirectory))
    $ownedPrefix = $ownedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($candidate -ne $ownedRoot -and !$candidate.StartsWith($ownedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Mutation output must stay within the owned directory $ownedRoot; received $candidate."
    }
    return $candidate
}

function Get-MutationSummary([string]$ReportPath) {
    if (!(Test-Path $ReportPath -PathType Leaf)) { throw "Mutation report not found: $ReportPath" }
    $report = Get-Content $ReportPath -Raw | ConvertFrom-Json -AsHashtable
    $mutants = [Collections.Generic.List[object]]::new()
    foreach ($file in $report.files.GetEnumerator()) {
        foreach ($mutant in $file.Value.mutants) {
            $mutants.Add([pscustomobject]@{
                File = $file.Key.Replace('\', '/')
                Id = [string]$mutant.id
                Status = [string]$mutant.status
                Mutator = [string]$mutant.mutatorName
                Replacement = [string]$mutant.replacement
                Line = [int]$mutant.location.start.line
            })
        }
    }
    if ($mutants.Count -eq 0) { throw 'Mutation report contains no mutants.' }

    $counts = [ordered]@{}
    foreach ($status in 'Killed', 'Survived', 'Timeout', 'NoCoverage', 'RuntimeError', 'CompileError', 'Ignored') {
        $counts[$status] = @($mutants | Where-Object Status -eq $status).Count
    }
    [pscustomobject]@{
        Total = $mutants.Count
        Counts = $counts
        Survivors = @($mutants | Where-Object Status -eq 'Survived')
        Mutants = $mutants.ToArray()
    }
}

function Write-MutationSummary(
    [object]$Summary,
    [string]$Path,
    [string]$Candidate,
    [double]$Score,
    [double]$ElapsedSeconds
) {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# Countdown mutation result')
    $lines.Add('')
    $lines.Add("- Candidate: ``$Candidate``")
    $lines.Add("- Mutation score: $($Score.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture))%")
    $lines.Add("- Elapsed: $($ElapsedSeconds.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)) seconds")
    $lines.Add("- Created: $($Summary.Total)")
    foreach ($status in $Summary.Counts.Keys) { $lines.Add("- ${status}: $($Summary.Counts[$status])") }
    $lines.Add('')
    $lines.Add('## Surviving mutants')
    $lines.Add('')
    if ($Summary.Survivors.Count -eq 0) {
        $lines.Add('None.')
    }
    else {
        $lines.Add('| File | Line | Mutator | Replacement |')
        $lines.Add('|---|---:|---|---|')
        foreach ($mutant in $Summary.Survivors) {
            $replacement = $mutant.Replacement.Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
            $lines.Add("| ``$($mutant.File)`` | $($mutant.Line) | $($mutant.Mutator) | ``$replacement`` |")
        }
    }
    $lines | Set-Content $Path -Encoding utf8
}

Export-ModuleMember -Function Resolve-MutationOutputPath, Get-MutationSummary, Write-MutationSummary
