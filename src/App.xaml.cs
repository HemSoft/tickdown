// Copyright © 2025 HemSoft

namespace TickDown;

using global::TickDown.Core.Models;
using global::TickDown.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TickDown.Services;
using TickDown.Views;
using Windows.Graphics;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private readonly IHost? host;
    private Window? window;
    private bool isExitQueued;
    private bool isClosing;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        this.host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => _ = services.AddAppServices())
            .Build();
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public static IServiceProvider Services => ((App)Current).host?.Services ?? throw new InvalidOperationException("Services not available");

    /// <summary>
    /// Gets the main application window.
    /// </summary>
    public Window? MainWindow => this.window;

    /// <summary>
    /// Invoked when the application is launched normally by the end user.  Other entry points
    /// will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        this.window ??= new Window();
        this.window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/app.ico"));

        if (this.window.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            this.window.Content = rootFrame;
        }

        _ = rootFrame.Navigate(typeof(MainPage), args.Arguments);

        ISettingsService settingsService = Services.GetRequiredService<ISettingsService>();
        WindowSettings? settings = await LoadWindowSettingsObservedAsync(settingsService);

        // Initialize theme after window is set up
        IThemeService themeService = Services.GetRequiredService<IThemeService>();
        await themeService.InitializeAsync();

        this.window.Activate();
        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this.window);
        RestoreWindowPlacement(this.window.AppWindow, windowHandle, settings);
        this.window.AppWindow.Closing += this.OnAppWindowClosing;
    }

    private static void RestoreWindowPlacement(AppWindow appWindow, nint windowHandle, WindowSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        if (settings.HasSavedPlacement)
        {
            RestoreNormalBounds(windowHandle, settings);
        }

        RestoreMaximizedState(appWindow, settings);
    }

    private static void RestoreNormalBounds(nint windowHandle, WindowSettings settings)
    {
        RectInt32 savedBounds = new(settings.X, settings.Y, settings.Width, settings.Height);
        DisplayArea? displayArea = DisplayArea.GetFromRect(savedBounds, DisplayAreaFallback.Nearest);
        if (displayArea is null)
        {
            return;
        }

        WindowBounds workArea = new(
            displayArea.WorkArea.X,
            displayArea.WorkArea.Y,
            displayArea.WorkArea.Width,
            displayArea.WorkArea.Height);
        WindowBounds? restoredBounds = WindowPlacement.ResolveVisibleBounds(
            new WindowBounds(settings.X, settings.Y, settings.Width, settings.Height),
            workArea);
        if (restoredBounds is WindowBounds bounds)
        {
            NativeWindowPlacement.MoveAndResize(windowHandle, bounds, workArea);
        }
    }

    private static void RestoreMaximizedState(AppWindow appWindow, WindowSettings settings)
    {
        if (settings.IsMaximized)
        {
            ((OverlappedPresenter)appWindow.Presenter).Maximize();
        }
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e) =>
        throw new InvalidOperationException("Failed to load Page " + e.SourcePageType.FullName);

    private static async Task<WindowSettings?> LoadWindowSettingsObservedAsync(ISettingsService settingsService)
    {
        try
        {
            return await settingsService.LoadWindowSettingsAsync();
        }
        catch (IOException)
        {
            // The settings failure event reports the problem in the main view.
            return null;
        }
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (this.isExitQueued)
        {
            return;
        }

        args.Cancel = true;
        if (this.isClosing)
        {
            return;
        }

        this.isClosing = true;
        Frame content = (Frame)this.window!.Content;
        content.IsEnabled = false;
        try
        {
            ISettingsService settingsService = Services.GetRequiredService<ISettingsService>();
            AppWindow appWindow = this.window.AppWindow;
            bool isMaximized = appWindow.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Maximized;

            WindowSettings settings = await settingsService.LoadWindowSettingsAsync() ?? new WindowSettings
            {
                X = 100,
                Y = 100,
                Width = 800,
                Height = 600,
            };
            nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this.window);
            WindowBounds currentBounds = NativeWindowPlacement.GetBounds(windowHandle);
            settings.UpdateWindowState(
                isMaximized,
                currentBounds.X,
                currentBounds.Y,
                currentBounds.Width,
                currentBounds.Height);

            await settingsService.SaveWindowSettingsAsync(settings);
            await settingsService.FlushAsync();
            this.isExitQueued = true;
            this.window.Close();
        }
        catch (IOException)
        {
            // Keep the app open. The visible settings error explains the failure,
            // and another edit can retry the unsaved snapshot before closing.
        }
        finally
        {
            this.isClosing = false;
            if (!this.isExitQueued)
            {
                content.IsEnabled = true;
            }
        }
    }
}