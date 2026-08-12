# QuickConvert Branding and Native Title Bar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the selected “Q in motion” identity to QuickConvert, make the native Windows title bar dark, and reuse the same branding in the application and installer.

**Architecture:** Generate committed SVG, PNG, and multi-size ICO assets with one dependency-free PowerShell script. Keep the native WPF window chrome and apply DWM immersive dark mode through a small injectable adapter, then wire the ICO into WPF and Inno Setup.

**Tech Stack:** .NET 8 WPF, C#, Windows DWM API, PowerShell with System.Drawing, SVG/PNG/ICO, Inno Setup 6.

## Global Constraints

- Target Windows 10/11 x64.
- Selected mark: “C — Q in motion”.
- Gradient: `#9B8CFF` to `#5145D6`; white symbol.
- ICO sizes: 16, 20, 24, 32, 40, 48, 64, 128, and 256 px.
- Preserve native window chrome, resizing, system menu, Snap Layouts, and caption buttons.
- DWM failure must never prevent application startup.
- Do not add an external graphics or UI dependency.
- Do not change conversion, downloader, IPC, or installation scope.

---

### Task 1: Reproducible branding assets

**Files:**
- Create: `tools/build-brand-assets.ps1`
- Create: `assets/branding/quickconvert-logo.svg`
- Generate: `assets/branding/quickconvert.ico`
- Generate: `assets/branding/quickconvert-256.png`
- Generate: `assets/branding/quickconvert-wizard-small.png`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Consumes: no external tools; Windows `System.Drawing`.
- Produces: `assets/branding/quickconvert.ico` with PNG-compressed frames at the nine required sizes plus two PNG files and an SVG source.

- [ ] **Step 1: Write the failing asset contract test**

Add a helper that reads the ICO directory entries and this test to `Program.cs`:

```csharp
tests.Run("branding assets contain the selected Q mark and required icon sizes", () =>
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var branding = Path.Combine(root, "assets", "branding");
    var svg = File.ReadAllText(Path.Combine(branding, "quickconvert-logo.svg"));
    TestSuite.Equal(true, svg.Contains("#9B8CFF", StringComparison.OrdinalIgnoreCase));
    TestSuite.Equal(true, svg.Contains("#5145D6", StringComparison.OrdinalIgnoreCase));
    TestSuite.Equal(true, svg.Contains("aria-label=\"QuickConvert Q in motion\"", StringComparison.Ordinal));

    var sizes = ReadIcoSizes(Path.Combine(branding, "quickconvert.ico"));
    TestSuite.SequenceEqual(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }, sizes);

    TestSuite.Equal((256, 256), ReadPngSize(Path.Combine(branding, "quickconvert-256.png")));
    TestSuite.Equal((64, 64), ReadPngSize(Path.Combine(branding, "quickconvert-wizard-small.png")));
});
```

Add:

```csharp
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
```

- [ ] **Step 2: Run the test and verify RED**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
```

Expected: FAIL with a missing `assets/branding/quickconvert-logo.svg`.

- [ ] **Step 3: Add the SVG source**

Create a 64 × 64 SVG containing a 58 × 58 rounded square at (3,3), a diagonal purple gradient, a white circular Q stroke with an arrowhead, and a white Q tail. Use the exact label and colors asserted by the test:

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"
     role="img" aria-label="QuickConvert Q in motion">
  <defs>
    <linearGradient id="quickconvert-gradient" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#9B8CFF"/>
      <stop offset="1" stop-color="#5145D6"/>
    </linearGradient>
  </defs>
  <rect x="3" y="3" width="58" height="58" rx="16" fill="url(#quickconvert-gradient)"/>
  <path d="M42.5 39.5A14 14 0 1 1 46 25l-5.4.2 8-10.2 8.8 9.6-5.3.2A20 20 0 1 0 47 45Z" fill="#fff"/>
  <path d="M34 35 48 49" stroke="#fff" stroke-width="6" stroke-linecap="round"/>
</svg>
```

- [ ] **Step 4: Add the dependency-free generator**

Create `tools/build-brand-assets.ps1` with `$ErrorActionPreference = "Stop"`. Load `System.Drawing`, draw the same rounded gradient square and two white paths into transparent anti-aliased bitmaps, and encode each frame as PNG.

