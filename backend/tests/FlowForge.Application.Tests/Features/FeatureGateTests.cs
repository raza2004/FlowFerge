using System.Collections.Concurrent;
using System.Text.Json;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Application.Features;
using FlowForge.Domain.Common;
using FlowForge.Domain.Features;
using FlowForge.Domain.Features.Repositories;
using FluentAssertions;
using Moq;

namespace FlowForge.Application.Tests.Features;

public class FeatureGateTests
{
    private readonly Mock<IFeatureFlagRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly InMemoryCache _cache = new();
    private readonly List<FeatureFlag> _flags = new();

    public FeatureGateTests()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _flags);
        _uow.SetupGet(u => u.FeatureFlags).Returns(_repo.Object);
    }

    private FeatureGate Gate() => new(_uow.Object, _cache);

    private FeatureFlag AddFlag(string key, bool enabled)
    {
        var flag = FeatureFlag.Create(key, key, "d", enabled).Value;
        _flags.Add(flag);
        return flag;
    }

    [Fact]
    public async Task AFeatureWithNoFlagRow_IsOn_AndUnknownKeysAreOn()
    {
        (await Gate().IsEnabledAsync(FeatureKeys.Sprints, Guid.NewGuid())).Should().BeTrue();
        (await Gate().IsEnabledAsync("not-a-real-feature", Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public async Task GlobalOffAndWorkspaceOverrides_AreAppliedPerWorkspace()
    {
        var flag = AddFlag(FeatureKeys.AiAssistant, enabled: false);
        var allowed = Guid.NewGuid();
        var other = Guid.NewGuid();
        flag.SetOverride(allowed, true);

        (await Gate().IsEnabledAsync(FeatureKeys.AiAssistant, allowed)).Should().BeTrue();
        (await Gate().IsEnabledAsync(FeatureKeys.AiAssistant, other)).Should().BeFalse();
    }

    [Fact]
    public async Task TheMap_ForOneWorkspace_IsReadFromTheDatabaseOnce_ThenServedFromTheCache()
    {
        AddFlag(FeatureKeys.Sprints, enabled: false);
        var tenant = Guid.NewGuid();

        await Gate().IsEnabledAsync(FeatureKeys.Sprints, tenant);
        await Gate().IsEnabledAsync(FeatureKeys.Attachments, tenant);
        await Gate().GetAllForTenantAsync(tenant);

        _repo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WorkspacesAreCachedSeparately()
    {
        var flag = AddFlag(FeatureKeys.Sprints, enabled: true);
        var blocked = Guid.NewGuid();
        var normal = Guid.NewGuid();
        flag.SetOverride(blocked, false);

        (await Gate().IsEnabledAsync(FeatureKeys.Sprints, blocked)).Should().BeFalse();
        (await Gate().IsEnabledAsync(FeatureKeys.Sprints, normal)).Should().BeTrue();
        (await Gate().IsEnabledAsync(FeatureKeys.Sprints, blocked)).Should().BeFalse("the cached answer for one workspace must not leak to another");
    }

    [Fact]
    public async Task Invalidating_DropsEveryCachedAnswer_SoAdminChangesApplyAtOnce()
    {
        var flag = AddFlag(FeatureKeys.Attachments, enabled: true);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        (await Gate().IsEnabledAsync(FeatureKeys.Attachments, tenantA)).Should().BeTrue();
        (await Gate().IsEnabledAsync(FeatureKeys.Attachments, tenantB)).Should().BeTrue();

        flag.SetEnabled(false);
        (await Gate().IsEnabledAsync(FeatureKeys.Attachments, tenantA)).Should().BeTrue("still the cached answer until invalidated");

        await Gate().InvalidateAsync();

        (await Gate().IsEnabledAsync(FeatureKeys.Attachments, tenantA)).Should().BeFalse();
        (await Gate().IsEnabledAsync(FeatureKeys.Attachments, tenantB)).Should().BeFalse();
    }

    [Fact]
    public async Task WithACacheThatStoresNothing_EveryAnswerComesStraightFromTheDatabase()
    {
        var flag = AddFlag(FeatureKeys.Sprints, enabled: true);
        var gate = new FeatureGate(_uow.Object, new NothingCache());
        var tenant = Guid.NewGuid();

        (await gate.IsEnabledAsync(FeatureKeys.Sprints, tenant)).Should().BeTrue();
        flag.SetEnabled(false);
        (await gate.IsEnabledAsync(FeatureKeys.Sprints, tenant)).Should().BeFalse();
    }

    /// <summary>Stands in for "no Redis configured": every read misses and nothing is stored.</summary>
    private sealed class NothingCache : ICache
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> IncrementAsync(string key, CancellationToken ct = default) => Task.FromResult(0L);
    }

    /// <summary>JSON round-trips like Redis does, so a type that wouldn't survive caching fails here too.</summary>
    private sealed class InMemoryCache : ICache
    {
        private readonly ConcurrentDictionary<string, string> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
            Task.FromResult(_store.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json) : default);

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        {
            _store[key] = JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _store.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<long> IncrementAsync(string key, CancellationToken ct = default)
        {
            var next = _store.TryGetValue(key, out var current) ? long.Parse(current) + 1 : 1;
            _store[key] = next.ToString();
            return Task.FromResult(next);
        }
    }
}
