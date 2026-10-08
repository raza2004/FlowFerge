using System.Collections.Concurrent;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Shared.Results;

namespace FlowForge.API.IntegrationTests;

/// <summary>
/// Stand-in for MinIO in integration tests, so the suite needs no storage container (and
/// can't be broken by a registry change). Everything above IFileStorage - validation,
/// tenant checks, DTOs, download headers - still runs for real; only the bytes' storage differs.
/// </summary>
public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public int ObjectCount => _objects.Count;

    public async Task<Result> UploadAsync(string key, Stream content, long size, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _objects[key] = buffer.ToArray();
        return Result.Success();
    }

    public async Task<Result> DownloadAsync(string key, Stream destination, CancellationToken ct = default)
    {
        if (!_objects.TryGetValue(key, out var bytes))
            return Result.Failure(Error.NotFound("Storage.NotFound", "Stored file not found"));

        await destination.WriteAsync(bytes, ct);
        return Result.Success();
    }

    public Task<Result> DeleteAsync(string key, CancellationToken ct = default)
    {
        _objects.TryRemove(key, out _);
        return Task.FromResult(Result.Success());
    }
}
