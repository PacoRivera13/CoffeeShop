using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CoffeeShop.Application.Storage;
using CoffeeShop.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoffeeShop.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Endpoint the API itself talks to, e.g. http://minio:9000 inside Docker.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Endpoint used only to sign download URLs a browser can reach, e.g. http://localhost:9000.</summary>
    public string PublicEndpoint { get; set; } = "";

    public string Bucket { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
    public bool CreateBucketIfMissing { get; set; }
}

/// <summary>
/// S3 adapter that works against MinIO and real S3. One client talks to the internal endpoint; a second one,
/// built on the public endpoint, only signs URLs, because a signature is bound to the host it was made for.
/// </summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    /// <summary>S3 rejects presigned URLs valid for longer than 7 days.</summary>
    public static readonly TimeSpan MaxUrlLifetime = TimeSpan.FromDays(7);

    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _presigner;
    private readonly string _bucket;
    private readonly TimeProvider _clock;

    public S3FileStorage(IOptions<StorageOptions> options, [FromKeyedServices(Clocks.Real)] TimeProvider clock)
    {
        var settings = options.Value;
        var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);

        _client = CreateClient(credentials, settings.Endpoint, settings);
        _presigner = string.IsNullOrWhiteSpace(settings.PublicEndpoint)
            ? _client
            : CreateClient(credentials, settings.PublicEndpoint, settings);
        _bucket = settings.Bucket;
        _clock = clock;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct) =>
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, ct);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<DownloadUrl> GetDownloadUrlAsync(string key, TimeSpan expires, CancellationToken ct)
    {
        if (expires <= TimeSpan.Zero || expires > MaxUrlLifetime)
            throw new ArgumentOutOfRangeException(nameof(expires), expires, $"Download URLs must expire within {MaxUrlLifetime.TotalDays} days.");

        var expiresAt = _clock.GetUtcNow() + expires;
        var url = await _presigner.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
            Protocol = _presigner.Config.UseHttp ? Protocol.HTTP : Protocol.HTTPS,
        });
        return new DownloadUrl(new Uri(url), expiresAt);
    }

    /// <summary>
    /// Lists at most one key: it proves the endpoint answers, the credentials may read the bucket and the bucket exists,
    /// and it honours cancellation, so a timed-out readiness check leaves no request behind.
    /// </summary>
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        try
        {
            await _client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket, MaxKeys = 1 }, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct) =>
        await _client.DeleteObjectAsync(_bucket, key, ct);

    /// <summary>Creates the bucket when it is missing. It gets no policy, so it stays private.</summary>
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }
    }

    public void Dispose()
    {
        if (!ReferenceEquals(_presigner, _client))
            _presigner.Dispose();
        _client.Dispose();
    }

    private static AmazonS3Client CreateClient(AWSCredentials credentials, string endpoint, StorageOptions settings) =>
        new(credentials, new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = settings.Region,
            ForcePathStyle = settings.ForcePathStyle,
            UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            // SDK v4 adds integrity checksums to every request by default; some S3-compatible servers reject them.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
}
