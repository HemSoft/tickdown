namespace TickDown.Services;

using System.Text.Json;

/// <summary>
/// Service for saving and loading application settings and timers.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly string _windowSettingsPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    public SettingsService()
    {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(folder, "TickDown");
        _ = Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "timers.json");
        _windowSettingsPath = Path.Combine(appFolder, "window.json");
    }

    /// <summary>
    /// Saves the list of timers to storage.
    /// </summary>
    /// <param name="timers">The timers to save.</param>
    public async Task SaveTimersAsync(IEnumerable<CountdownTimer> timers)
    {
        string json = JsonSerializer.Serialize(timers);
        await File.WriteAllTextAsync(_filePath, json);
    }

    /// <summary>
    /// Loads the list of timers from storage.
    /// </summary>
    /// <returns>A collection of loaded timers.</returns>
    public async Task<IEnumerable<CountdownTimer>> LoadTimersAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<IEnumerable<CountdownTimer>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Saves the window settings to storage.
    /// </summary>
    /// <param name="settings">The window settings to save.</param>
    public async Task SaveWindowSettingsAsync(WindowSettings settings)
    {
        string json = JsonSerializer.Serialize(settings);
        await File.WriteAllTextAsync(_windowSettingsPath, json);
    }

    /// <summary>
    /// Loads the window settings from storage.
    /// </summary>
    /// <returns>The loaded window settings, or null if not found.</returns>
    public async Task<WindowSettings?> LoadWindowSettingsAsync()
    {
        if (!File.Exists(_windowSettingsPath))
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_windowSettingsPath);
            return JsonSerializer.Deserialize<WindowSettings>(json);
        }
        catch
        {
            return null;
        }
    }
}