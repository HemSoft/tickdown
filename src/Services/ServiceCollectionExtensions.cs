namespace TickDown.Services;

public static class ServiceCollectionExtensions
{
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