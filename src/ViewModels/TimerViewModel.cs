// Copyright © 2025 HemSoft

namespace TickDown.ViewModels;

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using TickDown.Core.Models;
using TickDown.Core.Services;

/// <summary>
/// View model for a single countdown timer.
/// </summary>
public partial class TimerViewModel : ObservableObject
{
    private static readonly Regex TimePattern = new(
        @"^(\d+(?:\.\d+)?)\s*(h|hours?|m|min|s|sec)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ITimerService timerService;
    private readonly DispatcherQueue dispatcher;
    private string timeDisplay = "00:05:00";
    private string name;
    private bool isRunning = false;
    private bool isPaused = false;
    private double progressPercentage = 0;
    private string endTimeDisplay = string.Empty;
    private bool isUpdatingTimeDisplay = false;
    private DateTimeOffset targetDate = DateTimeOffset.Now.Date;
    private TimeSpan targetTime = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(5));
    private int hours = 0;
    private int minutes = 5;
    private int seconds = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerViewModel"/> class.
    /// </summary>
    /// <param name="timerService">The timer service.</param>
    /// <param name="model">The optional timer model to wrap.</param>
    public TimerViewModel(ITimerService timerService, CountdownTimer? model = null)
    {
        this.timerService = timerService;
        this.dispatcher = DispatcherQueue.GetForCurrentThread();
        this.Model = model ?? new CountdownTimer(TimeSpan.FromMinutes(5), "New Timer");

        this.name = this.Model.Name;
        if (this.Model.Duration.TotalSeconds > 0)
        {
            this.hours = this.Model.Duration.Hours;
            this.minutes = this.Model.Duration.Minutes;
            this.seconds = this.Model.Duration.Seconds;
        }

        this.UpdateState();
        this.UpdateTimeDisplay();

        this.timerService.Tick += this.OnGlobalTick;
    }

    /// <summary>
    /// Event raised when the timer requests to be removed.
    /// </summary>
    public event EventHandler? RequestRemove;

    /// <summary>
    /// Gets the minimum year for the date picker.
    /// </summary>
    public static DateTimeOffset MinYear => DateTimeOffset.Now;

    /// <summary>
    /// Gets the underlying countdown timer model.
    /// </summary>
    public CountdownTimer Model { get; }

