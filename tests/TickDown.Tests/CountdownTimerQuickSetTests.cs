// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using System.Text.Json;
using TickDown.Core.Models;

/// <summary>
/// Tests Quick Set interval behavior on countdown timers.
/// </summary>
public class CountdownTimerQuickSetTests
{
    /// <summary>
    /// Verifies that new timers retain the established Quick Set defaults.
    /// </summary>
    [Fact]
    public void NewTimerUsesEstablishedQuickSetDefaults()
    {
        CountdownTimer timer = new();

        Assert.Equal(1, timer.QuickSetInterval1Minutes);
        Assert.Equal(5, timer.QuickSetInterval2Minutes);
        Assert.Equal(10, timer.QuickSetInterval3Minutes);
        Assert.Equal(15, timer.QuickSetInterval4Minutes);
    }

    /// <summary>
    /// Verifies that configured Quick Set intervals can be changed.
    /// </summary>
    [Fact]
    public void QuickSetIntervalsCanBeChanged()
    {
        CountdownTimer timer = new()
        {
            QuickSetInterval1Minutes = 2,
            QuickSetInterval2Minutes = 8,
            QuickSetInterval3Minutes = 20,
            QuickSetInterval4Minutes = 45,
        };

        Assert.Equal(2, timer.QuickSetInterval1Minutes);
        Assert.Equal(8, timer.QuickSetInterval2Minutes);
        Assert.Equal(20, timer.QuickSetInterval3Minutes);
        Assert.Equal(45, timer.QuickSetInterval4Minutes);
    }

    /// <summary>
    /// Verifies that old saved timers without Quick Set fields receive the defaults.
    /// </summary>
    [Fact]
    public void SavedTimerWithoutQuickSetIntervalsUsesDefaults()
    {
        CountdownTimer? timer = JsonSerializer.Deserialize<CountdownTimer>("{}");

        Assert.NotNull(timer);
        Assert.Equal(1, timer.QuickSetInterval1Minutes);
        Assert.Equal(5, timer.QuickSetInterval2Minutes);
        Assert.Equal(10, timer.QuickSetInterval3Minutes);
        Assert.Equal(15, timer.QuickSetInterval4Minutes);
    }

    /// <summary>
    /// Verifies that configured Quick Set intervals survive serialization.
    /// </summary>
    [Fact]
    public void QuickSetIntervalsRoundTripThroughJson()
    {
        CountdownTimer source = new()
        {
            QuickSetInterval1Minutes = 3,
            QuickSetInterval2Minutes = 12,
            QuickSetInterval3Minutes = 25,
            QuickSetInterval4Minutes = 90,
        };

        string json = JsonSerializer.Serialize(source);
        CountdownTimer? restored = JsonSerializer.Deserialize<CountdownTimer>(json);

        Assert.NotNull(restored);
        Assert.Equal(3, restored.QuickSetInterval1Minutes);
        Assert.Equal(12, restored.QuickSetInterval2Minutes);
        Assert.Equal(25, restored.QuickSetInterval3Minutes);
        Assert.Equal(90, restored.QuickSetInterval4Minutes);
    }

    /// <summary>
    /// Verifies that non-positive Quick Set intervals are normalized.
    /// </summary>
    [Fact]
    public void NonPositiveQuickSetIntervalsAreNormalized()
    {
        CountdownTimer timer = new()
        {
            QuickSetInterval1Minutes = 0,
            QuickSetInterval2Minutes = -5,
        };

        Assert.Equal(1, timer.QuickSetInterval1Minutes);
        Assert.Equal(1, timer.QuickSetInterval2Minutes);
    }
}