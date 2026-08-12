using QuickConvert.Core.Jobs;

namespace QuickConvert.Core.Conversion;

public static class YtDlpCommandBuilder
{
    public static ProcessCommand Build(DownloadMediaRequest request, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var arguments = new List<string>
        {
            "--no-playlist",
            "--newline",
            "--no-warnings",
            "--progress",
            "--print", "after_move:QC_OUTPUT:%(filepath)s",
            "-P", outputDirectory,
            "-o", "%(title).200B [%(id)s].quickconvert.partial.%(ext)s"
        };

        if (string.Equals(request.MediaType, "mp3", StringComparison.OrdinalIgnoreCase))
        {
            arguments.AddRange(["-f", "bestaudio/best", "-x", "--audio-format", "mp3", "--audio-quality", "192K"]);
        }
        else if (string.Equals(request.MediaType, "mp4", StringComparison.OrdinalIgnoreCase))
        {
            var format = request.MaxResolution.ToLowerInvariant() switch
            {
                "best" => "bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/b",
                "1080p" => "bv*[height<=1080][ext=mp4]+ba[ext=m4a]/b[height<=1080][ext=mp4]/b[height<=1080]",
                "720p" => "bv*[height<=720][ext=mp4]+ba[ext=m4a]/b[height<=720][ext=mp4]/b[height<=720]",
                "480p" => "bv*[height<=480][ext=mp4]+ba[ext=m4a]/b[height<=480][ext=mp4]/b[height<=480]",
                _ => throw new ArgumentOutOfRangeException(nameof(request), "Nieobsługiwana jakość.")
            };
            arguments.AddRange(["-f", format, "--merge-output-format", "mp4"]);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Nieobsługiwany typ pobierania.");
        }

        arguments.Add(request.Url);
        return new ProcessCommand("yt-dlp.exe", arguments);
    }
}
