using QuickConvert.Core.Conversion;
using QuickConvert.Core.Jobs;
using QuickConvert.Core.Messaging;
using QuickConvert.Core.Settings;
using QuickConvert.Core.Updates;
using QuickConvert.App;
using System.Text;

var tests = new TestSuite();

tests.Run("video targets include MP4 and extracted MP3", () =>
{
    var outputs = FormatCatalog.GetCompatibleOutputs(["movie.mkv"]);
    TestSuite.Equal(true, outputs.Contains("mp4"));
    TestSuite.Equal(true, outputs.Contains("mp3"));
});

tests.Run("mixed image and video selection has no common target", () =>
{
    var outputs = FormatCatalog.GetCompatibleOutputs(["photo.png", "movie.mp4"]);
    TestSuite.Equal(0, outputs.Count);
});

tests.Run("YouTube watch URL is normalized without playlist context", () =>
{
    var result = YoutubeUrlValidator.Validate(
        "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL123&index=2");
    TestSuite.Equal(true, result.IsValid);
    TestSuite.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", result.NormalizedUrl);
});

tests.Run("lookalike and insecure YouTube URLs are rejected", () =>
{
    TestSuite.Equal(false, YoutubeUrlValidator.Validate("https://youtube.com.evil.test/watch?v=abc").IsValid);
    TestSuite.Equal(false, YoutubeUrlValidator.Validate("http://youtube.com/watch?v=abc").IsValid);
});

tests.Run("output path never overwrites an existing file", () =>
{
    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        @"C:\Media\clip.mp3",
        @"C:\Media\clip (1).mp3"
    };
    var result = OutputPathResolver.GetAvailablePath(
        @"C:\Media\clip.mkv", "mp3", existing.Contains);
    TestSuite.Equal(@"C:\Media\clip (2).mp3", result);
});

tests.Run("output path supports a separate destination and collision suffixes", () =>
{
    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        @"D:\Downloads\QuickConvert\clip.mp3",
        @"D:\Downloads\QuickConvert\clip (1).mp3"
    };

    var result = OutputPathResolver.GetAvailablePath(
        @"C:\Media\clip.mkv",
        "mp3",
        @"D:\Downloads\QuickConvert",
        existing.Contains);

    TestSuite.Equal(@"D:\Downloads\QuickConvert\clip (2).mp3", result);
});

tests.Run("temporary path keeps target extension for FFmpeg format detection", () =>
{
    TestSuite.Equal(
        @"C:\Media\clip.quickconvert.partial.mp4",
        OutputPathResolver.GetTemporaryPath(@"C:\Media\clip.mp4"));
});

tests.Run("MP4 arguments use H264 AAC and never invoke a shell", () =>
{
    var command = FfmpegCommandBuilder.Build(
        @"C:\Media\input.mkv",
        @"C:\Media\output.quickconvert.partial.mp4",
        "mp4",
        ConversionPreset.Balanced);

    TestSuite.Equal(false, command.UseShellExecute);
    TestSuite.SequenceEqual(
        ["-hide_banner", "-nostdin", "-y", "-i", @"C:\Media\input.mkv",
         "-c:v", "libx264", "-crf", "23", "-c:a", "aac", "-b:a", "192k",
         "-progress", "pipe:1", "-nostats", @"C:\Media\output.quickconvert.partial.mp4"],
        command.Arguments);
});

tests.Run("lossy FFmpeg arguments follow the selected quality preset", () =>
{
    var cases = new[]
    {
        ("mp4", ConversionPreset.Economy, new[] { new[] { "-crf", "28" }, new[] { "-b:a", "128k" } }),
        ("mp4", ConversionPreset.Balanced, new[] { new[] { "-crf", "23" }, new[] { "-b:a", "192k" } }),
        ("mp4", ConversionPreset.Highest, new[] { new[] { "-crf", "18" }, new[] { "-b:a", "256k" } }),
        ("webm", ConversionPreset.Economy, new[] { new[] { "-crf", "38" }, new[] { "-b:a", "96k" } }),
        ("webm", ConversionPreset.Balanced, new[] { new[] { "-crf", "33" }, new[] { "-b:a", "128k" } }),
        ("webm", ConversionPreset.Highest, new[] { new[] { "-crf", "28" }, new[] { "-b:a", "192k" } }),
        ("mp3", ConversionPreset.Economy, new[] { new[] { "-b:a", "128k" } }),
        ("mp3", ConversionPreset.Balanced, new[] { new[] { "-b:a", "192k" } }),
        ("mp3", ConversionPreset.Highest, new[] { new[] { "-b:a", "320k" } }),
        ("m4a", ConversionPreset.Economy, new[] { new[] { "-b:a", "128k" } }),
        ("m4a", ConversionPreset.Balanced, new[] { new[] { "-b:a", "192k" } }),
        ("m4a", ConversionPreset.Highest, new[] { new[] { "-b:a", "320k" } }),
        ("opus", ConversionPreset.Economy, new[] { new[] { "-b:a", "96k" } }),
        ("opus", ConversionPreset.Balanced, new[] { new[] { "-b:a", "128k" } }),
        ("opus", ConversionPreset.Highest, new[] { new[] { "-b:a", "192k" } }),
        ("jpg", ConversionPreset.Economy, new[] { new[] { "-q:v", "5" } }),
        ("jpg", ConversionPreset.Balanced, new[] { new[] { "-q:v", "3" } }),
        ("jpg", ConversionPreset.Highest, new[] { new[] { "-q:v", "2" } }),
        ("webp", ConversionPreset.Economy, new[] { new[] { "-quality", "75" } }),
        ("webp", ConversionPreset.Balanced, new[] { new[] { "-quality", "85" } }),
        ("webp", ConversionPreset.Highest, new[] { new[] { "-quality", "95" } })
    };

    foreach (var (format, preset, expectedSequences) in cases)
    {
        var command = FfmpegCommandBuilder.Build("input", $"output.{format}", format, preset);
        foreach (var expected in expectedSequences)
            ContainsArguments(command.Arguments, expected);
    }
});

