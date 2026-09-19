// Copyright © 2025 HemSoft

namespace Microsoft.UI.Xaml.Media;

using Windows.UI;

/// <summary>
/// Retains presentation color data for view-model command tests.
/// </summary>
/// <param name="color">The brush color.</param>
public class Brush(Color color)
{
    /// <summary>
    /// Gets the color retained for command-test assertions.
    /// </summary>
    public Color Color { get; } = color;
}