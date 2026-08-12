# Conversion Settings and Empty State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn “Więcej ustawień” into functional persistent conversion controls, add two output locations, open completed conversion folders on demand, and polish the expander and empty format state.

**Architecture:** Extend the existing request enums and FFmpeg builder with three deterministic presets. Keep filesystem decisions in the conversion engine through an injected downloads directory, persist UI preferences in a focused JSON store, and expose simple bindable properties from `MainViewModel` without changing downloader or IPC behavior.

**Tech Stack:** .NET 8, C#, WPF XAML/MVVM, System.Text.Json, existing console test runner and FFmpeg integration fixtures.

## Global Constraints

- Default values: `Balanced`, `Adjacent`, and `OpenFolderOnCompletion = false`.
- Settings path: `%LocalAppData%\QuickConvert\settings.json`.
- Output modes: next to source or `Downloads\QuickConvert`.
- Source resolution, FPS, aspect ratio, and original files remain unchanged.
- Lossless formats ignore the quality preset.
- A failed settings read/write or Explorer launch must not block conversion.
- Do not add arbitrary folder selection, manual codec controls, source deletion, cloud sync, or downloader setting changes.
- Preserve atomic partial-file publication and collision suffixes.

---

### Task 1: Three deterministic FFmpeg presets

**Files:**
- Modify: `src/QuickConvert.Core/Jobs/JobContracts.cs`
- Modify: `src/QuickConvert.Core/Conversion/FfmpegCommandBuilder.cs`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Produces enum values `ConversionPreset.Economy`, `Balanced`, and `Highest`.
- Produces exact command arguments from `FfmpegCommandBuilder.Build(..., ConversionPreset preset)`.

- [ ] **Step 1: Write failing preset matrix tests**

Add table-driven assertions for:
- MP4 Economy: H.264 CRF 28 and AAC 128k;
- MP4 Balanced: CRF 23 and AAC 192k;
- MP4 Highest: CRF 18 and AAC 256k;
- WebM: CRF 38/33/28 and Opus 96k/128k/192k;
- MP3 and M4A: 128k/192k/320k;
- Opus: 96k/128k/192k;
- JPG `-q:v`: 5/3/2;
- WebP `-quality`: 75/85/95.

Use a helper:

```csharp
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
```

Add a separate test asserting that FLAC, WAV, PNG, and GIF produce identical arguments for all three presets.

- [ ] **Step 2: Verify RED**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
```

Expected: compilation fails because the three preset names do not exist.

- [ ] **Step 3: Add enum values and argument profiles**

Define:

```csharp
public enum ConversionPreset
{
    Economy,
    Balanced,
    Highest
}
```

Replace every existing `ConversionPreset.Default` call site with `ConversionPreset.Balanced`. Refactor only the lossy cases in `FfmpegCommandBuilder` to select values through a total switch, for example:

```csharp
var (crf, audioBitrate) = preset switch
{
    ConversionPreset.Economy => ("28", "128k"),
    ConversionPreset.Balanced => ("23", "192k"),
    ConversionPreset.Highest => ("18", "256k"),
    _ => throw new ArgumentOutOfRangeException(nameof(preset))
};
```

Keep FLAC, WAV, PNG, and GIF independent of `preset`. Keep `-progress pipe:1 -nostats` and shell-free execution unchanged.

- [ ] **Step 4: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
git add src/QuickConvert.Core/Jobs/JobContracts.cs src/QuickConvert.Core/Conversion/FfmpegCommandBuilder.cs tests/QuickConvert.Tests/Program.cs
git commit -m "feat: add conversion quality presets"
```

Expected: all tests pass and the build reports zero warnings.

---

### Task 2: Downloads output mode

**Files:**
- Modify: `src/QuickConvert.Core/Jobs/JobContracts.cs`
- Modify: `src/QuickConvert.Core/Conversion/OutputPathResolver.cs`
- Modify: `src/QuickConvert.Core/Conversion/ConversionEngine.cs`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Adds `OutputDirectoryMode.DownloadsQuickConvert`.
- Extends `ConversionEngine(string ffmpegPath, IProcessRunner processRunner, string? downloadsDirectory = null)`.
- Adds `OutputPathResolver.GetAvailablePath(string sourcePath, string targetExtension, string outputDirectory, Func<string,bool> fileExists)`.
- The existing three-argument resolver delegates to the source directory overload.

- [ ] **Step 1: Write failing path and engine tests**

Add tests asserting:
- adjacent mode still produces `C:\Media\clip.mp3`;
- explicit output directory produces `D:\Downloads\QuickConvert\clip.mp3`;
- explicit-directory collisions produce `clip (2).mp3`;
- a conversion request using `DownloadsQuickConvert` creates an injected temporary downloads directory and publishes its output there;
- the original source remains present.

