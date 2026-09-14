using FlowForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

        // Program.cs reads builder.Configuration["Jwt:Key"] directly at the top level,
        // before builder.Build() runs. ConfigureAppConfiguration's additions only land
        // once the host actually builds - too late for that line, so it always saw
        // "missing" regardless of what was registered here. UseSetting writes into the
        // config source WebApplicationBuilder.Configuration itself is built from, so it's
        // visible even to code that reads configuration before Build() is called - which
        // is exactly why it's the mechanism WebApplicationFactory docs recommend for this.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Key", "Integration-Test-Only-Secret-Key-32Chars!");
        builder.UseSetting("Jwt:Issuer", "FlowForge");
        builder.UseSetting("Jwt:Audience", "FlowForgeUsers");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenDays", "7");
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
