using QuickConvert.Core.Jobs;

namespace QuickConvert.Core.Conversion;

public sealed class DownloadEngine
{
    private readonly string _ytDlpPath;
    private readonly IProcessRunner _processRunner;

    public DownloadEngine(string ytDlpPath, IProcessRunner processRunner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ytDlpPath);
        _ytDlpPath = ytDlpPath;
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<JobExecutionResult> DownloadAsync(
        DownloadMediaRequest request,
        string outputDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var validation = YoutubeUrlValidator.Validate(request.Url);
        if (!validation.IsValid)
            return JobExecutionResult.Failed("unsupported_url");

        Directory.CreateDirectory(outputDirectory);
        var preexistingPartials = Directory
            .EnumerateFiles(outputDirectory, "*quickconvert.partial*", SearchOption.TopDirectoryOnly)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedRequest = request with { Url = validation.NormalizedUrl! };
        var command = YtDlpCommandBuilder.Build(normalizedRequest, outputDirectory) with
        {
            FileName = _ytDlpPath
        };

        try
        {
            var result = await _processRunner.RunAsync(
                command, progress, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                DeleteNewPartials(outputDirectory, preexistingPartials);
                return JobExecutionResult.Failed(
                    ToolErrorClassifier.Classify(result.StandardError),
                    Tail(result.StandardError));
            }

            var output = result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(line => line.StartsWith("QC_OUTPUT:", StringComparison.Ordinal));
            if (output is null)
                return JobExecutionResult.Failed("tool_failed", "Narzędzie nie podało ścieżki wyniku.");

            var outputPath = Path.GetFullPath(output["QC_OUTPUT:".Length..]);
            var root = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!outputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(outputPath))
                return JobExecutionResult.Failed("tool_failed", "Nie znaleziono bezpiecznej ścieżki wyniku.");

            const string marker = ".quickconvert.partial";
            var markerIndex = outputPath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < root.Length)
                return JobExecutionResult.Failed("tool_failed", "Narzędzie podało nieprawidłową nazwę wyniku.");

            var proposedFinalPath = outputPath.Remove(markerIndex, marker.Length);
            var finalPath = GetAvailableDownloadPath(proposedFinalPath);
            File.Move(outputPath, finalPath);
            return new JobExecutionResult(true, [finalPath], null, null);
        }
        catch (OperationCanceledException)
        {
            DeleteNewPartials(outputDirectory, preexistingPartials);
            return JobExecutionResult.Failed("canceled");
        }
    }

    private static string Tail(string value) =>
        value.Length <= 2000 ? value : value[^2000..];

    private static string GetAvailableDownloadPath(string proposedPath)
    {
        if (!File.Exists(proposedPath))
            return proposedPath;
        var directory = Path.GetDirectoryName(proposedPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(proposedPath);
        var extension = Path.GetExtension(proposedPath);
        var index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name} ({index++}){extension}");
        } while (File.Exists(candidate));
        return candidate;
    }

    private static void DeleteNewPartials(string outputDirectory, ISet<string> preexisting)
    {
        foreach (var path in Directory.EnumerateFiles(
                     outputDirectory, "*quickconvert.partial*", SearchOption.TopDirectoryOnly))
        {
            if (!preexisting.Contains(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // The tool may still be releasing its last file handle.
                }
            }
        }
    }
}
