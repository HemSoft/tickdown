// Copyright © 2025 HemSoft

namespace TickDown.Core.Models;

/// <summary>
/// Represents the state of a countdown timer.
/// </summary>
public enum TimerState
{
    /// <summary>Timer is stopped.</summary>
    Stopped,

    /// <summary>Timer is running.</summary>
    Running,

    /// <summary>Timer is paused.</summary>
    Paused,

    /// <summary>Timer has completed.</summary>
    Completed,
}

/// <summary>
/// Represents a countdown timer with duration, remaining time, and state.
/// </summary>
public class CountdownTimer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CountdownTimer"/> class.
    /// </summary>
    public CountdownTimer() => this.State = TimerState.Stopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="CountdownTimer"/> class with a specified duration.
    /// </summary>
    /// <param name="duration">The duration of the timer.</param>
    /// <param name="name">The name of the timer.</param>
    public CountdownTimer(TimeSpan duration, string name = "")
        : this()
    {
        this.Duration = duration;
        this.Remaining = duration;
        this.Name = name;
    }

    /// <summary>
    /// Gets or sets the total duration of the timer.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the remaining time on the timer.
    /// </summary>
    public TimeSpan Remaining { get; set; }

    /// <summary>
    /// Gets or sets the current state of the timer.
    /// </summary>
    public TimerState State { get; set; }

    /// <summary>
    /// Gets or sets the name of the timer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time when the timer was started.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the time when the timer will end.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets the progress percentage of the timer (0-100).
    /// </summary>
    public double ProgressPercentage => this.Duration.TotalSeconds > 0
        ? Math.Max(0, (this.Duration.TotalSeconds - this.Remaining.TotalSeconds) / this.Duration.TotalSeconds * 100)
        : 0;

    /// <summary>
    /// Resets the timer to its initial duration.
    /// </summary>
    public void Reset()
    {
        this.Remaining = this.Duration;
        this.State = TimerState.Stopped;
        this.StartTime = null;
        this.EndTime = null;
    }

    /// <summary>
    /// Starts the timer.
    /// </summary>
    public void Start()
    {
        if (this.State == TimerState.Stopped)
        {
            this.StartTime = DateTime.Now;
            this.EndTime = this.StartTime.Value.Add(this.Remaining);
        }

        this.State = TimerState.Running;
    }

    /// <summary>
    /// Pauses the timer.
    /// </summary>
    public void Pause()
    {
        if (this.State == TimerState.Running)
        {
            this.State = TimerState.Paused;
        }
    }

    /// <summary>
    /// Stops the timer and preserves the remaining time.
    /// </summary>
    public void Stop()
    {
        if (this.State == TimerState.Running && this.EndTime.HasValue)
        {
            this.Remaining = this.EndTime.Value - DateTime.Now;
            if (this.Remaining < TimeSpan.Zero)
            {
                this.Remaining = TimeSpan.Zero;
            }
        }

        this.State = TimerState.Stopped;
        this.StartTime = null;
        this.EndTime = null;
    }

    /// <summary>
    /// Sets the duration of the timer.
    /// </summary>
    /// <param name="duration">The new duration.</param>
    public void SetDuration(TimeSpan duration)
    {
        this.Duration = duration;
        if (this.State == TimerState.Stopped)
        {
            this.Remaining = duration;
        }
    }

    /// <summary>
    /// Updates the timer state based on elapsed time.
    /// </summary>
    public void Tick()
    {
        if (this.State == TimerState.Running && this.EndTime.HasValue)
        {
            DateTime now = DateTime.Now;
            this.Remaining = this.EndTime.Value - now;

            if (this.Remaining <= TimeSpan.Zero)
            {
                this.Remaining = TimeSpan.Zero;
                this.State = TimerState.Completed;
            }
        }
    }
}