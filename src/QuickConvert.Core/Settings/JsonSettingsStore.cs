using System.Text.Json;
using System.Text.Json.Serialization;
using QuickConvert.Core.Jobs;

namespace QuickConvert.Core.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public JsonSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public async Task<QuickConvertSettings> LoadAsync()
    {
        if (!File.Exists(_path))
            return QuickConvertSettings.Defaults;

        try
        {
            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer.DeserializeAsync<StoredSettings>(
                stream,
                Options).ConfigureAwait(false);
            return IsValid(settings)
                ? new QuickConvertSettings(
                    settings!.QualityPreset,
                    settings.OutputDirectoryMode,
                    settings.OpenFolderOnCompletion,
                    settings.RunInBackgroundDuringJobs ?? true)
                : QuickConvertSettings.Defaults;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return QuickConvertSettings.Defaults;
        }
    }

    public async Task SaveAsync(QuickConvertSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _saveGate.WaitAsync().ConfigureAwait(false);
        var temporaryPath = $"{_path}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(settings, Options);
            await File.WriteAllTextAsync(temporaryPath, json).ConfigureAwait(false);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            TryDeleteTemporary(temporaryPath);
            _saveGate.Release();
        }
    }

    private static bool IsValid(StoredSettings? settings) =>
        settings is not null &&
        Enum.IsDefined(settings.QualityPreset) &&
        Enum.IsDefined(settings.OutputDirectoryMode);

    private sealed record StoredSettings(
        ConversionPreset QualityPreset,
        OutputDirectoryMode OutputDirectoryMode,
        bool OpenFolderOnCompletion,
        bool? RunInBackgroundDuringJobs);

    private static void TryDeleteTemporary(string path)
    {
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
}
