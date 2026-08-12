namespace QuickConvert.Core.Conversion;

public sealed record YoutubeUrlValidation(bool IsValid, string? NormalizedUrl, string? Error);

public static class YoutubeUrlValidator
{
    public static YoutubeUrlValidation Validate(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return Invalid("Adres musi używać HTTPS.");

        var host = uri.IdnHost.ToLowerInvariant();
        if (host == "youtu.be")
        {
            var videoId = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return IsVideoId(videoId)
                ? Valid($"https://youtu.be/{videoId}")
                : Invalid("Adres nie zawiera identyfikatora filmu.");
        }

        if (host != "youtube.com" && !host.EndsWith(".youtube.com", StringComparison.Ordinal))
            return Invalid("Obsługiwane są wyłącznie adresy YouTube.");

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 2 &&
            string.Equals(segments[0], "shorts", StringComparison.OrdinalIgnoreCase) &&
            IsVideoId(segments[1]))
            return Valid($"https://www.youtube.com/shorts/{segments[1]}");

        if (!string.Equals(uri.AbsolutePath.TrimEnd('/'), "/watch", StringComparison.OrdinalIgnoreCase))
            return Invalid("Obsługiwane są filmy i Shorts.");

        var query = ParseQuery(uri.Query);
        return query.TryGetValue("v", out var id) && IsVideoId(id)
            ? Valid($"https://www.youtube.com/watch?v={Uri.EscapeDataString(id)}")
            : Invalid("Adres nie zawiera identyfikatora filmu.");
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => Uri.UnescapeDataString(parts[0]), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => Uri.UnescapeDataString(group.First()[1]),
                StringComparer.OrdinalIgnoreCase);

    private static bool IsVideoId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length is >= 3 and <= 32 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static YoutubeUrlValidation Valid(string url) => new(true, url, null);
    private static YoutubeUrlValidation Invalid(string error) => new(false, null, error);
}
