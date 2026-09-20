// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using global::TickDown.Diagnostics;

/// <summary>
/// Verifies the opt-in desktop qualification counters without representing a native UI test.
/// </summary>
[Collection(QualificationDiagnosticsIsolation.Name)]
public sealed class QualificationDiagnosticsTests
{
    /// <summary>
    /// Verifies queue, throughput, subscription, alarm, and media counters and their reset boundary.
    /// </summary>
    [Fact]
    public void RuntimeCountersCaptureAndResetAnIsolatedWindow()
    {
        string? previousDirectory = Environment.GetEnvironmentVariable("TICKDOWN_QUALIFICATION_DIRECTORY");
        try
        {
            Environment.SetEnvironmentVariable("TICKDOWN_QUALIFICATION_DIRECTORY", Path.GetTempPath());
            QualificationDiagnostics.RefreshConfiguration();
            Assert.True(QualificationDiagnostics.Enabled);

            QualificationDiagnostics.AddTimerSubscription();
            bool alarmRepeatActive = false;
            QualificationDiagnostics.SetAlarmRepeatActive(ref alarmRepeatActive, true);
            QualificationDiagnostics.SetMediaPlayerActive(true);
            long completed = QualificationDiagnostics.QueueTick();
            QualificationDiagnostics.CompleteTick(completed);
            long canceled = QualificationDiagnostics.QueueTick();
            QualificationDiagnostics.CancelTick(canceled);

            QualificationRuntimeMetrics active = QualificationDiagnostics.CaptureRuntimeMetrics(true);
            Assert.Equal(1, active.TickSamples);
            Assert.Equal(1, active.DisplayedTicks);
            Assert.Equal(0, active.PendingUiCallbacks);
            Assert.Equal(1, active.MaximumPendingUiCallbacks);
            Assert.Equal(1, active.TimerSubscriptions);
            Assert.Equal(1, active.ActiveAlarmRepeatTimers);
            Assert.Equal(1, active.ActiveMediaPlayers);
            Assert.True(active.TickLatencyMaximumMilliseconds >= 0);

            QualificationDiagnostics.RemoveTimerSubscription();
            QualificationDiagnostics.SetAlarmRepeatActive(ref alarmRepeatActive, false);
            QualificationDiagnostics.SetMediaPlayerActive(false);
            QualificationRuntimeMetrics settled = QualificationDiagnostics.CaptureRuntimeMetrics(false);
            Assert.Equal(0, settled.TickSamples);
            Assert.Equal(0, settled.DisplayedTicks);
            Assert.Equal(0, settled.TimerSubscriptions);
            Assert.Equal(0, settled.ActiveAlarmRepeatTimers);
            Assert.Equal(0, settled.ActiveMediaPlayers);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TICKDOWN_QUALIFICATION_DIRECTORY", previousDirectory);
            QualificationDiagnostics.RefreshConfiguration();
        }

        Assert.False(QualificationDiagnostics.Enabled);
        Assert.Equal(0, QualificationDiagnostics.QueueTick());
    }
}