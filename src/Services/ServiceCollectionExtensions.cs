namespace TickDown.Services;

using Microsoft.Extensions.DependencyInjection;
using TickDown.Core.Services;
using TickDown.ViewModels;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        // Core services
        _ = services.AddSingleton<ITimerService, TimerService>();

        // ViewModels
        _ = services.AddTransient<MainViewModel>();

        return services;
    }
}