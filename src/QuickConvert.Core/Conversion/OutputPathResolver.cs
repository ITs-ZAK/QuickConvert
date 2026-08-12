namespace QuickConvert.Core.Conversion;

public static class OutputPathResolver
{
    public static string GetAvailablePath(
        string sourcePath,
        string targetExtension,
        Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExtension);
        ArgumentNullException.ThrowIfNull(fileExists);

        var extension = targetExtension.TrimStart('.').ToLowerInvariant();
        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var candidate = Path.Combine(directory, $"{baseName}.{extension}");
        var index = 1;

        while (fileExists(candidate))
            candidate = Path.Combine(directory, $"{baseName} ({index++}).{extension}");

        return candidate;
    }

    public static string GetTemporaryPath(string finalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        var directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
        var extension = Path.GetExtension(finalPath);
        var baseName = Path.GetFileNameWithoutExtension(finalPath);
        return Path.Combine(directory, $"{baseName}.quickconvert.partial{extension}");
    }
}
