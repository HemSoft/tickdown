namespace TickDown.Core.Services;

using TickDown.Core.Models;

public interface ISettingsService
{
    Task SaveTimersAsync(IEnumerable<CountdownTimer> timers);
    Task<IEnumerable<CountdownTimer>> LoadTimersAsync();
    Task SaveWindowSettingsAsync(WindowSettings settings);
    Task<WindowSettings?> LoadWindowSettingsAsync();
}