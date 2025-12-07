// Copyright © 2025 HemSoft

namespace TickDown.Services;

using System.Timers;
using global::TickDown.Core.Services;

/// <summary>
/// Service that provides a global timer tick.
/// </summary>
public sealed class TimerService : ITimerService
{
    private readonly Timer timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerService"/> class.
    /// </summary>
    public TimerService()
    {
        this.timer = new Timer(100);
        this.timer.Elapsed += this.OnTimerElapsed;
        this.timer.Start();
    }

    /// <summary>
    /// Occurs when the timer interval has elapsed.
    /// </summary>
    public event EventHandler? Tick;

    /// <summary>
    /// Disposes the timer resources.
    /// </summary>
    public void Dispose() => this.timer?.Dispose();

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e) => this.Tick?.Invoke(this, EventArgs.Empty);
}