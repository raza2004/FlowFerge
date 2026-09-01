using FlowForge.Application.Common.Abstractions;
using FlowForge.Domain.Common;
using FlowForge.Domain.Identity.Repositories;
using FlowForge.Domain.Notifications.Repositories;
using FlowForge.Domain.Projects.Repositories;
using FlowForge.Domain.Workflows.Repositories;
using FlowForge.Infrastructure.Persistence;
using FlowForge.Infrastructure.Persistence.Repositories;
using FlowForge.Infrastructure.Services.AI;
using FlowForge.Infrastructure.Services.Auth;
using FlowForge.Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Database
        services.AddDbContext<FlowForgeDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        // Repositories (Identity)
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
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

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Auth services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // AI
        services.AddSingleton<IAiService, OpenAiService>();

        // Notifications (email + Slack; real-time SignalR push is registered in the API layer)
        services.AddHttpClient(nameof(SlackWebhookNotifier));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<ISlackNotifier, SlackWebhookNotifier>();

        // Context accessors (scoped per request)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ITenantContext, TenantContext>();

        return services;
    }
}
