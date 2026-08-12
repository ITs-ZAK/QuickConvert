namespace QuickConvert.Core.Jobs;

public static class ToolErrorClassifier
{
    public static string Classify(string? error)
    {
        var value = error ?? string.Empty;
        if (ContainsAny(value, "no space left", "not enough space", "disk full"))
            return "disk_full";
        if (ContainsAny(value, "video unavailable", "private video", "this video is not available", "requested format is not available"))
            return "media_unavailable";
        if (ContainsAny(value, "unable to download", "connection timed out", "network is unreachable", "temporary failure in name resolution", "http error 429"))
            return "network_error";
        return "tool_failed";
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
