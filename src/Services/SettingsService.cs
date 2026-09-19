// Copyright © 2025 HemSoft

namespace TickDown.Services;

using System.Text.Json;
using global::TickDown.Core.Models;
using global::TickDown.Core.Services;

/// <summary>
/// Saves frozen settings snapshots in order with atomic replacement and recovery.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly object sync = new();
    private readonly Dictionary<string, IOException> writeFailures = [];
    private readonly string filePath;
    private readonly string windowSettingsPath;
    private Task pendingWrites = Task.CompletedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    public SettingsService()
        : this(GetSettingsDirectory())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="directory">The directory for this isolated settings store.</param>
    public SettingsService(string directory)
    {
        string fullPath = Path.GetFullPath(directory);
        this.filePath = Path.Combine(fullPath, "timers.json");
        this.windowSettingsPath = Path.Combine(fullPath, "window.json");
    }

    /// <inheritdoc/>
    public event EventHandler<SettingsFailureEventArgs>? PersistenceFailed;

    /// <inheritdoc/>
    public Task SaveTimersAsync(IEnumerable<CountdownTimer> timers) => this.SaveAsync(this.filePath, timers);

    /// <inheritdoc/>
    public async Task<IEnumerable<CountdownTimer>> LoadTimersAsync() =>
        await this.LoadAsync<CountdownTimer[]>(this.filePath) ?? [];

    /// <inheritdoc/>
    public Task SaveWindowSettingsAsync(WindowSettings settings) => this.SaveAsync(this.windowSettingsPath, settings);

    /// <inheritdoc/>
    public Task<WindowSettings?> LoadWindowSettingsAsync() => this.LoadAsync<WindowSettings>(this.windowSettingsPath);

    /// <inheritdoc/>
    public async Task FlushAsync()
    {
        while (true)
        {
            Task pending = this.GetPendingWrites();
            await ObservePreviousWriteAsync(pending).ConfigureAwait(false);
            lock (this.sync)
            {
                if (pending != this.pendingWrites)
                {
                    continue;
                }

                if (this.writeFailures.Count > 0)
                {
                    throw new IOException("Some settings could not be saved.", new AggregateException(this.writeFailures.Values));
                }

                return;
            }
        }
    }

    private static string GetSettingsDirectory()
    {
        string? isolated = Environment.GetEnvironmentVariable("TICKDOWN_SETTINGS_DIRECTORY");
        return string.IsNullOrWhiteSpace(isolated)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TickDown")
            : isolated;
    }

    private static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException;

    private static async Task ObservePreviousWriteAsync(Task previous)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The originating task and failure event report this error. A later
            // snapshot must still be able to retry and repair the same file.
        }
    }

    private static async Task<T> ReadAsync<T>(string path)
    {
        string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json) ?? throw new JsonException("A settings document cannot be null.");
    }

    private static void PreserveCorruptPrimaryBestEffort(string path)
    {
        try
        {
            File.Copy(path, path + ".corrupt." + Guid.NewGuid().ToString("N"));
            File.Delete(path);
        }
        catch (IOException)
        {
            // A validated backup is still usable. Keep the original if archiving
            // or removing it is blocked rather than discarding recovered data.
        }
        catch (UnauthorizedAccessException)
        {
            // A read-only primary can remain alongside its valid backup.
        }
    }

    private Task GetPendingWrites()
    {
        lock (this.sync)
        {
            return this.pendingWrites;
        }
    }

    private Task SaveAsync<T>(string path, T data)
    {
        lock (this.sync)
        {
            // Serialization and enqueueing share one ordering point. Callers can
            // mutate their models after this method returns without changing a save.
            try
            {
                string json = JsonSerializer.Serialize(data);
                this.pendingWrites = this.WriteAfterAsync(this.pendingWrites, path, json);
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                this.pendingWrites = this.FailAfterAsync(this.pendingWrites, path, exception);
            }

            return this.pendingWrites;
        }
    }

    private async Task FailAfterAsync(Task previous, string path, Exception exception)
    {
        await ObservePreviousWriteAsync(previous).ConfigureAwait(false);
        throw this.RecordWriteFailure(path, exception);
    }

    private async Task WriteAfterAsync(Task previous, string path, string json)
    {
        await ObservePreviousWriteAsync(previous).ConfigureAwait(false);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                File.Replace(temporary, path, path + ".bak");
            }
            else
            {
                File.Move(temporary, path);
            }

            lock (this.sync)
            {
                _ = this.writeFailures.Remove(path);
            }
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw this.RecordWriteFailure(path, exception);
        }
        finally
        {
            // A leftover temporary file is never considered a valid snapshot.
            try
            {
                File.Delete(temporary);
            }
            catch (IOException)
            {
                // Preserve an inaccessible temporary file for diagnosis.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve an inaccessible temporary file for diagnosis.
            }
        }
    }

    private IOException RecordWriteFailure(string path, Exception exception)
    {
        IOException failure = new($"Could not save {Path.GetFileName(path)}. Previous settings were preserved.", exception);
        lock (this.sync)
        {
            this.writeFailures[path] = failure;
        }

        this.PersistenceFailed?.Invoke(this, new SettingsFailureEventArgs(failure.Message, failure));
        return failure;
    }

    private Task<T?> LoadAsync<T>(string path)
    {
        lock (this.sync)
        {
            Task<T?> load = this.LoadAfterAsync<T>(this.pendingWrites, path);
            this.pendingWrites = load;
            return load;
        }
    }

    private async Task<T?> LoadAfterAsync<T>(Task previous, string path)
    {
        await ObservePreviousWriteAsync(previous).ConfigureAwait(false);
        try
        {
            return await ReadAsync<T>(path).ConfigureAwait(false);
        }
        catch (FileNotFoundException) when (!File.Exists(path + ".bak"))
        {
            return default;
        }
        catch (DirectoryNotFoundException) when (!File.Exists(path + ".bak"))
        {
            return default;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            try
            {
                T? recovered = await ReadAsync<T>(path + ".bak").ConfigureAwait(false);
                if (exception is JsonException)
                {
                    PreserveCorruptPrimaryBestEffort(path);
                }

                this.PersistenceFailed?.Invoke(this, new SettingsFailureEventArgs($"Loaded {Path.GetFileName(path)} from backup. Original data was preserved.", exception, true));
                return recovered;
            }
            catch (Exception recoveryFailure) when (IsStorageFailure(recoveryFailure))
            {
                IOException failure = new($"Could not load {Path.GetFileName(path)}. No valid backup was available; existing files were preserved.", new AggregateException(exception, recoveryFailure));
                this.PersistenceFailed?.Invoke(this, new SettingsFailureEventArgs(failure.Message, failure));
                throw failure;
            }
        }
    }
}