// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using System.Runtime.Loader;

/// <summary>
/// Loads every production assembly when the coverage runner supplies its candidate closure.
/// </summary>
public sealed class ProductionAssemblyCoverageTests
{
    /// <summary>
    /// Loads every exact production candidate so Coverlet can instrument the full project-reference inventory.
    /// </summary>
    [Fact]
    public void LoadsProductionAssemblyCandidates()
    {
        string? assemblyPaths = Environment.GetEnvironmentVariable("TICKDOWN_COVERAGE_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(assemblyPaths))
        {
            return;
        }

        string[] candidates = assemblyPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(candidates);
        foreach (string candidate in candidates)
        {
            System.Reflection.Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            Assert.Equal(Path.GetFileNameWithoutExtension(candidate), assembly.GetName().Name);
        }
    }
}