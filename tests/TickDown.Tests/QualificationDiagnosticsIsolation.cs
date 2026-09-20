// Copyright © 2025 HemSoft

namespace TickDown.Tests;

/// <summary>
/// Prevents process-wide diagnostics state from overlapping other test collections.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public static class QualificationDiagnosticsIsolation
{
    /// <summary>
    /// Identifies the isolated diagnostics test collection.
    /// </summary>
    public const string Name = "Qualification diagnostics isolation";
}