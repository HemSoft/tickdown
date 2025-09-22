using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TickDown.Core.Services;
using TickDown.Services;
using TickDown.ViewModels;

namespace TickDown.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ITimerService, TimerService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        return services;
    }
}
