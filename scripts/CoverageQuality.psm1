Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RelativeSourcePath([string]$Path, [string]$RepoRoot = '') {
    if (![string]::IsNullOrWhiteSpace($RepoRoot) -and [IO.Path]::IsPathFullyQualified($Path)) {
        return [IO.Path]::GetRelativePath([IO.Path]::GetFullPath($RepoRoot), [IO.Path]::GetFullPath($Path)).Replace('\', '/')
    }
    $normalized = $Path.Replace('\', '/')
    $sourceIndex = $normalized.LastIndexOf('/src/', [StringComparison]::OrdinalIgnoreCase)
    if ($sourceIndex -ge 0) { return $normalized.Substring($sourceIndex + 1) }
    return $normalized
}

function Get-CSharpQualifiedName($Name) {
    $identifierKind = [Microsoft.CodeAnalysis.CSharp.SyntaxKind]::IdentifierToken
    return @($Name.DescendantTokens() | Where-Object { $_.RawKind -eq $identifierKind } | ForEach-Object ValueText) -join '.'
}

function Get-CSharpTypeDeclarationName($Declaration) {
    $arity = if ($null -eq $Declaration.TypeParameterList) { 0 } else { $Declaration.TypeParameterList.Parameters.Count }
    return $Declaration.Identifier.ValueText + $(if ($arity -gt 0) { "``$arity" } else { '' })
}

function Resolve-CoverageResultsPath([string]$RepoRoot, [string]$ResultsDirectory) {
    $ownedRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts/coverage'))
    $candidate = [IO.Path]::GetFullPath((Join-Path $RepoRoot $ResultsDirectory))
    $ownedPrefix = $ownedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($candidate -ne $ownedRoot -and !$candidate.StartsWith($ownedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Coverage results must stay within the owned directory $ownedRoot; received $candidate."
    }
    return $candidate
}

function Format-CoverageTypeName([Type]$Type) {
    if ($Type.IsGenericParameter) { return $Type.Name }
    if ($Type.IsArray) {
        $rankMarker = ',' * ($Type.GetArrayRank() - 1)
        return "$(Format-CoverageTypeName $Type.GetElementType())[$rankMarker]"
    }
    if ($Type.IsByRef) { return "$(Format-CoverageTypeName $Type.GetElementType())&" }
    if ($Type.IsGenericType) {
        $definition = $Type.GetGenericTypeDefinition().FullName.Replace('+', '/')
        $arguments = @($Type.GetGenericArguments() | ForEach-Object { Format-CoverageTypeName $_ }) -join ','
        return "$definition<$arguments>"
    }
    return $Type.FullName.Replace('+', '/')
}

function Get-MethodGenericArities([string[]]$AssemblyPath) {
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
    $map = @{}
    foreach ($path in $AssemblyPath) {
        if (!(Test-Path $path -PathType Leaf)) { throw "Covered assembly not found: $path" }
        $assembly = [Reflection.Assembly]::LoadFrom($path)
        $assemblyName = $assembly.GetName().Name
        foreach ($type in $assembly.GetTypes()) {
            foreach ($method in $type.GetMethods($flags)) {
                $arity = $method.GetGenericArguments().Count
                $parameters = @($method.GetParameters() | ForEach-Object { Format-CoverageTypeName $_.ParameterType }) -join ','
                $key = "[$assemblyName]$($type.FullName.Replace('+', '/'))::$($method.Name)($parameters)"
                if (!$map.ContainsKey($key)) { $map[$key] = [Collections.Generic.List[int]]::new() }
                if (!$map[$key].Contains($arity)) { $map[$key].Add($arity) }
            }
        }
    }
    return $map
}

function Get-ConversionReturnTypes([string[]]$AssemblyPath) {
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
    $map = @{}
    foreach ($path in $AssemblyPath) {
        if (!(Test-Path $path -PathType Leaf)) { throw "Covered assembly not found: $path" }
        $assembly = [Reflection.Assembly]::LoadFrom($path)
        $assemblyName = $assembly.GetName().Name
        foreach ($type in $assembly.GetTypes()) {
            foreach ($method in $type.GetMethods($flags) | Where-Object {
                    $_.Name -in 'op_Implicit', 'op_Explicit', 'op_CheckedImplicit', 'op_CheckedExplicit'
                }) {
                $parameters = @($method.GetParameters() | ForEach-Object { Format-CoverageTypeName $_.ParameterType }) -join ','
                $key = "[$assemblyName]$($type.FullName.Replace('+', '/'))::$($method.Name)($parameters)"
                if (!$map.ContainsKey($key)) { $map[$key] = [Collections.Generic.List[string]]::new() }
                $returnType = Format-CoverageTypeName $method.ReturnType
                if (!$map[$key].Contains($returnType)) { $map[$key].Add($returnType) }
            }
        }
    }
    return $map
}

function Resolve-CoveredAssemblyPaths([string[]]$AssemblyNames, [string]$TestAssemblyPath) {
    if (!(Test-Path $TestAssemblyPath -PathType Leaf)) { throw "Current test assembly not found: $TestAssemblyPath" }
    $outputDirectory = Split-Path $TestAssemblyPath -Parent
    return @(
        $AssemblyNames | ForEach-Object {
            $path = Join-Path $outputDirectory "$_.dll"
            if (!(Test-Path $path -PathType Leaf)) { throw "Covered assembly not found in current test output: $path" }
            $path
        }
    )
}

function Get-UnexpectedSourceLinkedTypes([string]$AssemblyPath, [string[]]$AllowedOuterTypes) {
    if (!(Test-Path $AssemblyPath -PathType Leaf)) { throw "Source-linked assembly not found: $AssemblyPath" }
    $assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
    return @(
        foreach ($type in $assembly.GetTypes()) {
            foreach ($outerType in $AllowedOuterTypes) {
                if (!$type.FullName.StartsWith($outerType, [StringComparison]::Ordinal)) { continue }
                if ($type.FullName -ne $outerType -and !$type.FullName.StartsWith("$outerType+", [StringComparison]::Ordinal)) {
                    "$($type.FullName) matches the $outerType* collection filter but is not that type or one of its nested types"
                }
                break
            }
        }
    )
}

function Get-CoverageSourceExclusionViolations([string]$SourceRoot, [string[]]$PreprocessorSymbols = @()) {
    if (!(Test-Path $SourceRoot -PathType Container)) { throw "Production source root not found: $SourceRoot" }
    Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
    $parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithPreprocessorSymbols($PreprocessorSymbols)
    $forbiddenNames = @('ExcludeFromCodeCoverage', 'ExcludeFromCoverage')
    $documents = @(
        foreach ($file in Get-ChildItem $SourceRoot -Filter *.cs -File -Recurse | Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]') {
            $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([string](Get-Content $file.FullName -Raw), $parseOptions)
            [pscustomobject]@{ File = $file; Tree = $tree; Root = $tree.GetRoot() }
        }
    )
    $globalForbiddenAliases = @{}
    foreach ($document in $documents) {
        foreach ($usingDirective in $document.Root.DescendantNodes() | Where-Object {
                $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax] -and $_.GlobalKeyword.RawKind -ne 0
            }) {
            if ($null -eq $usingDirective.Alias) { continue }
            $targetName = Get-CSharpQualifiedName $usingDirective.Name
            $targetSimpleName = ($targetName -split '\.')[-1] -replace 'Attribute$', ''
            if ($targetSimpleName -in $forbiddenNames) { $globalForbiddenAliases[$usingDirective.Alias.Name.Identifier.ValueText] = $true }
        }
    }
    $violations = [Collections.Generic.List[string]]::new()
    foreach ($document in $documents) {
        $forbiddenAliases = @{}
        foreach ($alias in $globalForbiddenAliases.Keys) { $forbiddenAliases[$alias] = $true }
        foreach ($usingDirective in $document.Root.DescendantNodes() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax] }) {
            if ($null -eq $usingDirective.Alias) { continue }
            $targetName = Get-CSharpQualifiedName $usingDirective.Name
            $targetSimpleName = ($targetName -split '\.')[-1] -replace 'Attribute$', ''
            if ($targetSimpleName -in $forbiddenNames) { $forbiddenAliases[$usingDirective.Alias.Name.Identifier.ValueText] = $true }
        }
        foreach ($attribute in $document.Root.DescendantNodes() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax] }) {
            $attributeName = Get-CSharpQualifiedName $attribute.Name
            $simpleName = ($attributeName -split '\.')[-1] -replace 'Attribute$', ''
            if ($simpleName -notin $forbiddenNames -and !$forbiddenAliases.ContainsKey($simpleName)) { continue }
            $lineNumber = 1 + $document.Tree.GetLineSpan($attribute.Span).StartLinePosition.Line
            $violations.Add("$($document.File.FullName):$lineNumber uses $attributeName")
        }
    }
    return $violations.ToArray()
}

