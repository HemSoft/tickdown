#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."
$editorConfig = Get-Content (Join-Path $root '.editorconfig') -Raw
$styleCop = Get-Content (Join-Path $root 'stylecop.json') -Raw | ConvertFrom-Json
$passed = 0

function Assert-Match([string]$Text, [string]$Pattern, [string]$Name) {
    if ($Text -notmatch $Pattern) { throw "$Name does not match '$Pattern'" }
    $script:passed++
}

Assert-Match $editorConfig '(?m)^csharp_using_directive_placement = inside_namespace:warning\r?$' 'Using placement'
Assert-Match $editorConfig '(?m)^dotnet_sort_system_directives_first = true\r?$' 'System import order'
Assert-Match $editorConfig '(?m)^dotnet_separate_import_directive_groups = false\r?$' 'Import grouping'
Assert-Match $editorConfig '(?m)^dotnet_code_quality_unused_parameters = all\r?$' 'Unused parameter scope'
Assert-Match $editorConfig '(?m)^dotnet_diagnostic\.IDE0060\.severity = warning\r?$' 'Unused parameter severity'
foreach ($memberKind in 'event', 'field', 'method', 'property') {
    Assert-Match $editorConfig "(?m)^dotnet_style_qualification_for_$memberKind = true:warning\r?$" "$memberKind qualification"
}

if ($styleCop.settings.orderingRules.usingDirectivesPlacement -ne 'insideNamespace') {
    throw 'StyleCop using placement conflicts with .editorconfig.'
}
$passed++
if ($styleCop.settings.orderingRules.systemUsingDirectivesFirst -ne $true) {
    throw 'StyleCop System import order conflicts with .editorconfig.'
}
$passed++

"Passed $passed style-policy assertions."
