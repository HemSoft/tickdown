// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using System.Text.Json;
using TickDown.Core.Models;

/// <summary>
/// Tests the predefined completion colors for countdown timers.
/// </summary>
public class CountdownTimerColorTests
{
    /// <summary>
    /// Verifies that gold is available and survives timer persistence.
    /// </summary>
    [Fact]
    public void GoldIsAvailableAndRoundTripsThroughJson()
    {
        Assert.Contains(CompletionColorPalette.Gold, CompletionColorPalette.Colors);

        CountdownTimer source = new()
        {
            CompletionColor = CompletionColorPalette.Gold,
        };

        string json = JsonSerializer.Serialize(source);
        CountdownTimer? restored = JsonSerializer.Deserialize<CountdownTimer>(json);

        Assert.NotNull(restored);
        Assert.Equal(CompletionColorPalette.Gold, restored.CompletionColor);
    }
}