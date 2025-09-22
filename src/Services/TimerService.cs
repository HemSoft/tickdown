namespace TickDown.Services;

using System.Timers;
using TickDown.Core.Models;
using TickDown.Core.Services;

public class TimerService : ITimerService
{
    private readonly Timer _timer;
    private CountdownTimer? _currentTimer;

    public event EventHandler<CountdownTimer>? TimerTick;
    public event EventHandler<CountdownTimer>? TimerCompleted;

    public CountdownTimer? CurrentTimer => _currentTimer;
    public bool IsRunning => _currentTimer?.State == TimerState.Running;

    public TimerService()
    {
        _timer = new Timer(100); // Update every 100ms for smooth progress
        _timer.Elapsed += OnTimerElapsed;
        _currentTimer = null; // Start with no timer
    }

    public void SetTimer(TimeSpan duration, string name = "")
    {
        _timer.Stop();
        _currentTimer = new CountdownTimer(duration, name);
    }

    public void Start()
    {
        if (_currentTimer == null)
        {
            return;
        }

        // Only start if the timer is in a startable state
        if (_currentTimer.State == TimerState.Stopped || _currentTimer.State == TimerState.Paused)
        {
            _currentTimer.Start();
            _timer.Start();
        }
    }

    public void Pause()
    {
        if (_currentTimer?.State != TimerState.Running)
        {
            return;
        }

        _timer.Stop();
        _currentTimer.Pause();
    }

    public void Stop()
    {
        _timer.Stop();
        _currentTimer?.Stop();
    }

    public void Reset()
    {
        _timer.Stop();
        _currentTimer?.Reset();
    }

    public void SetDuration(TimeSpan duration)
    {
        _currentTimer?.SetDuration(duration);
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_currentTimer == null)
        {
            _timer.Stop();
            return;
        }

        _currentTimer.Tick();
        TimerTick?.Invoke(this, _currentTimer);

        if (_currentTimer.State == TimerState.Completed)
        {
            _timer.Stop();
            TimerCompleted?.Invoke(this, _currentTimer);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
