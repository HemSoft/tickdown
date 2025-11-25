namespace TickDown.Services;

/// <summary>
/// Extension methods for setting up application services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the application services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        // Core services
        _ = services.AddSingleton<ITimerService, TimerService>();
        _ = services.AddSingleton<ISettingsService, SettingsService>();

        // ViewModels
        _ = services.AddTransient<MainViewModel>();

        return services;
    }
}