using QuickConvert.Core.Jobs;

namespace QuickConvert.Core.Conversion;

public sealed class ConversionEngine
{
    private readonly string _ffmpegPath;
    private readonly IProcessRunner _processRunner;
    private readonly string _downloadsDirectory;

    public ConversionEngine(
        string ffmpegPath,
        IProcessRunner processRunner,
        string? downloadsDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        _ffmpegPath = ffmpegPath;
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _downloadsDirectory = downloadsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "QuickConvert");
    }

    public async Task<JobExecutionResult> ConvertAsync(
        ConvertFilesRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Paths.Count == 0)
            return JobExecutionResult.Failed("invalid_request");

        if (request.Paths.Any(path => !File.Exists(path)))
            return JobExecutionResult.Failed("source_missing");

        var compatible = FormatCatalog.GetCompatibleOutputs(request.Paths);
        if (!compatible.Contains(request.OutputFormat, StringComparer.OrdinalIgnoreCase))
            return JobExecutionResult.Failed("unsupported_format");

        var outputs = new List<string>();
        foreach (var sourcePath in request.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? temporaryPath = null;

            try
            {
                var outputDirectory = request.OutputDirectoryMode switch
                {
                    OutputDirectoryMode.Adjacent => Path.GetDirectoryName(sourcePath) ?? string.Empty,
                    OutputDirectoryMode.DownloadsQuickConvert => _downloadsDirectory,
                    _ => throw new ArgumentOutOfRangeException(nameof(request.OutputDirectoryMode))
                };
                Directory.CreateDirectory(outputDirectory);
                var finalPath = OutputPathResolver.GetAvailablePath(
                    sourcePath, request.OutputFormat, outputDirectory, File.Exists);
                temporaryPath = OutputPathResolver.GetTemporaryPath(finalPath);

                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);

                var command = FfmpegCommandBuilder.Build(
                    sourcePath, temporaryPath, request.OutputFormat, request.Preset) with
                {
                    FileName = _ffmpegPath
                };
                var result = await _processRunner.RunAsync(
                    command, progress, cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0 || !File.Exists(temporaryPath))
                    return new JobExecutionResult(
                        false, outputs, ToolErrorClassifier.Classify(result.StandardError), Tail(result.StandardError));

                File.Move(temporaryPath, finalPath);
                outputs.Add(finalPath);
            }
            catch (OperationCanceledException)
            {
                DeletePartial(temporaryPath);
                return new JobExecutionResult(false, outputs, "canceled", null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DeletePartial(temporaryPath);
                return new JobExecutionResult(false, outputs, "output_unavailable", exception.Message);
            }
            finally
            {
                DeletePartial(temporaryPath);
            }
        }

        return new JobExecutionResult(true, outputs, null, null);
    }

    private static void DeletePartial(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string Tail(string value) =>
        value.Length <= 2000 ? value : value[^2000..];
}