Use the existing `OutputCreatingProcessRunner` and a temporary directory outside the source fixture directory.

- [ ] **Step 2: Verify RED**

Run the .NET tests. Expected: compilation fails because `DownloadsQuickConvert` and the output-directory overload do not exist.

- [ ] **Step 3: Implement directory resolution**

Add `DownloadsQuickConvert` to the enum. Store an injected downloads path in `ConversionEngine`; when absent, default to `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "QuickConvert")`.

```csharp
public enum OutputDirectoryMode
{
    Adjacent,
    DownloadsQuickConvert
}

public ConversionEngine(
    string ffmpegPath,
    IProcessRunner processRunner,
    string? downloadsDirectory = null)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
    _ffmpegPath = ffmpegPath;
    _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    _downloadsDirectory = downloadsDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "QuickConvert");
}
```

Before processing a downloads-mode request, call `Directory.CreateDirectory`. Pass either the source directory or downloads directory to the new resolver overload. Retain temporary naming and collision logic.

```csharp
var outputDirectory = request.OutputDirectoryMode switch
{
    OutputDirectoryMode.Adjacent => Path.GetDirectoryName(sourcePath) ?? string.Empty,
    OutputDirectoryMode.DownloadsQuickConvert => _downloadsDirectory,
    _ => throw new ArgumentOutOfRangeException(nameof(request.OutputDirectoryMode))
};
var finalPath = OutputPathResolver.GetAvailablePath(
    sourcePath, request.OutputFormat, outputDirectory, File.Exists);
```

Catch `IOException` and `UnauthorizedAccessException` around destination setup/publication and return `JobExecutionResult.Failed("output_unavailable", exception.Message)` after deleting only the task partial.

- [ ] **Step 4: Add the user-facing error**

Map `output_unavailable` in `MainViewModel.GetFriendlyError` to `Nie można zapisać w wybranym folderze.`.

- [ ] **Step 5: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
git add src/QuickConvert.Core src/QuickConvert.App/MainViewModel.cs tests/QuickConvert.Tests/Program.cs
git commit -m "feat: add Downloads output mode"
```

---

### Task 3: Persistent conversion settings

**Files:**
- Create: `src/QuickConvert.Core/Settings/QuickConvertSettings.cs`
- Create: `src/QuickConvert.Core/Settings/JsonSettingsStore.cs`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Produces `public sealed record QuickConvertSettings(ConversionPreset QualityPreset, OutputDirectoryMode OutputDirectoryMode, bool OpenFolderOnCompletion)`.
- Produces `public static QuickConvertSettings Defaults { get; }`.
- Produces `JsonSettingsStore(string path)`, `Task<QuickConvertSettings> LoadAsync()`, and `Task SaveAsync(QuickConvertSettings settings)`.

- [ ] **Step 1: Write failing persistence tests**

Add async tests asserting:
- missing file returns `QuickConvertSettings.Defaults`;
- save then load round-trips `Highest`, `DownloadsQuickConvert`, and true;
- malformed JSON returns defaults;
- numeric or string values outside defined enum values return defaults;
- after save, `settings.json` exists and `settings.json.tmp` does not;
- two sequential saves load the newest complete value.

- [ ] **Step 2: Verify RED**

Run .NET tests. Expected: compilation fails because settings types do not exist.

- [ ] **Step 3: Implement validated atomic JSON storage**

Use `JsonSerializerOptions` with `JsonStringEnumConverter` and `WriteIndented = true`. Validate both enums with `Enum.IsDefined`; any undefined value returns `QuickConvertSettings.Defaults`.

```csharp
public sealed record QuickConvertSettings(
    ConversionPreset QualityPreset,
    OutputDirectoryMode OutputDirectoryMode,
    bool OpenFolderOnCompletion)
{
    public static QuickConvertSettings Defaults { get; } = new(
        ConversionPreset.Balanced,
        OutputDirectoryMode.Adjacent,
        false);
}
```

For save:
1. create the parent directory;
2. serialize to `<path>.tmp`;
3. move with overwrite to the final path;
4. guard save operations with a private `SemaphoreSlim`;
5. delete a leftover temporary file only if the current save failed.

Catch `IOException`, `UnauthorizedAccessException`, and `JsonException` during load and return defaults. Let `SaveAsync` surface errors to its caller so the ViewModel can intentionally ignore them.

The atomic replacement is exactly:

```csharp
var temporaryPath = $"{_path}.tmp";
await File.WriteAllTextAsync(temporaryPath, json).ConfigureAwait(false);
File.Move(temporaryPath, _path, overwrite: true);
```

- [ ] **Step 4: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
git add src/QuickConvert.Core/Settings tests/QuickConvert.Tests/Program.cs
git commit -m "feat: persist conversion preferences"
```

