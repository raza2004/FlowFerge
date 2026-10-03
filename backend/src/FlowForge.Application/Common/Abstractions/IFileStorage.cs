using FlowForge.Shared.Results;

namespace FlowForge.Application.Common.Abstractions;

/// <summary>Object storage for uploaded files (MinIO / any S3-compatible store in Infrastructure).</summary>
public interface IFileStorage
{
    Task<Result> UploadAsync(string key, Stream content, long size, string contentType, CancellationToken ct = default);

    /// <summary>Copies the stored object into <paramref name="destination"/>.</summary>
    Task<Result> DownloadAsync(string key, Stream destination, CancellationToken ct = default);

    Task<Result> DeleteAsync(string key, CancellationToken ct = default);
}
