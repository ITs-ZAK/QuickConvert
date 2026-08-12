using System.Diagnostics;
using System.Text;

namespace QuickConvert.Core.Jobs;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class SystemProcessRunner : IProcessRunner
{
    public Task<ProcessResult> RunAsync(
        ProcessCommand command,
        IProgress<string>? progress,
        CancellationToken cancellationToken) =>
        RunCoreAsync(command, progress, cancellationToken);

    private static async Task<ProcessResult> RunCoreAsync(
        ProcessCommand command,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in command.Arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"Nie można uruchomić {Path.GetFileName(command.FileName)}.");

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between the state check and Kill.
            }
        });

        var output = new StringBuilder();
        var error = new StringBuilder();
        var outputTask = PumpAsync(process.StandardOutput, output, progress, cancellationToken);
        var errorTask = PumpAsync(process.StandardError, error, progress, cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private static async Task PumpAsync(
        TextReader reader,
        StringBuilder destination,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            destination.AppendLine(line);
            progress?.Report(line);
        }
    }
}