tests.Run("lossless FFmpeg arguments do not change with quality preset", () =>
{
    foreach (var format in new[] { "flac", "wav", "png", "gif" })
    {
        var economy = FfmpegCommandBuilder.Build("input", $"output.{format}", format, ConversionPreset.Economy);
        var balanced = FfmpegCommandBuilder.Build("input", $"output.{format}", format, ConversionPreset.Balanced);
        var highest = FfmpegCommandBuilder.Build("input", $"output.{format}", format, ConversionPreset.Highest);

        TestSuite.SequenceEqual(economy.Arguments, balanced.Arguments);
        TestSuite.SequenceEqual(balanced.Arguments, highest.Arguments);
    }
});

tests.Run("native message accepts a whitelisted extension and valid request", () =>
{
    const string json = """
        {"version":1,"requestId":"req-1","operation":"download","url":"https://youtu.be/dQw4w9WgXcQ","mediaType":"mp3","maxResolution":"best"}
    """;
    var result = NativeMessageValidator.Validate(
        json, "chrome-extension://abpjmchafogplinlgklgfoljglakhalp/");
    TestSuite.Equal(true, result.IsValid);
    TestSuite.Equal("accepted", result.Code);
    TestSuite.Equal("https://youtu.be/dQw4w9WgXcQ", result.Request!.Url);
});

tests.Run("native message rejects foreign callers and malformed quality", () =>
{
    const string json = """
        {"version":1,"requestId":"req-2","operation":"download","url":"https://youtu.be/dQw4w9WgXcQ","mediaType":"mp4","maxResolution":"16k"}
        """;
    TestSuite.Equal(
        "unauthorized_caller",
        NativeMessageValidator.Validate(json, "chrome-extension://foreign/").Code);
    TestSuite.Equal(
        "invalid_request",
        NativeMessageValidator.Validate(json, "chrome-extension://abpjmchafogplinlgklgfoljglakhalp/").Code);
    TestSuite.Equal(
        "unauthorized_caller",
        NativeMessageValidator.Validate(json, "chrome-extension://quickconvert-test/").Code);
});

tests.Run("Chrome extension ID is derived from the first 128 SHA256 bits", () =>
{
    TestSuite.Equal(
        "lkhibglpipabmpokebebeanofnkocccd",
        ChromeExtensionIdentity.ComputeId(Encoding.UTF8.GetBytes("abc")));
});

tests.Run("update schedule enforces its complete interval", () =>
{
    var now = new DateTimeOffset(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    TestSuite.Equal(true, UpdateSchedule.ShouldCheck(null, now, TimeSpan.FromDays(1)));
    TestSuite.Equal(false, UpdateSchedule.ShouldCheck(now.AddHours(-23), now, TimeSpan.FromDays(1)));
    TestSuite.Equal(true, UpdateSchedule.ShouldCheck(now.AddDays(-1), now, TimeSpan.FromDays(1)));
});

tests.Run("dark Fluent theme exposes reusable controls with interaction states", () =>
{
    var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ui", "App.xaml"));
    foreach (var key in new[]
    {
        "WindowBrush", "SurfaceBrush", "SurfaceHoverBrush", "BorderBrush",
        "AccentBrush", "AccentHoverBrush", "TextBrush", "MutedBrush",
        "DangerBrush", "WarningBrush", "CardStyle", "PrimaryButtonStyle",
        "SecondaryButtonStyle", "DangerButtonStyle", "FormatButtonStyle"
    })
        TestSuite.Equal(true, xaml.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal));

    foreach (var state in new[] { "IsMouseOver", "IsPressed", "IsEnabled", "IsKeyboardFocused" })
        TestSuite.Equal(true, xaml.Contains($"Property=\"{state}\"", StringComparison.Ordinal));
});

