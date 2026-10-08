using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Auditing;
using FlowForge.Domain.Common;
using FlowForge.Domain.Features.Repositories;
using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Notifications.Repositories;
using FlowForge.Domain.Projects.Repositories;
using FlowForge.Domain.Workflows.Repositories;
using FlowForge.Infrastructure.Persistence;
using FlowForge.Infrastructure.Persistence.Repositories;
using FlowForge.Infrastructure.Services;
using FlowForge.Infrastructure.Services.AI;
using FlowForge.Infrastructure.Services.Auth;
using FlowForge.Infrastructure.Services.Caching;
using FlowForge.Infrastructure.Services.Messaging;
using FlowForge.Infrastructure.Services.Notifications;
using FlowForge.Infrastructure.Services.Storage;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowForge.Infrastructure;

public static class DependencyInjection
{
    /// <param name="configureConsumers">
    /// Lets a specific process (FlowForge.Workers) register the MassTransit consumers it
    /// hosts, without Infrastructure needing to reference that process's project. The API
    /// calls AddInfrastructure with no argument here - it only ever publishes messages.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config, Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        // Database
        services.AddDbContext<FlowForgeDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        // Repositories (Identity)
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Repositories (Projects)
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<ITaskCommentRepository, TaskCommentRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();

        // Repositories (Workflows / Notifications)
        services.AddScoped<IAutomationRuleRepository, AutomationRuleRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Repositories (Auditing)
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Auth services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // AI
        services.AddSingleton<IAiService, OpenAiService>();

        services.AddSingleton<IAppLinks, AppLinks>();

        // Cache: Redis when Redis:ConnectionString is set, otherwise a no-op. Either way the app
        // behaves identically, just faster with Redis, and Redis being down never breaks a request.
        var redisConnection = config["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnection))
            services.AddSingleton<ICache, NullCache>();
        else
            services.AddSingleton<ICache>(sp => new RedisCache(redisConnection, sp.GetRequiredService<ILogger<RedisCache>>()));
        services.AddSingleton<IFileStorage, MinioFileStorage>();

        // Notifications (email + Slack; real-time SignalR push is registered in the API layer)
        services.AddHttpClient(nameof(SlackWebhookNotifier));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<ISlackNotifier, SlackWebhookNotifier>();

        // Message bus (RabbitMQ via MassTransit) - carries slow notification channels
        // (email/Slack) off the request thread. Every process (API, Workers) that calls
        // AddInfrastructure gets a bus connection; only Workers registers consumers on it.
        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(config["RabbitMQ:Host"] ?? "localhost", "/", h =>
                {
                    h.Username(config["RabbitMQ:Username"] ?? "guest");
                    h.Password(config["RabbitMQ:Password"] ?? "guest");
                });

                cfg.ConfigureEndpoints(context);
            });
        });
        services.AddScoped<IMessageBus, MassTransitMessageBus>();

        // Context accessors (scoped per request)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ITenantContext, TenantContext>();

        return services;
    }
}
