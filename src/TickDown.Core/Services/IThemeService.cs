// Copyright © 2025 HemSoft

namespace TickDown.Core.Services;

/// <summary>
/// Service interface for managing application theme.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Occurs when the theme changes.
    /// </summary>
    event EventHandler? ThemeChanged;

    /// <summary>
    /// Gets the current theme setting.
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Sets the application theme.
    /// </summary>
    /// <param name="theme">The theme to set: "Light", "Dark", or "System".</param>
    void SetTheme(string theme);

    /// <summary>
    /// Initializes the theme service and applies the saved theme.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync();
}