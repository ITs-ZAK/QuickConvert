using QuickConvert.Core.Jobs;
using QuickConvert.Core.Conversion;
using System.Text.Json;

namespace QuickConvert.Core.Messaging;

public sealed record NativeValidationResult(
    bool IsValid,
    string Code,
    DownloadMediaRequest? Request);

public static class NativeMessageValidator
{
    private static readonly HashSet<string> AllowedCallers = new(StringComparer.Ordinal)
    {
        "chrome-extension://abpjmchafogplinlgklgfoljglakhalp/",
        "quickconvert@local"
    };

    private static readonly HashSet<string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "mp4"
    };

    private static readonly HashSet<string> Resolutions = new(StringComparer.OrdinalIgnoreCase)
    {
        "best", "1080p", "720p", "480p"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static NativeValidationResult Validate(string json, string? callerOrigin)
    {
        if (callerOrigin is null || !AllowedCallers.Contains(callerOrigin))
            return Invalid("unauthorized_caller");

        if (string.IsNullOrWhiteSpace(json) || json.Length > 64 * 1024)
            return Invalid("invalid_request");

        NativeEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<NativeEnvelope>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Invalid("invalid_request");
        }

        if (envelope is null ||
            envelope.Version != 1 ||
            !string.Equals(envelope.Operation, "download", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(envelope.RequestId) ||
            envelope.RequestId.Length > 128 ||
            !MediaTypes.Contains(envelope.MediaType ?? string.Empty) ||
            !Resolutions.Contains(envelope.MaxResolution ?? string.Empty))
            return Invalid("invalid_request");

        var url = YoutubeUrlValidator.Validate(envelope.Url ?? string.Empty);
        if (!url.IsValid)
            return Invalid("unsupported_url");

        return new NativeValidationResult(
            true,
            "accepted",
            new DownloadMediaRequest(
                envelope.RequestId,
                url.NormalizedUrl!,
                envelope.MediaType!.ToLowerInvariant(),
                envelope.MaxResolution!.ToLowerInvariant()));
    }

    private static NativeValidationResult Invalid(string code) => new(false, code, null);

    private sealed record NativeEnvelope(
        int Version,
        string? RequestId,
        string? Operation,
        string? Url,
        string? MediaType,
        string? MaxResolution);
}
