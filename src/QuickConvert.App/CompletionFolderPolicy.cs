using System.IO;
using QuickConvert.Core.Jobs;

namespace QuickConvert.App;

public static class CompletionFolderPolicy
{
    public static string? GetFolder(
        string kind,
        JobStatus status,
        bool enabled,
        IReadOnlyList<string> outputPaths)
    {
        if (!enabled ||
            !string.Equals(kind, "convert", StringComparison.Ordinal) ||
            status != JobStatus.Completed ||
            outputPaths.Count == 0)
            return null;

        var directory = Path.GetDirectoryName(outputPaths[0]);
        return string.IsNullOrWhiteSpace(directory) ? null : directory;
    }
}
