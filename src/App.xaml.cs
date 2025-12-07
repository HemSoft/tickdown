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
        WindowSettings? settings = await settingsService.LoadWindowSettingsAsync();
        if (settings is not null)
        {
            AppWindow appWindow = this.window.AppWindow;
            appWindow.MoveAndResize(new RectInt32(settings.X, settings.Y, settings.Width, settings.Height));

            if (settings.IsMaximized)
            {
                ((OverlappedPresenter)appWindow.Presenter).Maximize();
            }
        }

        this.window.Activate();
        this.window.AppWindow.Closing += this.OnAppWindowClosing;
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e) =>
        throw new InvalidOperationException("Failed to load Page " + e.SourcePageType.FullName);

    private static async Task<(int X, int Y, int Width, int Height)> GetSavedOrDefaultPositionAsync(ISettingsService settingsService)
    {
        WindowSettings? existing = await settingsService.LoadWindowSettingsAsync();
        return existing is not null
            ? (existing.X, existing.Y, existing.Width, existing.Height)
            : (100, 100, 800, 600);
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (this.isExitQueued)
        {
            return;
        }

        args.Cancel = true;

        ISettingsService settingsService = Services.GetRequiredService<ISettingsService>();
        AppWindow appWindow = this.window!.AppWindow;
        bool isMaximized = (appWindow.Presenter as OverlappedPresenter)?.State == OverlappedPresenterState.Maximized;

        // When maximized, preserve the previous non-maximized position; otherwise capture current position
        (int x, int y, int width, int height) = isMaximized
            ? await GetSavedOrDefaultPositionAsync(settingsService)
            : (appWindow.Position.X, appWindow.Position.Y, appWindow.Size.Width, appWindow.Size.Height);

        await settingsService.SaveWindowSettingsAsync(new WindowSettings
        {
            IsMaximized = isMaximized,
            X = x,
            Y = y,
            Width = width,
            Height = height,
        });

        this.isExitQueued = true;
        this.window.Close();
    }
}