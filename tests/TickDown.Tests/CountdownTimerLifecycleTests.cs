// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using System.Text.Json;
using TickDown.Core.Models;

/// <summary>
/// Tests countdown transitions without wall-clock delays.
/// </summary>
public class CountdownTimerLifecycleTests
{
    /// <summary>
    /// Verifies that paused time never consumes the remaining countdown.
    /// </summary>
    /// <param name="pausedSeconds">The time spent paused.</param>
    [Theory]
    [InlineData(2)]
    [InlineData(60)]
    public void ResumePreservesRemainingTime(int pausedSeconds)
    {
        ManualClock clock = new();
        CountdownTimer timer = new(TimeSpan.FromSeconds(30), "Test", clock);
        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(7));
        timer.Pause();
        Assert.Equal(TimeSpan.FromSeconds(23), timer.Remaining);
        Assert.Equal(TimerState.Paused, timer.State);
        Assert.Null(timer.EndTime);

        clock.Advance(TimeSpan.FromSeconds(pausedSeconds));
        timer.Tick();
        Assert.Equal(TimeSpan.FromSeconds(23), timer.Remaining);
        timer.Start();
        timer.Tick();
        Assert.Equal(TimerState.Running, timer.State);
        Assert.Equal(TimeSpan.FromSeconds(23), timer.Remaining);
        clock.Advance(TimeSpan.FromSeconds(23));
        timer.Tick();
        Assert.Equal(TimerState.Completed, timer.State);
        Assert.Equal(TimeSpan.Zero, timer.Remaining);
    }

    /// <summary>
    /// Verifies completion can be restarted with a new full duration.
    /// </summary>
    [Fact]
    public void CompletedTimerRestartsWithFreshDeadline()
    {
        ManualClock clock = new();
        CountdownTimer timer = new(TimeSpan.FromSeconds(3), "Test", clock);
        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(4));
        timer.Tick();
        Assert.Equal(TimerState.Completed, timer.State);
        timer.SetDuration(TimeSpan.FromSeconds(10));
        timer.Start();
        timer.Tick();
        Assert.Equal(TimerState.Running, timer.State);
        Assert.Equal(TimeSpan.FromSeconds(10), timer.Remaining);
        clock.Advance(TimeSpan.FromSeconds(9));
        timer.Tick();
        Assert.Equal(TimerState.Running, timer.State);
        clock.Advance(TimeSpan.FromSeconds(1));
        timer.Tick();
        Assert.Equal(TimerState.Completed, timer.State);
    }

    /// <summary>
    /// Verifies repeated start does not extend an active countdown.
    /// </summary>
    [Fact]
    public void StartWhileRunningKeepsDeadline()
    {
        ManualClock clock = new();
        CountdownTimer timer = new(TimeSpan.FromSeconds(30), "Test", clock);
        timer.Start();
        DateTime? deadline = timer.EndTime;
        clock.Advance(TimeSpan.FromSeconds(5));
        timer.Start();
        Assert.Equal(deadline, timer.EndTime);
        timer.Tick();
        Assert.Equal(TimeSpan.FromSeconds(25), timer.Remaining);
    }

    /// <summary>
    /// Verifies stop samples remaining time and reset restores the full duration.
    /// </summary>
    [Fact]
    public void StopPreservesRemainingAndResetRestoresDuration()
    {
        ManualClock clock = new();
        CountdownTimer timer = new(TimeSpan.FromSeconds(30), "Test", clock);
        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(7));
        timer.Stop();
        Assert.Equal(TimerState.Stopped, timer.State);
        Assert.Equal(TimeSpan.FromSeconds(23), timer.Remaining);
        Assert.Null(timer.StartTime);
        Assert.Null(timer.EndTime);
        clock.Advance(TimeSpan.FromMinutes(1));
        timer.Start();
        timer.Tick();
        Assert.Equal(TimeSpan.FromSeconds(23), timer.Remaining);
        timer.Reset();
        Assert.Equal(TimerState.Stopped, timer.State);
        Assert.Equal(timer.Duration, timer.Remaining);
        Assert.Null(timer.StartTime);
        Assert.Null(timer.EndTime);
    }

    /// <summary>
    /// Verifies pausing at the deadline completes instead of preserving a negative duration.
    /// </summary>
    [Fact]
    public void PauseAtDeadlineCompletes()
    {
        ManualClock clock = new();
        CountdownTimer timer = new(TimeSpan.FromSeconds(3), "Test", clock);
        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(3));
        timer.Pause();
        Assert.Equal(TimerState.Completed, timer.State);
        Assert.Equal(TimeSpan.Zero, timer.Remaining);
    }

    /// <summary>
    /// Verifies the injectable clock does not become part of the persisted model.
    /// </summary>
    [Fact]
    public void PausedTimerRoundTripsWithoutClockState()
    {
        CountdownTimer timer = new(TimeSpan.FromSeconds(30), "Test", new ManualClock());
        timer.Start();
        timer.Pause();
        string json = JsonSerializer.Serialize(timer);
        CountdownTimer? restored = JsonSerializer.Deserialize<CountdownTimer>(json);
        Assert.NotNull(restored);
        Assert.Equal(TimerState.Paused, restored.State);
        Assert.Equal(timer.Remaining, restored.Remaining);
        restored.Start();
        Assert.Equal(TimerState.Running, restored.State);
        Assert.True(restored.EndTime.HasValue);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan duration) => this.now += duration;
    }
}