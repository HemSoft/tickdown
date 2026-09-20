// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using System.Text.Json;
using TickDown.Core.Models;
using TickDown.Core.Services;
using TickDown.ViewModels;

/// <summary>
/// Verifies snapshots from the actual view-model commands at the settings boundary.
/// </summary>
public class TimerPersistenceTests
{
    /// <summary>
    /// Verifies that one rename persists the new name immediately.
    /// </summary>
    [Fact]
    public void RenameSavesTheNewName()
    {
        using Fixture fixture = new();
        fixture.Timer.Name = "Renamed";
        Assert.NotEmpty(fixture.Settings.Snapshots);
        Assert.All(fixture.Settings.Snapshots, snapshot => Assert.Equal("Renamed", snapshot.Name));
    }

    /// <summary>
    /// Verifies every Quick Set snapshot contains the final complete duration.
    /// </summary>
    [Fact]
    public void QuickSetSavesOnlyTheFinalDuration()
    {
        using Fixture fixture = new();
        fixture.Timer.SetQuickTimeCommand.Execute(75);
        Assert.NotEmpty(fixture.Settings.Snapshots);
        Assert.All(fixture.Settings.Snapshots, snapshot => Assert.Equal(TimeSpan.FromMinutes(75), snapshot.Duration));
    }

    /// <summary>
    /// Verifies parsed hours, minutes and seconds are committed together.
    /// </summary>
    [Fact]
    public void TextEditSavesOnlyTheFinalDuration()
    {
        using Fixture fixture = new();
        fixture.Timer.TimeDisplay = "01:02:03";
        Assert.NotEmpty(fixture.Settings.Snapshots);
        Assert.All(fixture.Settings.Snapshots, snapshot => Assert.Equal(new TimeSpan(1, 2, 3), snapshot.Duration));
    }

    /// <summary>
    /// Verifies component edits commit before notifying persistence observers.
    /// </summary>
    [Fact]
    public void ComponentEditSavesUpdatedDuration()
    {
        using Fixture fixture = new();
        fixture.Timer.Minutes = 12;
        Assert.NotEmpty(fixture.Settings.Snapshots);
        Assert.All(fixture.Settings.Snapshots, snapshot => Assert.Equal(TimeSpan.FromMinutes(12), snapshot.Duration));
    }

    /// <summary>
    /// Verifies target-date commands do not save intermediate durations.
    /// </summary>
    [Fact]
    public void TargetEndTimeSavesOnlyTheFinalDuration()
    {
        using Fixture fixture = new();
        DateTime target = DateTime.Now.AddHours(2).AddMinutes(17).AddSeconds(31);
        fixture.Timer.TargetDate = new DateTimeOffset(target.Date);
        fixture.Timer.TargetTime = target.TimeOfDay;
        fixture.Timer.SetEndTimeCommand.Execute(null);
        Assert.NotEmpty(fixture.Settings.Snapshots);
        Assert.All(fixture.Settings.Snapshots, snapshot => Assert.Equal(fixture.Timer.Model.Duration, snapshot.Duration));
        Assert.True(fixture.Timer.Model.Duration > TimeSpan.FromHours(2));
    }

    /// <summary>
    /// Verifies a rejected save becomes a visible error without an unobserved task.
    /// </summary>
    [Fact]
    public void SaveFailureIsVisible()
    {
        using Fixture fixture = new();
        fixture.Settings.FailNextSave = true;
        fixture.Timer.Name = "Unsaved";
        Assert.True(fixture.Main.HasPersistenceError);
        Assert.Equal("Test save failure", fixture.Main.PersistenceError);
    }

    /// <summary>
    /// Verifies delayed theme initialization updates the already-created selector model.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task DelayedThemeInitializationUpdatesSelector()
    {
        using Fixture fixture = new();
        Assert.Equal("System", fixture.Main.CurrentTheme);
        fixture.Theme.InitializedTheme = "Dark";
        await fixture.Theme.InitializeAsync();
        Assert.Equal("Dark", fixture.Main.CurrentTheme);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TestTimerService ticks = new();

        public Fixture()
        {
            this.Main = new(this.ticks, this.Settings, this.Theme, new TestAudioService());
            this.Timer = Assert.Single(this.Main.Timers);
            this.ticks.RaiseTick();
            Assert.Empty(this.Settings.Snapshots);
        }

        public CapturingSettingsService Settings { get; } = new();

        public TimerViewModel Timer { get; }

        public MainViewModel Main { get; }

        public TestThemeService Theme { get; } = new();

        public void Dispose()
        {
            this.Timer.RemoveCommand.Execute(null);
            this.Timer.Dispose();
            this.ticks.Dispose();
        }
    }

    private sealed class CapturingSettingsService : ISettingsService
    {
        public event EventHandler<SettingsFailureEventArgs>? PersistenceFailed;

        public bool FailNextSave { get; set; }

        public List<CountdownTimer> Snapshots { get; } = [];

        public WindowSettings? Window { get; private set; }

        public Task FlushAsync() => Task.CompletedTask;

        public Task SaveTimersAsync(IEnumerable<CountdownTimer> timers)
        {
            if (this.FailNextSave)
            {
                this.FailNextSave = false;
                IOException failure = new("Test save failure");
                this.PersistenceFailed?.Invoke(this, new SettingsFailureEventArgs(failure.Message, failure));
                return Task.FromException(failure);
            }

            string json = JsonSerializer.Serialize(timers);
            this.Snapshots.AddRange(JsonSerializer.Deserialize<CountdownTimer[]>(json)!);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<CountdownTimer>> LoadTimersAsync() =>
            Task.FromResult<IEnumerable<CountdownTimer>>([new CountdownTimer(TimeSpan.FromMinutes(5), "Original")]);

        public Task SaveWindowSettingsAsync(WindowSettings settings)
        {
            this.Window = settings;
            return Task.CompletedTask;
        }

        public Task<WindowSettings?> LoadWindowSettingsAsync() => Task.FromResult(this.Window);
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

        public void StopSound()
        {
        }
    }

    private sealed class TestThemeService : IThemeService
    {
        public event EventHandler? ThemeChanged;

        public string CurrentTheme { get; private set; } = "System";

        public string InitializedTheme { get; set; } = "System";

        public void SetTheme(string theme)
        {
            if (this.CurrentTheme == theme)
            {
                return;
            }

            this.CurrentTheme = theme;
            this.ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task InitializeAsync()
        {
            this.CurrentTheme = this.InitializedTheme;
            this.ThemeChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}