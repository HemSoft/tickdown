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
    /// <param name="owner">The caller that owns this playback request.</param>
    void PlaySound(string soundName, object owner);

    /// <summary>
    /// Stops and releases native playback only when it belongs to the caller.
    /// </summary>
    /// <param name="owner">The caller relinquishing its playback request.</param>
    void StopSound(object owner);
}