function Get-UnlinkedPartialTypeViolations(
    [string]$SourceRoot,
    [hashtable]$LinkedTypePatterns,
    [string[]]$PreprocessorSymbols = @()
) {
    if (!(Test-Path $SourceRoot -PathType Container)) { throw "Production source root not found: $SourceRoot" }
    Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
    $partialKind = [Microsoft.CodeAnalysis.CSharp.SyntaxKind]::PartialKeyword
    $parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithPreprocessorSymbols($PreprocessorSymbols)
    $violations = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem $SourceRoot -Filter *.cs -File -Recurse | Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]') {
        $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([string](Get-Content $file.FullName -Raw), $parseOptions)
        $declarations = @($tree.GetRoot().DescendantNodes() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax] })
        foreach ($declaration in $declarations) {
            $isPartial = @($declaration.Modifiers | Where-Object { $_.RawKind -eq $partialKind }).Count -gt 0
            if (!$isPartial) { continue }
            $namespaceNodes = @($declaration.Ancestors() | Where-Object {
                    $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax] -or
                    $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax]
                })
            [array]::Reverse($namespaceNodes)
            $containingTypes = @($declaration.Ancestors() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax] })
            [array]::Reverse($containingTypes)
            $identityParts = [Collections.Generic.List[string]]::new()
            foreach ($namespaceNode in $namespaceNodes) { $identityParts.Add((Get-CSharpQualifiedName $namespaceNode.Name)) }
            foreach ($containingType in $containingTypes) { $identityParts.Add((Get-CSharpTypeDeclarationName $containingType)) }
            $identityParts.Add((Get-CSharpTypeDeclarationName $declaration))
            $typeIdentity = $identityParts -join '.'
            if (!$LinkedTypePatterns.ContainsKey($typeIdentity)) { continue }
            $relativePath = [IO.Path]::GetRelativePath($SourceRoot, $file.FullName).Replace('\', '/')
            if ($relativePath -ne $LinkedTypePatterns[$typeIdentity]) {
                $lineNumber = 1 + $tree.GetLineSpan($declaration.Span).StartLinePosition.Line
                $violations.Add("$($file.FullName):$lineNumber declares excluded partial $typeIdentity outside linked path $($LinkedTypePatterns[$typeIdentity])")
            }
        }
    }
    return $violations.ToArray()
}

