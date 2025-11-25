namespace TickDown.ViewModels;

using Microsoft.UI.Dispatching;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TickDown.Core.Models;
using TickDown.Core.Services;

public partial class MainViewModel : ObservableObject
{
    private readonly ITimerService _timerService;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty]
    private string _timeDisplay = "00:00:00";

    [ObservableProperty]
    private string _timerName = "Timer";

    [ObservableProperty]
    private bool _isRunning = false;

    [ObservableProperty]
    private bool _isPaused = false;

    [ObservableProperty]
    private double _progressPercentage = 0;

    public int Hours
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && !_timerService.IsRunning)
            {
                UpdateTimeDisplay();
            }
        }
    } = 0;

    public int Minutes
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && !_timerService.IsRunning)
            {
                UpdateTimeDisplay();
            }
        }
    } = 5;

    public int Seconds
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && !_timerService.IsRunning)
            {
                UpdateTimeDisplay();
            }
        }
    } = 0;

    public MainViewModel(ITimerService timerService)
    {
        _timerService = timerService;
        // Capture the UI thread dispatcher at construction time (we're on UI thread here)
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _timerService.TimerTick += OnTimerTick;
        _timerService.TimerCompleted += OnTimerCompleted;

        UpdateTimeDisplay();
    }

    [RelayCommand]
    private void StartTimer()
    {
        // Simple test: just change the timer name to verify command is working
        TimerName = "Timer Started!";

        // Force start button behavior regardless of state
        var duration = new TimeSpan(Hours, Minutes, Seconds);

        // If no duration is set, default to 5 minutes
        if (duration.TotalSeconds <= 0)
        {
            duration = TimeSpan.FromMinutes(5);
            Minutes = 5;
            Hours = 0;
            Seconds = 0;
        }

        // Always create a new timer
        _timerService.SetTimer(duration, TimerName);
        _timerService.Start();

        UpdateState();
        UpdateTimeDisplay();
    }

    [RelayCommand]
    private void PauseTimer()
    {
        _timerService.Pause();
        UpdateState();
    }

    [RelayCommand]
    private void StopTimer()
    {
        _timerService.Stop();
        UpdateState();
        UpdateTimeDisplay();
    }

    [RelayCommand]
    private void ResetTimer()
    {
        _timerService.Reset();
        UpdateState();
        UpdateTimeDisplay();
    }

    [RelayCommand]
    private void SetQuickTimer(object parameter)
    {
        if (parameter is string timeStr)
        {
            switch (timeStr)
            {
                case "1min":
                    SetTimer(0, 1, 0);
                    break;
                case "5min":
                    SetTimer(0, 5, 0);
                    break;
                case "10min":
                    SetTimer(0, 10, 0);
                    break;
                case "15min":
                    SetTimer(0, 15, 0);
                    break;
                case "30min":
                    SetTimer(0, 30, 0);
                    break;
                case "1hour":
                    SetTimer(1, 0, 0);
                    break;
            }
        }
    }

    private void SetTimer(int hours, int minutes, int seconds)
    {
        Hours = hours;
        Minutes = minutes;
        Seconds = seconds;

        var duration = new TimeSpan(hours, minutes, seconds);
        _timerService.SetTimer(duration, TimerName);
        UpdateTimeDisplay();
    }

    private void OnTimerTick(object? sender, CountdownTimer timer)
    {
        // Update on UI thread
        _dispatcher.TryEnqueue(() =>
        {
            UpdateTimeDisplay();
            ProgressPercentage = timer.ProgressPercentage;
            UpdateState();
        });
    }

    private void OnTimerCompleted(object? sender, CountdownTimer timer)
    {
        // Update on UI thread
        _dispatcher.TryEnqueue(() =>
        {
            UpdateState();
            UpdateTimeDisplay();
            // Future: Show notification, play sound, etc.
        });
    }

    private void UpdateTimeDisplay()
    {
        var timer = _timerService.CurrentTimer;
        TimeSpan timeToShow;

        if (timer == null || timer.State == TimerState.Stopped)
        {
            // Show the input time when no timer is set or when stopped
            timeToShow = new TimeSpan(Hours, Minutes, Seconds);
        }
        else
        {
            // Show the remaining time from the active timer
            timeToShow = timer.Remaining;
        }

        TimeDisplay = timeToShow.ToString(@"hh\:mm\:ss");
    }

    private void UpdateState()
    {
        IsRunning = _timerService.IsRunning;
        IsPaused = _timerService.CurrentTimer?.State == TimerState.Paused;
    }
}