tests.Run("main window preserves commands inside the dark Fluent hierarchy", () =>
{
    var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ui", "MainWindow.xaml"));
    foreach (var binding in new[]
    {
        "SelectFilesCommand", "ConvertCommand", "CancelCommand", "OpenOutputCommand",
        "RetryCommand", "OpenUpdateCommand", "OpenLogCommand", "ClearLocalDataCommand"
    })
        TestSuite.Equal(true, xaml.Contains(binding, StringComparison.Ordinal));

    foreach (var marker in new[]
    {
        "Lokalnie • Bez chmury", "Wybierz format wyniku", "Kolejka zadań",
        "Folder pobierania", "Prywatność", "Strefa niebezpieczna"
    })
        TestSuite.Equal(true, xaml.Contains(marker, StringComparison.Ordinal));

    TestSuite.Equal(true, xaml.Contains("Background=\"{StaticResource WindowBrush}\"", StringComparison.Ordinal));
    TestSuite.Equal(true, xaml.Contains("Style=\"{StaticResource CardStyle}\"", StringComparison.Ordinal));
    TestSuite.Equal(true, xaml.Contains("Style=\"{StaticResource DangerButtonStyle}\"", StringComparison.Ordinal));
});

tests.Run("conversion settings XAML exposes dark controls and functional bindings", () =>
{
    var appXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ui", "App.xaml"));
    foreach (var style in new[] { "DarkComboBoxStyle", "DarkCheckBoxStyle", "DarkExpanderStyle" })
        TestSuite.Equal(true, appXaml.Contains($"x:Key=\"{style}\"", StringComparison.Ordinal));

    var windowXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ui", "MainWindow.xaml"));
    foreach (var binding in new[]
    {
        "ItemsSource=\"{Binding QualityChoices}\"",
        "SelectedItem=\"{Binding SelectedQuality}\"",
        "ItemsSource=\"{Binding OutputDirectoryChoices}\"",
        "SelectedItem=\"{Binding SelectedOutputDirectory}\"",
        "IsChecked=\"{Binding OpenFolderOnCompletion}\"",
        "Text=\"{Binding FormatEmptyMessage}\"",
        "Binding=\"{Binding HasCompatibleFormats}\" Value=\"False\"",
        "Style=\"{StaticResource DarkExpanderStyle}\""
    })
        TestSuite.Equal(true, windowXaml.Contains(binding, StringComparison.Ordinal));

    TestSuite.Equal(
        true,
        windowXaml.Contains("FLAC, WAV, PNG i GIF nie zmieniają jakości", StringComparison.Ordinal));
});

tests.Run("branding assets contain the selected Q mark and required icon sizes", () =>
{
    var root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var branding = Path.Combine(root, "assets", "branding");
    var svg = File.ReadAllText(Path.Combine(branding, "quickconvert-logo.svg"));
    TestSuite.Equal(true, svg.Contains("#9B8CFF", StringComparison.OrdinalIgnoreCase));
    TestSuite.Equal(true, svg.Contains("#5145D6", StringComparison.OrdinalIgnoreCase));
    TestSuite.Equal(
        true,
        svg.Contains("aria-label=\"QuickConvert Q in motion\"", StringComparison.Ordinal));

    var sizes = ReadIcoSizes(Path.Combine(branding, "quickconvert.ico"));
    TestSuite.SequenceEqual(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }, sizes);
    TestSuite.Equal(
        (256, 256),
        ReadPngSize(Path.Combine(branding, "quickconvert-256.png")));
    TestSuite.Equal(
        (64, 64),
        ReadPngSize(Path.Combine(branding, "quickconvert-wizard-small.png")));
});

tests.Run("dark title bar falls back from DWM attribute 20 to 19", () =>
{
    var attributes = new List<int>();
    int Setter(nint handle, int attribute, ref int value, int size)
    {
        attributes.Add(attribute);
        return attribute == 19 ? 0 : -1;
    }

    TestSuite.Equal(true, DarkTitleBar.TryApply((nint)123, Setter));
    TestSuite.SequenceEqual(new[] { 20, 19 }, attributes);
});

tests.Run("dark title bar ignores unavailable DWM without blocking startup", () =>
{
    int MissingDwm(nint handle, int attribute, ref int value, int size) =>
        throw new DllNotFoundException("dwmapi.dll");
    TestSuite.Equal(false, DarkTitleBar.TryApply((nint)123, MissingDwm));
    TestSuite.Equal(false, DarkTitleBar.TryApply(nint.Zero, MissingDwm));
});

tests.Run("application embeds branding and applies dark title bar after handle creation", () =>
{
    var root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var project = File.ReadAllText(Path.Combine(
        root, "src", "QuickConvert.App", "QuickConvert.App.csproj"));
    var xaml = File.ReadAllText(Path.Combine(
        root, "src", "QuickConvert.App", "MainWindow.xaml"));
    var codeBehind = File.ReadAllText(Path.Combine(
        root, "src", "QuickConvert.App", "MainWindow.xaml.cs"));

    TestSuite.Equal(
        true,
        project.Contains("<ApplicationIcon>../../assets/branding/quickconvert.ico</ApplicationIcon>"));
    TestSuite.Equal(true, project.Contains("Link=\"Assets/quickconvert.ico\""));
    TestSuite.Equal(true, xaml.Contains("Icon=\"Assets/quickconvert.ico\""));
    TestSuite.Equal(true, xaml.Contains("Data=\"M42.5 39.5", StringComparison.Ordinal));
    TestSuite.Equal(true, codeBehind.Contains("OnSourceInitialized", StringComparison.Ordinal));
    TestSuite.Equal(true, codeBehind.Contains("DarkTitleBar.TryApply", StringComparison.Ordinal));
});