    /// <summary>
    /// Gets or sets the formatted time display string.
    /// </summary>
    public string TimeDisplay
    {
        get => this.timeDisplay;
        set
        {
            if (this.SetProperty(ref this.timeDisplay, value))
            {
                this.OnTimeDisplayChanged(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the name of the timer.
    /// </summary>
    public string Name
    {
        get => this.name;
        set
        {
            if (this.SetProperty(ref this.name, value))
            {
                this.OnNameChanged(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the timer is currently running.
    /// </summary>
    public bool IsRunning
    {
        get => this.isRunning;
        set => this.SetProperty(ref this.isRunning, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the timer is currently paused.
    /// </summary>
    public bool IsPaused
    {
        get => this.isPaused;
        set => this.SetProperty(ref this.isPaused, value);
    }

    /// <summary>
    /// Gets or sets the progress of the timer as a percentage (0-1).
    /// </summary>
    public double ProgressPercentage
    {
        get => this.progressPercentage;
        set => this.SetProperty(ref this.progressPercentage, value);
    }

    /// <summary>
    /// Gets or sets the formatted end time display string.
    /// </summary>
    public string EndTimeDisplay
    {
        get => this.endTimeDisplay;
        set => this.SetProperty(ref this.endTimeDisplay, value);
    }

    /// <summary>
    /// Gets or sets the target end date for the timer.
    /// </summary>
    public DateTimeOffset TargetDate
    {
        get => this.targetDate;
        set => this.SetProperty(ref this.targetDate, value);
    }

    /// <summary>
    /// Gets or sets the target end time for the timer.
    /// </summary>
    public TimeSpan TargetTime
    {
        get => this.targetTime;
        set => this.SetProperty(ref this.targetTime, value);
    }

    /// <summary>
    /// Gets or sets the total hours of the timer duration.
    /// </summary>
    public int Hours
    {
        get => this.hours;
        set
        {
            if (this.SetProperty(ref this.hours, value))
            {
                this.UpdateDuration();
            }
        }
    }

    /// <summary>
    /// Gets or sets the minutes component of the timer duration.
    /// </summary>
    public int Minutes
    {
        get => this.minutes;
        set
        {
            if (this.SetProperty(ref this.minutes, value))
            {
                this.UpdateDuration();
            }
        }
    }

    /// <summary>
    /// Gets or sets the seconds component of the timer duration.
    /// </summary>
    public int Seconds
    {
        get => this.seconds;
        set
        {
            if (this.SetProperty(ref this.seconds, value))
            {
                this.UpdateDuration();
            }
        }
    }

    private static bool TryParseTime(string value, out TimeSpan result)
    {
        value = value.Trim();

        Match match = TimePattern.Match(value);
        if (match.Success && double.TryParse(match.Groups[1].Value, out double num))
        {
            string unit = match.Groups[2].Value.ToLowerInvariant();
            result = unit switch
            {
                "h" or "hour" or "hours" => TimeSpan.FromHours(num),
                "s" or "sec" => TimeSpan.FromSeconds(num),
                _ => TimeSpan.FromMinutes(num),
            };
            return true;
        }

        return TimeSpan.TryParse(value, CultureInfo.CurrentCulture, out result);
    }

    private static string FormatEndTime(DateTime endTime)
    {
        string daySuffix = GetDaySuffix(endTime.Day);
        string timeZone = TimeZoneInfo.Local.IsDaylightSavingTime(endTime)
            ? TimeZoneInfo.Local.DaylightName
            : TimeZoneInfo.Local.StandardName;
        string timeZoneAbbr = GetTimeZoneAbbreviation(timeZone);

        return $"{endTime:dddd, MMMM} {endTime.Day}{daySuffix} {endTime:yyyy, h:mm tt} {timeZoneAbbr}";
    }

    private static string GetDaySuffix(int day) =>
        day switch
        {
            1 or 21 or 31 => "st",
            2 or 22 => "nd",
            3 or 23 => "rd",
            _ => "th",
        };

    private static string GetTimeZoneAbbreviation(string timeZoneName)
    {
        string[] words = timeZoneName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            >= 2 => string.Concat(words.Select(w => w[0])),
            _ when timeZoneName.Length > 3 => timeZoneName[..3].ToUpperInvariant(),
            _ => timeZoneName.ToUpperInvariant(),
        };
    }

    private void OnNameChanged(string value) => this.Model.Name = value;

    private void OnTimeDisplayChanged(string value)
    {
        if (this.isUpdatingTimeDisplay)
        {
            return;
        }

        if (TryParseTime(value, out TimeSpan result))
        {
            this.Hours = (int)result.TotalHours;
            this.Minutes = result.Minutes;
            this.Seconds = result.Seconds;
        }
        else
        {
            this.UpdateTimeDisplay();
        }
    }

    private void UpdateDuration()
    {
        if (!this.Model.State.Equals(TimerState.Stopped))
        {
            return;
        }

        TimeSpan duration = new(this.Hours, this.Minutes, this.Seconds);
        this.Model.SetDuration(duration);
        this.UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Start()
    {
        if (this.Model.Remaining == this.Model.Duration || this.Model.Remaining <= TimeSpan.Zero)
        {
            TimeSpan duration = new(this.Hours, this.Minutes, this.Seconds);
            if (duration.TotalSeconds <= 0)
            {
                this.Hours = 0;
                this.Minutes = 5;
                this.Seconds = 0;
                duration = new TimeSpan(0, 5, 0);
            }

            this.Model.SetDuration(duration);
        }

        this.Model.Start();
        this.UpdateState();
    }

    [RelayCommand]
    private void Pause()
    {
        this.Model.Pause();
        this.UpdateState();
    }

    [RelayCommand]
    private void Stop()
    {
        this.Model.Stop();
        this.UpdateState();
        this.UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Reset()
    {
        this.Model.Reset();
        this.UpdateState();
        this.UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Remove()
    {
        this.timerService.Tick -= this.OnGlobalTick;
        this.RequestRemove?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SetQuickTime(string timeStr)
    {
        if (this.Model.State != TimerState.Stopped)
        {
            return;
        }

        if (TryParseTime(timeStr, out TimeSpan result))
        {
            this.SetTime((int)result.TotalHours, result.Minutes, result.Seconds);
        }
    }

    [RelayCommand]
    private void SetEndTime()
    {
        if (this.Model.State != TimerState.Stopped)
        {
            return;
        }

        DateTime targetDateTime = this.TargetDate.Date.Add(this.TargetTime);
        TimeSpan duration = targetDateTime - DateTime.Now;

        if (duration.TotalSeconds <= 0)
        {
            return;
        }

        this.SetTime((int)duration.TotalHours, duration.Minutes, duration.Seconds);
    }

    private void SetTime(int h, int m, int s)
    {
        // Set backing fields directly to avoid triggering UpdateDuration multiple times
        this.hours = h;
        this.minutes = m;
        this.seconds = s;

        // Notify property changes
        this.OnPropertyChanged(nameof(this.Hours));
        this.OnPropertyChanged(nameof(this.Minutes));
        this.OnPropertyChanged(nameof(this.Seconds));

        // Update duration and display
        this.UpdateDuration();
    }

    private void OnGlobalTick(object? sender, EventArgs e)
    {
        if (this.Model.State == TimerState.Running)
        {
            this.Model.Tick();
            _ = this.dispatcher.TryEnqueue(() =>
            {
                this.UpdateTimeDisplay();
                this.ProgressPercentage = this.Model.ProgressPercentage;

                if (this.Model.State == TimerState.Completed)
                {
                    this.UpdateState();
                }
            });
        }
    }

    private void UpdateTimeDisplay()
    {
        this.isUpdatingTimeDisplay = true;

        TimeSpan timeToShow = this.Model.State == TimerState.Stopped && this.Model.Remaining == this.Model.Duration
            ? this.Model.Duration
            : this.Model.Remaining;

        this.TimeDisplay = timeToShow.TotalHours >= 24
            ? $"{(int)timeToShow.TotalHours}:{timeToShow.Minutes:D2}:{timeToShow.Seconds:D2}"
            : timeToShow.ToString(@"hh\:mm\:ss");
        this.isUpdatingTimeDisplay = false;

        this.UpdateEndTimeDisplay();
    }

    private void UpdateEndTimeDisplay()
    {
        if (this.Model.State is TimerState.Running or TimerState.Paused)
        {
            DateTime endTime = DateTime.Now.Add(this.Model.Remaining);
            this.EndTimeDisplay = FormatEndTime(endTime);
        }
        else
        {
            this.EndTimeDisplay = string.Empty;
        }
    }

    private void UpdateState()
    {
        this.IsRunning = this.Model.State == TimerState.Running;
        this.IsPaused = this.Model.State == TimerState.Paused;
    }
}