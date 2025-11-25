namespace TickDown.Services;

using System.Timers;

/// <summary>
/// Service that provides a global timer tick.
/// </summary>
public sealed class TimerService : ITimerService
{
    private readonly Timer _timer;

    /// <summary>
    /// Occurs when the timer interval has elapsed.
    /// </summary>
    public event EventHandler? Tick;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerService"/> class.
    /// </summary>
    public TimerService()
    {
        _timer = new Timer(100); // Update every 100ms for smooth progress
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e) => Tick?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Disposes the timer resources.
    /// </summary>
    public void Dispose() => _timer?.Dispose();
}