tests.Run("completion folder opens only for a successful conversion", () =>
{
    var outputs = new[] { @"C:\Output\converted.mp3" };

    TestSuite.Equal(
        @"C:\Output",
        CompletionFolderPolicy.GetFolder("convert", JobStatus.Completed, true, outputs));
    TestSuite.Equal(null, CompletionFolderPolicy.GetFolder("convert", JobStatus.Completed, false, outputs));
    TestSuite.Equal(null, CompletionFolderPolicy.GetFolder("convert", JobStatus.Failed, true, outputs));
    TestSuite.Equal(null, CompletionFolderPolicy.GetFolder("convert", JobStatus.Canceled, true, outputs));
    TestSuite.Equal(null, CompletionFolderPolicy.GetFolder("download", JobStatus.Completed, true, outputs));
    TestSuite.Equal(null, CompletionFolderPolicy.GetFolder("convert", JobStatus.Completed, true, []));
});

tests.Run("format empty state distinguishes no files from incompatible files", () =>
{
    TestSuite.Equal("Najpierw wybierz pliki", FormatEmptyState.GetMessage(0, false));
    TestSuite.Equal(
        "Brak wspólnego formatu dla tego zestawu plików",
        FormatEmptyState.GetMessage(2, false));
    TestSuite.Equal(string.Empty, FormatEmptyState.GetMessage(1, true));
});

tests.Run("installer uses QuickConvert branding assets", () =>
{
    var root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var installer = File.ReadAllText(Path.Combine(root, "installer", "QuickConvert.iss"));
    TestSuite.Equal(
        true,
        installer.Contains(@"SetupIconFile=..\assets\branding\quickconvert.ico"));
    TestSuite.Equal(
        true,
        installer.Contains(@"WizardSmallImageFile=..\assets\branding\quickconvert-wizard-small.png"));
    TestSuite.Equal(
        true,
        installer.Contains(@"UninstallDisplayIcon={app}\{#MyAppExeName}"));
});

tests.Run("v0.2.0 release version and installer branding stay aligned", () =>
{
    var root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var buildProps = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
    var installer = File.ReadAllText(Path.Combine(root, "installer", "QuickConvert.iss"));

    TestSuite.Equal(true, buildProps.Contains("<Version>0.2.0</Version>", StringComparison.Ordinal));
    TestSuite.Equal(true, installer.Contains("#define MyAppVersion \"0.2.0\"", StringComparison.Ordinal));
    TestSuite.Equal(true, installer.Contains(
        "OutputBaseFilename=QuickConvert-{#MyAppVersion}-win-x64-setup",
        StringComparison.Ordinal));
    TestSuite.Equal(true, installer.Contains(
        @"SetupIconFile=..\assets\branding\quickconvert.ico",
        StringComparison.Ordinal));
    TestSuite.Equal(true, installer.Contains(
        @"WizardSmallImageFile=..\assets\branding\quickconvert-wizard-small.png",
        StringComparison.Ordinal));
    TestSuite.Equal(true, installer.Contains(
        @"UninstallDisplayIcon={app}\{#MyAppExeName}",
        StringComparison.Ordinal));
});

tests.Run("release workflow names releases and generates notes", () =>
{
    var root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

    TestSuite.Equal(true, workflow.Contains(
        "name: QuickConvert ${{ github.ref_name }}",
        StringComparison.Ordinal));
    TestSuite.Equal(true, workflow.Contains(
        "generate_release_notes: true",
        StringComparison.Ordinal));
    TestSuite.Equal(true, workflow.Contains(
        "artifacts/installer/*.exe",
        StringComparison.Ordinal));
    TestSuite.Equal(true, workflow.Contains(
        "artifacts/SHA256SUMS.txt",
        StringComparison.Ordinal));
});

tests.Run("GitHub release parser reports only a newer semantic version", () =>
{
    TestSuite.Equal(
        "https://api.github.com/repos/ITs-ZAK/QuickConvert/releases/latest",
        ReleaseEndpoints.LatestReleaseApi);
    const string json = """{"tag_name":"v1.2.0","html_url":"https://github.com/ITs-ZAK/QuickConvert/releases/tag/v1.2.0"}""";
    var release = GitHubReleaseParser.Parse(json, new Version(1, 1, 0));
    TestSuite.Equal("1.2.0", release!.Version.ToString());
    TestSuite.Equal(
        "https://github.com/ITs-ZAK/QuickConvert/releases/tag/v1.2.0",
        release.Url);
    TestSuite.Equal(null, GitHubReleaseParser.Parse(json, new Version(1, 2, 0)));
});

tests.Run("tool errors are mapped to actionable categories", () =>
{
    TestSuite.Equal("network_error", ToolErrorClassifier.Classify("Unable to download webpage: connection timed out"));
    TestSuite.Equal("media_unavailable", ToolErrorClassifier.Classify("Video unavailable"));
    TestSuite.Equal("disk_full", ToolErrorClassifier.Classify("No space left on device"));
    TestSuite.Equal("tool_failed", ToolErrorClassifier.Classify("unknown encoder failure"));
});

