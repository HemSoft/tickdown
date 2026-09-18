// Copyright © 2025 HemSoft

namespace Microsoft.UI.Xaml.Media;

using Windows.UI;

/// <summary>
/// Retains a color without activating a native WinUI object in command tests.
/// </summary>
/// <param name="color">The brush color.</param>
public sealed class SolidColorBrush(Color color) : Brush(color);