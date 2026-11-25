namespace CoffeeShop.Application.Storage;

public sealed record DownloadUrl(Uri Url, DateTimeOffset ExpiresAt);

/// <summary>Provider-agnostic object storage: S3-compatible today (MinIO in development, S3 later).</summary>
public interface IFileStorage
{
    /// <summary>Stores the stream under the key, replacing any existing object.</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

    Task<bool> ExistsAsync(string key, CancellationToken ct);

    /// <summary>
    /// Returns a time-limited URL a browser can use to download the object without credentials, with the instant it
    /// stops working (computed by the signer, on the clock the storage service checks it against).
    /// </summary>
    Task<DownloadUrl> GetDownloadUrlAsync(string key, TimeSpan expires, CancellationToken ct);

    /// <summary>True when the storage is reachable and its bucket exists; for readiness checks.</summary>
    Task<bool> IsReadyAsync(CancellationToken ct);

    /// <summary>Deletes the object; deleting a missing key is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}
