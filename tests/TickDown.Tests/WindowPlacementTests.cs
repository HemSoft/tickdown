// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using TickDown.Core.Models;

/// <summary>
/// Verifies persisted window bounds remain usable across display changes.
/// </summary>
public class WindowPlacementTests
{
    /// <summary>
    /// Verifies available-display geometry is not adjusted during restoration.
    /// </summary>
    [Fact]
    public void IntersectingBoundsRemainUnchanged()
    {
        WindowBounds saved = new(240, 180, 760, 640);
        WindowBounds workArea = new(0, 0, 1920, 1040);

        WindowBounds? restored = WindowPlacement.ResolveVisibleBounds(saved, workArea);

        Assert.Equal(saved, restored);
    }

    /// <summary>
    /// Verifies a disconnected right-side display falls back to the nearest work-area edge.
    /// </summary>
    [Fact]
    public void OffscreenBoundsMoveIntoNearestWorkArea()
    {
        WindowBounds saved = new(4000, 200, 900, 700);
        WindowBounds workArea = new(0, 0, 1920, 1040);

        WindowBounds? restored = WindowPlacement.ResolveVisibleBounds(saved, workArea);

        Assert.Equal(new WindowBounds(1020, 200, 900, 700), restored);
    }

    /// <summary>
    /// Verifies fallback bounds shrink when the available display is smaller than the saved window.
    /// </summary>
    [Fact]
    public void OversizedOffscreenBoundsFitNearestWorkArea()
    {
        WindowBounds saved = new(-3000, -2000, 2000, 1200);
        WindowBounds workArea = new(0, 0, 1280, 720);

        WindowBounds? restored = WindowPlacement.ResolveVisibleBounds(saved, workArea);

        Assert.Equal(workArea, restored);
    }

    /// <summary>
    /// Verifies invalid saved sizes defer to normal Windows placement.
    /// </summary>
    /// <param name="width">The invalid saved width.</param>
    /// <param name="height">The invalid saved height.</param>
    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, 0)]
    [InlineData(-1, 600)]
    [InlineData(800, -1)]
    public void InvalidSavedSizeCannotBeRestored(int width, int height)
    {
        WindowBounds saved = new(100, 100, width, height);
        WindowBounds workArea = new(0, 0, 1920, 1040);

        Assert.Null(WindowPlacement.ResolveVisibleBounds(saved, workArea));
    }
}