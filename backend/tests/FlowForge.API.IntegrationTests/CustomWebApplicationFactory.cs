using FlowForge.Application.Common.Abstractions;
using FlowForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace FlowForge.API.IntegrationTests;

/// <summary>
/// Boots the real API (real MediatR pipeline, real EF Core, real domain event
/// dispatch/automation handlers) against a throwaway PostgreSQL container instead of
/// mocks - this is what actually exercises the reentrant-SaveChanges domain event fix
/// and the AuditLoggingBehavior end to end, not just through mocked repositories.
///
/// File storage is swapped for an in-memory IFileStorage (see InMemoryFileStorage), so no
/// MinIO container is needed. RabbitMQ is deliberately not started either: MassTransit
/// connects in the background and the API keeps working without a broker (publishing
/// email/Slack jobs just fails and is logged), and OpenAI/SMTP/Slack clients are only
/// constructed when a request needs them.
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

        // The suite registers and logs in dozens of times a minute from one address; the real
        // limits would throttle the tests themselves. RateLimitingFlowTests turns them down on purpose.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage, InMemoryFileStorage>();
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
