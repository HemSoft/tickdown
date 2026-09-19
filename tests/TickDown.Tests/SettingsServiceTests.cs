// Copyright © 2025 HemSoft

namespace TickDown.Tests;

using TickDown.Core.Models;
using TickDown.Core.Services;
using TickDown.Services;

/// <summary>
/// Exercises production persistence against isolated temporary directories.
/// </summary>
public class SettingsServiceTests
{
    /// <summary>
    /// Verifies 32 overlapping frozen snapshots finish in their accepted order.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task BurstSavesPreserveTheLastAcceptedSnapshot()
    {
        using Fixture fixture = new();
        CountdownTimer[] timers = [.. Enumerable.Range(0, 1000)
            .Select(index => new CountdownTimer(TimeSpan.FromMinutes(5), index.ToString(System.Globalization.CultureInfo.InvariantCulture)))];
        List<Task> saves = [];
        for (int edit = 0; edit < 32; edit++)
        {
            foreach (CountdownTimer timer in timers)
            {
                timer.Name = $"edit-{edit}";
            }

            saves.Add(fixture.Store.SaveTimersAsync(timers));
        }

        foreach (CountdownTimer timer in timers)
        {
            timer.Name = "not accepted";
        }

        await fixture.Store.FlushAsync();
        await Task.WhenAll(saves);
        Assert.All(saves, task => Assert.True(task.IsCompletedSuccessfully));
        CountdownTimer[] loaded = [.. await fixture.Store.LoadTimersAsync()];
        Assert.Equal(1000, loaded.Length);
        Assert.All(loaded, timer => Assert.Equal("edit-31", timer.Name));
        Assert.Empty(fixture.Errors);
        Assert.Empty(Directory.GetFiles(fixture.Directory, "*.tmp"));
    }

    /// <summary>
    /// Verifies missing and deliberately empty settings are successful loads.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task MissingAndEmptyFilesAreNotLoadFailures()
    {
        using Fixture fixture = new();
        Assert.Empty(await fixture.Store.LoadTimersAsync());
        Assert.Null(await fixture.Store.LoadWindowSettingsAsync());
        await fixture.Store.SaveTimersAsync([]);
        Assert.Empty(await fixture.Store.LoadTimersAsync());
        Assert.Empty(fixture.Errors);
    }

    /// <summary>
    /// Verifies malformed data is retained and never treated as a successful empty load.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task MalformedFileWithoutBackupIsPreservedAndReported()
    {
        using Fixture fixture = new();
        await File.WriteAllTextAsync(fixture.TimersPath, "{broken");
        _ = await Assert.ThrowsAsync<IOException>(fixture.Store.LoadTimersAsync);
        Assert.False(Assert.Single(fixture.Errors).IsRecovered);
        Assert.Equal("{broken", await File.ReadAllTextAsync(fixture.TimersPath));
        _ = await Assert.ThrowsAsync<IOException>(fixture.Store.LoadTimersAsync);
    }

    /// <summary>
    /// Verifies backup recovery survives repeated reads and a fresh service instance.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task ValidBackupRemainsReadableAfterCorruption()
    {
        using Fixture fixture = new();
        await fixture.SaveNamedAsync("previous");
        await fixture.SaveNamedAsync("current");
        await File.WriteAllTextAsync(fixture.TimersPath, "null");
        Assert.Equal("previous", Assert.Single(await fixture.Store.LoadTimersAsync()).Name);
        Assert.Equal("previous", Assert.Single(await fixture.Store.LoadTimersAsync()).Name);
        SettingsService reopened = new(fixture.Directory);
        Assert.Equal("previous", Assert.Single(await reopened.LoadTimersAsync()).Name);
        Assert.All(fixture.Errors, error => Assert.True(error.IsRecovered));
        _ = Assert.Single(Directory.GetFiles(fixture.Directory, "timers.json.corrupt.*"));
    }