tests.Run("yt-dlp command always disables playlists and limits MP4 resolution", () =>
{
    var command = YtDlpCommandBuilder.Build(
        new DownloadMediaRequest("req", "https://youtu.be/dQw4w9WgXcQ", "mp4", "1080p"),
        @"C:\Users\Test\Downloads\QuickConvert");
    TestSuite.Equal(false, command.UseShellExecute);
    TestSuite.Equal(true, command.Arguments.Contains("--no-playlist"));
    TestSuite.Equal(true, command.Arguments.Contains("bv*[height<=1080][ext=mp4]+ba[ext=m4a]/b[height<=1080][ext=mp4]/b[height<=1080]"));
});

await tests.RunAsync("process runner captures real stdout without shell execution", async () =>
{
    var runner = new SystemProcessRunner();
    var result = await runner.RunAsync(
        new ProcessCommand("cmd.exe", ["/d", "/c", "echo", "quickconvert"]),
        null,
        CancellationToken.None);
    TestSuite.Equal(0, result.ExitCode);
    TestSuite.Equal(true, result.StandardOutput.Contains("quickconvert", StringComparison.OrdinalIgnoreCase));
});

await tests.RunAsync("history store persists only the newest 50 entries", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertTests-{Guid.NewGuid():N}");
    try
    {
        var store = new JsonHistoryStore(Path.Combine(directory, "history.json"));
        for (var index = 0; index < 55; index++)
            await store.AddAsync(new JobHistoryEntry(
                $"job-{index}", "convert", $"file-{index}", JobStatus.Completed, DateTimeOffset.UtcNow));

        var entries = await store.LoadAsync();
        TestSuite.Equal(50, entries.Count);
        TestSuite.Equal("job-54", entries[0].Id);
        TestSuite.Equal("job-5", entries[^1].Id);
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
});

await tests.RunAsync("corrupted history is treated as empty instead of blocking the app", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertHistory-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "history.json");
    await File.WriteAllTextAsync(path, "{broken json");
    try
    {
        var entries = await new JsonHistoryStore(path).LoadAsync();
        TestSuite.Equal(0, entries.Count);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
});

await tests.RunAsync("history can be cleared without leaving stale entries", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertHistory-{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "history.json");
    var store = new JsonHistoryStore(path);
    try
    {
        await store.AddAsync(new JobHistoryEntry(
            "job", "convert", "file", JobStatus.Completed, DateTimeOffset.UtcNow));
        await store.ClearAsync();
        TestSuite.Equal(0, (await store.LoadAsync()).Count);
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
});

await tests.RunAsync("missing settings file returns balanced adjacent defaults", async () =>
{
    var path = Path.Combine(Path.GetTempPath(), $"QuickConvertSettings-{Guid.NewGuid():N}", "settings.json");
    var settings = await new JsonSettingsStore(path).LoadAsync();

    TestSuite.Equal(ConversionPreset.Balanced, settings.QualityPreset);
    TestSuite.Equal(OutputDirectoryMode.Adjacent, settings.OutputDirectoryMode);
    TestSuite.Equal(false, settings.OpenFolderOnCompletion);
});

await tests.RunAsync("settings save round-trips and leaves no partial file", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertSettings-{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "settings.json");
    try
    {
        var store = new JsonSettingsStore(path);
        var expected = new QuickConvertSettings(
            ConversionPreset.Highest,
            OutputDirectoryMode.DownloadsQuickConvert,
            true);

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        TestSuite.Equal(expected, actual);
        TestSuite.Equal(true, File.Exists(path));
        TestSuite.Equal(false, File.Exists($"{path}.tmp"));
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
});

await tests.RunAsync("malformed and undefined settings values fall back to defaults", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertSettings-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "settings.json");
    try
    {
        var store = new JsonSettingsStore(path);
        await File.WriteAllTextAsync(path, "{broken json");
        TestSuite.Equal(QuickConvertSettings.Defaults, await store.LoadAsync());

        await File.WriteAllTextAsync(path, """
            {"qualityPreset":999,"outputDirectoryMode":"Adjacent","openFolderOnCompletion":false}
            """);
        TestSuite.Equal(QuickConvertSettings.Defaults, await store.LoadAsync());

        await File.WriteAllTextAsync(path, """
            {"qualityPreset":"Unknown","outputDirectoryMode":"Adjacent","openFolderOnCompletion":false}
            """);
        TestSuite.Equal(QuickConvertSettings.Defaults, await store.LoadAsync());
    }
    finally
    {
        Directory.Delete(directory, true);
    }
});

await tests.RunAsync("sequential settings saves publish the newest complete value", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertSettings-{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "settings.json");
    try
    {
        var store = new JsonSettingsStore(path);
        await store.SaveAsync(QuickConvertSettings.Defaults);
        var newest = new QuickConvertSettings(
            ConversionPreset.Economy,
            OutputDirectoryMode.DownloadsQuickConvert,
            true);
        await store.SaveAsync(newest);

        TestSuite.Equal(newest, await store.LoadAsync());
        TestSuite.Equal(false, File.Exists($"{path}.tmp"));
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
});

