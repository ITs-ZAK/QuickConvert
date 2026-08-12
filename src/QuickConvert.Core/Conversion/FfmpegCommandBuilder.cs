using QuickConvert.Core.Jobs;

namespace QuickConvert.Core.Conversion;

public static class FfmpegCommandBuilder
{
    public static ProcessCommand Build(
        string inputPath,
        string outputPath,
        string targetFormat,
        ConversionPreset preset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var format = targetFormat.TrimStart('.').ToLowerInvariant();
        var arguments = new List<string>
        {
            "-hide_banner", "-nostdin", "-y", "-i", inputPath
        };

        arguments.AddRange(format switch
        {
            "mp4" or "mkv" => ["-c:v", "libx264", "-crf", H264Crf(preset), "-c:a", "aac", "-b:a", H264AudioBitrate(preset)],
            "webm" => ["-c:v", "libvpx-vp9", "-crf", Vp9Crf(preset), "-b:v", "0", "-c:a", "libopus", "-b:a", OpusBitrate(preset)],
            "mp3" => ["-vn", "-c:a", "libmp3lame", "-b:a", GeneralAudioBitrate(preset)],
            "m4a" => ["-vn", "-c:a", "aac", "-b:a", GeneralAudioBitrate(preset)],
            "opus" => ["-vn", "-c:a", "libopus", "-b:a", OpusBitrate(preset)],
            "flac" => ["-vn", "-c:a", "flac"],
            "wav" => ["-vn", "-c:a", "pcm_s16le"],
            "jpg" => ["-frames:v", "1", "-q:v", JpegQuality(preset)],
            "png" => ["-frames:v", "1"],
            "webp" => ["-c:v", "libwebp", "-quality", WebpQuality(preset)],
            "gif" => ["-f", "gif"],
            _ => throw new ArgumentOutOfRangeException(nameof(targetFormat), "Nieobsługiwany format wyjściowy.")
        });

        arguments.AddRange(["-progress", "pipe:1", "-nostats", outputPath]);
        return new ProcessCommand("ffmpeg.exe", arguments);
    }

    private static string H264Crf(ConversionPreset preset) => preset switch
    {
        ConversionPreset.Economy => "28",
        ConversionPreset.Balanced => "23",
        ConversionPreset.Highest => "18",
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    private static string Vp9Crf(ConversionPreset preset) => preset switch
    {
        ConversionPreset.Economy => "38",
        ConversionPreset.Balanced => "33",
        ConversionPreset.Highest => "28",
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    private static string GeneralAudioBitrate(ConversionPreset preset) => preset switch
    {
        ConversionPreset.Economy => "128k",
        ConversionPreset.Balanced => "192k",
        ConversionPreset.Highest => "320k",
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    private static string H264AudioBitrate(ConversionPreset preset) => preset switch
    {
        ConversionPreset.Economy => "128k",
        ConversionPreset.Balanced => "192k",
        ConversionPreset.Highest => "256k",
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    private static string OpusBitrate(ConversionPreset preset) => preset switch
    {
        ConversionPreset.Economy => "96k",
        ConversionPreset.Balanced => "128k",
        ConversionPreset.Highest => "192k",
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    private static string JpegQuality(ConversionPreset preset) => preset switch
    {
        ConversionPreset.Economy => "5",
        ConversionPreset.Balanced => "3",
        ConversionPreset.Highest => "2",
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    private static string WebpQuality(ConversionPreset preset) => preset switch
    {
        ConversionPreset.Economy => "75",
        ConversionPreset.Balanced => "85",
        ConversionPreset.Highest => "95",
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };
}
