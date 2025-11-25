namespace TickDown.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using global::TickDown.Core.Models;
using global::TickDown.Core.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Globalization;

/// <summary>
/// View model for a single countdown timer.
/// </summary>
public partial class TimerViewModel : ObservableObject
{
    private readonly ITimerService _timerService;
    private readonly DispatcherQueue _dispatcher;

    /// <summary>
    /// Gets the underlying countdown timer model.
    /// </summary>
    public CountdownTimer Model { get; }

    private string _timeDisplay = "00:05:00";
    /// <summary>
    /// Gets or sets the formatted time display string.
    /// </summary>
    public string TimeDisplay
    {
        get => _timeDisplay;
        set
        {
            if (SetProperty(ref _timeDisplay, value))
            {
                OnTimeDisplayChanged(value);
            }
        }
    }

    private string _name;
    /// <summary>
    /// Gets or sets the name of the timer.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnNameChanged(value);
            }
        }
    }

    private bool _isRunning = false;
    /// <summary>
    /// Gets or sets a value indicating whether the timer is currently running.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    private bool _isPaused = false;
    /// <summary>
    /// Gets or sets a value indicating whether the timer is currently paused.
    /// </summary>
    public bool IsPaused
    {
        get => _isPaused;
        set => SetProperty(ref _isPaused, value);
    }

    private double _progressPercentage = 0;
    /// <summary>
    /// Gets or sets the progress of the timer as a percentage (0-1).
    /// </summary>
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    private bool _isUpdatingTimeDisplay = false;

    /// <summary>
    /// Event raised when the timer requests to be removed.
    /// </summary>
    public event EventHandler? RequestRemove;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerViewModel"/> class.
    /// </summary>
    /// <param name="timerService">The timer service.</param>
    /// <param name="model">The optional timer model to wrap.</param>
    public TimerViewModel(ITimerService timerService, CountdownTimer? model = null)
    {
        _timerService = timerService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Model = model ?? new CountdownTimer(TimeSpan.FromMinutes(5), "New Timer");

        // Initialize properties from model
        _name = Model.Name;
        if (Model.Duration.TotalSeconds > 0)
        {
            _hours = Model.Duration.Hours;
            _minutes = Model.Duration.Minutes;
            _seconds = Model.Duration.Seconds;
        }

        UpdateState();
        UpdateTimeDisplay();

        _timerService.Tick += OnGlobalTick;
    }

    private void OnNameChanged(string value) => Model.Name = value;

    private void OnTimeDisplayChanged(string value)
    {
        if (_isUpdatingTimeDisplay)
        {
            return;
        }

        if (TryParseTime(value, out TimeSpan result))
        {
            Hours = (int)result.TotalHours;
            Minutes = result.Minutes;
            Seconds = result.Seconds;
        }
        else
        {
            UpdateTimeDisplay();
        }
    }

    private static bool TryParseTime(string value, out TimeSpan result)
    {
        // Handle simple integers as minutes
        if (int.TryParse(value, out int minutes))
        {
            result = TimeSpan.FromMinutes(minutes);
            return true;
        }

        // Handle "10m", "1h", "30s"
        value = value.Trim().ToLowerInvariant();
        if (value.EndsWith('m') || value.EndsWith("min"))
        {
            string num = value.Replace("min", "").Replace("m", "").Trim();
            if (double.TryParse(num, out double m))
            {
                result = TimeSpan.FromMinutes(m);
                return true;
            }
        }
        if (value.EndsWith('h') || value.EndsWith("hour") || value.EndsWith("hours"))
        {
            string num = value.Replace("hours", "").Replace("hour", "").Replace("h", "").Trim();
            if (double.TryParse(num, out double h))
            {
                result = TimeSpan.FromHours(h);
                return true;
            }
        }
        if (value.EndsWith('s') || value.EndsWith("sec"))
        {
            string num = value.Replace("sec", "").Replace("s", "").Trim();
            if (double.TryParse(num, out double s))
            {
                result = TimeSpan.FromSeconds(s);
                return true;
            }
        }

        return TimeSpan.TryParse(value, CultureInfo.CurrentCulture, out result);
    }

    private int _hours = 0;
    /// <summary>
    /// Gets or sets the total hours of the timer duration.
    /// </summary>
    public int Hours
    {
        get => _hours;
        set
        {
            if (SetProperty(ref _hours, value))
            {
                OnHoursChanged();
            }
        }
    }

    private int _minutes = 5;
    /// <summary>
    /// Gets or sets the minutes component of the timer duration.
    /// </summary>
    public int Minutes
    {
        get => _minutes;
        set
        {
            if (SetProperty(ref _minutes, value))
            {
                OnMinutesChanged();
            }
        }
    }

    private int _seconds = 0;
    /// <summary>
    /// Gets or sets the seconds component of the timer duration.
    /// </summary>
    public int Seconds
    {
        get => _seconds;
        set
        {
            if (SetProperty(ref _seconds, value))
            {
                OnSecondsChanged();
            }
        }
    }

    private void OnHoursChanged() => UpdateDuration();
    private void OnMinutesChanged() => UpdateDuration();
    private void OnSecondsChanged() => UpdateDuration();

    private void UpdateDuration()
    {
        if (!Model.State.Equals(TimerState.Stopped))
        {
            return;
        }

        TimeSpan duration = new(Hours, Minutes, Seconds);
        Model.SetDuration(duration);
        UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Start()
    {
        if (Model.Duration.TotalSeconds <= 0)
        {
            // Default if zero
            Hours = 0;
            Minutes = 5;
            Seconds = 0;
        }

        Model.Start();
        UpdateState();
    }

    [RelayCommand]
    private void Pause()
    {
        Model.Pause();
        UpdateState();
    }

    [RelayCommand]
    private void Stop()
    {
        Model.Stop();
        UpdateState();
        UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Reset()
    {
        Model.Reset();
        UpdateState();
        UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Remove()
    {
        _timerService.Tick -= OnGlobalTick;
        RequestRemove?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SetQuickTime(string timeStr)
    {
        if (Model.State != TimerState.Stopped)
        {
            return;
        }

        switch (timeStr)
        {
            case "1min": SetTime(0, 1, 0); break;
            case "5min": SetTime(0, 5, 0); break;
            case "10min": SetTime(0, 10, 0); break;
            case "15min": SetTime(0, 15, 0); break;
            case "30min": SetTime(0, 30, 0); break;
            case "1hour": SetTime(1, 0, 0); break;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accesses generated instance properties")]
    private void SetTime(int h, int m, int s)
    {
        Hours = h;
        Minutes = m;
        Seconds = s;
        // UpdateDuration called by property changes
    }

    private void OnGlobalTick(object? sender, EventArgs e)
    {
        if (Model.State == TimerState.Running)
        {
            Model.Tick();
            _ = _dispatcher.TryEnqueue(() =>
            {
                UpdateTimeDisplay();
                ProgressPercentage = Model.ProgressPercentage;

                if (Model.State == TimerState.Completed)
                {
                    UpdateState();
                    // Play sound?
                }
            });
        }
    }

    private void UpdateTimeDisplay()
    {
        _isUpdatingTimeDisplay = true;
        TimeSpan timeToShow = Model.State == TimerState.Stopped
            ? Model.Duration
            : Model.Remaining;

        TimeDisplay = timeToShow.TotalHours >= 24
            ? $"{(int)timeToShow.TotalHours}:{timeToShow.Minutes:D2}:{timeToShow.Seconds:D2}"
            : timeToShow.ToString(@"hh\:mm\:ss");
        _isUpdatingTimeDisplay = false;
    }

    private void UpdateState()
    {
        IsRunning = Model.State == TimerState.Running;
        IsPaused = Model.State == TimerState.Paused;
    }
}