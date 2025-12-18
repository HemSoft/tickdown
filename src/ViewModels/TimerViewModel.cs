// Copyright © 2025 HemSoft

namespace TickDown.ViewModels;

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using TickDown.Core.Models;
using TickDown.Core.Services;
using Windows.UI;

/// <summary>
/// View model for a single countdown timer.
/// </summary>
public sealed partial class TimerViewModel : ObservableObject, IDisposable
{
    private static readonly Regex TimePattern = new(
        @"^(\d+(?:\.\d+)?)\s*(h|hours?|m|min|s|sec)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly IReadOnlyList<string> PredefinedColorValues =
    [
        "#4CAF50", // Green
        "#F44336", // Red
        "#2196F3", // Blue
        "#FFEB3B", // Yellow
        "#FF9800", // Orange
        "#9C27B0", // Purple
    ];

    private readonly ITimerService timerService;
    private readonly IAudioService audioService;
    private readonly DispatcherQueue dispatcher;
    private readonly Timer? alarmRepeatTimer;

    private string timeDisplay = "00:05:00";
    private string name;
    private bool isRunning;
    private bool isPaused;
    private bool isCompleted;
    private double progressPercentage;
    private string endTimeDisplay = string.Empty;
    private bool isUpdatingTimeDisplay;
    private DateTimeOffset targetDate = DateTimeOffset.Now.Date;
    private TimeSpan targetTime = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(5));
    private int hours;
    private int minutes = 5;
    private int seconds;
    private DateTime? alarmExpirationTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimerViewModel"/> class.
    /// </summary>
    /// <param name="timerService">The timer service.</param>
    /// <param name="audioService">The audio service.</param>
    /// <param name="model">The optional timer model to wrap.</param>
    public TimerViewModel(ITimerService timerService, IAudioService audioService, CountdownTimer? model = null)
    {
        this.timerService = timerService;
        this.audioService = audioService;
        this.dispatcher = DispatcherQueue.GetForCurrentThread();
        this.Model = model ?? new CountdownTimer(TimeSpan.FromMinutes(5), "New Timer");

        this.name = this.Model.Name;
        if (this.Model.Duration.TotalSeconds > 0)
        {
            this.hours = this.Model.Duration.Hours;
            this.minutes = this.Model.Duration.Minutes;
            this.seconds = this.Model.Duration.Seconds;
        }

        this.alarmRepeatTimer = new Timer();
        this.alarmRepeatTimer.Elapsed += this.OnAlarmRepeatTimerElapsed;

        this.UpdateState();
        this.UpdateTimeDisplay();
        this.UpdateCompletionBackground();
        this.UpdateProgressBarColor();

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
    /// Gets the predefined completion colors.
    /// </summary>
    public static IReadOnlyList<string> PredefinedColors => PredefinedColorValues;

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
    /// Gets or sets the progress of the timer as a percentage (0-100).
    /// </summary>
    public double ProgressPercentage
    {
        get => this.progressPercentage;
        set
        {
            if (this.SetProperty(ref this.progressPercentage, value))
            {
                this.UpdateProgressBarColor();
            }
        }
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

    /// <summary>
    /// Gets or sets a value indicating whether the timer has completed.
    /// </summary>
    public bool IsCompleted
    {
        get => this.isCompleted;
        set
        {
            if (this.SetProperty(ref this.isCompleted, value))
            {
                this.UpdateCompletionBackground();
            }
        }
    }

    /// <summary>
    /// Gets the available alarm sounds.
    /// </summary>
    public IReadOnlyList<string> AvailableSounds => this.audioService.AvailableSounds;

    /// <summary>
    /// Gets or sets a value indicating whether to show completion color.
    /// </summary>
    public bool EnableCompletionColor
    {
        get => this.Model.EnableCompletionColor;
        set
        {
            if (this.Model.EnableCompletionColor != value)
            {
                this.Model.EnableCompletionColor = value;
                this.OnPropertyChanged();
                this.UpdateCompletionBackground();
            }
        }
    }

    /// <summary>
    /// Gets or sets the completion color.
    /// </summary>
    public string CompletionColor
    {
        get => this.Model.CompletionColor;
        set
        {
            if (this.Model.CompletionColor != value)
            {
                this.Model.CompletionColor = value;
                this.OnPropertyChanged();
                this.UpdateCompletionBackground();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to play an alarm sound.
    /// </summary>
    public bool EnableAlarm
    {
        get => this.Model.EnableAlarm;
        set
        {
            if (this.Model.EnableAlarm != value)
            {
                this.Model.EnableAlarm = value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the alarm sound.
    /// </summary>
    public string AlarmSound
    {
        get => this.Model.AlarmSound;
        set
        {
            if (this.Model.AlarmSound != value)
            {
                this.Model.AlarmSound = value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to repeat the alarm.
    /// </summary>
    public bool EnableAlarmRepeat
    {
        get => this.Model.EnableAlarmRepeat;
        set
        {
            if (this.Model.EnableAlarmRepeat != value)
            {
                this.Model.EnableAlarmRepeat = value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the alarm repeat interval in seconds.
    /// </summary>
    public int AlarmRepeatIntervalSeconds
    {
        get => this.Model.AlarmRepeatIntervalSeconds;
        set
        {
            if (this.Model.AlarmRepeatIntervalSeconds != value)
            {
                this.Model.AlarmRepeatIntervalSeconds = value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the alarm expiration time in minutes.
    /// </summary>
    public int AlarmExpirationMinutes
    {
        get => this.Model.AlarmExpirationMinutes;
        set
        {
            if (this.Model.AlarmExpirationMinutes != value)
            {
                this.Model.AlarmExpirationMinutes = value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the completion background brush.
    /// </summary>
    public Brush? CompletionBackgroundBrush { get; private set; }

    /// <summary>
    /// Gets the progress bar brush that transitions from red to green.
    /// </summary>
    public Brush ProgressBarBrush { get; private set; } = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54));

    /// <inheritdoc/>
    public void Dispose() => this.alarmRepeatTimer?.Dispose();

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

    private static SolidColorBrush? CreateBrushFromHex(string hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return null;
        }

        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            byte r = Convert.ToByte(hex[..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            return new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }

        return null;
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
        this.StopAlarmRepeat();
        this.Model.Stop();
        this.IsCompleted = false;
        this.UpdateState();
        this.UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Reset()
    {
        this.StopAlarmRepeat();
        this.Model.Reset();
        this.IsCompleted = false;
        this.UpdateState();
        this.UpdateTimeDisplay();
    }

    [RelayCommand]
    private void Remove()
    {
        this.StopAlarmRepeat();
        this.timerService.Tick -= this.OnGlobalTick;
        this.RequestRemove?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Dismiss()
    {
        this.StopAlarmRepeat();
        this.IsCompleted = false;
        this.UpdateCompletionBackground();
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
            TimerState previousState = this.Model.State;
            this.Model.Tick();
            _ = this.dispatcher.TryEnqueue(() =>
            {
                this.UpdateTimeDisplay();
                this.ProgressPercentage = this.Model.ProgressPercentage;

                if (this.Model.State == TimerState.Completed && previousState != TimerState.Completed)
                {
                    this.OnTimerCompleted();
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
            : timeToShow.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
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

    private void OnTimerCompleted()
    {
        this.IsCompleted = true;

        if (this.EnableAlarm)
        {
            this.audioService.PlaySound(this.AlarmSound);

            if (this.EnableAlarmRepeat)
            {
                this.StartAlarmRepeat();
            }
        }
    }

    private void StartAlarmRepeat()
    {
        if (this.alarmRepeatTimer is null)
        {
            return;
        }

        this.alarmExpirationTime = DateTime.Now.AddMinutes(this.AlarmExpirationMinutes);
        this.alarmRepeatTimer.Interval = this.AlarmRepeatIntervalSeconds * 1000;
        this.alarmRepeatTimer.Start();
    }

    private void StopAlarmRepeat()
    {
        this.alarmRepeatTimer?.Stop();
        this.alarmExpirationTime = null;
    }

    private void OnAlarmRepeatTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (this.alarmExpirationTime.HasValue && DateTime.Now >= this.alarmExpirationTime.Value)
        {
            _ = this.dispatcher.TryEnqueue(this.StopAlarmRepeat);
            return;
        }

        if (this.IsCompleted && this.EnableAlarm)
        {
            this.audioService.PlaySound(this.AlarmSound);
        }
        else
        {
            _ = this.dispatcher.TryEnqueue(this.StopAlarmRepeat);
        }
    }

    private void UpdateCompletionBackground()
    {
        this.CompletionBackgroundBrush = this.IsCompleted && this.EnableCompletionColor
            ? CreateBrushFromHex(this.CompletionColor)
            : null;

        this.OnPropertyChanged(nameof(this.CompletionBackgroundBrush));
    }

    private void UpdateProgressBarColor()
    {
        // Progress goes from 100 (just started) to 0 (completed)
        // Color transitions from red (0% progress remaining) to green (100% progress remaining)
        // Red: RGB(244, 67, 54) - #F44336
        // Green: RGB(76, 175, 80) - #4CAF50
        double progress = this.progressPercentage / 100.0;

        byte r = (byte)(244 + ((76 - 244) * progress));
        byte g = (byte)(67 + ((175 - 67) * progress));
        byte b = (byte)(54 + ((80 - 54) * progress));

        this.ProgressBarBrush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        this.OnPropertyChanged(nameof(this.ProgressBarBrush));
    }
}