---

### Task 4: ViewModel settings and completion behavior

**Files:**
- Create: `src/QuickConvert.App/ConversionSettingChoice.cs`
- Create: `src/QuickConvert.App/CompletionFolderPolicy.cs`
- Modify: `tests/QuickConvert.Tests/QuickConvert.Tests.csproj`
- Modify: `tests/QuickConvert.Tests/Program.cs`
- Modify: `src/QuickConvert.App/MainViewModel.cs`

**Interfaces:**
- Produces `internal sealed record ConversionSettingChoice<T>(T Value, string Label)`.
- Produces `CompletionFolderPolicy.GetFolder(string kind, JobStatus status, bool enabled, IReadOnlyList<string> outputPaths)`.
- Exposes `QualityChoices`, `OutputDirectoryChoices`, `SelectedQuality`, `SelectedOutputDirectory`, `OpenFolderOnCompletion`, `HasCompatibleFormats`, and `FormatEmptyMessage`.

- [ ] **Step 1: Link the focused policy source into tests and write RED tests**

Link `CompletionFolderPolicy.cs` into the test project. Add tests asserting it returns:
- the first output directory for completed `convert` with enabled=true;
- null for disabled, failed, canceled, download kind, or no outputs.

Add a source contract test requiring all seven new ViewModel property names.

Expected RED: missing type/source.

- [ ] **Step 2: Implement the policy and choices**

`CompletionFolderPolicy.GetFolder` returns a directory only when all conditions match and the first path has a nonempty directory.

```csharp
public static string? GetFolder(
    string kind,
    JobStatus status,
    bool enabled,
    IReadOnlyList<string> outputPaths)
{
    if (!enabled || kind != "convert" || status != JobStatus.Completed || outputPaths.Count == 0)
        return null;
    return Path.GetDirectoryName(outputPaths[0]);
}
```

Create choices:
- `Economy → "Oszczędna"`;
- `Balanced → "Zbalansowana"`;
- `Highest → "Najwyższa"`;
- `Adjacent → "Obok oryginału"`;
- `DownloadsQuickConvert → "Pobrane\\QuickConvert"`.

- [ ] **Step 3: Load and save settings in MainViewModel**

Create `JsonSettingsStore` at the LocalAppData settings path. Initialize public choice collections immediately, then asynchronously load settings and update selected properties on the UI context.

```csharp
var settingsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "QuickConvert",
    "settings.json");
_settingsStore = new JsonSettingsStore(settingsPath);
```

Each setter:
- normalizes and updates the backing field;
- raises `PropertyChanged`;
- starts `SaveSettingsIgnoringErrorsAsync`.

Do not write during initial field construction. Saving catches `IOException` and `UnauthorizedAccessException`.

Use `SelectedQuality.Value` and `SelectedOutputDirectory.Value` when constructing `ConvertFilesRequest`.

```csharp
var request = new ConvertFilesRequest(
    SelectedFiles.ToArray(),
    selectedFormat,
    SelectedQuality.Value,
    SelectedOutputDirectory.Value);
```

Construct `ConversionEngine` with the existing `DownloadDirectory`.

- [ ] **Step 4: Implement empty-state properties**

After `LoadFiles`, raise changes for:
- `HasCompatibleFormats => CompatibleFormats.Count > 0`;
- `FormatEmptyMessage`, equal to `Najpierw wybierz pliki` when no selected paths, otherwise `Brak wspólnego formatu dla tego zestawu plików`.

- [ ] **Step 5: Open the completed conversion folder**

In the existing job-finished UI callback, call the policy with the current `OpenFolderOnCompletion`. If it returns a folder, start `explorer.exe` with that folder through `ProcessStartInfo.ArgumentList`, and catch `InvalidOperationException`, `System.ComponentModel.Win32Exception`, and `IOException`. Do not alter job status.

```csharp
var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
startInfo.ArgumentList.Add(folder);
Process.Start(startInfo);
```

