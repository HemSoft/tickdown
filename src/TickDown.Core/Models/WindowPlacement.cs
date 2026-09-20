// Copyright © 2025 HemSoft

namespace TickDown.Core.Models;

/// <summary>
/// Resolves persisted window bounds against the displays available at startup.
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// Keeps valid intersecting bounds unchanged and moves unavailable-display bounds into the nearest work area.
    /// </summary>
    /// <param name="savedBounds">The persisted normal window bounds.</param>
    /// <param name="workArea">The nearest display work area.</param>
    /// <returns>Restorable bounds, or <see langword="null"/> when either rectangle is invalid.</returns>
    public static WindowBounds? ResolveVisibleBounds(WindowBounds savedBounds, WindowBounds workArea)
    {
        if (!HasArea(savedBounds) || !HasArea(workArea))
        {
            return null;
        }

        if (Intersects(savedBounds, workArea))
        {
            return savedBounds;
        }

        int width = Math.Min(savedBounds.Width, workArea.Width);
        int height = Math.Min(savedBounds.Height, workArea.Height);
        int x = Math.Clamp(savedBounds.X, workArea.X, workArea.X + workArea.Width - width);
        int y = Math.Clamp(savedBounds.Y, workArea.Y, workArea.Y + workArea.Height - height);
        return new WindowBounds(x, y, width, height);
    }

    private static bool HasArea(WindowBounds bounds) => bounds.Width > 0 && bounds.Height > 0;

    private static bool Intersects(WindowBounds first, WindowBounds second) =>
        (long)first.X < (long)second.X + second.Width &&
        (long)first.X + first.Width > second.X &&
        (long)first.Y < (long)second.Y + second.Height &&
        (long)first.Y + first.Height > second.Y;
}