# Dark Fluent UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Replace the low-contrast WPF presentation with a readable dark Fluent interface while preserving all QuickConvert behavior and bindings.

**Architecture:** Keep MVVM and the existing TabControl. Define reusable presentation resources in App.xaml, consume them from a redesigned MainWindow.xaml, and protect critical resource and binding contracts with XAML source tests in the existing console test project.

**Tech Stack:** .NET 8, WPF XAML, C#, existing custom console test runner; no external UI libraries.

## Global Constraints

- Windows 10/11 x64; fixed dark theme using #10131A and a purple accent.
- Segoe UI, 8–16 px rounded geometry, explicit high-contrast surfaces.
- Preserve all commands, collections, scrolling, and keyboard navigation.
- No drag-and-drop, theme switching, animation, icon library, or engine changes.
- Correct Polish copy and save XAML as UTF-8.

---

### Task 1: Shared dark Fluent theme

**Files:**
- Modify: tests/QuickConvert.Tests/QuickConvert.Tests.csproj
- Modify: tests/QuickConvert.Tests/Program.cs
- Modify: src/QuickConvert.App/App.xaml

**Interfaces:**
- Produces brushes WindowBrush, SurfaceBrush, SurfaceHoverBrush, BorderBrush, AccentBrush, AccentHoverBrush, TextBrush, MutedBrush, DangerBrush, WarningBrush.
- Produces styles CardStyle, PrimaryButtonStyle, SecondaryButtonStyle, DangerButtonStyle, FormatButtonStyle, SectionTitleStyle, MutedTextStyle.

- [ ] **Step 1: Copy production XAML into test output**

Add to the test project:

```xml
<ItemGroup>
  <None Include="../../src/QuickConvert.App/App.xaml"
        Link="Ui/App.xaml" CopyToOutputDirectory="PreserveNewest" />
  <None Include="../../src/QuickConvert.App/MainWindow.xaml"
        Link="Ui/MainWindow.xaml" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing resource contract test**

```csharp
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
```

- [ ] **Step 3: Verify RED**

Run:

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
```

Expected: FAIL because the named resources and interaction triggers do not exist.

- [ ] **Step 4: Implement App.xaml resources**

Create the listed brushes with these values:

```xml
<SolidColorBrush x:Key="WindowBrush" Color="#10131A" />
<SolidColorBrush x:Key="SurfaceBrush" Color="#191E29" />
<SolidColorBrush x:Key="SurfaceHoverBrush" Color="#222938" />
<SolidColorBrush x:Key="BorderBrush" Color="#2D3545" />
<SolidColorBrush x:Key="AccentBrush" Color="#7567F8" />
<SolidColorBrush x:Key="AccentHoverBrush" Color="#887CFF" />
<SolidColorBrush x:Key="TextBrush" Color="#F5F7FF" />
<SolidColorBrush x:Key="MutedBrush" Color="#A8B0C2" />
<SolidColorBrush x:Key="DangerBrush" Color="#D65368" />
<SolidColorBrush x:Key="WarningBrush" Color="#F2C66D" />
```

Add an explicit Window background/foreground, CardStyle with a 14 px radius, and a rounded base button template. Its triggers must visibly distinguish mouse-over, pressed, disabled, and keyboard focus. Base primary, secondary, danger, and format styles on that template.

- [ ] **Step 5: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
git add tests/QuickConvert.Tests/QuickConvert.Tests.csproj tests/QuickConvert.Tests/Program.cs src/QuickConvert.App/App.xaml
git commit -m "style: add reusable dark Fluent theme"
```

Expected: all tests pass and build exits 0.

---

### Task 2: Main window hierarchy

**Files:**
- Modify: tests/QuickConvert.Tests/Program.cs
- Modify: src/QuickConvert.App/MainWindow.xaml

**Interfaces:**
- Consumes Task 1 resources and every existing MainViewModel binding.
- Produces the header, themed tabs, selection and queue cards, history cards, information cards, and danger zone.

- [ ] **Step 1: Write the failing view contract test**

```csharp
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

    TestSuite.Equal(true, xaml.Contains("Background=\"{StaticResource WindowBrush}\""));
    TestSuite.Equal(true, xaml.Contains("Style=\"{StaticResource CardStyle}\""));
    TestSuite.Equal(true, xaml.Contains("Style=\"{StaticResource DangerButtonStyle}\""));
});
```

- [ ] **Step 2: Verify RED**

Run the .NET test command from Task 1. Expected: FAIL on the new copy and styles.

- [ ] **Step 3: Rebuild the shell**

Set the window to 800 × 640 with explicit WindowBrush and TextBrush. Add a rounded Q monogram, product name, subtitle, and right-aligned “Lokalnie • Bez chmury” capsule. Keep TabControl semantics but style headers as rounded selectors with visible focus.

- [ ] **Step 4: Rebuild Convert**

Use a vertical ScrollViewer. Add a CardStyle selection card with SelectionTitle, supporting text, primary file button, format heading, unchanged CompatibleFormats and ConvertCommand bindings, WarningBrush warning, and corrected settings expander. Render queue entries as CardStyle items with unchanged bindings and distinct primary, secondary, and danger actions.

- [ ] **Step 5: Rebuild History and Information**

Render history as full-width cards. Split Information into Folder pobierania, Prywatność, Aktualizacje i diagnostyka, and Strefa niebezpieczna cards. Keep existing DownloadDirectory, UpdateMessage, and commands. Apply DangerButtonStyle only to data clearing.

- [ ] **Step 6: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
npm run test:extensions
npm run check:extensions
git add tests/QuickConvert.Tests/Program.cs src/QuickConvert.App/MainWindow.xaml
git commit -m "style: redesign QuickConvert main window"
```

Expected: .NET tests, build, extension tests, and JavaScript syntax checks all exit 0.

---

### Task 3: Visual QA and final verification

**Files:**
- Modify only for an observed defect: src/QuickConvert.App/App.xaml
- Modify only for an observed defect: src/QuickConvert.App/MainWindow.xaml

- [ ] **Step 1: Launch the Release app**

```powershell
dotnet run --no-build --configuration Release --project src\QuickConvert.App\QuickConvert.App.csproj
```

Expected: a dark #10131A main window instead of the white surface in the supplied screenshot.

- [ ] **Step 2: Inspect visual states**

Check all tabs, mouse and keyboard navigation, hover, pressed, disabled and focus states, format wrapping, Polish glyphs, the default 800 × 640 layout, and scrolling at 620 × 480. Confirm the destructive action is separated and red.

- [ ] **Step 3: Correct only observed XAML defects**

Adjust only spacing, sizing, wrapping, brushes, or triggers. Do not alter the view model. Relaunch and repeat Step 2 after each change.

- [ ] **Step 4: Run final verification**

```powershell
dotnet run --no-build --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj -- --integration
dotnet build QuickConvert.slnx --configuration Release --no-restore
npm run test:extensions
npm run check:extensions
git diff --check
git status --short --branch
```

Expected: at least 29 .NET tests including real FFmpeg pass; build and extension checks exit 0; diff check is empty.

- [ ] **Step 5: Commit visual corrections when present**

```powershell
git add src/QuickConvert.App/App.xaml src/QuickConvert.App/MainWindow.xaml
git commit -m "fix: polish dark Fluent layout"
```

