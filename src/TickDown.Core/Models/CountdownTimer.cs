namespace TickDown.Core.Models;

public enum TimerState
{
    Stopped,
    Running,
    Paused,
    Completed
}

public class CountdownTimer
{
    public TimeSpan Duration { get; set; }
    public TimeSpan Remaining { get; set; }
    public TimerState State { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public CountdownTimer()
    {
        State = TimerState.Stopped;
    }

    public CountdownTimer(TimeSpan duration, string name = "")
    {
        Duration = duration;
        Remaining = duration;
        Name = name;
        State = TimerState.Stopped;
    }

    public double ProgressPercentage => Duration.TotalSeconds > 0
        ? Math.Max(0, (Duration.TotalSeconds - Remaining.TotalSeconds) / Duration.TotalSeconds * 100)
        : 0;

    public void Reset()
    {
        Remaining = Duration;
        State = TimerState.Stopped;
        StartTime = null;
        EndTime = null;
    }

    public void Start()
    {
        if (State == TimerState.Stopped)
        {
            StartTime = DateTime.Now;
            EndTime = StartTime.Value.Add(Remaining);
        }
        State = TimerState.Running;
    }

    public void Pause()
    {
        if (State == TimerState.Running)
        {
            State = TimerState.Paused;
        }
    }

    public void Stop()
    {
        State = TimerState.Stopped;
        StartTime = null;
        EndTime = null;
    }

    public void SetDuration(TimeSpan duration)
    {
        Duration = duration;
        if (State == TimerState.Stopped)
        {
            Remaining = duration;
        }
    }

    public void Tick()
    {
        if (State == TimerState.Running && EndTime.HasValue)
        {
            var now = DateTime.Now;
            Remaining = EndTime.Value - now;

            if (Remaining <= TimeSpan.Zero)
            {
                Remaining = TimeSpan.Zero;
                State = TimerState.Completed;
            }
        }
    }

}