The script must:
- use `[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias`;
- scale all geometry from a 64-unit coordinate system;
- create PNG frames for `@(16,20,24,32,40,48,64,128,256)`;
- write an ICONDIR header, nine ICONDIRENTRY records, then PNG bytes;
- save the 256 frame as `quickconvert-256.png`;
- render a separate 64 × 64 `quickconvert-wizard-small.png`;
- dispose every bitmap, graphics object, brush, pen, path, and stream in `finally` blocks.

The ICO entry writer uses:

```powershell
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$frames.Count)
$offset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $writer.Write([byte]$(if ($frame.Size -eq 256) { 0 } else { $frame.Size }))
    $writer.Write([byte]$(if ($frame.Size -eq 256) { 0 } else { $frame.Size }))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$frame.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $frame.Bytes.Length
}
foreach ($frame in $frames) { $writer.Write($frame.Bytes) }
```

- [ ] **Step 5: Generate assets and verify GREEN**

```powershell
.\tools\build-brand-assets.ps1
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
```

Expected: all tests pass, and rerunning the script produces identical SHA-256 hashes.

- [ ] **Step 6: Commit assets and generator**

```powershell
git add tools/build-brand-assets.ps1 assets/branding tests/QuickConvert.Tests/Program.cs
git commit -m "feat: add QuickConvert brand assets"
```

---

### Task 2: Fault-tolerant native dark title bar

**Files:**
- Create: `src/QuickConvert.App/DarkTitleBar.cs`
- Modify: `tests/QuickConvert.Tests/QuickConvert.Tests.csproj`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Produces: `internal delegate int DwmAttributeSetter(nint handle, int attribute, ref int value, int size)`.
- Produces: `internal static bool DarkTitleBar.TryApply(nint windowHandle, DwmAttributeSetter? setter = null)`.
- Returns true when attribute 20 or fallback 19 succeeds; returns false without throwing otherwise.

- [ ] **Step 1: Link the focused production source into tests**

Add:

```xml
<Compile Include="../../src/QuickConvert.App/DarkTitleBar.cs"
         Link="Ui/DarkTitleBar.cs" />
```

- [ ] **Step 2: Write failing fallback tests**

```csharp
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
```

- [ ] **Step 3: Verify RED**

Run the .NET tests. Expected: compilation fails because `DarkTitleBar` does not exist.

- [ ] **Step 4: Implement the adapter**

Use `[DllImport("dwmapi.dll", PreserveSig = true)]` for a private native setter. `TryApply` must return false for a zero handle, call attribute 20 first, call 19 only after a nonzero HRESULT, and catch `DllNotFoundException`, `EntryPointNotFoundException`, and `BadImageFormatException`.

- [ ] **Step 5: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
git add src/QuickConvert.App/DarkTitleBar.cs tests/QuickConvert.Tests/QuickConvert.Tests.csproj tests/QuickConvert.Tests/Program.cs
git commit -m "feat: support native dark Windows title bar"
```

Expected: all tests pass and build has no warnings.

---

### Task 3: Application icon and window integration

**Files:**
- Modify: `src/QuickConvert.App/QuickConvert.App.csproj`
- Modify: `src/QuickConvert.App/MainWindow.xaml`
- Modify: `src/QuickConvert.App/MainWindow.xaml.cs`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Consumes: `assets/branding/quickconvert.ico` and `DarkTitleBar.TryApply`.
- Produces: an embedded EXE/window icon, DWM invocation after handle creation, and the selected vector mark in the in-app header.

- [ ] **Step 1: Write failing source integration test**

```csharp
tests.Run("application embeds branding and applies dark title bar after handle creation", () =>
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var project = File.ReadAllText(Path.Combine(root, "src", "QuickConvert.App", "QuickConvert.App.csproj"));
    var xaml = File.ReadAllText(Path.Combine(root, "src", "QuickConvert.App", "MainWindow.xaml"));
    var codeBehind = File.ReadAllText(Path.Combine(root, "src", "QuickConvert.App", "MainWindow.xaml.cs"));

    TestSuite.Equal(true, project.Contains("<ApplicationIcon>../../assets/branding/quickconvert.ico</ApplicationIcon>"));
    TestSuite.Equal(true, project.Contains("Link=\"Assets/quickconvert.ico\""));
    TestSuite.Equal(true, xaml.Contains("Icon=\"Assets/quickconvert.ico\""));
    TestSuite.Equal(true, xaml.Contains("Data=\"M42.5 39.5", StringComparison.Ordinal));
    TestSuite.Equal(true, codeBehind.Contains("OnSourceInitialized", StringComparison.Ordinal));
    TestSuite.Equal(true, codeBehind.Contains("DarkTitleBar.TryApply", StringComparison.Ordinal));
});
```

- [ ] **Step 2: Verify RED**

Run the .NET tests. Expected: FAIL because the project and window do not reference the ICO.

- [ ] **Step 3: Embed the ICO**

Add:

```xml
<ApplicationIcon>../../assets/branding/quickconvert.ico</ApplicationIcon>
```

and:

```xml
<Resource Include="../../assets/branding/quickconvert.ico"
          Link="Assets/quickconvert.ico" />
