using System.Reflection;
using FluentValidation;
using FlowForge.Application.Common.Behaviors;
using FlowForge.Application.Features;
using FlowForge.Application.Identity.Services;
using FlowForge.Application.Jobs;
using FlowForge.Application.Notifications.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(FeatureGateBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuditLoggingBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(assembly);

        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IAuthSessionFactory, AuthSessionFactory>();
        services.AddScoped<IFeatureGate, FeatureGate>();

        // Scheduled work. The logic lives here so it's testable; FlowForge.Workers only schedules it.
        services.AddScoped<DueSoonReminderService>();
        services.AddScoped<DataCleanupService>();

        return services;
    }
}
