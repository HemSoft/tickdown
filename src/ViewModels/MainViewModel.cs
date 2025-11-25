namespace TickDown.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TickDown.Core.Models;
using TickDown.Core.Services;

public partial class MainViewModel : ObservableObject
{
    private readonly ITimerService _timerService;
    private readonly ISettingsService _settingsService;
    private bool _isLoading = true;

    public ObservableCollection<TimerViewModel> Timers { get; } = [];

    public MainViewModel(ITimerService timerService, ISettingsService settingsService)
    {
        _timerService = timerService;
        _settingsService = settingsService;

        Timers.CollectionChanged += Timers_CollectionChanged;

        // Load timers
        _ = LoadTimersAsync();
    }

    private async Task LoadTimersAsync()
    {
        IEnumerable<CountdownTimer> timers = await _settingsService.LoadTimersAsync();
        if (timers.Any())
        {
            foreach (CountdownTimer timer in timers)
            {
                AddTimerInternal(timer);
            }
        }
        else
        {
            AddTimer();
        }
        _isLoading = false;
    }

    private void Timers_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (TimerViewModel item in e.NewItems)
            {
                item.PropertyChanged += Timer_PropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (TimerViewModel item in e.OldItems)
            {
                item.PropertyChanged -= Timer_PropertyChanged;
            }
        }
        SaveTimers();
    }

    private void Timer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimerViewModel.Name)
            or nameof(TimerViewModel.Hours)
            or nameof(TimerViewModel.Minutes)
            or nameof(TimerViewModel.Seconds)
            or nameof(TimerViewModel.IsRunning)
            or nameof(TimerViewModel.IsPaused))
        {
            SaveTimers();
        }
    }

    private void SaveTimers()
    {
        if (_isLoading)
        {
            return;
        }
        _ = _settingsService.SaveTimersAsync(Timers.Select(t => t.Model));
    }

    [RelayCommand]
    private void AddTimer() => AddTimerInternal(null);

    private void AddTimerInternal(CountdownTimer? model)
    {
        TimerViewModel timerVm = new(_timerService, model);
        timerVm.RequestRemove += OnRemoveTimerRequested;
        Timers.Add(timerVm);
    }

    private void OnRemoveTimerRequested(object? sender, EventArgs e)
    {
        if (sender is TimerViewModel vm)
        {
            vm.RequestRemove -= OnRemoveTimerRequested;
            _ = Timers.Remove(vm);
        }
    }
}