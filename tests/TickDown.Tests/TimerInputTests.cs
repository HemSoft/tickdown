// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using System.Globalization;
using TickDown.Core.Models;
using TickDown.Core.Services;
using TickDown.ViewModels;

/// <summary>
/// Exercises duration validation through the actual editable view-model property.
/// </summary>
public class TimerInputTests
{
    /// <summary>
    /// Verifies invalid input restores the previous valid duration and display.
    /// </summary>
    /// <param name="input">The invalid input.</param>
    [Theory]
    [InlineData("999999999999999999999999999999h")]
    [InlineData("99999999999999999s")]
    [InlineData("100000000hours")]
    [InlineData("NaN")]
    [InlineData("NaNh")]
    [InlineData("Infinitys")]
    [InlineData("-Infinitym")]
    [InlineData("1e309h")]
    [InlineData("-1h")]
    [InlineData("-00:00:01")]
    [InlineData("-1")]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("1..5m")]
    [InlineData("1,5m")]
    [InlineData("10675199.02:48:05.4775807")]
    public void InvalidInputPreservesDuration(string input)
    {
        using Fixture fixture = new();
        fixture.Timer.TimeDisplay = input;
        Assert.Equal(TimeSpan.FromMinutes(5), fixture.Timer.Model.Duration);
        Assert.Equal("00:05:00", fixture.Timer.TimeDisplay);
    }

    /// <summary>
    /// Verifies unit decimals are invariant and time-span text uses current culture.
    /// </summary>
    /// <param name="culture">The parsing culture.</param>
    /// <param name="input">The valid text.</param>
    /// <param name="seconds">The expected whole-second duration.</param>
    [Theory]
    [InlineData("en-US", "1.5m", 90)]
    [InlineData("de-DE", "1.5m", 90)]
    [InlineData("tr-TR", "1.5MIN", 90)]
    [InlineData("en-US", " 2 hours ", 7200)]
    [InlineData("en-US", "1hour", 3600)]
    [InlineData("en-US", "15sec", 15)]
    [InlineData("en-US", "10", 600)]
    [InlineData("en-US", "01:02:03", 3723)]
    [InlineData("en-US", "1.02:03:04", 93784)]
    [InlineData("de-DE", "00:00:01,5", 1)]
    [InlineData("en-US", "0s", 0)]
    [InlineData("en-US", "0.5s", 0)]
    public void SupportedFormatsHaveAnExplicitCulture(string culture, string input, int seconds)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using Fixture fixture = new();
            fixture.Timer.TimeDisplay = input;
            Assert.Equal(TimeSpan.FromSeconds(seconds), fixture.Timer.Model.Duration);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// Verifies very long pasted numbers do not overflow numeric conversion.
    /// </summary>
    [Fact]
    public void VeryLongNumericInputIsRejected()
    {
        using Fixture fixture = new();
        fixture.Timer.TimeDisplay = new string('9', 5000) + "h";
        Assert.Equal(TimeSpan.FromMinutes(5), fixture.Timer.Model.Duration);
    }

    /// <summary>
    /// Verifies supported long durations are not restricted by an arbitrary new cap.
    /// </summary>
    [Fact]
    public void LongRepresentableDurationCanStart()
    {
        using Fixture fixture = new();
        fixture.Timer.TimeDisplay = "1000000h";
        Assert.Equal(TimeSpan.FromHours(1000000), fixture.Timer.Model.Duration);
        fixture.Timer.StartCommand.Execute(null);
        Assert.Equal(TimerState.Running, fixture.Timer.Model.State);
    }

    /// <summary>
    /// Verifies the calendar boundary rather than only the larger TimeSpan boundary.
    /// </summary>
    [Fact]
    public void CalendarBoundariesAreCheckedBeforeUpdatingTheModel()
    {
        using Fixture fixture = new();
        long days = (DateTime.MaxValue.Date - DateTime.Today).Days;
        string accepted = ((days - 1) * 24).ToString(CultureInfo.InvariantCulture) + "h";
        fixture.Timer.TimeDisplay = accepted;
        TimeSpan duration = TimeSpan.FromDays(days - 1);
        Assert.Equal(duration, fixture.Timer.Model.Duration);
        fixture.Timer.TimeDisplay = ((days + 1) * 24).ToString(CultureInfo.InvariantCulture) + "h";
        Assert.Equal(duration, fixture.Timer.Model.Duration);
        fixture.Timer.StartCommand.Execute(null);
        Assert.Equal(TimerState.Running, fixture.Timer.Model.State);
    }

    /// <summary>
    /// Verifies loaded or formerly valid durations cannot overflow the start path.
    /// </summary>
    /// <param name="state">The state before starting.</param>
    [Theory]
    [InlineData(TimerState.Stopped)]
    [InlineData(TimerState.Paused)]
    [InlineData(TimerState.Completed)]
    public void UnrepresentableDeadlineCannotStart(TimerState state)
    {
        CountdownTimer model = new(TimeSpan.MaxValue)
        {
            State = state,
            Remaining = state == TimerState.Completed ? TimeSpan.Zero : TimeSpan.MaxValue,
        };
        using Fixture fixture = new(model);
        fixture.Timer.StartCommand.Execute(null);
        Assert.Equal(state, fixture.Timer.Model.State);
        Assert.Empty(fixture.Timer.EndTimeDisplay);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestTimerService ticks = new();

        public Fixture(CountdownTimer? model = null)
        {
            this.Timer = new TimerViewModel(this.ticks, new TestAudioService(), model);
            this.ticks.RaiseTick();
        }

        public TimerViewModel Timer { get; }

        public void Dispose()
        {
            this.Timer.RemoveCommand.Execute(null);
            this.Timer.Dispose();
            this.ticks.Dispose();
        }
    }

    private sealed class TestTimerService : ITimerService
    {
        public event EventHandler? Tick;

        public void RaiseTick() => this.Tick?.Invoke(this, EventArgs.Empty);

        public void Dispose() => this.Tick = null;
    }

    private sealed class TestAudioService : IAudioService
    {
        public IReadOnlyList<string> AvailableSounds => [];

        public List<string> PlayedSounds { get; } = [];

        public void PlaySound(string soundName) => this.PlayedSounds.Add(soundName);
    }
}