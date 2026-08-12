using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using QuickConvert.Core.Jobs;
using QuickConvert.Core.Updates;

namespace QuickConvert.App;

internal sealed record UpdateState(
    DateTimeOffset? LastYtDlpCheck,
    DateTimeOffset? LastApplicationCheck);

internal sealed class UpdateCoordinator(string statePath, string ytDlpPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<GitHubReleaseInfo?> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        GitHubReleaseInfo? release = null;

        if (File.Exists(ytDlpPath) &&
            UpdateSchedule.ShouldCheck(state.LastYtDlpCheck, now, TimeSpan.FromDays(1)))
        {
            try
            {
                await new SystemProcessRunner().RunAsync(
                    new ProcessCommand(ytDlpPath, ["-U"]),
                    null,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is IOException or InvalidOperationException)
            {
                // The last working executable remains available.
            }
            state = state with { LastYtDlpCheck = now };
        }

        if (UpdateSchedule.ShouldCheck(
            state.LastApplicationCheck, now, TimeSpan.FromDays(7)))
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                client.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("QuickConvert", currentVersion.ToString()));
                var json = await client.GetStringAsync(
                    ReleaseEndpoints.LatestReleaseApi,
                    cancellationToken);
                release = GitHubReleaseParser.Parse(json, currentVersion);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                // Update checks never block local conversion.
            }
            state = state with { LastApplicationCheck = now };
        }

        await SaveAsync(state, cancellationToken);
        return release;
    }

    private async Task<UpdateState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath))
            return new UpdateState(null, null);
        try
        {
            await using var stream = File.OpenRead(statePath);
            return await JsonSerializer.DeserializeAsync<UpdateState>(
                stream, JsonOptions, cancellationToken) ?? new UpdateState(null, null);
        }
        catch (JsonException)
        {
            return new UpdateState(null, null);
        }
    }

    private async Task SaveAsync(UpdateState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var temporary = $"{statePath}.tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
        File.Move(temporary, statePath, true);
    }
}
