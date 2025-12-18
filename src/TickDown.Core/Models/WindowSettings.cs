// Copyright © 2025 HemSoft

namespace TickDown.Core.Models;

/// <summary>
/// Represents the window position and size settings.
/// </summary>
public class WindowSettings
{
    /// <summary>
    /// Gets or sets the X position of the window.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the Y position of the window.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets the width of the window.
    /// </summary>
    public int Width { get; set; } = 400;

    /// <summary>
    /// Gets or sets the height of the window.
    /// </summary>
    public int Height { get; set; } = 300;

    /// <summary>
    /// Gets or sets a value indicating whether the window position has been set.
    /// </summary>
    public bool IsPositionSet { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the window is maximized.
    /// </summary>
    public bool IsMaximized { get; set; }

    /// <summary>
    /// Gets or sets the application theme. Valid values: "Light", "Dark", "System".
    /// </summary>
    public string Theme { get; set; } = "System";
}