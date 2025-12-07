// Copyright © 2025 HemSoft

namespace TickDown.Services;

using System.Text.Json;
using global::TickDown.Core.Models;
using global::TickDown.Core.Services;

/// <summary>
/// Service for saving and loading application settings and timers.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string filePath;
    private readonly string windowSettingsPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    public SettingsService()
    {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(folder, "TickDown");
        _ = Directory.CreateDirectory(appFolder);
        this.filePath = Path.Combine(appFolder, "timers.json");
        this.windowSettingsPath = Path.Combine(appFolder, "window.json");
    }

    /// <inheritdoc/>
    public Task SaveTimersAsync(IEnumerable<CountdownTimer> timers) =>
        SaveAsync(this.filePath, timers);

    /// <inheritdoc/>
    public async Task<IEnumerable<CountdownTimer>> LoadTimersAsync() =>
        await LoadAsync<IEnumerable<CountdownTimer>>(this.filePath) ?? [];

    /// <inheritdoc/>
    public Task SaveWindowSettingsAsync(WindowSettings settings) =>
        SaveAsync(this.windowSettingsPath, settings);

    /// <inheritdoc/>
    public Task<WindowSettings?> LoadWindowSettingsAsync() =>
        LoadAsync<WindowSettings>(this.windowSettingsPath);

    private static async Task SaveAsync<T>(string path, T data)
    {
        string json = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(path, json);
    }

    private static async Task<T?> LoadAsync<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            string json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}