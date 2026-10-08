using System.Text.Json;
using FlowForge.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FlowForge.Infrastructure.Services.Caching;

/// <summary>
/// ICache on Redis, built to fail open. Timeouts are short and commands fail immediately when
/// there's no connection (instead of queueing), and after any failure Redis is skipped entirely
/// for a cool-off period, so an outage costs one slow request rather than slowing every request.
/// </summary>
public sealed class RedisCache : ICache, IAsyncDisposable
{
    private const string KeyPrefix = "flowforge:";
    private static readonly TimeSpan CoolOff = TimeSpan.FromSeconds(15);

    private readonly ILogger<RedisCache> _logger;
    private readonly Lazy<Task<ConnectionMultiplexer>> _connection;
    private long _skipUntilTicks;

    public RedisCache(string connectionString, ILogger<RedisCache> logger)
    {
        _logger = logger;

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 500;
        options.SyncTimeout = 250;
        options.AsyncTimeout = 250;
        options.BacklogPolicy = BacklogPolicy.FailFast;

        _connection = new Lazy<Task<ConnectionMultiplexer>>(() => ConnectionMultiplexer.ConnectAsync(options));
    }

    private bool InCoolOff => Environment.TickCount64 < Interlocked.Read(ref _skipUntilTicks);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await RunAsync(db => db.StringGetAsync(KeyPrefix + key), default(RedisValue));
        if (value.IsNullOrEmpty) return default;

        try
        {
            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (JsonException ex)
        {
            // A value we can't read is just a miss (e.g. the shape changed between deployments).
            _logger.LogWarning(ex, "Ignoring unreadable cache entry {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) =>
        await RunAsync(db => db.StringSetAsync(KeyPrefix + key, JsonSerializer.Serialize(value), ttl), false);

    public async Task RemoveAsync(string key, CancellationToken ct = default) =>
        await RunAsync(db => db.KeyDeleteAsync(KeyPrefix + key), false);

    public async Task<long> IncrementAsync(string key, CancellationToken ct = default) =>
        await RunAsync(db => db.StringIncrementAsync(KeyPrefix + key), 0L);

    private async Task<TResult> RunAsync<TResult>(Func<IDatabase, Task<TResult>> operation, TResult fallback)
    {
        if (InCoolOff) return fallback;

        try
        {
            var connection = await _connection.Value;
            return await operation(connection.GetDatabase());
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or ObjectDisposedException)
        {
            Interlocked.Exchange(ref _skipUntilTicks, Environment.TickCount64 + (long)CoolOff.TotalMilliseconds);
            _logger.LogWarning("Redis unavailable, skipping the cache for {Seconds}s: {Message}", CoolOff.TotalSeconds, ex.Message);
            return fallback;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated && _connection.Value.IsCompletedSuccessfully)
            await _connection.Value.Result.DisposeAsync();
    }
}

/// <summary>Used when no Redis is configured: every read is a miss and writes go nowhere.</summary>
public sealed class NullCache : ICache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task<long> IncrementAsync(string key, CancellationToken ct = default) => Task.FromResult(0L);
}
