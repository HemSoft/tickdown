// Copyright © 2025 HemSoft

namespace TickDown.Diagnostics;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using TickDown.ViewModels;

/// <summary>
/// Answers file-based diagnostic snapshot requests from the isolated desktop harness.
/// </summary>
internal sealed class QualificationSnapshotWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string directory;
    private readonly MainViewModel viewModel;
    private readonly Func<double> getZoomFactor;
    private readonly DispatcherQueueTimer timer;
    private long lastRequestId = -1;
    private bool isWriting;

    private QualificationSnapshotWriter(string directory, MainViewModel viewModel, Func<double> getZoomFactor)
    {
        this.directory = directory;
        this.viewModel = viewModel;
        this.getZoomFactor = getZoomFactor;
        this.timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        this.timer.Interval = TimeSpan.FromMilliseconds(200);
        this.timer.Tick += this.OnTimerTick;
        this.timer.Start();
    }

    /// <summary>
    /// Stops polling for qualification requests.
    /// </summary>
    public void Dispose()
    {
        this.timer.Stop();
        this.timer.Tick -= this.OnTimerTick;
    }

    /// <summary>
    /// Starts the writer when the process was launched with an isolated qualification directory.
    /// </summary>
    /// <param name="viewModel">The main view model whose timer state is captured.</param>
    /// <param name="getZoomFactor">A callback that reads the current root zoom factor.</param>
    /// <returns>A running writer, or <see langword="null"/> when qualification is disabled.</returns>
    internal static QualificationSnapshotWriter? TryStart(MainViewModel viewModel, Func<double> getZoomFactor)
    {
        string? directory = Environment.GetEnvironmentVariable("TICKDOWN_QUALIFICATION_DIRECTORY");
        QualificationDiagnostics.RefreshConfiguration();
        if (!QualificationDiagnostics.Enabled || string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        _ = Directory.CreateDirectory(directory);
        return new QualificationSnapshotWriter(Path.GetFullPath(directory), viewModel, getZoomFactor);
    }

    private async void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _ = args;
        QualificationRequest? request = await this.ReadRequestAsync().ConfigureAwait(true);
        if (!this.CanWrite(request))
        {
            return;
        }

        this.isWriting = true;
        try
        {
            await this.WriteSnapshotAsync(request!).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // The harness observes a missing response and reports the failed probe.
        }
        catch (UnauthorizedAccessException)
        {
            // Qualification diagnostics must not crash the application.
        }
    }

    private QualificationSnapshot CaptureSnapshot(QualificationRequest request)
    {
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        QualificationRuntimeMetrics runtime = QualificationDiagnostics.CaptureRuntimeMetrics(request.ResetLatencyWindow);
        return new QualificationSnapshot
        {
            RequestId = request.RequestId,
            Candidate = Environment.GetEnvironmentVariable("TICKDOWN_QUALIFICATION_CANDIDATE") ?? string.Empty,
            CapturedUtc = DateTimeOffset.UtcNow,
            SettingsDirectory = Environment.GetEnvironmentVariable("TICKDOWN_SETTINGS_DIRECTORY") ?? string.Empty,
            ProcessorCount = Environment.ProcessorCount,
            TotalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            ManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            PrivateMemoryBytes = process.PrivateMemorySize64,
            WorkingSetBytes = process.WorkingSet64,
            HandleCount = process.HandleCount,
            ThreadCount = process.Threads.Count,
            TimerCount = this.viewModel.Timers.Count,
            RunningTimerCount = this.viewModel.Timers.Count(timer => timer.IsRunning),
            CompletedTimerCount = this.viewModel.Timers.Count(timer => timer.IsCompleted),
            CurrentTheme = this.viewModel.CurrentTheme,
            ZoomFactor = this.getZoomFactor(),
            Runtime = runtime,
        };
    }

    private async Task<QualificationRequest?> ReadRequestAsync()
    {
        string requestPath = Path.Combine(this.directory, "request.json");
        try
        {
            return !File.Exists(requestPath)
                ? null
                : JsonSerializer.Deserialize<QualificationRequest>(await File.ReadAllTextAsync(requestPath).ConfigureAwait(true));
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private bool CanWrite(QualificationRequest? request) => !this.isWriting && this.IsNewRequest(request);

    private bool IsNewRequest(QualificationRequest? request) => request is not null && request.RequestId > this.lastRequestId;

    private async Task WriteSnapshotAsync(QualificationRequest request)
    {
        try
        {
            QualificationSnapshot snapshot = this.CaptureSnapshot(request);
            string responsePath = Path.Combine(this.directory, $"snapshot-{request.RequestId}.json");
            string temporaryPath = responsePath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(snapshot, JsonOptions)).ConfigureAwait(true);
            File.Move(temporaryPath, responsePath, true);
            this.lastRequestId = request.RequestId;
        }
        finally
        {
            this.isWriting = false;
        }
    }
}