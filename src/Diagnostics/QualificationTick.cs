// Copyright © 2025 HemSoft

namespace TickDown.Diagnostics;

/// <summary>
/// Identifies one queued display callback and its measurement generation.
/// </summary>
internal readonly struct QualificationTick
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QualificationTick"/> struct.
    /// </summary>
    /// <param name="startedTimestamp">The high-resolution queue timestamp.</param>
    /// <param name="generation">The measurement generation active when queued.</param>
    internal QualificationTick(long startedTimestamp, long generation)
    {
        this.StartedTimestamp = startedTimestamp;
        this.Generation = generation;
    }

    /// <summary>
    /// Gets the high-resolution queue timestamp.
    /// </summary>
    internal long StartedTimestamp { get; }

    /// <summary>
    /// Gets the measurement generation active when queued.
    /// </summary>
    internal long Generation { get; }
}