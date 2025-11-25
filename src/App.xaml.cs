namespace TickDown;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Navigation;
using TickDown.Core.Models;
using TickDown.Core.Services;
using TickDown.Services;
using TickDown.Views;
using Windows.Graphics;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private readonly IHost? _host;

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public static IServiceProvider Services => ((App)Current)._host?.Services ?? throw new InvalidOperationException("Services not available");

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Set up dependency injection
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => _ = services.AddAppServices())
            .Build();
    }

    /// <summary>
    /// Invoked when the application is launched normally by the end user.  Other entry points
    /// will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window ??= new Window();
        _window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/app.ico"));

        if (_window.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            _window.Content = rootFrame;
        }

        _ = rootFrame.Navigate(typeof(MainPage), args.Arguments);

        // Restore window settings
        ISettingsService settingsService = Services.GetRequiredService<ISettingsService>();
        WindowSettings? settings = await settingsService.LoadWindowSettingsAsync();
        if (settings is not null)
        {
            AppWindow appWindow = _window.AppWindow;
            appWindow.MoveAndResize(new RectInt32(settings.X, settings.Y, settings.Width, settings.Height));

            if (settings.IsMaximized)
            {
                ((OverlappedPresenter)appWindow.Presenter).Maximize();
            }
        }

        _window.Activate();
        _window.AppWindow.Closing += OnAppWindowClosing;
    }

    private bool _isExitQueued;

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExitQueued)
        {
            return;
        }

        args.Cancel = true;

        AppWindow appWindow = _window!.AppWindow;
        OverlappedPresenter? presenter = appWindow.Presenter as OverlappedPresenter;
        bool isMaximized = presenter?.State == OverlappedPresenterState.Maximized;

        WindowSettings settings = new()
        {
            IsMaximized = isMaximized
        };

        if (!isMaximized)
        {
            settings.X = appWindow.Position.X;
            settings.Y = appWindow.Position.Y;
            settings.Width = appWindow.Size.Width;
            settings.Height = appWindow.Size.Height;
        }
        else
        {
            ISettingsService settingsService = Services.GetRequiredService<ISettingsService>();
            WindowSettings? existingSettings = await settingsService.LoadWindowSettingsAsync();
            if (existingSettings != null)
            {
                settings.X = existingSettings.X;
                settings.Y = existingSettings.Y;
                settings.Width = existingSettings.Width;
                settings.Height = existingSettings.Height;
            }
            else
            {
                settings.X = 100;
                settings.Y = 100;
                settings.Width = 800;
                settings.Height = 600;
            }
        }

        ISettingsService service = Services.GetRequiredService<ISettingsService>();
        await service.SaveWindowSettingsAsync(settings);

        _isExitQueued = true;
        _window.Close();
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails
    /// </summary>
    /// <param name="sender">The Frame which failed navigation</param>
    /// <param name="e">Details about the navigation failure</param>
    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e) => throw new InvalidOperationException("Failed to load Page " + e.SourcePageType.FullName);
}