await tests.RunAsync("conversion engine publishes output only after successful tool completion", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertEngine-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "sample.wav");
    await File.WriteAllTextAsync(source, "source remains untouched");
    try
    {
        var engine = new ConversionEngine(
            @"C:\Tools\ffmpeg.exe",
            new OutputCreatingProcessRunner(exitCode: 0));
        var result = await engine.ConvertAsync(
            new ConvertFilesRequest([source], "mp3", ConversionPreset.Balanced, OutputDirectoryMode.Adjacent),
            null,
            CancellationToken.None);

        TestSuite.Equal(true, result.Success);
        TestSuite.Equal(1, result.OutputPaths.Count);
        TestSuite.Equal(true, File.Exists(result.OutputPaths[0]));
        TestSuite.Equal("source remains untouched", await File.ReadAllTextAsync(source));
        TestSuite.Equal(0, Directory.GetFiles(directory, "*.quickconvert.partial.*").Length);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
});

await tests.RunAsync("downloads output mode creates its directory and preserves the source", async () =>
{
    var root = Path.Combine(Path.GetTempPath(), $"QuickConvertDownloads-{Guid.NewGuid():N}");
    var sourceDirectory = Path.Combine(root, "source");
    var downloadsDirectory = Path.Combine(root, "downloads", "QuickConvert");
    Directory.CreateDirectory(sourceDirectory);
    var source = Path.Combine(sourceDirectory, "sample.wav");
    await File.WriteAllTextAsync(source, "source remains untouched");
    try
    {
        var engine = new ConversionEngine(
            @"C:\Tools\ffmpeg.exe",
            new OutputCreatingProcessRunner(exitCode: 0),
            downloadsDirectory);
        var result = await engine.ConvertAsync(
            new ConvertFilesRequest(
                [source],
                "mp3",
                ConversionPreset.Balanced,
                OutputDirectoryMode.DownloadsQuickConvert),
            null,
            CancellationToken.None);

        TestSuite.Equal(true, result.Success);
        TestSuite.Equal(Path.Combine(downloadsDirectory, "sample.mp3"), result.OutputPaths.Single());
        TestSuite.Equal(true, Directory.Exists(downloadsDirectory));
        TestSuite.Equal("source remains untouched", await File.ReadAllTextAsync(source));
    }
    finally
    {
        Directory.Delete(root, true);
    }
});

await tests.RunAsync("unavailable output directory returns a categorized failure", async () =>
{
    var root = Path.Combine(Path.GetTempPath(), $"QuickConvertUnavailable-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var source = Path.Combine(root, "sample.wav");
    var blockedDestination = Path.Combine(root, "not-a-directory");
    await File.WriteAllTextAsync(source, "source");
    await File.WriteAllTextAsync(blockedDestination, "file blocks directory creation");
    try
    {
        var engine = new ConversionEngine(
            @"C:\Tools\ffmpeg.exe",
            new OutputCreatingProcessRunner(exitCode: 0),
            blockedDestination);
        var result = await engine.ConvertAsync(
            new ConvertFilesRequest(
                [source],
                "mp3",
                ConversionPreset.Balanced,
                OutputDirectoryMode.DownloadsQuickConvert),
            null,
            CancellationToken.None);

        TestSuite.Equal(false, result.Success);
        TestSuite.Equal("output_unavailable", result.ErrorCode);
        TestSuite.Equal(true, File.Exists(source));
    }
    finally
    {
        Directory.Delete(root, true);
    }
});

await tests.RunAsync("failed conversion removes its partial output", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertEngine-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var source = Path.Combine(directory, "broken.wav");
    await File.WriteAllTextAsync(source, "source");
    try
    {
        var engine = new ConversionEngine(
            @"C:\Tools\ffmpeg.exe",
            new OutputCreatingProcessRunner(exitCode: 1));
        var result = await engine.ConvertAsync(
            new ConvertFilesRequest([source], "mp3", ConversionPreset.Balanced, OutputDirectoryMode.Adjacent),
            null,
            CancellationToken.None);

        TestSuite.Equal(false, result.Success);
        TestSuite.Equal("tool_failed", result.ErrorCode);
        TestSuite.Equal(false, File.Exists(Path.Combine(directory, "broken.mp3")));
        TestSuite.Equal(0, Directory.GetFiles(directory, "*.quickconvert.partial.*").Length);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
});

await tests.RunAsync("download engine returns the path reported by yt-dlp", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertDownload-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var engine = new DownloadEngine(
            @"C:\Tools\yt-dlp.exe",
            new DownloadOutputProcessRunner(directory));
        var result = await engine.DownloadAsync(
            new DownloadMediaRequest("req", "https://youtu.be/dQw4w9WgXcQ", "mp3", "best"),
            directory,
            null,
            CancellationToken.None);

        TestSuite.Equal(true, result.Success);
        TestSuite.Equal(Path.Combine(directory, "downloaded.mp3"), result.OutputPaths.Single());
        TestSuite.Equal(false, File.Exists(Path.Combine(directory, "downloaded.quickconvert.partial.mp3")));
    }
    finally
    {
        Directory.Delete(directory, true);
    }
});

