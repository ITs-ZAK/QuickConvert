namespace QuickConvert.Core.Jobs;

public enum ConversionPreset
{
    Economy,
    Balanced,
    Highest
}

public enum OutputDirectoryMode
{
    Adjacent
}

public sealed record ConvertFilesRequest(
    IReadOnlyList<string> Paths,
    string OutputFormat,
    ConversionPreset Preset,
    OutputDirectoryMode OutputDirectoryMode);

public sealed record JobExecutionResult(
    bool Success,
    IReadOnlyList<string> OutputPaths,
    string? ErrorCode,
    string? ErrorDetails)
{
    public static JobExecutionResult Failed(string code, string? details = null) =>
        new(false, [], code, details);
}

public sealed record ProcessCommand(string FileName, IReadOnlyList<string> Arguments)
{
    public bool UseShellExecute => false;
}

public sealed record DownloadMediaRequest(
    string RequestId,
    string Url,
    string MediaType,
    string MaxResolution);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessCommand command,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
