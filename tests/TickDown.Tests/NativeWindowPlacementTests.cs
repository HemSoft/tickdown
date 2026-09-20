// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using System.ComponentModel;
using TickDown;
using TickDown.Core.Models;

/// <summary>
/// Verifies native restoration completes a DPI transition before applying saved size.
/// </summary>
public class NativeWindowPlacementTests
{
    /// <summary>
    /// Verifies restoration moves without resizing before applying bounds from 100% and 175% displays.
    /// </summary>
    /// <param name="x">The saved left coordinate.</param>
    /// <param name="y">The saved top coordinate.</param>
    /// <param name="width">The saved width.</param>
    /// <param name="height">The saved height.</param>
    [Theory]
    [InlineData(320, 220, 900, 700)]
    [InlineData(3800, 200, 1750, 1225)]
    public void MoveAndResizeMovesToTargetDisplayBeforeSizing(int x, int y, int width, int height)
    {
        WindowBounds bounds = new(x, y, width, height);
        List<(int X, int Y, int Width, int Height, uint Flags)> calls = [];

        NativeWindowPlacement.MoveAndResize(
            42,
            bounds,
            (windowHandle, insertAfter, x, y, width, height, flags) =>
            {
                Assert.Equal(42, windowHandle);
                Assert.Equal(0, insertAfter);
                calls.Add((x, y, width, height, flags));
                return true;
            });

        Assert.Equal(2, calls.Count);
        Assert.Equal((x, y, width, height), (calls[0].X, calls[0].Y, calls[0].Width, calls[0].Height));
        Assert.Equal((x, y, width, height), (calls[1].X, calls[1].Y, calls[1].Width, calls[1].Height));
        Assert.NotEqual(0U, calls[0].Flags & 0x0001U);
        Assert.Equal(0U, calls[1].Flags & 0x0001U);
    }

    /// <summary>
    /// Verifies restoration stops if the target-display move fails.
    /// </summary>
    [Fact]
    public void MoveAndResizeStopsAfterMoveFailure()
    {
        int calls = 0;

        _ = Assert.Throws<Win32Exception>(() => NativeWindowPlacement.MoveAndResize(
            42,
            new WindowBounds(100, 200, 800, 600),
            (_, _, _, _, _, _, _) =>
            {
                calls++;
                return false;
            }));

        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Verifies restoration reports a final sizing failure after a successful move.
    /// </summary>
    [Fact]
    public void MoveAndResizeReportsFinalSizingFailure()
    {
        int calls = 0;

        _ = Assert.Throws<Win32Exception>(() => NativeWindowPlacement.MoveAndResize(
            42,
            new WindowBounds(100, 200, 800, 600),
            (_, _, _, _, _, _, _) => ++calls == 1));

        Assert.Equal(2, calls);
    }
}