    /// <summary>
    /// Verifies a failed replacement preserves valid data and prevents a clean flush.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task LockedDestinationPreservesDataAndRetryRepairsFailure()
    {
        using Fixture fixture = new();
        await fixture.SaveNamedAsync("previous");
        await fixture.SaveNamedAsync("current");
        string backup = await File.ReadAllTextAsync(fixture.TimersPath + ".bak");
        using (FileStream locked = new(fixture.TimersPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            _ = await Assert.ThrowsAsync<IOException>(() => fixture.SaveNamedAsync("failed"));
            _ = await Assert.ThrowsAsync<IOException>(fixture.Store.FlushAsync);
            Assert.True(locked.Length > 0);
        }

        Assert.Equal("current", Assert.Single(await fixture.Store.LoadTimersAsync()).Name);
        Assert.Equal(backup, await File.ReadAllTextAsync(fixture.TimersPath + ".bak"));
        Assert.Empty(Directory.GetFiles(fixture.Directory, "*.tmp"));
        await fixture.SaveNamedAsync("retry");
        await fixture.Store.FlushAsync();
        Assert.Equal("retry", Assert.Single(await fixture.Store.LoadTimersAsync()).Name);
        _ = Assert.Single(fixture.Errors);
    }

    /// <summary>
    /// Verifies an interrupted temporary write cannot replace committed data.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task IncompleteTemporaryFileIsNeverLoaded()
    {
        using Fixture fixture = new();
        await fixture.SaveNamedAsync("committed");
        await File.WriteAllTextAsync(fixture.TimersPath + ".interrupted.tmp", "{incomplete");
        Assert.Equal("committed", Assert.Single(await fixture.Store.LoadTimersAsync()).Name);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>
    /// Verifies recovery and a later accepted edit cannot race to move the new file.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task RecoveryAndSaveShareOneOrderingPoint()
    {
        using Fixture fixture = new();
        await fixture.SaveNamedAsync("backup");
        await fixture.SaveNamedAsync("old");
        await File.WriteAllTextAsync(fixture.TimersPath, "{invalid");
        Task<IEnumerable<CountdownTimer>> recovery = fixture.Store.LoadTimersAsync();
        Task latest = fixture.SaveNamedAsync("latest");
        Assert.Equal("backup", Assert.Single(await recovery).Name);
        await latest;
        await fixture.Store.FlushAsync();
        Assert.Equal("latest", Assert.Single(await fixture.Store.LoadTimersAsync()).Name);
    }

    /// <summary>
    /// Verifies a read-only corrupt primary cannot hide its valid backup.
    /// </summary>
    /// <returns>The test completion task.</returns>
    [Fact]
    public async Task ReadOnlyCorruptPrimaryStillLoadsValidBackup()
    {
        using Fixture fixture = new();
        await fixture.SaveNamedAsync("backup");
        await fixture.SaveNamedAsync("current");
        await File.WriteAllTextAsync(fixture.TimersPath, "{corrupt");
        File.SetAttributes(fixture.TimersPath, FileAttributes.ReadOnly);
        Assert.Equal("backup", Assert.Single(await fixture.Store.LoadTimersAsync()).Name);
        Assert.Equal("backup", Assert.Single(await fixture.Store.LoadTimersAsync()).Name);
        SettingsService reopened = new(fixture.Directory);
        Assert.Equal("backup", Assert.Single(await reopened.LoadTimersAsync()).Name);
        Assert.Equal(2, fixture.Errors.Count);
        Assert.All(fixture.Errors, error => Assert.True(error.IsRecovered));
        Assert.Equal("{corrupt", await File.ReadAllTextAsync(fixture.TimersPath));
        _ = Assert.Single(Directory.GetFiles(fixture.Directory, "timers.json.corrupt.*"));
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            this.Directory = Path.Combine(Path.GetTempPath(), "tickdown-tests-" + Guid.NewGuid().ToString("N"));
            _ = System.IO.Directory.CreateDirectory(this.Directory);
            this.Store = new SettingsService(this.Directory);
            this.Store.PersistenceFailed += (_, failure) => this.Errors.Add(failure);
        }

        public string Directory { get; }

        public string TimersPath => Path.Combine(this.Directory, "timers.json");

        public SettingsService Store { get; }

        public List<SettingsFailureEventArgs> Errors { get; } = [];

        public Task SaveNamedAsync(string name) => this.Store.SaveTimersAsync([new CountdownTimer(TimeSpan.FromMinutes(5), name)]);

        public void Dispose()
        {
            foreach (string path in System.IO.Directory.GetFiles(this.Directory))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            System.IO.Directory.Delete(this.Directory, recursive: true);
        }
    }
}