using FlowForge.Application.Common.Abstractions;
using FlowForge.Shared.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace FlowForge.Infrastructure.Services.Storage;

/// <summary>
/// IFileStorage on MinIO (S3-compatible). The bucket is created on first use, so a fresh
/// `docker compose up` needs no manual setup. Failures come back as Results - a storage
/// outage should produce a clear error, not an unhandled 500.
/// </summary>
public sealed class MinioFileStorage : IFileStorage
{
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly ILogger<MinioFileStorage> _logger;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public MinioFileStorage(IConfiguration config, ILogger<MinioFileStorage> logger)
    {
        _logger = logger;
        _bucket = config["MinIO:BucketName"] ?? "flowforge-files";
        _client = new MinioClient()
            .WithEndpoint(config["MinIO:Endpoint"] ?? "localhost:9000")
            .WithCredentials(config["MinIO:AccessKey"] ?? "", config["MinIO:SecretKey"] ?? "")
            .WithSSL(bool.TryParse(config["MinIO:UseSSL"], out var ssl) && ssl)
            .Build();
    }

    public async Task<Result> UploadAsync(string key, Stream content, long size, string contentType, CancellationToken ct = default)
    {
        try
        {
            await EnsureBucketAsync(ct);
            await _client.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key)
                .WithStreamData(content)
                .WithObjectSize(size)
                .WithContentType(contentType), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload of {Key} to bucket {Bucket} failed", key, _bucket);
            return Result.Failure(Error.Failure("Storage.UploadFailed", "The file couldn't be stored. Try again in a moment."));
        }
    }

    public async Task<Result> DownloadAsync(string key, Stream destination, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key)
                .WithCallbackStream((stream, token) => stream.CopyToAsync(destination, token)), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download of {Key} from bucket {Bucket} failed", key, _bucket);
            return Result.Failure(Error.Failure("Storage.DownloadFailed", "The file couldn't be retrieved."));
        }
    }

    public async Task<Result> DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(key), ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Delete of {Key} from bucket {Bucket} failed", key, _bucket);
            return Result.Failure(Error.Failure("Storage.DeleteFailed", "The stored file couldn't be deleted."));
        }
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketReady) return;
        await _bucketLock.WaitAsync(ct);
        try
        {
            if (_bucketReady) return;
            if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket), ct))
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), ct);
            _bucketReady = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }
}
