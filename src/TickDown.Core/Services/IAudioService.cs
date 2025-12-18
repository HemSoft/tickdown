// Copyright © 2025 HemSoft

namespace TickDown.Core.Services;

/// <summary>
/// Service interface for playing system sounds.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Gets the list of available system sounds.
    /// </summary>
    IReadOnlyList<string> AvailableSounds { get; }

    /// <summary>
    /// Plays the specified system sound.
    /// </summary>
    /// <param name="soundName">The name of the system sound to play.</param>
    void PlaySound(string soundName);
}