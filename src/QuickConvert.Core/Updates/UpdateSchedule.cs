using System.Text.Json;

namespace QuickConvert.Core.Updates;

public static class UpdateSchedule
{
    public static bool ShouldCheck(
        DateTimeOffset? lastCheck,
        DateTimeOffset now,
        TimeSpan interval) =>
        lastCheck is null || now - lastCheck.Value >= interval;
}

public sealed record GitHubReleaseInfo(Version Version, string Url);

public static class GitHubReleaseParser
{
    public static GitHubReleaseInfo? Parse(string json, Version currentVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(currentVersion);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
        var url = root.GetProperty("html_url").GetString();
        if (!Version.TryParse(tag, out var version) ||
            version <= currentVersion ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return null;
        return new GitHubReleaseInfo(version, uri.AbsoluteUri);
    }
}
