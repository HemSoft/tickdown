// Copyright © 2025 HemSoft

namespace TickDown.Diagnostics;

/// <summary>
/// Represents a candidate-bound process and application snapshot.
/// </summary>
internal sealed class QualificationSnapshot
{
    /// <summary>
    /// Gets the matching request identifier.
    /// </summary>
    public long RequestId { get; init; }

    /// <summary>
    /// Gets the candidate Git revision supplied by the harness.
    /// </summary>
    public string Candidate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC capture timestamp.
    /// </summary>
    public DateTimeOffset CapturedUtc { get; init; }

    /// <summary>
    /// Gets the isolated settings directory used by the process.
    /// </summary>
    public string SettingsDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the logical processor count.
    /// </summary>
    public int ProcessorCount { get; init; }

    /// <summary>
    /// Gets the memory available to the managed runtime.
    /// </summary>
    public long TotalAvailableMemoryBytes { get; init; }

    /// <summary>
    /// Gets the managed heap size reported by the most recent runtime collection.
    /// </summary>
    public long ManagedHeapBytes { get; init; }

    /// <summary>
    /// Gets the private process memory size.
    /// </summary>
    public long PrivateMemoryBytes { get; init; }

    /// <summary>
    /// Gets the process working-set size.
    /// </summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>
    /// Gets the process handle count.
    /// </summary>
    public int HandleCount { get; init; }

    /// <summary>
    /// Gets the process thread count.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gets the number of timer view models.
    /// </summary>
    public int TimerCount { get; init; }

    /// <summary>
    /// Gets the number of running timer view models.
    /// </summary>
    public int RunningTimerCount { get; init; }

    /// <summary>
    /// Gets the number of completed timer view models.
    /// </summary>
    public int CompletedTimerCount { get; init; }

    /// <summary>
    /// Gets the selected application theme.
    /// </summary>
    public string CurrentTheme { get; init; } = string.Empty;

    /// <summary>
    /// Gets the root scroll viewer zoom factor.
    /// </summary>
    public double ZoomFactor { get; init; }

    /// <summary>
    /// Gets queue, timer, media, throughput, and latency counters.
    /// </summary>
    public QualificationRuntimeMetrics Runtime { get; init; } = new();
}