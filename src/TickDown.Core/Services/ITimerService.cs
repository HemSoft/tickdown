namespace TickDown.Core.Services;

public interface ITimerService : IDisposable
{
    event EventHandler? Tick;
}