function Get-StateMachineMap([string[]]$AssemblyPath) {
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
    $stateMachineAttributes = 'AsyncStateMachineAttribute', 'IteratorStateMachineAttribute', 'AsyncIteratorStateMachineAttribute'
    $map = @{}
    foreach ($path in $AssemblyPath) {
        if (!(Test-Path $path -PathType Leaf)) { throw "Covered assembly not found: $path" }
        $assembly = [Reflection.Assembly]::LoadFrom($path)
        $assemblyName = $assembly.GetName().Name
        foreach ($type in $assembly.GetTypes()) {
            foreach ($method in $type.GetMethods($flags)) {
                $attributes = @($method.GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -in $stateMachineAttributes })
                foreach ($attribute in $attributes) {
                    $stateType = $attribute.ConstructorArguments[0].Value
                    $stateTypeName = $stateType.FullName.Replace('+', '/')
                    $genericSuffix = if ($method.GetGenericArguments().Count -gt 0) { "``$($method.GetGenericArguments().Count)" } else { '' }
                    $parameters = @($method.GetParameters() | ForEach-Object { $_.ParameterType.ToString() }) -join ','
                    $identity = [pscustomobject]@{
                        Class = $type.FullName.Replace('+', '/')
                        Method = "$($method.Name)$genericSuffix"
                        Signature = "($parameters)"
                    }
                    $stateMachineKey = "[$assemblyName]$stateTypeName"
                    if ($map.ContainsKey($stateMachineKey)) {
                        $existing = $map[$stateMachineKey]
                        if ($existing.Class -ne $identity.Class -or $existing.Method -ne $identity.Method -or $existing.Signature -ne $identity.Signature) {
                            throw "Conflicting state machine identity: $stateTypeName"
                        }
                        continue
                    }
                    $map[$stateMachineKey] = $identity
                }
            }
        }
    }
    return $map
}

