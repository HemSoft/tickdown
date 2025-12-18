// Copyright © 2025 HemSoft

namespace TickDown.Services;

using TickDown.Core.Services;
using Windows.Media.Core;
using Windows.Media.Playback;

/// <summary>
/// Service for playing Windows system sounds from the Media folder.
/// </summary>
public sealed class AudioService : IAudioService, IDisposable
{
    private static readonly string MediaFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");

    private static readonly Dictionary<string, string> SoundFiles = new()
    {
        ["Alarm 01"] = "Alarm01.wav",
        ["Alarm 02"] = "Alarm02.wav",
        ["Alarm 03"] = "Alarm03.wav",
        ["Alarm 04"] = "Alarm04.wav",
        ["Alarm 05"] = "Alarm05.wav",
        ["Alarm 06"] = "Alarm06.wav",
        ["Alarm 07"] = "Alarm07.wav",
        ["Alarm 08"] = "Alarm08.wav",
        ["Alarm 09"] = "Alarm09.wav",
        ["Alarm 10"] = "Alarm10.wav",
        ["Ring 01"] = "Ring01.wav",
        ["Ring 02"] = "Ring02.wav",
        ["Ring 03"] = "Ring03.wav",
        ["Ring 04"] = "Ring04.wav",
        ["Ring 05"] = "Ring05.wav",
        ["Ring 06"] = "Ring06.wav",
        ["Ring 07"] = "Ring07.wav",
        ["Ring 08"] = "Ring08.wav",
        ["Ring 09"] = "Ring09.wav",
        ["Ring 10"] = "Ring10.wav",
        ["Chimes"] = "chimes.wav",
        ["Chord"] = "chord.wav",
        ["Ding"] = "ding.wav",
        ["Notify"] = "notify.wav",
        ["Tada"] = "tada.wav",
        ["Windows Notify"] = "Windows Notify.wav",
        ["Windows Notify Calendar"] = "Windows Notify Calendar.wav",
        ["Windows Notify Email"] = "Windows Notify Email.wav",
    };

    private MediaPlayer? mediaPlayer;
    private bool disposed;

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableSounds => [.. SoundFiles.Keys];

    /// <inheritdoc/>
    public void PlaySound(string soundName)
    {
        if (!SoundFiles.TryGetValue(soundName, out string? fileName))
        {
            fileName = "Alarm01.wav";
        }

        string filePath = Path.Combine(MediaFolder, fileName);
        if (!File.Exists(filePath))
        {
            return;
        }

        this.StopCurrentPlayback();

        this.mediaPlayer = new MediaPlayer
        {
            Source = MediaSource.CreateFromUri(new Uri(filePath)),
            AutoPlay = true,
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.disposed)
        {
            this.StopCurrentPlayback();
            this.disposed = true;
        }
    }

    private void StopCurrentPlayback()
    {
        if (this.mediaPlayer is not null)
        {
            this.mediaPlayer.Pause();
            this.mediaPlayer.Dispose();
            this.mediaPlayer = null;
        }
    }
}