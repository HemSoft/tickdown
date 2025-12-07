// Copyright © 2025 HemSoft

namespace TickDown.Views;

using Microsoft.Extensions.DependencyInjection;
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
}