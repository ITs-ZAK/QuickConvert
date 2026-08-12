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
            "mp4" or "mkv" => ["-c:v", "libx264", "-crf", "20", "-c:a", "aac", "-b:a", "192k"],
            "webm" => ["-c:v", "libvpx-vp9", "-crf", "30", "-b:v", "0", "-c:a", "libopus", "-b:a", "128k"],
            "mp3" => ["-vn", "-c:a", "libmp3lame", "-b:a", "192k"],
            "m4a" => ["-vn", "-c:a", "aac", "-b:a", "192k"],
            "opus" => ["-vn", "-c:a", "libopus", "-b:a", "128k"],
            "flac" => ["-vn", "-c:a", "flac"],
            "wav" => ["-vn", "-c:a", "pcm_s16le"],
            "jpg" => ["-frames:v", "1", "-q:v", "2"],
            "png" => ["-frames:v", "1"],
            "webp" => ["-c:v", "libwebp", "-quality", "90"],
            "gif" => ["-f", "gif"],
            _ => throw new ArgumentOutOfRangeException(nameof(targetFormat), "Nieobsługiwany format wyjściowy.")
        });

        arguments.AddRange(["-progress", "pipe:1", "-nostats", outputPath]);
        return new ProcessCommand("ffmpeg.exe", arguments);
    }
}