function Get-CoverageExclusionViolations(
    [string[]]$AssemblyPath,
    [string[]]$TrustedGeneratedMembers = @(),
    [string[]]$SourceLinkedTestTypes = @()
) {
    $violations = [Collections.Generic.List[string]]::new()
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
    foreach ($path in $AssemblyPath) {
        if (!(Test-Path $path -PathType Leaf)) { throw "Covered assembly not found: $path" }
        $assembly = [Reflection.Assembly]::LoadFrom($path)
        foreach ($attribute in $assembly.GetCustomAttributesData()) {
            if ($attribute.AttributeType.Name -in 'ExcludeFromCodeCoverageAttribute', 'ExcludeFromCoverageAttribute') {
                $violations.Add("$($assembly.GetName().Name) assembly uses $($attribute.AttributeType.Name)")
            }
        }
        foreach ($type in $assembly.GetTypes()) {
            if ($assembly.GetName().Name -eq 'TickDown.Tests' -and $type.FullName -notin $SourceLinkedTestTypes) { continue }
            $typeAttributes = @($type.GetCustomAttributesData())
            foreach ($attribute in $typeAttributes) {
                if ($attribute.AttributeType.Name -in 'ExcludeFromCodeCoverageAttribute', 'ExcludeFromCoverageAttribute') {
                    $violations.Add("$($type.FullName) uses $($attribute.AttributeType.Name)")
                }
            }
            foreach ($member in $type.GetMembers($flags)) {
                $memberAttributes = @($member.GetCustomAttributesData())
                $generatedAttributes = @($memberAttributes | Where-Object { $_.AttributeType.Name -eq 'GeneratedCodeAttribute' })
                $memberIdentity = "$($type.FullName).$($member.Name)"
                $memberIsTrustedGenerated = $memberIdentity -in $TrustedGeneratedMembers -and
                    @($generatedAttributes | Where-Object {
                            $_.ConstructorArguments.Count -gt 0 -and
                            ([string]$_.ConstructorArguments[0].Value) -eq 'CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator'
                        }).Count -gt 0
                foreach ($attribute in $memberAttributes) {
                    if ($attribute.AttributeType.Name -in 'ExcludeFromCodeCoverageAttribute', 'ExcludeFromCoverageAttribute' -and !$memberIsTrustedGenerated) {
                        $violations.Add("$($type.FullName).$($member.Name) uses $($attribute.AttributeType.Name)")
                    }
                }
            }
        }
    }
    return $violations
}

