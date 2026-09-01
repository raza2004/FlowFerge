using System.Reflection;
using FluentValidation;
using FlowForge.Application.Common.Behaviors;
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
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(assembly);

        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        return services;
    }
}