```

Set `Icon="Assets/quickconvert.ico"` on `MainWindow`.

- [ ] **Step 4: Replace the header monogram**

Replace the text `Q` inside the 48 px header tile with a 64-unit `Viewbox` containing the selected two white WPF `Path` elements. Keep the existing purple surface and header layout.

- [ ] **Step 5: Apply DWM after source initialization**

Override:

```csharp
protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);
    DarkTitleBar.TryApply(new WindowInteropHelper(this).Handle);
}
```

Add `using System.Windows.Interop;`.

- [ ] **Step 6: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
git add src/QuickConvert.App tests/QuickConvert.Tests/Program.cs
git commit -m "feat: integrate QuickConvert branding into the app"
```

Expected: tests pass, WPF resources compile, and EXE metadata contains the custom icon.

---

### Task 4: Installer branding and final release verification

**Files:**
- Modify: `installer/QuickConvert.iss`
- Modify: `tests/QuickConvert.Tests/Program.cs`
- Generate: `artifacts/installer/QuickConvert-0.1.0-win-x64-setup.exe`

**Interfaces:**
- Consumes: branding assets from Task 1 and the existing release pipeline.
- Produces: branded setup executable and wizard header while preserving all registry and Native Messaging entries.

- [ ] **Step 1: Write failing installer contract test**

```csharp
tests.Run("installer uses QuickConvert branding assets", () =>
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var installer = File.ReadAllText(Path.Combine(root, "installer", "QuickConvert.iss"));
    TestSuite.Equal(true, installer.Contains(@"SetupIconFile=..\assets\branding\quickconvert.ico"));
    TestSuite.Equal(true, installer.Contains(@"WizardSmallImageFile=..\assets\branding\quickconvert-wizard-small.png"));
    TestSuite.Equal(true, installer.Contains(@"UninstallDisplayIcon={app}\{#MyAppExeName}"));
});
```

- [ ] **Step 2: Verify RED**

Run the .NET tests. Expected: FAIL because `SetupIconFile` and `WizardSmallImageFile` are absent.

- [ ] **Step 3: Configure Inno Setup**

Add directly under the other visual setup properties:

```ini
SetupIconFile=..\assets\branding\quickconvert.ico
WizardSmallImageFile=..\assets\branding\quickconvert-wizard-small.png
```

Do not change `AppId`, registry entries, install directory, privileges, or Native Messaging manifests.

- [ ] **Step 4: Verify tests and build a fresh release**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
.\tools\build-release.ps1
```

Expected: tests pass; Inno Setup reports a successful compile and writes the setup EXE.

- [ ] **Step 5: Inspect generated branding**

Verify:
- the built app and setup EXE show the Q-in-motion icon in Explorer;
- the native title bar is dark and shows the icon at 100% and 150% scaling;
- minimize, maximize, close, Alt+Space, dragging, resizing, and Windows 11 Snap Layouts still work;
- the installer header displays the 64 px branding without stretching;
- uninstall and shell integration retain the application icon.

- [ ] **Step 6: Run final verification**

```powershell
dotnet run --no-build --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj -- --integration
dotnet build QuickConvert.slnx --configuration Release --no-restore
npm run test:extensions
npm run check:extensions
git diff --check
git status --short --branch
```

Expected: at least 34 .NET tests including real FFmpeg pass; build and extension checks exit 0; diff check is empty.

- [ ] **Step 7: Commit installer configuration**

```powershell
git add installer/QuickConvert.iss tests/QuickConvert.Tests/Program.cs
git commit -m "feat: brand QuickConvert installer"
```
