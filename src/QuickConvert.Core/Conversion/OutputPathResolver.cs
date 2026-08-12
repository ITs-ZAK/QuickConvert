namespace QuickConvert.Core.Conversion;

public static class OutputPathResolver
{
    public static string GetAvailablePath(
        string sourcePath,
        string targetExtension,
        Func<string, bool> fileExists)
    {
        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        return GetAvailablePath(sourcePath, targetExtension, directory, fileExists);
    }

    public static string GetAvailablePath(
        string sourcePath,
        string targetExtension,
        string outputDirectory,
        Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExtension);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(fileExists);

        var extension = targetExtension.TrimStart('.').ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var candidate = Path.Combine(outputDirectory, $"{baseName}.{extension}");
        var index = 1;

        while (fileExists(candidate))
            candidate = Path.Combine(outputDirectory, $"{baseName} ({index++}).{extension}");

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