await tests.RunAsync("failed download removes newly created QuickConvert partial files", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertDownload-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var engine = new DownloadEngine(
            @"C:\Tools\yt-dlp.exe",
            new FailingDownloadProcessRunner(directory));
        var result = await engine.DownloadAsync(
            new DownloadMediaRequest("req", "https://youtu.be/dQw4w9WgXcQ", "mp4", "720p"),
            directory,
            null,
            CancellationToken.None);

        TestSuite.Equal(false, result.Success);
        TestSuite.Equal(0, Directory.GetFiles(directory, "*quickconvert.partial*").Length);
    }
    finally
    {
        Directory.Delete(directory, true);
    }
});

await tests.RunAsync("native protocol round-trips a length-prefixed UTF8 message", async () =>
{
    await using var stream = new MemoryStream();
    const string message = """{"code":"accepted","title":"Zażółć"}""";
    await NativeMessageProtocol.WriteAsync(stream, message, CancellationToken.None);
    stream.Position = 0;
    var result = await NativeMessageProtocol.ReadAsync(stream, CancellationToken.None);
    TestSuite.Equal(message, result);
});

await tests.RunAsync("named-pipe IPC delivers one validated envelope", async () =>
{
    var pipeName = $"QuickConvert.Tests.{Guid.NewGuid():N}";
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    IpcEnvelope? received = null;
    var server = new SingleInstanceIpcServer(pipeName);
    var serverTask = server.ReceiveOnceAsync(
        envelope =>
        {
            received = envelope;
            return Task.FromResult(new IpcResponse(true, "accepted"));
        },
        cancellation.Token);

    var client = new SingleInstanceIpcClient(pipeName);
    var response = await client.SendAsync(
        IpcEnvelope.ForDownload(new DownloadMediaRequest(
            "req-pipe", "https://youtu.be/dQw4w9WgXcQ", "mp3", "best")),
        cancellation.Token);
    await serverTask;

    TestSuite.Equal(true, response.Accepted);
    TestSuite.Equal("req-pipe", received!.Download!.RequestId);
});

await tests.RunAsync("job queue runs tasks sequentially and records completion", async () =>
{
    await using var queue = new JobQueue();
    var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var secondStarted = false;

    var first = queue.Enqueue("first", "convert", async (_, cancellationToken) =>
    {
        firstStarted.SetResult();
        await releaseFirst.Task.WaitAsync(cancellationToken);
        return new JobExecutionResult(true, ["first.mp3"], null, null);
    });
    var second = queue.Enqueue("second", "convert", (_, _) =>
    {
        secondStarted = true;
        return Task.FromResult(new JobExecutionResult(true, ["second.mp3"], null, null));
    });

    await firstStarted.Task;
    TestSuite.Equal(false, secondStarted);
    releaseFirst.SetResult();
    await queue.WhenIdleAsync(CancellationToken.None);

    TestSuite.Equal(JobStatus.Completed, first.Status);
    TestSuite.Equal(JobStatus.Completed, second.Status);
    TestSuite.Equal(true, secondStarted);
});

await tests.RunAsync("job queue can cancel a currently running task", async () =>
{
    await using var queue = new JobQueue();
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var job = queue.Enqueue("cancel me", "convert", async (_, cancellationToken) =>
    {
        started.SetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new JobExecutionResult(true, [], null, null);
    });

    await started.Task;
    TestSuite.Equal(true, queue.Cancel(job.Id));
    await queue.WhenIdleAsync(CancellationToken.None);
    TestSuite.Equal(JobStatus.Canceled, job.Status);
});

