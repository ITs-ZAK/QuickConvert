using QuickConvert.Core.Jobs;

namespace QuickConvert.Core.Conversion;

public sealed class ConversionEngine
{
    private readonly string _ffmpegPath;
    private readonly IProcessRunner _processRunner;

    public ConversionEngine(string ffmpegPath, IProcessRunner processRunner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        _ffmpegPath = ffmpegPath;
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
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
            var finalPath = OutputPathResolver.GetAvailablePath(
                sourcePath, request.OutputFormat, File.Exists);
            var temporaryPath = OutputPathResolver.GetTemporaryPath(finalPath);

            try
            {
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
            finally
            {
                DeletePartial(temporaryPath);
            }
        }

        return new JobExecutionResult(true, outputs, null, null);
    }

    private static void DeletePartial(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string Tail(string value) =>
        value.Length <= 2000 ? value : value[^2000..];
}