function Get-CoverageFunctions(
    [string]$CoveragePath,
    [hashtable]$StateMachineMap = @{},
    [hashtable]$GenericMethodArities = @{},
    [hashtable]$ConversionReturnTypes = @{},
    [string]$RepoRoot = ''
) {
    if (!(Test-Path $CoveragePath -PathType Leaf)) { throw "Coverage file not found: $CoveragePath" }
    [xml]$coverage = Get-Content $CoveragePath -Raw
    $results = [Collections.Generic.List[object]]::new()
    $genericOccurrences = @{}
    $conversionOccurrences = @{}
    $functionIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

    foreach ($package in $coverage.coverage.packages.package) {
        $assemblyName = [string]$package.name
        foreach ($class in $package.classes.class) {
        $className = [string]$class.name
        $stateMachineKey = "[$assemblyName]$className"
        $isStateMachine = $StateMachineMap.ContainsKey($stateMachineKey)
        if ($isStateMachine) {
            $reportedClass = $StateMachineMap[$stateMachineKey].Class
            $reportedMethod = $StateMachineMap[$stateMachineKey].Method
            $reportedSignature = $StateMachineMap[$stateMachineKey].Signature
        }
        else {
            $reportedClass = $className
            $reportedMethod = $null
            $reportedSignature = $null
        }

        $source = Get-RelativeSourcePath ([string]$class.filename) $RepoRoot
        $methods = @($class.methods.method | Where-Object { $null -ne $_ })
        $methodGroups = if ($isStateMachine) {
            , [pscustomobject]@{
                Methods = @($methods | Where-Object { @($_.lines.line | Where-Object { $null -ne $_ }).Count -gt 0 })
                Name = $reportedMethod
                Signature = $reportedSignature
            }
        }
        else {
            @($methods | ForEach-Object { [pscustomobject]@{ Methods = @($_); Name = [string]$_.name; Signature = [string]$_.signature } })
        }

        foreach ($group in $methodGroups) {
            if ($group.Methods.Count -eq 0) { throw "State machine $className has no source-bearing methods." }
            $coveredBranches = 0
            $validBranches = 0
            $coveredLines = 0
            $validLines = 0
            $complexity = 1.0
            foreach ($method in $group.Methods) {
                $methodComplexity = [double]::Parse([string]$method.complexity, [Globalization.CultureInfo]::InvariantCulture)
                $complexity += [Math]::Max(0, $methodComplexity - 1)
                foreach ($line in @($method.lines.line)) {
                    if ($null -eq $line) { continue }
                    $validLines++
                    if ([int]$line.hits -gt 0) { $coveredLines++ }
                    if ([string]$line.branch -eq 'True' -and [string]$line.'condition-coverage' -match '\((\d+)/(\d+)\)') {
                        $coveredBranches += [int]$Matches[1]
                        $validBranches += [int]$Matches[2]
                    }
                }
            }

            if ($validBranches -gt 0) {
                $basis = 'branch+line'
                $branchRate = $coveredBranches / $validBranches
                $lineRate = $coveredLines / $validLines
                $coverageRate = [Math]::Min($branchRate, $lineRate)
            }
            elseif ($validLines -gt 0) {
                $basis = 'line'
                $coverageRate = $coveredLines / $validLines
            }
            else {
                throw "Method $($class.name)::$($group.Name) has no measurable lines."
            }

            $crap = ($complexity * $complexity * [Math]::Pow(1 - $coverageRate, 3)) + $complexity
            $signature = $group.Signature
            $methodName = $group.Name
            if (!$isStateMachine) {
                $genericKey = "[$assemblyName]${reportedClass}::${methodName}${signature}"
                if ($ConversionReturnTypes.ContainsKey($genericKey)) {
                    $occurrence = if ($conversionOccurrences.ContainsKey($genericKey)) { $conversionOccurrences[$genericKey] } else { 0 }
                    $returnTypes = @($ConversionReturnTypes[$genericKey])
                    if ($occurrence -ge $returnTypes.Count) { throw "No unused conversion return type found for $genericKey." }
                    $signature = "$signature->$($returnTypes[$occurrence])"
                    $conversionOccurrences[$genericKey] = $occurrence + 1
                }
                elseif ($GenericMethodArities.ContainsKey($genericKey)) {
                    $occurrence = if ($genericOccurrences.ContainsKey($genericKey)) { $genericOccurrences[$genericKey] } else { 0 }
                    $arities = @($GenericMethodArities[$genericKey])
                    if ($occurrence -ge $arities.Count) { throw "No unused method identity found for $genericKey." }
                    if ($arities[$occurrence] -gt 0) { $methodName = "$methodName``$($arities[$occurrence])" }
                    $genericOccurrences[$genericKey] = $occurrence + 1
                }
            }
            $functionId = "[$assemblyName]${reportedClass}::${methodName}${signature}"
            if (!$functionIds.Add($functionId)) { throw "Duplicate coverage function identity: $functionId" }
            $results.Add([pscustomobject]@{
                Id = $functionId
                Assembly = $assemblyName
                Class = $reportedClass
                Method = $methodName
                Signature = $signature
                Source = $source
                Complexity = $complexity
                CoverageBasis = $basis
                Coverage = [Math]::Round($coverageRate, 6)
                Crap = [Math]::Round($crap, 4)
            })
        }
    }
    }

    if ($results.Count -eq 0) { throw 'Coverage report contains no functions.' }
    return $results.ToArray()
}

