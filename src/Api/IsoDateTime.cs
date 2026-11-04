using System.Globalization;

namespace CoffeeShop.Api;

internal static class IsoDateTime
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
    ];

    /// <summary>Parses ISO 8601 only; a value without an offset is read as UTC.</summary>
    public static bool TryParse(string value, out DateTimeOffset result) =>
        DateTimeOffset.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
}
