// Copyright © 2025 HemSoft

namespace TickDown.Services;

using global::TickDown.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using TickDown.ViewModels;

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
        _ = services.AddSingleton<ITimerService, TimerService>();
        _ = services.AddSingleton<ISettingsService, SettingsService>();
        _ = services.AddSingleton<IThemeService, ThemeService>();
        _ = services.AddSingleton<IAudioService, AudioService>();
        _ = services.AddTransient<MainViewModel>();

        return services;
    }
}