// Copyright © 2025 HemSoft

namespace TickDown.Core.Models;

/// <summary>
/// Defines the completion colors available in the timer color picker.
/// </summary>
public static class CompletionColorPalette
{
    /// <summary>
    /// The hexadecimal value for gold.
    /// </summary>
    public const string Gold = "#FFD700";

    private static readonly IReadOnlyList<string> ColorValues =
    [
        "#4CAF50", // Green
        "#F44336", // Red
        "#2196F3", // Blue
        "#FFEB3B", // Yellow
        "#FF9800", // Orange
        Gold,
        "#9C27B0", // Purple
    ];

    /// <summary>
    /// Gets the predefined completion color values.
    /// </summary>
    public static IReadOnlyList<string> Colors => ColorValues;
}