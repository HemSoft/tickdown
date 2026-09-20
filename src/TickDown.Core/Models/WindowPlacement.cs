// Copyright © 2025 HemSoft

namespace TickDown.Core.Models;

/// <summary>
/// Resolves persisted window bounds against the displays available at startup.
/// </summary>
public static class WindowPlacement
{
    private const int MinimumVisibleCaptionWidth = 64;
    private const int CaptionHeight = 32;

    /// <summary>
    /// Keeps bounds with a usable caption unchanged and moves inaccessible bounds into the nearest work area.
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

        if (FitsWorkArea(savedBounds, workArea) && HasUsableCaption(savedBounds, workArea))
        {
            return savedBounds;
        }

        int width = Math.Min(savedBounds.Width, workArea.Width);
        int height = Math.Min(savedBounds.Height, workArea.Height);
        long maximumX = (long)workArea.X + workArea.Width - width;
        long maximumY = (long)workArea.Y + workArea.Height - height;
        int x = (int)Math.Clamp((long)savedBounds.X, workArea.X, maximumX);
        int y = (int)Math.Clamp((long)savedBounds.Y, workArea.Y, maximumY);
        return new WindowBounds(x, y, width, height);
    }

    private static bool HasArea(WindowBounds bounds) => bounds.Width > 0 && bounds.Height > 0;

    private static bool FitsWorkArea(WindowBounds savedBounds, WindowBounds workArea) =>
        savedBounds.Width <= workArea.Width && savedBounds.Height <= workArea.Height;

    private static bool HasUsableCaption(WindowBounds savedBounds, WindowBounds workArea)
    {
        int requiredWidth = Math.Min(savedBounds.Width, MinimumVisibleCaptionWidth);
        int requiredHeight = Math.Min(savedBounds.Height, CaptionHeight);
        long visibleWidth = CalculateOverlap(savedBounds.X, savedBounds.Width, workArea.X, workArea.Width);
        long visibleHeight = CalculateOverlap(savedBounds.Y, requiredHeight, workArea.Y, workArea.Height);
        return visibleWidth >= requiredWidth && visibleHeight >= requiredHeight;
    }

    private static long CalculateOverlap(int firstStart, int firstLength, int secondStart, int secondLength)
    {
        long firstEnd = (long)firstStart + firstLength;
        long secondEnd = (long)secondStart + secondLength;
        return Math.Max(0, Math.Min(firstEnd, secondEnd) - Math.Max(firstStart, secondStart));
    }
}