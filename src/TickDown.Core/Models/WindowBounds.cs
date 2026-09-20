// Copyright © 2025 HemSoft

namespace TickDown.Core.Models;

/// <summary>
/// Describes window bounds in screen coordinates.
/// </summary>
public readonly record struct WindowBounds
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowBounds"/> struct.
    /// </summary>
    /// <param name="x">The left screen coordinate.</param>
    /// <param name="y">The top screen coordinate.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public WindowBounds(int x, int y, int width, int height)
    {
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
    }

    /// <summary>
    /// Gets the left screen coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the top screen coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height in pixels.
    /// </summary>
    public int Height { get; }
}