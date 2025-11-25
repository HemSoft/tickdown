namespace TickDown.Services;

using System.Timers;
using TickDown.Core.Models;
using TickDown.Core.Services;

public class TimerService : ITimerService
{
    private readonly Timer _timer;

    public event EventHandler<CountdownTimer>? TimerTick;
    public event EventHandler<CountdownTimer>? TimerCompleted;

    public CountdownTimer? CurrentTimer
    {
        get;
        private set
        {
            _timer.Stop();
            field = value;
        }
    }

    public bool IsRunning => CurrentTimer?.State == TimerState.Running;

    public TimerService()
    {
        _timer = new Timer(100); // Update every 100ms for smooth progress
        _timer.Elapsed += OnTimerElapsed;
        CurrentTimer = null; // Start with no timer
    }

    public void SetTimer(TimeSpan duration, string name = "")
    {
        CurrentTimer = new CountdownTimer(duration, name);
    }

    public void Start()
    {
        if (CurrentTimer == null)
        {
            return;
        }

        // Only start if the timer is in a startable state
        if (CurrentTimer.State == TimerState.Stopped || CurrentTimer.State == TimerState.Paused)
        {
            CurrentTimer.Start();
            _timer.Start();
        }
    }

    public void Pause()
    {
        if (CurrentTimer?.State != TimerState.Running)
        {
            return;
        }

        _timer.Stop();
        CurrentTimer.Pause();
    }

    public void Stop()
    {
        _timer.Stop();
        CurrentTimer?.Stop();
    }

    public void Reset()
    {
        _timer.Stop();
        CurrentTimer?.Reset();
    }

    public void SetDuration(TimeSpan duration)
    {
        CurrentTimer?.SetDuration(duration);
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (CurrentTimer == null)
        {
            _timer.Stop();
            return;
        }

        CurrentTimer.Tick();
        TimerTick?.Invoke(this, CurrentTimer);

        if (CurrentTimer.State == TimerState.Completed)
        {
            _timer.Stop();
            TimerCompleted?.Invoke(this, CurrentTimer);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
