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
    /// Verifies a queued UI tick completes the timer and dismiss releases alarm playback.
    /// </summary>
    [Fact]
    public void CompletionTickAndDismissReleaseAlarmResources()
    {
        CountdownTimer model = new(TimeSpan.FromSeconds(1))
        {
            State = TimerState.Running,
            EndTime = DateTime.Now.AddSeconds(-1),
            EnableAlarm = true,
            EnableAlarmRepeat = true,
        };
        using TestTimerService ticks = new();
        TestAudioService audio = new();
        using TimerViewModel timer = new(ticks, audio, model);
        ticks.RaiseTick();
        Assert.True(timer.IsCompleted);
        _ = Assert.Single(audio.PlayedSounds);
        timer.DismissCommand.Execute(null);
        Assert.False(timer.IsCompleted);
        Assert.True(audio.StopCount > 0);
    }

    /// <summary>
    /// Verifies an unrelated timer cannot stop another timer's active alarm.
    /// </summary>
    [Fact]
    public void StoppingAnotherTimerPreservesAlarmOwnership()
    {
        CountdownTimer completed = new(TimeSpan.FromSeconds(1))
        {
            State = TimerState.Running,
            EndTime = DateTime.Now.AddSeconds(-1),
            EnableAlarm = true,
        };
        using TestTimerService ticks = new();
        TestAudioService audio = new();
        using TimerViewModel alarmOwner = new(ticks, audio, completed);
        using TimerViewModel unrelated = new(ticks, audio, new CountdownTimer(TimeSpan.FromMinutes(1)));
        ticks.RaiseTick();
        Assert.Same(alarmOwner, audio.CurrentOwner);
        unrelated.Dispose();
        Assert.Same(alarmOwner, audio.CurrentOwner);
        alarmOwner.DismissCommand.Execute(null);
        Assert.Null(audio.CurrentOwner);
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

    /// <summary>
    /// Verifies end-time formatting covers both names without depending on the runner's local zone.
    /// </summary>
    [Fact]
    public void EndTimeFormattingUsesExplicitStandardAndDaylightNames()
    {
        TimeZoneInfo.TransitionTime daylightStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified), 3, 2, DayOfWeek.Sunday);
        TimeZoneInfo.TransitionTime daylightEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified), 11, 1, DayOfWeek.Sunday);
        TimeZoneInfo.AdjustmentRule adjustment = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2030, 12, 31, 0, 0, 0, DateTimeKind.Unspecified),
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd);
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Qualification zone",
            TimeSpan.FromHours(-5),
            "Qualification zone",
            "Test Standard",
            "Test Daylight",
            [adjustment]);

        Assert.EndsWith(" TS", TimerViewModel.FormatEndTime(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Unspecified), timeZone));
        Assert.EndsWith(" TD", TimerViewModel.FormatEndTime(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified), timeZone));
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

        public int StopCount { get; private set; }

        public object? CurrentOwner { get; private set; }

        public void PlaySound(string soundName, object owner)
        {
            this.PlayedSounds.Add(soundName);
            this.CurrentOwner = owner;
        }

        public void StopSound(object owner)
        {
            if (ReferenceEquals(this.CurrentOwner, owner))
            {
                this.CurrentOwner = null;
                this.StopCount++;
            }
        }
    }
}