- [ ] **Step 6: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
git add src/QuickConvert.App tests/QuickConvert.Tests
git commit -m "feat: connect persistent conversion settings"
```

---

### Task 5: Dark controls, custom chevron, and empty format state

**Files:**
- Modify: `src/QuickConvert.App/App.xaml`
- Modify: `src/QuickConvert.App/MainWindow.xaml`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Consumes ViewModel properties from Task 4.
- Produces `DarkComboBoxStyle`, `DarkCheckBoxStyle`, and `DarkExpanderStyle`.
- Removes the system expander glyph from the main window.

- [ ] **Step 1: Write failing XAML contract test**

Require:
- all three style keys in `App.xaml`;
- `ItemsSource="{Binding QualityChoices}"`;
- `SelectedItem="{Binding SelectedQuality}"`;
- equivalent output-directory bindings;
- `IsChecked="{Binding OpenFolderOnCompletion}"`;
- both format empty-state messages through `FormatEmptyMessage`;
- `Style="{StaticResource DarkExpanderStyle}"`;
- the literal note about FLAC/WAV/PNG/GIF.

Expected RED: styles and bindings are absent.

- [ ] **Step 2: Implement dark ComboBox and CheckBox styles**

Create compact templates using existing `SurfaceHoverBrush`, `BorderBrush`, `TextBrush`, `AccentBrush`, and `FocusBrush`. Preserve keyboard navigation and disabled state. ComboBox items must use a dark popup surface and readable selected/hover states.

The controls must consume the shared resources rather than fixed light colors:

```xml
<Setter Property="Background" Value="{StaticResource SurfaceHoverBrush}" />
<Setter Property="Foreground" Value="{StaticResource TextBrush}" />
<Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
```

- [ ] **Step 3: Implement custom ExpanderStyle**

The template contains:
- a header `ToggleButton`;
- a 10 × 10 Path chevron using `AccentHoverBrush`;
- right-pointing geometry when collapsed and down-pointing geometry when expanded;
- a content presenter visible only when expanded;
- visible keyboard focus.

Do not animate and do not use the system Expander template.

Use explicit geometries in triggers:

```xml
<Path x:Name="Chevron" Data="M 2 1 L 7 5 L 2 9"
      Stroke="{StaticResource AccentHoverBrush}" StrokeThickness="2" />
<Trigger Property="IsChecked" Value="True">
    <Setter TargetName="Chevron" Property="Data" Value="M 1 2 L 5 7 L 9 2" />
</Trigger>
```

- [ ] **Step 4: Add functional settings controls**

Inside the expander, use a two-column responsive layout or stacked rows:
- labels and ComboBoxes for quality/output;
- checkbox for opening the folder;
- muted lossless-format note.

Bind choice labels with `DisplayMemberPath="Label"`.

```xml
<ComboBox ItemsSource="{Binding QualityChoices}"
          SelectedItem="{Binding SelectedQuality}"
          DisplayMemberPath="Label"
          Style="{StaticResource DarkComboBoxStyle}" />
<ComboBox ItemsSource="{Binding OutputDirectoryChoices}"
          SelectedItem="{Binding SelectedOutputDirectory}"
          DisplayMemberPath="Label"
          Style="{StaticResource DarkComboBoxStyle}" />
<CheckBox Content="Otwórz folder po zakończeniu"
          IsChecked="{Binding OpenFolderOnCompletion}"
          Style="{StaticResource DarkCheckBoxStyle}" />
```

- [ ] **Step 5: Add empty state**

Place a muted `TextBlock` before the format ItemsControl. Use a `DataTrigger` on `HasCompatibleFormats=False` to show it, and hide it when true. Keep format wrapping unchanged.

- [ ] **Step 6: Verify and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
npm run test:extensions
npm run check:extensions
git diff --check
git add src/QuickConvert.App tests/QuickConvert.Tests/Program.cs
git commit -m "style: polish conversion settings panel"
```

---

### Task 6: Release build and visual verification

**Files:**
- Generated: `artifacts/publish/QuickConvert.exe`
- Generated: `artifacts/installer/QuickConvert-0.1.0-win-x64-setup.exe`

- [ ] **Step 1: Build the full release**

```powershell
.\tools\build-release.ps1
```

Expected: .NET publish, tool preparation, extensions, and Inno Setup all succeed.

- [ ] **Step 2: Verify the live application**

Check:
- custom purple chevron, no white circle;
- “Najpierw wybierz pliki” before selection;
- no-common-format message for an incompatible mixed selection;
- all controls remain readable in the dark theme;
- settings survive close and reopen;
- each preset produces a successful short conversion;
- Downloads mode writes to `Downloads\QuickConvert`;
- open-folder triggers only after completed conversions.

- [ ] **Step 3: Run final automated verification**

```powershell
dotnet run --no-build --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj -- --integration
dotnet build QuickConvert.slnx --configuration Release --no-restore
npm run test:extensions
npm run check:extensions
git diff --check
git status --short --branch
```

Expected: all .NET tests including real FFmpeg pass, build has zero warnings, extension checks pass, and the worktree is clean after planned commits.
