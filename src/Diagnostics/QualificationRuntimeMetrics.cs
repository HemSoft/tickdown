// Copyright © 2025 HemSoft

namespace TickDown.Diagnostics;

/// <summary>
/// Represents timer and resource-owner counters captured by the qualification probe.
/// </summary>
internal sealed class QualificationRuntimeMetrics
{
    /// <summary>
    /// Gets the number of measured display callbacks.
    /// </summary>
    public int TickSamples { get; init; }

    /// <summary>
    /// Gets the median tick-to-display latency in milliseconds.
    /// </summary>
    public double TickLatencyP50Milliseconds { get; init; }

    /// <summary>
    /// Gets the 95th-percentile tick-to-display latency in milliseconds.
    /// </summary>
    public double TickLatencyP95Milliseconds { get; init; }

    /// <summary>
    /// Gets the 99th-percentile tick-to-display latency in milliseconds.
    /// </summary>
    public double TickLatencyP99Milliseconds { get; init; }

    /// <summary>
    /// Gets the maximum tick-to-display latency in milliseconds.
    /// </summary>
    public double TickLatencyMaximumMilliseconds { get; init; }

    /// <summary>
    /// Gets the number of timer displays updated in the current measurement window.
    /// </summary>
    public long DisplayedTicks { get; init; }

    /// <summary>
    /// Gets the current number of queued timer callbacks.
    /// </summary>
    public int PendingUiCallbacks { get; init; }

    /// <summary>
    /// Gets the maximum number of queued timer callbacks in the current window.
    /// </summary>
    public int MaximumPendingUiCallbacks { get; init; }

    /// <summary>
    /// Gets the number of live subscriptions to the global timer service.
    /// </summary>
    public int TimerSubscriptions { get; init; }

    /// <summary>
    /// Gets the number of active repeating-alarm timers.
    /// </summary>
    public int ActiveAlarmRepeatTimers { get; init; }

    /// <summary>
    /// Gets the number of active native media players.
    /// </summary>
    public int ActiveMediaPlayers { get; init; }
}