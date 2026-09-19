// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using System.Runtime.Loader;

/// <summary>
/// Loads the production application assembly when the coverage runner supplies it.
/// </summary>
public sealed class ProductionAssemblyCoverageTests
{
    /// <summary>
    /// Loads the exact application candidate so Coverlet can instrument its full source inventory.
    /// </summary>
    [Fact]
    public void LoadsProductionApplicationCandidate()
    {
        string? assemblyPath = Environment.GetEnvironmentVariable("TICKDOWN_COVERAGE_APP_ASSEMBLY");
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return;
        }

        System.Reflection.Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        Assert.Equal("TickDown", assembly.GetName().Name);
    }
}