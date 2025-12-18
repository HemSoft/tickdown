// Copyright © 2025 HemSoft

namespace TickDown.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TickDown.ViewModels;
using Windows.System;

/// <summary>
/// A simple page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    private const float ZoomStep = 0.1f;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    public MainPage()
    {
        this.InitializeComponent();
        this.ViewModel = App.Services.GetRequiredService<MainViewModel>();
        this.DataContext = this.ViewModel;

        this.SetupZoomAccelerators();
    }

    /// <summary>
    /// Gets the main view model.
    /// </summary>
    public MainViewModel ViewModel { get; }

    private void SetupZoomAccelerators()
    {
        this.AddAccelerator(VirtualKey.Add, () => this.Zoom(ZoomStep));
        this.AddAccelerator(VirtualKey.Subtract, () => this.Zoom(-ZoomStep));
        this.AddAccelerator(VirtualKey.Number0, this.ResetZoom);
        this.AddAccelerator((VirtualKey)187, () => this.Zoom(ZoomStep));   // Ctrl+=
        this.AddAccelerator((VirtualKey)189, () => this.Zoom(-ZoomStep));  // Ctrl+-
    }

    private void AddAccelerator(VirtualKey key, Action action)
    {
        KeyboardAccelerator accelerator = new() { Key = key, Modifiers = VirtualKeyModifiers.Control };
        accelerator.Invoked += (s, e) => action();
        this.KeyboardAccelerators.Add(accelerator);
    }

    private void Zoom(float delta) =>
        _ = this.RootScrollViewer.ChangeView(null, null, this.RootScrollViewer.ZoomFactor + delta);

    private void ResetZoom() => _ = this.RootScrollViewer.ChangeView(null, null, 1.0f);

    private void OnColorButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string colorHex)
        {
            FrameworkElement? element = button;
            while (element != null && element.DataContext is not TimerViewModel)
            {
                element = element.Parent as FrameworkElement;
            }

            if (element?.DataContext is TimerViewModel timerVm)
            {
                timerVm.CompletionColor = colorHex;
            }
        }

        // Touch a field to satisfy S2325 - XAML event handlers must be instance methods
        _ = this.ViewModel;
    }

    private void OnCustomColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        FrameworkElement? element = sender;
        while (element != null && element.DataContext is not TimerViewModel)
        {
            element = element.Parent as FrameworkElement;
        }

        if (element?.DataContext is TimerViewModel timerVm)
        {
            timerVm.CompletionColor = $"#{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}";
        }

        // Touch a field to satisfy CA1822 - XAML event handlers must be instance methods
        _ = this.ViewModel;
    }

    private async void OnSelectSoundClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        FrameworkElement? parent = element;
        while (parent != null && parent.DataContext is not TimerViewModel)
        {
            parent = parent.Parent as FrameworkElement;
        }

        if (parent?.DataContext is not TimerViewModel timerVm)
        {
            return;
        }

        SoundPickerDialog dialog = new()
        {
            XamlRoot = this.XamlRoot,
            SelectedSound = timerVm.AlarmSound,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            timerVm.AlarmSound = dialog.SelectedSound;
        }
    }
}