function Test-CoverageBaseline([object[]]$Functions, [string]$BaselinePath, [switch]$AllowNewFunctions) {
    if (!(Test-Path $BaselinePath -PathType Leaf)) { throw "Coverage baseline not found: $BaselinePath" }
    $baseline = Get-Content $BaselinePath -Raw | ConvertFrom-Json -AsHashtable
    if ($baseline.version -ne 3) { throw "Unsupported coverage baseline version: $($baseline.version)" }

    $failures = [Collections.Generic.List[string]]::new()
    foreach ($prefix in $baseline.requiredSourcePrefixes) {
        if (!($Functions.Source | Where-Object { $_.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) })) {
            $failures.Add("Coverage is missing required production source prefix '$prefix'.")
        }
    }
    foreach ($source in $baseline.requiredSources) {
        if ($source -notin $Functions.Source) {
            $failures.Add("Coverage is missing baseline source '$source'.")
        }
    }

    $currentIds = @{}
    foreach ($function in $Functions) { $currentIds[$function.Id] = $true }
    foreach ($functionId in $baseline.functions.Keys) {
        if (!$currentIds.ContainsKey($functionId)) {
            $failures.Add("Coverage is missing baseline function $functionId.")
        }
    }

    foreach ($function in $Functions) {
        if ($baseline.functions.ContainsKey($function.Id)) {
            $allowed = [double]$baseline.allowedCrapIncrease
            $previous = [double]$baseline.functions[$function.Id].crap
            if ($function.Crap -gt $previous + $allowed) {
                $failures.Add("Existing function $($function.Id) increased CRAP from $previous to $($function.Crap).")
            }
        }
        elseif ($function.Crap -gt [double]$baseline.maxNewFunctionCrap) {
            $failures.Add("New function $($function.Id) has CRAP $($function.Crap), above $($baseline.maxNewFunctionCrap).")
        }
        elseif (!$AllowNewFunctions) {
            $failures.Add("New function $($function.Id) must be added to the reviewed coverage baseline.")
        }
    }

    return $failures.ToArray()
}

function Write-CoverageBaseline([object[]]$Functions, [string]$Path) {
    $entries = [ordered]@{}
    foreach ($function in $Functions | Sort-Object Id) {
        $entries[$function.Id] = [ordered]@{
            crap = $function.Crap
            complexity = $function.Complexity
            coverage = $function.Coverage
            coverageBasis = $function.CoverageBasis
            source = $function.Source
        }
    }
    $baseline = [ordered]@{
        version = 3
        formula = 'complexity^2 * (1 - coverage)^3 + complexity'
        maxNewFunctionCrap = 30
        allowedCrapIncrease = 0.01
        requiredSourcePrefixes = @('src/TickDown.Core/', 'src/ViewModels/', 'src/Services/')
        requiredSources = @($Functions.Source | Sort-Object -Unique)
        functions = $entries
    }
    $baseline | ConvertTo-Json -Depth 8 | Set-Content $Path -Encoding utf8
}

function Write-CoverageReports([object[]]$Functions, [string]$OutputDirectory) {
    New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
    $ordered = @($Functions | Sort-Object -Property @{ Expression = 'Crap'; Descending = $true }, @{ Expression = 'Id'; Descending = $false })
    $ordered | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputDirectory 'function-risk.json') -Encoding utf8

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# Worst function risk')
    $lines.Add('')
    $lines.Add('| Function | Complexity | Coverage | Basis | CRAP |')
    $lines.Add('|---|---:|---:|---|---:|')
    foreach ($function in $ordered | Select-Object -First 25) {
        $coveragePercent = ($function.Coverage * 100).ToString('0.##', [Globalization.CultureInfo]::InvariantCulture)
        $lines.Add("| ``$($function.Id)`` | $($function.Complexity) | $coveragePercent% | $($function.CoverageBasis) | $($function.Crap) |")
    }
    $lines.Add('')
    $lines.Add('CRAP uses the lower of branch and line coverage when branches exist; otherwise it uses line coverage.')
    $lines | Set-Content (Join-Path $OutputDirectory 'function-risk.md') -Encoding utf8
}

Export-ModuleMember -Function Resolve-CoverageResultsPath, Resolve-CoveredAssemblyPaths, Get-UnexpectedSourceLinkedTypes, Get-MethodGenericArities, Get-ConversionReturnTypes, Get-CoverageSourceExclusionViolations, Get-UnlinkedPartialTypeViolations, Get-StateMachineMap, Get-CoverageExclusionViolations, Get-CoverageFunctions, Test-CoverageBaseline, Write-CoverageBaseline, Write-CoverageReports
