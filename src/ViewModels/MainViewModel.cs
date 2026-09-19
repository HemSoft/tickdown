// Copyright © 2025 HemSoft

namespace TickDown.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using global::TickDown.Core.Models;
using global::TickDown.Core.Services;
using Microsoft.UI.Dispatching;

/// <summary>
/// The main view model for the application, managing the collection of timers.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ITimerService timerService;
    private readonly ISettingsService settingsService;
    private readonly IThemeService themeService;
    private readonly IAudioService audioService;
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
    private string persistenceError = string.Empty;
    private bool isLoading = true;
    private string currentTheme;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="timerService">The timer service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="themeService">The theme service.</param>
    /// <param name="audioService">The audio service.</param>
    public MainViewModel(ITimerService timerService, ISettingsService settingsService, IThemeService themeService, IAudioService audioService)
    {
        this.timerService = timerService;
        this.settingsService = settingsService;
        this.settingsService.PersistenceFailed += this.OnPersistenceFailed;
        this.themeService = themeService;
        this.audioService = audioService;

        this.currentTheme = themeService.CurrentTheme;
        this.themeService.ThemeChanged += this.OnThemeChanged;

        this.Timers.CollectionChanged += this.Timers_CollectionChanged;

        _ = this.LoadTimersAsync();
    }

    /// <summary>
    /// Gets the available theme options.
    /// </summary>
    public static IReadOnlyList<string> AvailableThemes { get; } = ["Light", "Dark", "System"];

    /// <summary>
    /// Gets the collection of timer view models.
    /// </summary>
    public ObservableCollection<TimerViewModel> Timers { get; } = [];

    /// <summary>
    /// Gets or sets the current theme.
    /// </summary>
    public string CurrentTheme
    {
        get => this.currentTheme;
        set
        {
            if (this.SetProperty(ref this.currentTheme, value))
            {
                this.themeService.SetTheme(value);
            }
        }
    }

    /// <summary>
    /// Gets the most recent storage failure or recovery message.
    /// </summary>
    public string PersistenceError
    {
        get => this.persistenceError;
        private set
        {
            if (this.SetProperty(ref this.persistenceError, value))
            {
                this.OnPropertyChanged(nameof(this.HasPersistenceError));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether storage needs user attention.
    /// </summary>
    public bool HasPersistenceError => this.PersistenceError.Length > 0;

    private void OnPersistenceFailed(object? sender, SettingsFailureEventArgs e) =>
        _ = this.dispatcher.TryEnqueue(() => this.PersistenceError = e.Message);

    private void OnThemeChanged(object? sender, EventArgs e) => this.CurrentTheme = this.themeService.CurrentTheme;

    private async Task LoadTimersAsync()
    {
        try
        {
            IEnumerable<CountdownTimer> timers = await this.settingsService.LoadTimersAsync();
            if (timers.Any())
            {
                foreach (CountdownTimer timer in timers)
                {
                    this.AddTimerInternal(timer);
                }
            }
            else
            {
                this.AddTimer();
            }
        }
        catch (IOException)
        {
            // The settings failure event supplies the visible error. Do not
            // replace a failed load with a new default timer and save over it.
        }
        finally
        {
            this.isLoading = false;
        }
    }

    private void Timers_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (TimerViewModel item in e.NewItems)
            {
                item.PropertyChanged += this.Timer_PropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (TimerViewModel item in e.OldItems)
            {
                item.PropertyChanged -= this.Timer_PropertyChanged;
            }
        }

        this.SaveTimers();
    }

    private void Timer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimerViewModel.Name)
            or nameof(TimerViewModel.Hours)
            or nameof(TimerViewModel.Minutes)
            or nameof(TimerViewModel.Seconds)
            or nameof(TimerViewModel.IsRunning)
            or nameof(TimerViewModel.IsPaused)
            or nameof(TimerViewModel.EnableCompletionColor)
            or nameof(TimerViewModel.CompletionColor)
            or nameof(TimerViewModel.EnableAlarm)
            or nameof(TimerViewModel.AlarmSound)
            or nameof(TimerViewModel.EnableAlarmRepeat)
            or nameof(TimerViewModel.AlarmRepeatIntervalSeconds)
            or nameof(TimerViewModel.AlarmExpirationMinutes)
            or nameof(TimerViewModel.QuickSetInterval1Minutes)
            or nameof(TimerViewModel.QuickSetInterval2Minutes)
            or nameof(TimerViewModel.QuickSetInterval3Minutes)
            or nameof(TimerViewModel.QuickSetInterval4Minutes))
        {
            this.SaveTimers();
        }
    }

    private void SaveTimers()
    {
        if (this.isLoading)
        {
            return;
        }

        _ = this.SaveTimersObservedAsync();
    }

    private async Task SaveTimersObservedAsync()
    {
        try
        {
            await this.settingsService.SaveTimersAsync(this.Timers.Select(t => t.Model));
        }
        catch (IOException)
        {
            // The settings failure event supplies the visible error, and FlushAsync
            // prevents shutdown while a write failure remains unresolved.
        }
    }

    [RelayCommand]
    private void AddTimer() => this.AddTimerInternal(null);

    private void AddTimerInternal(CountdownTimer? model)
    {
        TimerViewModel timerVm = new(this.timerService, this.audioService, model);
        timerVm.RequestRemove += this.OnRemoveTimerRequested;
        this.Timers.Add(timerVm);
    }

    private void OnRemoveTimerRequested(object? sender, EventArgs e)
    {
        if (sender is TimerViewModel vm)
        {
            vm.RequestRemove -= this.OnRemoveTimerRequested;
            _ = this.Timers.Remove(vm);
        }
    }
}