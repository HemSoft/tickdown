namespace TickDown.Services;

using System.Text.Json;
using TickDown.Core.Models;
using TickDown.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly string _windowSettingsPath;

    public SettingsService()
    {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(folder, "TickDown");
        _ = Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "timers.json");
        _windowSettingsPath = Path.Combine(appFolder, "window.json");
    }

    public async Task SaveTimersAsync(IEnumerable<CountdownTimer> timers)
    {
        string json = JsonSerializer.Serialize(timers);
        await File.WriteAllTextAsync(_filePath, json);
    }

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

    public async Task SaveWindowSettingsAsync(WindowSettings settings)
    {
        string json = JsonSerializer.Serialize(settings);
        await File.WriteAllTextAsync(_windowSettingsPath, json);
    }

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