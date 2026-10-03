using FlowForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Xunit;

namespace FlowForge.API.IntegrationTests;

/// <summary>
/// Boots the real API (real MediatR pipeline, real EF Core, real domain event
/// dispatch/automation handlers) against throwaway PostgreSQL and MinIO containers instead
/// of mocks, so persistence and file storage are exercised end to end.
///
/// RabbitMQ is deliberately not started: MassTransit connects in the background and the
/// API keeps working without a broker (publishing email/Slack jobs just fails and is
/// logged), and OpenAI/SMTP/Slack clients are only constructed when a request needs them.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("flowforge_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder("minio/minio:RELEASE.2025-04-22T22-12-26Z").Build();

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

        builder.UseSetting("MinIO:Endpoint", $"{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}");
        builder.UseSetting("MinIO:AccessKey", _minio.GetAccessKey());
        builder.UseSetting("MinIO:SecretKey", _minio.GetSecretKey());
        builder.UseSetting("MinIO:BucketName", "flowforge-test");
        builder.UseSetting("MinIO:UseSSL", "false");
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());

        // Accessing Services builds the host (ConfigureWebHost runs here), by which
        // point the container above is already up and its connection string is real.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowForgeDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _minio.DisposeAsync();
        await base.DisposeAsync();
    }
}
