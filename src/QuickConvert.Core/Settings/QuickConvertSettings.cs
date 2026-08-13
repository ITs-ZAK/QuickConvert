using QuickConvert.Core.Jobs;

namespace QuickConvert.Core.Settings;

public sealed record QuickConvertSettings(
    ConversionPreset QualityPreset,
    OutputDirectoryMode OutputDirectoryMode,
    bool OpenFolderOnCompletion,
    bool RunInBackgroundDuringJobs)
{
    public static QuickConvertSettings Defaults { get; } = new(
        ConversionPreset.Balanced,
        OutputDirectoryMode.Adjacent,
        false,
        true);
}
