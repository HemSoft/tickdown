namespace TickDown.Core.Services;

using TickDown.Core.Models;

public interface ITimerService : IDisposable
{
    event EventHandler<CountdownTimer>? TimerTick;
    event EventHandler<CountdownTimer>? TimerCompleted;

    CountdownTimer? CurrentTimer { get; }
    bool IsRunning { get; }

    void SetTimer(TimeSpan duration, string name = "");
    void Start();
    void Pause();
    void Stop();
    void Reset();
    void SetDuration(TimeSpan duration);
}