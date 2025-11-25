namespace TickDown.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using TickDown.ViewModels;
using Windows.System;

/// <summary>
/// A simple page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    /// <summary>
    /// Gets the main view model.
    /// </summary>
    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = ViewModel;

        SetupZoomAccelerators();
    }

    private void SetupZoomAccelerators()
    {
        // Numpad +
        KeyboardAccelerator zoomIn = new() { Key = VirtualKey.Add, Modifiers = VirtualKeyModifiers.Control };
        zoomIn.Invoked += (s, e) => ZoomIn();
        KeyboardAccelerators.Add(zoomIn);

        // Numpad -
        KeyboardAccelerator zoomOut = new() { Key = VirtualKey.Subtract, Modifiers = VirtualKeyModifiers.Control };
        zoomOut.Invoked += (s, e) => ZoomOut();
        KeyboardAccelerators.Add(zoomOut);

        // Ctrl + 0 to reset
        KeyboardAccelerator zoomReset = new() { Key = VirtualKey.Number0, Modifiers = VirtualKeyModifiers.Control };
        zoomReset.Invoked += (s, e) => ResetZoom();
        KeyboardAccelerators.Add(zoomReset);

        // Standard + (on = key)
        KeyboardAccelerator zoomInStd = new() { Key = (VirtualKey)187, Modifiers = VirtualKeyModifiers.Control };
        zoomInStd.Invoked += (s, e) => ZoomIn();
        KeyboardAccelerators.Add(zoomInStd);

        // Standard -
        KeyboardAccelerator zoomOutStd = new() { Key = (VirtualKey)189, Modifiers = VirtualKeyModifiers.Control };
        zoomOutStd.Invoked += (s, e) => ZoomOut();
        KeyboardAccelerators.Add(zoomOutStd);
    }

    private void ZoomIn()
    {
        float newZoom = RootScrollViewer.ZoomFactor + 0.1f;
        _ = RootScrollViewer.ChangeView(null, null, newZoom);
    }

    private void ZoomOut()
    {
        float newZoom = RootScrollViewer.ZoomFactor - 0.1f;
        _ = RootScrollViewer.ChangeView(null, null, newZoom);
    }

    private void ResetZoom() => _ = RootScrollViewer.ChangeView(null, null, 1.0f);
}