// Copyright © 2025 HemSoft

namespace TickDown.Diagnostics;

using System.Diagnostics;

/// <summary>
/// Records opt-in runtime counters for the isolated desktop qualification workload.
/// </summary>
internal static class QualificationDiagnostics
{
    private static readonly object Sync = new();
    private static readonly List<double> TickLatencies = [];
    private static int isConfigured = -1;
    private static int pendingUiCallbacks;
    private static int maximumPendingUiCallbacks;
    private static int timerSubscriptions;
    private static int activeAlarmRepeatTimers;
    private static int activeMediaPlayers;
    private static long displayedTicks;
    private static long alarmReplayRequests;

    /// <summary>
    /// Gets a value indicating whether an isolated qualification directory was configured before process start.
    /// </summary>
    internal static bool Enabled
    {
        get
        {
            int configured = Volatile.Read(ref isConfigured);
            if (configured >= 0)
            {
                return configured == 1;
            }

            bool enabled = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TICKDOWN_QUALIFICATION_DIRECTORY"));
            _ = Interlocked.CompareExchange(ref isConfigured, enabled ? 1 : 0, -1);
            return Volatile.Read(ref isConfigured) == 1;
        }
    }

    /// <summary>
    /// Refreshes opt-in state after a qualification harness or test changes the process environment.
    /// </summary>
    internal static void RefreshConfiguration() =>
        Interlocked.Exchange(ref isConfigured, string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TICKDOWN_QUALIFICATION_DIRECTORY")) ? 0 : 1);

    /// <summary>
    /// Records a timer callback waiting for the UI dispatcher.
    /// </summary>
    /// <returns>The high-resolution start timestamp, or zero when diagnostics are disabled.</returns>
    internal static long QueueTick()
    {
        if (!Enabled)
        {
            return 0;
        }

        int pending = Interlocked.Increment(ref pendingUiCallbacks);
        int currentMaximum;
        do
        {
            currentMaximum = Volatile.Read(ref maximumPendingUiCallbacks);
            if (pending <= currentMaximum)
            {
                break;
            }
        }
        while (Interlocked.CompareExchange(ref maximumPendingUiCallbacks, pending, currentMaximum) != currentMaximum);

        return Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Records a queued callback that updated the timer display.
    /// </summary>
    /// <param name="startedTimestamp">The timestamp returned by <see cref="QueueTick"/>.</param>
    internal static void CompleteTick(long startedTimestamp)
    {
        if (!Enabled || startedTimestamp == 0)
        {
            return;
        }

        _ = Interlocked.Decrement(ref pendingUiCallbacks);
        _ = Interlocked.Increment(ref displayedTicks);
        double latencyMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
        lock (Sync)
        {
            TickLatencies.Add(latencyMilliseconds);
        }
    }

    /// <summary>
    /// Removes a callback that could not or no longer needed to update the display.
    /// </summary>
    /// <param name="startedTimestamp">The timestamp returned by <see cref="QueueTick"/>.</param>
    internal static void CancelTick(long startedTimestamp)
    {
        if (Enabled && startedTimestamp != 0)
        {
            _ = Interlocked.Decrement(ref pendingUiCallbacks);
        }
    }

    /// <summary>
    /// Records one live subscription to the global timer.
    /// </summary>
    internal static void AddTimerSubscription()
    {
        if (Enabled)
        {
            _ = Interlocked.Increment(ref timerSubscriptions);
        }
    }

    /// <summary>
    /// Records removal of one subscription to the global timer.
    /// </summary>
    internal static void RemoveTimerSubscription()
    {
        if (Enabled)
        {
            _ = Interlocked.Decrement(ref timerSubscriptions);
        }
    }

    /// <summary>
    /// Records an alarm-repeat timer transition only when one owner's state changes.
    /// </summary>
    /// <param name="current">The owner's current active state.</param>
    /// <param name="active">The requested active state.</param>
    internal static void SetAlarmRepeatActive(ref bool current, bool active)
    {
        if (current == active)
        {
            return;
        }

        current = active;
        SetBooleanCounter(ref activeAlarmRepeatTimers, active);
    }

    /// <summary>
    /// Records a repeating alarm reaching its playback request.
    /// </summary>
    internal static void RecordAlarmReplay()
    {
        if (Enabled)
        {
            _ = Interlocked.Increment(ref alarmReplayRequests);
        }
    }

    /// <summary>
    /// Records whether the singleton audio service owns a native media player.
    /// </summary>
    /// <param name="active">Whether a player is active.</param>
    internal static void SetMediaPlayerActive(bool active) => Interlocked.Exchange(ref activeMediaPlayers, active ? 1 : 0);

    /// <summary>
    /// Captures current queue, subscription, alarm, media, throughput, and latency counters.
    /// </summary>
    /// <param name="resetLatency">Whether to begin a new latency and throughput window after capture.</param>
    /// <returns>The captured runtime metrics.</returns>
    internal static QualificationRuntimeMetrics CaptureRuntimeMetrics(bool resetLatency)
    {
        double[] latencies;
        long displayed = Interlocked.Read(ref displayedTicks);
        long alarmReplays = Interlocked.Read(ref alarmReplayRequests);
        int pending = Volatile.Read(ref pendingUiCallbacks);
        int maximumPending = Volatile.Read(ref maximumPendingUiCallbacks);
        lock (Sync)
        {
            latencies = [.. TickLatencies];
            if (resetLatency)
            {
                TickLatencies.Clear();
                _ = Interlocked.Exchange(ref maximumPendingUiCallbacks, pending);
                _ = Interlocked.Exchange(ref displayedTicks, 0);
                _ = Interlocked.Exchange(ref alarmReplayRequests, 0);
            }
        }

        Array.Sort(latencies);
        return new QualificationRuntimeMetrics
        {
            TickSamples = latencies.Length,
            TickLatencyP50Milliseconds = Percentile(latencies, 0.50),
            TickLatencyP95Milliseconds = Percentile(latencies, 0.95),
            TickLatencyP99Milliseconds = Percentile(latencies, 0.99),
            TickLatencyMaximumMilliseconds = latencies.Length == 0 ? 0 : latencies[^1],
            DisplayedTicks = displayed,
            PendingUiCallbacks = pending,
            MaximumPendingUiCallbacks = maximumPending,
            TimerSubscriptions = Volatile.Read(ref timerSubscriptions),
            ActiveAlarmRepeatTimers = Volatile.Read(ref activeAlarmRepeatTimers),
            AlarmReplayRequests = alarmReplays,
            ActiveMediaPlayers = Volatile.Read(ref activeMediaPlayers),
        };
    }

    private static void SetBooleanCounter(ref int counter, bool active)
    {
        if (!Enabled)
        {
            return;
        }

        if (active)
        {
            _ = Interlocked.Increment(ref counter);
            return;
        }

        int current;
        do
        {
            current = Volatile.Read(ref counter);
            if (current == 0)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref counter, current - 1, current) != current);
    }

    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        int index = (int)Math.Ceiling(percentile * values.Length) - 1;
        return values[Math.Clamp(index, 0, values.Length - 1)];
    }
}