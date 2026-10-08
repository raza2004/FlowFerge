using System.Diagnostics;
using FlowForge.Infrastructure.Services.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowForge.API.IntegrationTests;

/// <summary>
/// The cache's one hard requirement is that Redis problems never become the app's problems.
/// Port 1 is never listening, so this is a genuinely unreachable Redis without needing a container.
/// </summary>
public class RedisCacheTests
{
    private static RedisCache Unreachable() => new("localhost:1", NullLogger<RedisCache>.Instance);

    [Fact]
    public async Task WhenRedisIsDown_ReadsMissAndWritesAreDropped_WithoutThrowing()
    {
        await using var cache = Unreachable();

        (await cache.GetAsync<string>("k")).Should().BeNull();
        await cache.Invoking(c => c.SetAsync("k", "v", TimeSpan.FromMinutes(1))).Should().NotThrowAsync();
        await cache.Invoking(c => c.RemoveAsync("k")).Should().NotThrowAsync();
        (await cache.IncrementAsync("counter")).Should().Be(0);
    }

    [Fact]
    public async Task AfterOneFailure_RedisIsSkippedSoAnOutageDoesNotSlowEveryRequest()
    {
        await using var cache = Unreachable();
        await cache.GetAsync<string>("warm-up");

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 50; i++)
            await cache.GetAsync<string>($"k{i}");
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "calls during the cool-off return immediately");
    }

    [Fact]
    public async Task TheNullCache_IsAlwaysAMiss()
    {
        var cache = new NullCache();

        await cache.SetAsync("k", "v", TimeSpan.FromMinutes(1));

        (await cache.GetAsync<string>("k")).Should().BeNull();
        (await cache.IncrementAsync("c")).Should().Be(0);
    }
}
