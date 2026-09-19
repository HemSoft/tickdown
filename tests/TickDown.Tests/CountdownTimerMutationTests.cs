// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using TickDown.Core.Models;

/// <summary>
/// Locks down countdown behavior that realistic mutations must not change.
/// </summary>
public class CountdownTimerMutationTests
{
    /// <summary>
    /// Verifies constructor and persisted defaults that affect timer behavior.
    /// </summary>
    [Fact]
    public void DefaultTimerHasExpectedBehaviorSettings()
    {
        CountdownTimer timer = new();
        Assert.Equal(string.Empty, timer.Name);
        Assert.True(timer.EnableCompletionColor);
        Assert.Equal("#4CAF50", timer.CompletionColor);
        Assert.True(timer.EnableAlarm);
        Assert.Equal("Alarm 01", timer.AlarmSound);
        Assert.Equal(TimerState.Stopped, timer.State);
    }

    /// <summary>
    /// Verifies the injected clock is required.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNullClock() =>
        Assert.Throws<ArgumentNullException>(() => new CountdownTimer(TimeSpan.FromSeconds(1), "Test", null!));

    /// <summary>
    /// Verifies progress arithmetic at a non-boundary value.
    /// </summary>
    [Fact]
    public void ProgressUsesElapsedFraction()
    {
        CountdownTimer timer = new(TimeSpan.FromSeconds(40), "Test")
        {
            Remaining = TimeSpan.FromSeconds(30),
        };
        Assert.Equal(25, timer.ProgressPercentage);
    }

    /// <summary>
    /// Verifies zero is a valid countdown start boundary.
    /// </summary>
    [Fact]
    public void ZeroDurationCanStart()
    {
        ManualClock clock = new();
        CountdownTimer timer = new(TimeSpan.Zero, "Test", clock);
        timer.Start();
        Assert.Equal(TimerState.Running, timer.State);
        Assert.Equal(clock.GetLocalNow().DateTime, timer.EndTime);
    }

    /// <summary>
    /// Verifies an exact <see cref="DateTime.MaxValue"/> deadline remains representable.
    /// </summary>
    [Fact]
    public void ExactMaximumDeadlineCanStart()
    {
        ManualClock clock = new(new DateTimeOffset(DateTime.MaxValue.AddSeconds(-2), TimeSpan.Zero));
        CountdownTimer timer = new(TimeSpan.FromSeconds(2), "Test", clock);
        timer.Start();
        Assert.Equal(TimerState.Running, timer.State);
        Assert.Equal(DateTime.MaxValue, timer.EndTime);
    }

    /// <summary>
    /// Verifies duration edits do not rewrite remaining time in active or completed states.
    /// </summary>
    /// <param name="state">The non-stopped timer state.</param>
    [Theory]
    [InlineData(TimerState.Running)]
    [InlineData(TimerState.Paused)]
    [InlineData(TimerState.Completed)]
    public void SetDurationPreservesRemainingOutsideStoppedState(TimerState state)
    {
        CountdownTimer timer = new(TimeSpan.FromSeconds(30), "Test")
        {
            State = state,
            Remaining = TimeSpan.FromSeconds(12),
        };
        timer.SetDuration(TimeSpan.FromSeconds(60));
        Assert.Equal(TimeSpan.FromSeconds(60), timer.Duration);
        Assert.Equal(TimeSpan.FromSeconds(12), timer.Remaining);
    }

    /// <summary>
    /// Verifies duration edits reset remaining time when stopped.
    /// </summary>
    [Fact]
    public void SetDurationUpdatesRemainingWhenStopped()
    {
        CountdownTimer timer = new(TimeSpan.FromSeconds(30), "Test")
        {
            Remaining = TimeSpan.FromSeconds(12),
        };
        timer.SetDuration(TimeSpan.FromSeconds(60));
        Assert.Equal(TimeSpan.FromSeconds(60), timer.Remaining);
    }

    private sealed class ManualClock(DateTimeOffset? initialNow = null) : TimeProvider
    {
        private readonly DateTimeOffset now = initialNow ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => this.now;
    }
}