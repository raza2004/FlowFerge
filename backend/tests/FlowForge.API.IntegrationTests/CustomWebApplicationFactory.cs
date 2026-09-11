using FlowForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace FlowForge.API.IntegrationTests;

/// <summary>
/// Boots the real API (real MediatR pipeline, real EF Core, real domain event
/// dispatch/automation handlers) against a throwaway PostgreSQL container instead of
/// mocks - this is what actually exercises the reentrant-SaveChanges domain event fix
/// and the AuditLoggingBehavior end to end, not just through mocked repositories.
///
/// Nothing else in Infrastructure is currently wired to a real client (Redis/RabbitMQ/
/// MinIO are referenced packages but not yet registered in DI, and OpenAI/SMTP/Slack
/// clients are only constructed lazily when a request actually needs them), so
/// PostgreSQL is the only external dependency that needs a real backing service here.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("flowforge_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Jwt:Key"] = "Integration-Test-Only-Secret-Key-32Chars!",
                ["Jwt:Issuer"] = "FlowForge",
                ["Jwt:Audience"] = "FlowForgeUsers",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7"
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Accessing Services builds the host (ConfigureWebHost runs here), by which
        // point the container above is already up and its connection string is real.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowForgeDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
