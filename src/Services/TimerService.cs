namespace TickDown.Services;

using System.Timers;
using TickDown.Core.Services;

public sealed class TimerService : ITimerService
{
    private readonly Timer _timer;

    public event EventHandler? Tick;

    public TimerService()
    {
        _timer = new Timer(100); // Update every 100ms for smooth progress
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e) => Tick?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _timer?.Dispose();
}