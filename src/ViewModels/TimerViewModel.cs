namespace TickDown.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System.Globalization;
using TickDown.Core.Models;
using TickDown.Core.Services;

public partial class TimerViewModel : ObservableObject
{
    private readonly ITimerService _timerService;
    private readonly DispatcherQueue _dispatcher;

    public CountdownTimer Model { get; }

    [ObservableProperty]
    private string _timeDisplay = "00:05:00";

    [ObservableProperty]
    private string _name = "New Timer";

    [ObservableProperty]
    private bool _isRunning = false;

    [ObservableProperty]
    private bool _isPaused = false;

    [ObservableProperty]
    private double _progressPercentage = 0;

    private bool _isUpdatingTimeDisplay = false;

    public event EventHandler? RequestRemove;

    public TimerViewModel(ITimerService timerService)
    {
        _timerService = timerService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Model = new CountdownTimer(TimeSpan.FromMinutes(5), Name);

        _timerService.Tick += OnGlobalTick;

        UpdateTimeDisplay();
    }

    partial void OnNameChanged(string value)
    {
        Model.Name = value;
    }

    partial void OnTimeDisplayChanged(string value)
    {
        if (_isUpdatingTimeDisplay)
        {
            return;
        }

        if (TimeSpan.TryParse(value, CultureInfo.CurrentCulture, out TimeSpan result))
        {
            Hours = result.Hours;
            Minutes = result.Minutes;
            Seconds = result.Seconds;
        }
        else
        {
            UpdateTimeDisplay();
        }
    }

    [ObservableProperty]
    private int _hours = 0;

    [ObservableProperty]
    private int _minutes = 5;

    [ObservableProperty]
    private int _seconds = 0;

    partial void OnHoursChanged(int value) => UpdateDuration();
    partial void OnMinutesChanged(int value) => UpdateDuration();
    partial void OnSecondsChanged(int value) => UpdateDuration();

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

        TimeDisplay = timeToShow.ToString(@"hh\:mm\:ss");
        _isUpdatingTimeDisplay = false;
    }

    private void UpdateState()
    {
        IsRunning = Model.State == TimerState.Running;
        IsPaused = Model.State == TimerState.Paused;
    }
}