namespace TickDown.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TickDown.Core.Services;

public partial class MainViewModel : ObservableObject
{
    private readonly ITimerService _timerService;

    public ObservableCollection<TimerViewModel> Timers { get; } = [];

    public MainViewModel(ITimerService timerService)
    {
        _timerService = timerService;

        // Add a default timer
        AddTimer();
    }

    [RelayCommand]
    private void AddTimer()
    {
        TimerViewModel timerVm = new(_timerService);
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