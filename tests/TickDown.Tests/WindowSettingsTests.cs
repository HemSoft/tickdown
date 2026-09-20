// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using TickDown.Core.Models;

/// <summary>
/// Verifies geometry updates retain unrelated window preferences.
/// </summary>
public class WindowSettingsTests
{
    /// <summary>
    /// Verifies restored-window geometry changes without replacing theme data.
    /// </summary>
    [Fact]
    public void RestoredGeometryPreservesTheme()
    {
        WindowSettings settings = new()
        {
            Theme = "Dark",
            X = 10,
            Y = 20,
            Width = 300,
            Height = 400,
            IsMaximized = true,
        };
        settings.UpdateWindowState(false, 100, 200, 800, 600);
        Assert.Equal("Dark", settings.Theme);
        Assert.True(settings.IsPositionSet);
        Assert.False(settings.IsMaximized);
        Assert.Equal((100, 200, 800, 600), (settings.X, settings.Y, settings.Width, settings.Height));
    }

    /// <summary>
    /// Verifies untouched defaults do not override Windows first-run placement.
    /// </summary>
    [Fact]
    public void DefaultSettingsHaveNoSavedPlacement()
    {
        WindowSettings settings = new();
        Assert.False(settings.HasSavedPlacement);
    }

    /// <summary>
    /// Verifies geometry written before the position flag was maintained remains restorable.
    /// </summary>
    [Fact]
    public void LegacyGeometryHasSavedPlacement()
    {
        WindowSettings settings = new()
        {
            X = 120,
            Y = 80,
            Width = 900,
            Height = 700,
        };
        Assert.True(settings.HasSavedPlacement);
    }

    /// <summary>
    /// Verifies an explicit saved position remains valid even when it matches model defaults.
    /// </summary>
    [Fact]
    public void ExplicitDefaultGeometryHasSavedPlacement()
    {
        WindowSettings settings = new() { IsPositionSet = true };
        Assert.True(settings.HasSavedPlacement);
    }

    /// <summary>
    /// Verifies maximized shutdown keeps the prior restored geometry and theme.
    /// </summary>
    [Fact]
    public void MaximizedStatePreservesRestoredGeometryAndTheme()
    {
        WindowSettings settings = new()
        {
            Theme = "Light",
            X = 100,
            Y = 200,
            Width = 800,
            Height = 600,
        };
        settings.UpdateWindowState(true, -8, -8, 1936, 1056);
        Assert.Equal("Light", settings.Theme);
        Assert.True(settings.IsMaximized);
        Assert.Equal((100, 200, 800, 600), (settings.X, settings.Y, settings.Width, settings.Height));
    }
}