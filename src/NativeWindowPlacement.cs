// Copyright © 2025 HemSoft

namespace TickDown;

using System.ComponentModel;
using System.Runtime.InteropServices;
using global::TickDown.Core.Models;

/// <summary>
/// Reads and applies native window bounds without DPI virtualization.
/// </summary>
internal static class NativeWindowPlacement
{
    private const uint NoActivate = 0x0010;
    private const uint NoZOrder = 0x0004;

    /// <summary>
    /// Gets the outer bounds of a native window.
    /// </summary>
    /// <param name="windowHandle">The native window handle.</param>
    /// <returns>The current bounds in physical screen coordinates.</returns>
    internal static WindowBounds GetBounds(nint windowHandle) =>
        GetWindowRect(windowHandle, out NativeRect bounds)
            ? new WindowBounds(
                bounds.Left,
                bounds.Top,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top)
            : throw new Win32Exception(Marshal.GetLastWin32Error());

    /// <summary>
    /// Applies outer bounds to a native window without changing focus or z-order.
    /// </summary>
    /// <param name="windowHandle">The native window handle.</param>
    /// <param name="bounds">The bounds in physical screen coordinates.</param>
    internal static void MoveAndResize(nint windowHandle, WindowBounds bounds)
    {
        if (!SetWindowPos(
            windowHandle,
            0,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NoActivate | NoZOrder))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect bounds);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }
}