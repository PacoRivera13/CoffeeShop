namespace CoffeeShop.Application.Storage;

/// <summary>Provider-agnostic object storage: S3-compatible today (MinIO in development, S3 later).</summary>
public interface IFileStorage
{
    /// <summary>Stores the stream under the key, replacing any existing object.</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

    Task<bool> ExistsAsync(string key, CancellationToken ct);

    /// <summary>Returns a time-limited URL a browser can use to download the object without credentials.</summary>
    Task<Uri> GetDownloadUrlAsync(string key, TimeSpan expires, CancellationToken ct);

    /// <summary>Deletes the object; deleting a missing key is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}
