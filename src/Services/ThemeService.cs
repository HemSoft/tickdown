// Copyright © 2025 HemSoft

namespace TickDown.Services;

using Microsoft.UI.Xaml;
using TickDown.Core.Models;
using TickDown.Core.Services;
using Windows.UI.ViewManagement;

/// <summary>
/// Service for managing application theme.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly ISettingsService settingsService;
    private readonly UISettings uiSettings;
    private string currentTheme = "System";
    private WindowSettings windowSettings = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service.</param>
    public ThemeService(ISettingsService settingsService)
    {
        this.settingsService = settingsService;
        this.uiSettings = new UISettings();
        this.uiSettings.ColorValuesChanged += this.OnSystemThemeChanged;
    }

    /// <inheritdoc/>
    public event EventHandler? ThemeChanged;

    /// <inheritdoc/>
    public string CurrentTheme => this.currentTheme;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        try
        {
            this.windowSettings = await this.settingsService.LoadWindowSettingsAsync() ?? new WindowSettings();
            this.currentTheme = this.windowSettings.Theme;
        }
        catch (IOException)
        {
            // The settings failure event reports the load failure to the UI.
        }

        this.ApplyTheme();
    }

    /// <inheritdoc/>
    public void SetTheme(string theme)
    {
        if (this.currentTheme == theme)
        {
            return;
        }

        this.currentTheme = theme;
        this.ApplyTheme();
        this.ThemeChanged?.Invoke(this, EventArgs.Empty);
        _ = this.SaveThemeAsync();
    }

    private static FrameworkElement? GetRootElement()
    {
        Window? window = (Application.Current as App)?.MainWindow;
        return window?.Content as FrameworkElement;
    }

    private async Task SaveThemeAsync()
    {
        this.windowSettings.Theme = this.currentTheme;
        try
        {
            // Enqueue immediately, before any await, so shutdown's flush includes
            // the selected theme even when its write is still pending.
            await this.settingsService.SaveWindowSettingsAsync(this.windowSettings);
        }
        catch (IOException)
        {
            // The settings failure event reports the save failure to the UI.
        }
    }

    private void ApplyTheme()
    {
        if (Application.Current is not App)
        {
            return;
        }

        FrameworkElement? rootElement = GetRootElement();
        if (rootElement is null)
        {
            return;
        }

        rootElement.RequestedTheme = this.currentTheme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private void OnSystemThemeChanged(UISettings sender, object args)
    {
        if (this.currentTheme != "System" || sender != this.uiSettings)
        {
            return;
        }

        // Re-apply theme when system theme changes and we're following system
        _ = GetRootElement()?.DispatcherQueue.TryEnqueue(() =>
        {
            this.ApplyTheme();
            this.ThemeChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}