namespace TickDown.Core.Services;

using TickDown.Core.Models;

public interface ITimerService
{
    public event EventHandler<CountdownTimer>? TimerTick;
    public event EventHandler<CountdownTimer>? TimerCompleted;

    public CountdownTimer? CurrentTimer { get; }
    public bool IsRunning { get; }

    public void SetTimer(TimeSpan duration, string name = "");
    public void Start();
    public void Pause();
    public void Stop();
    public void Reset();
    public void SetDuration(TimeSpan duration);
}