if (args.Contains("--integration", StringComparer.OrdinalIgnoreCase))
{
    await tests.RunAsync("real FFmpeg converts audio video static and animated fixtures", async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), $"QuickConvertIntegration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var runner = new SystemProcessRunner();
            await GenerateFixtureAsync(runner,
                ["-f", "lavfi", "-i", "sine=frequency=1000:duration=0.25", "-c:a", "pcm_s16le", Path.Combine(directory, "tone.wav")]);
            await GenerateFixtureAsync(runner,
                ["-f", "lavfi", "-i", "testsrc=size=64x64:rate=10:duration=0.4", "-f", "lavfi", "-i", "sine=frequency=500:duration=0.4", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", Path.Combine(directory, "clip.mp4")]);
            await GenerateFixtureAsync(runner,
                ["-f", "lavfi", "-i", "color=c=blue:s=32x32", "-frames:v", "1", Path.Combine(directory, "still.png")]);
            await GenerateFixtureAsync(runner,
                ["-f", "lavfi", "-i", "testsrc=size=32x32:rate=5:duration=0.4", Path.Combine(directory, "animated.gif")]);

            var engine = new ConversionEngine("ffmpeg.exe", runner);
            var requests = new[]
            {
                new ConvertFilesRequest([Path.Combine(directory, "tone.wav")], "mp3", ConversionPreset.Balanced, OutputDirectoryMode.Adjacent),
                new ConvertFilesRequest([Path.Combine(directory, "clip.mp4")], "webm", ConversionPreset.Balanced, OutputDirectoryMode.Adjacent),
                new ConvertFilesRequest([Path.Combine(directory, "clip.mp4")], "mp3", ConversionPreset.Balanced, OutputDirectoryMode.Adjacent),
                new ConvertFilesRequest([Path.Combine(directory, "still.png")], "webp", ConversionPreset.Balanced, OutputDirectoryMode.Adjacent),
                new ConvertFilesRequest([Path.Combine(directory, "animated.gif")], "jpg", ConversionPreset.Balanced, OutputDirectoryMode.Adjacent)
            };

            foreach (var request in requests)
            {
                var result = await engine.ConvertAsync(request, null, CancellationToken.None);
                TestSuite.Equal(true, result.Success);
                TestSuite.Equal(true, new FileInfo(result.OutputPaths.Single()).Length > 0);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    });
}

return tests.Complete();

static int[] ReadIcoSizes(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream);
    TestSuite.Equal((ushort)0, reader.ReadUInt16());
    TestSuite.Equal((ushort)1, reader.ReadUInt16());
    var count = reader.ReadUInt16();
    var sizes = new int[count];
    for (var index = 0; index < count; index++)
    {
        var width = reader.ReadByte();
        var height = reader.ReadByte();
        sizes[index] = width == 0 ? 256 : width;
        TestSuite.Equal(sizes[index], height == 0 ? 256 : height);
        reader.ReadBytes(14);
    }
    return sizes;
}

static (int Width, int Height) ReadPngSize(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream);
    TestSuite.SequenceEqual(
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
        reader.ReadBytes(8));
    reader.ReadUInt32();
    TestSuite.SequenceEqual(new byte[] { 73, 72, 68, 82 }, reader.ReadBytes(4));
    var widthBytes = reader.ReadBytes(4);
    var heightBytes = reader.ReadBytes(4);
    if (BitConverter.IsLittleEndian)
    {
        Array.Reverse(widthBytes);
        Array.Reverse(heightBytes);
    }
    return (BitConverter.ToInt32(widthBytes), BitConverter.ToInt32(heightBytes));
}

static void ContainsArguments(
    IReadOnlyList<string> arguments,
    params string[] expectedSequence)
{
    for (var start = 0; start <= arguments.Count - expectedSequence.Length; start++)
    {
        if (arguments.Skip(start).Take(expectedSequence.Length).SequenceEqual(expectedSequence))
            return;
    }

    throw new InvalidOperationException(
        $"Expected argument sequence [{string.Join(", ", expectedSequence)}].");
}

static async Task GenerateFixtureAsync(SystemProcessRunner runner, IReadOnlyList<string> arguments)
{
    var command = new ProcessCommand(
        "ffmpeg.exe",
        ["-hide_banner", "-loglevel", "error", "-y", .. arguments]);
    var result = await runner.RunAsync(command, null, CancellationToken.None);
    if (result.ExitCode != 0)
        throw new InvalidOperationException($"Fixture generation failed: {result.StandardError}");
}

internal sealed class OutputCreatingProcessRunner(int exitCode) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessCommand command,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var output = command.Arguments[^1];
        await File.WriteAllTextAsync(output, "converted", cancellationToken);
        progress?.Report("progress=end");
        return new ProcessResult(exitCode, "progress=end", exitCode == 0 ? string.Empty : "failed");
    }
}

internal sealed class DownloadOutputProcessRunner(string directory) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessCommand command,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var output = Path.Combine(directory, "downloaded.quickconvert.partial.mp3");
        await File.WriteAllTextAsync(output, "downloaded", cancellationToken);
        progress?.Report("[download] 100%");
        return new ProcessResult(0, $"QC_OUTPUT:{output}{Environment.NewLine}", string.Empty);
    }
}

internal sealed class FailingDownloadProcessRunner(string directory) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessCommand command,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            Path.Combine(directory, "failed.quickconvert.partial.mp4.part"),
            "partial",
            cancellationToken);
        return new ProcessResult(1, string.Empty, "network failed");
    }
}

internal sealed class TestSuite
{
    private readonly List<string> _failures = [];
    private int _count;

    public void Run(string name, Action test)
    {
        _count++;
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            _failures.Add($"{name}: {exception.Message}");
        }
    }

    public async Task RunAsync(string name, Func<Task> test)
    {
        _count++;
        try
        {
            await test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            _failures.Add($"{name}: {exception.Message}");
        }
    }

    public int Complete()
    {
        if (_failures.Count == 0)
        {
            Console.WriteLine($"PASS: {_count} tests");
            return 0;
        }

        Console.Error.WriteLine($"FAILED: {_failures.Count}/{_count} tests");
        foreach (var failure in _failures)
            Console.Error.WriteLine($" - {failure}");
        return 1;
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        var expectedItems = expected.ToArray();
        var actualItems = actual.ToArray();
        if (!expectedItems.SequenceEqual(actualItems))
            throw new InvalidOperationException(
                $"Expected [{string.Join(", ", expectedItems)}], got [{string.Join(", ", actualItems)}].");
    }
}
