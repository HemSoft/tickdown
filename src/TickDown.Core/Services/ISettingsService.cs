// Copyright © 2025 HemSoft

namespace TickDown.Core.Services;

using TickDown.Core.Models;

/// <summary>
/// Service interface for managing application settings and timers persistence.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Occurs when settings cannot be saved or loaded, or a backup is recovered.
    /// </summary>
    event EventHandler<SettingsFailureEventArgs>? PersistenceFailed;

    /// <summary>
    /// Waits until accepted writes drain and reports any unresolved write failure.
    /// </summary>
    /// <returns>A task that completes when all accepted writes have finished.</returns>
    Task FlushAsync();

    /// <summary>
    /// Saves the list of timers to storage.
    /// </summary>
    /// <param name="timers">The timers to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveTimersAsync(IEnumerable<CountdownTimer> timers);

    /// <summary>
    /// Loads the list of timers from storage.
    /// </summary>
    /// <returns>A collection of loaded timers.</returns>
    Task<IEnumerable<CountdownTimer>> LoadTimersAsync();

    /// <summary>
    /// Saves the window settings to storage.
    /// </summary>
    /// <param name="settings">The window settings to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveWindowSettingsAsync(WindowSettings settings);

    /// <summary>
    /// Loads the window settings from storage.
    /// </summary>
    /// <returns>The loaded window settings, or null if not found.</returns>
    Task<WindowSettings?> LoadWindowSettingsAsync();
}