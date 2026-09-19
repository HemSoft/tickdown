Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RelativeSourcePath([string]$Path) {
    $normalized = $Path.Replace('\', '/')
    $sourceIndex = $normalized.LastIndexOf('/src/', [StringComparison]::OrdinalIgnoreCase)
    if ($sourceIndex -ge 0) { return $normalized.Substring($sourceIndex + 1) }
    return $normalized
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
        foreach ($type in $assembly.GetTypes()) {
            foreach ($method in $type.GetMethods($flags)) {
                $arity = $method.GetGenericArguments().Count
                $parameters = @($method.GetParameters() | ForEach-Object { Format-CoverageTypeName $_.ParameterType }) -join ','
                $key = "$($type.FullName.Replace('+', '/'))::$($method.Name)($parameters)"
                if (!$map.ContainsKey($key)) { $map[$key] = [Collections.Generic.List[int]]::new() }
                $map[$key].Add($arity)
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
        foreach ($type in $assembly.GetTypes()) {
            foreach ($method in $type.GetMethods($flags) | Where-Object {
                    $_.Name -in 'op_Implicit', 'op_Explicit', 'op_CheckedImplicit', 'op_CheckedExplicit'
                }) {
                $parameters = @($method.GetParameters() | ForEach-Object { Format-CoverageTypeName $_.ParameterType }) -join ','
                $key = "$($type.FullName.Replace('+', '/'))::$($method.Name)($parameters)"
                if (!$map.ContainsKey($key)) { $map[$key] = [Collections.Generic.List[string]]::new() }
                $map[$key].Add((Format-CoverageTypeName $method.ReturnType))
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

function Get-CoverageSourceExclusionViolations([string]$SourceRoot) {
    if (!(Test-Path $SourceRoot -PathType Container)) { throw "Production source root not found: $SourceRoot" }
    return @(
        Get-ChildItem $SourceRoot -Filter *.cs -File -Recurse |
            Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]' |
            Select-String -Pattern '\bExcludeFrom(?:Code)?Coverage(?:Attribute)?\b' |
            ForEach-Object { "$($_.Path):$($_.LineNumber) uses $($_.Matches[0].Value)" }
    )
}

function Get-StateMachineMap([string[]]$AssemblyPath) {
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
    $stateMachineAttributes = 'AsyncStateMachineAttribute', 'IteratorStateMachineAttribute', 'AsyncIteratorStateMachineAttribute'
    $map = @{}
    foreach ($path in $AssemblyPath) {
        if (!(Test-Path $path -PathType Leaf)) { throw "Covered assembly not found: $path" }
        $assembly = [Reflection.Assembly]::LoadFrom($path)
        foreach ($type in $assembly.GetTypes()) {
            foreach ($method in $type.GetMethods($flags)) {
                $attributes = @($method.GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -in $stateMachineAttributes })
                foreach ($attribute in $attributes) {
                    $stateType = $attribute.ConstructorArguments[0].Value
                    $stateTypeName = $stateType.FullName.Replace('+', '/')
                    if ($map.ContainsKey($stateTypeName)) { throw "Duplicate state machine identity: $stateTypeName" }
                    $genericSuffix = if ($method.GetGenericArguments().Count -gt 0) { "``$($method.GetGenericArguments().Count)" } else { '' }
                    $parameters = @($method.GetParameters() | ForEach-Object { $_.ParameterType.ToString() }) -join ','
                    $map[$stateTypeName] = [pscustomobject]@{
                        Class = $type.FullName.Replace('+', '/')
                        Method = "$($method.Name)$genericSuffix"
                        Signature = "($parameters)"
                    }
                }
            }
        }
    }
    return $map
}

function Get-CoverageExclusionViolations([string[]]$AssemblyPath) {
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
            $typeAttributes = @($type.GetCustomAttributesData())
            $typeIsGenerated = $type.FullName -eq 'AutoGeneratedProgram' -or
                @($typeAttributes | Where-Object { $_.AttributeType.Name -eq 'GeneratedCodeAttribute' }).Count -gt 0
            if (!$typeIsGenerated) {
                foreach ($attribute in $typeAttributes) {
                    if ($attribute.AttributeType.Name -in 'ExcludeFromCodeCoverageAttribute', 'ExcludeFromCoverageAttribute') {
                        $violations.Add("$($type.FullName) uses $($attribute.AttributeType.Name)")
                    }
                }
            }
            foreach ($member in $type.GetMembers($flags)) {
                $memberAttributes = @($member.GetCustomAttributesData())
                $memberIsGenerated = $typeIsGenerated -or
                    @($memberAttributes | Where-Object { $_.AttributeType.Name -eq 'GeneratedCodeAttribute' }).Count -gt 0
                if ($memberIsGenerated) { continue }
                foreach ($attribute in $memberAttributes) {
                    if ($attribute.AttributeType.Name -in 'ExcludeFromCodeCoverageAttribute', 'ExcludeFromCoverageAttribute') {
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
    [hashtable]$ConversionReturnTypes = @{}
) {
    if (!(Test-Path $CoveragePath -PathType Leaf)) { throw "Coverage file not found: $CoveragePath" }
    [xml]$coverage = Get-Content $CoveragePath -Raw
    $results = [Collections.Generic.List[object]]::new()
    $genericOccurrences = @{}
    $conversionOccurrences = @{}

    foreach ($class in $coverage.coverage.packages.package.classes.class) {
        $className = [string]$class.name
        $isStateMachine = $className -match '/<[^>]+>d__\d+(?:`\d+)?$'
        if ($isStateMachine) {
            if (!$StateMachineMap.ContainsKey($className)) {
                throw "No source method signature found for state machine $className."
            }
            $reportedClass = $StateMachineMap[$className].Class
            $reportedMethod = $StateMachineMap[$className].Method
            $reportedSignature = $StateMachineMap[$className].Signature
        }
        else {
            $reportedClass = $className
            $reportedMethod = $null
            $reportedSignature = $null
        }

        $source = Get-RelativeSourcePath ([string]$class.filename)
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
                $genericKey = "${reportedClass}::${methodName}${signature}"
                if ($GenericMethodArities.ContainsKey($genericKey)) {
                    $occurrence = if ($genericOccurrences.ContainsKey($genericKey)) { $genericOccurrences[$genericKey] } else { 0 }
                    $arities = @($GenericMethodArities[$genericKey])
                    if ($occurrence -ge $arities.Count) { throw "No unused method identity found for $genericKey." }
                    if ($arities[$occurrence] -gt 0) { $methodName = "$methodName``$($arities[$occurrence])" }
                    $genericOccurrences[$genericKey] = $occurrence + 1
                }
                if ($ConversionReturnTypes.ContainsKey($genericKey)) {
                    $occurrence = if ($conversionOccurrences.ContainsKey($genericKey)) { $conversionOccurrences[$genericKey] } else { 0 }
                    $returnTypes = @($ConversionReturnTypes[$genericKey])
                    if ($occurrence -ge $returnTypes.Count) { throw "No unused conversion return type found for $genericKey." }
                    $signature = "$signature->$($returnTypes[$occurrence])"
                    $conversionOccurrences[$genericKey] = $occurrence + 1
                }
            }
            $results.Add([pscustomobject]@{
                Id = "${reportedClass}::${methodName}${signature}"
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

    if ($results.Count -eq 0) { throw 'Coverage report contains no functions.' }
    return $results.ToArray()
}

function Test-CoverageBaseline([object[]]$Functions, [string]$BaselinePath, [switch]$AllowNewFunctions) {
    if (!(Test-Path $BaselinePath -PathType Leaf)) { throw "Coverage baseline not found: $BaselinePath" }
    $baseline = Get-Content $BaselinePath -Raw | ConvertFrom-Json -AsHashtable
    if ($baseline.version -ne 2) { throw "Unsupported coverage baseline version: $($baseline.version)" }

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
        version = 2
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

Export-ModuleMember -Function Resolve-CoverageResultsPath, Resolve-CoveredAssemblyPaths, Get-MethodGenericArities, Get-ConversionReturnTypes, Get-CoverageSourceExclusionViolations, Get-StateMachineMap, Get-CoverageExclusionViolations, Get-CoverageFunctions, Test-CoverageBaseline, Write-CoverageBaseline, Write-CoverageReports
