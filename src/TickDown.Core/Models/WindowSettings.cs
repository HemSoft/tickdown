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

    /// <summary>
    /// Updates maximized state and, when restored, the latest normal geometry.
    /// Other settings remain unchanged.
    /// </summary>
    /// <param name="isMaximized">Whether the window is maximized.</param>
    /// <param name="x">The current X position.</param>
    /// <param name="y">The current Y position.</param>
    /// <param name="width">The current width.</param>
    /// <param name="height">The current height.</param>
    public void UpdateWindowState(bool isMaximized, int x, int y, int width, int height)
    {
        this.IsMaximized = isMaximized;
        if (!isMaximized)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
        }
    }
}