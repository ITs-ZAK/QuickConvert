namespace QuickConvert.Core.Conversion;

public static class FormatCatalog
{
    private static readonly HashSet<string> AudioInputs =
        ["mp3", "m4a", "aac", "wav", "flac", "ogg", "opus", "wma"];

    private static readonly HashSet<string> VideoInputs =
        ["mp4", "mkv", "webm", "mov", "avi", "wmv", "m4v"];

    private static readonly HashSet<string> ImageInputs =
        ["jpg", "jpeg", "png", "webp", "gif", "bmp", "tiff", "tif"];

    private static readonly string[] AudioOutputs = ["mp3", "m4a", "opus", "flac", "wav"];
    private static readonly string[] VideoOutputs = ["mp4", "mkv", "webm", .. AudioOutputs];
    private static readonly string[] ImageOutputs = ["jpg", "png", "webp", "gif"];

    public static IReadOnlyCollection<string> GetCompatibleOutputs(IEnumerable<string> paths)
    {
        HashSet<string>? common = null;

        foreach (var path in paths)
        {
            var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            var outputs = GetOutputs(extension);
            common = common is null
                ? new HashSet<string>(outputs, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(common.Intersect(outputs, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
        }

        return common?.Order(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
    }

    private static IReadOnlyCollection<string> GetOutputs(string extension)
    {
        if (AudioInputs.Contains(extension))
            return AudioOutputs;
        if (VideoInputs.Contains(extension))
            return VideoOutputs;
        if (ImageInputs.Contains(extension))
            return ImageOutputs;
        return [];
    }
}
