# QuickConvert Zen Background and Tray v0.2.2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish QuickConvert `v0.2.2` with reliable Firefox/Zen native-messaging replies, a user-controlled background mode, and the QuickConvert product icon in the Windows tray.

**Architecture:** Keep the Chrome and Firefox/Zen response strategies separate inside the shared extension background script: Firefox returns a Promise, while Chrome retains `sendResponse` plus `return true`. Persist background behavior in the existing JSON settings store, expose a settings-load completion task from the view model, and route activation/close/tray decisions through a small pure `BackgroundBehaviorPolicy` before applying them in `App` and `MainWindow`.

**Tech Stack:** .NET 8, WPF, Windows Forms `NotifyIcon`, System.Text.Json, JavaScript WebExtensions Manifest V3, Node.js built-in test/assert/vm modules, PowerShell, Inno Setup, GitHub Actions.

## Global Constraints

- Product version is exactly `0.2.2`; Git tag is exactly `v0.2.2`; release name is exactly `QuickConvert v0.2.2`.
- Windows support remains Windows 10/11 x64 and installation remains per-user without administrator rights.
- Browser extension IDs remain `abpjmchafogplinlgklgfoljglakhalp` for Chrome and `quickconvert@local` for Firefox/Zen.
- Firefox/Zen uses a Promise-returning message listener; Chrome uses `sendResponse` and `return true`.
- `RunInBackgroundDuringJobs` defaults to `true`, including settings files created by versions that do not contain that property.
- With background mode enabled, extension downloads may run without opening the window and the tray icon exists only while work is active or a completion notification is being shown.
- With background mode disabled, extension downloads show/restore the window, closing an active window is blocked, and active work is never canceled merely because the user clicked X.
- Shell conversion and explicit activation always show/restore the main window.
- The tray uses the embedded `Assets/quickconvert.ico`; `SystemIcons.Application` must not be used.
- Existing privacy, URL validation, no-playlist, no-overwrite, partial-file, and cancellation guarantees remain unchanged.

---

### Task 1: Make native-messaging replies reliable in Firefox/Zen and Chrome

**Files:**
- Create: `extensions/tests/background.test.js`
- Modify: `extensions/shared/background.js`
- Modify: `package.json`

**Interfaces:**
- Consumes: browser globals `globalThis.browser` and `globalThis.chrome` and native host name `com.quickconvert.app`.
- Produces: `createFirefoxListener(browserApi)` returning `(message) => Promise<object> | false` and `createChromeListener(chromeApi)` returning `(message, sender, sendResponse) => boolean`, retained as top-level functions in `background.js` for VM-based tests.

- [ ] **Step 1: Add failing cross-browser listener tests**

Create `extensions/tests/background.test.js` with a Node `vm` harness that loads `extensions/shared/background.js` twice. In the Firefox harness expose only `browser`; capture the listener passed to `runtime.onMessage.addListener`; assert that a download returns the exact Promise response, a rejected native call resolves to `{code:"app_unavailable"}`, a null response resolves to the same fallback, and an unrelated action returns `false`. In the Chrome harness expose only `chrome`; assert that a download immediately returns `true`, later calls `sendResponse` with the exact host response, maps rejection/null to the fallback, and an unrelated action returns `false` without calling native messaging.

Use these concrete inputs and assertions:

```js
const download = { action: "download", payload: { requestId: "req-1" } };
assert.deepStrictEqual(await firefoxListener(download), { code: "accepted" });
assert.deepStrictEqual(await rejectedFirefoxListener(download), { code: "app_unavailable" });
assert.strictEqual(firefoxListener({ action: "ping" }), false);

let response;
assert.strictEqual(chromeListener(download, {}, value => { response = value; }), true);
await Promise.resolve();
assert.deepStrictEqual(response, { code: "accepted" });
assert.strictEqual(chromeListener({ action: "ping" }, {}, () => {}), false);
```

Update `package.json` so `test:extensions` runs both files:

```json
"test:extensions": "node extensions/tests/common.test.js && node extensions/tests/background.test.js"
```

- [ ] **Step 2: Run the extension tests to verify RED**

Run: `npm run test:extensions`

Expected: `common.test.js` passes and `background.test.js` fails because the current listener always uses the Chrome callback pattern.

- [ ] **Step 3: Implement the two response strategies**

Replace `extensions/shared/background.js` with the following behavior:

```js
"use strict";

const unavailable = () => ({ code: "app_unavailable" });
const isDownload = message => Boolean(message && message.action === "download");

function createFirefoxListener(browserApi) {
  return message => {
    if (!isDownload(message)) return false;
    return browserApi.runtime
      .sendNativeMessage("com.quickconvert.app", message.payload)
      .then(response => response ?? unavailable())
      .catch(unavailable);
  };
}

function createChromeListener(chromeApi) {
  return (message, _sender, sendResponse) => {
    if (!isDownload(message)) return false;
    chromeApi.runtime
      .sendNativeMessage("com.quickconvert.app", message.payload)
      .then(response => sendResponse(response ?? unavailable()))
      .catch(() => sendResponse(unavailable()));
    return true;
  };
}

if (globalThis.browser) {
  browser.runtime.onMessage.addListener(createFirefoxListener(browser));
} else {
  chrome.runtime.onMessage.addListener(createChromeListener(chrome));
}
```

Do not make the Firefox listener `async`; unrelated messages must synchronously return `false`.

- [ ] **Step 4: Run extension verification to confirm GREEN**

Run: `npm run test:extensions`

Run: `npm run check:extensions`

Expected: both commands exit `0`; all Firefox and Chrome branch assertions pass and all shared scripts parse successfully.

- [ ] **Step 5: Commit the browser fix**

```powershell
git add extensions/shared/background.js extensions/tests/background.test.js package.json
git commit -m "fix: support Zen native messaging responses"
```

---

### Task 2: Persist the background-mode preference with backward-compatible defaults

**Files:**
- Modify: `src/QuickConvert.Core/Settings/QuickConvertSettings.cs`
- Modify: `src/QuickConvert.Core/Settings/JsonSettingsStore.cs`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Produces: `QuickConvertSettings(..., bool OpenFolderOnCompletion, bool RunInBackgroundDuringJobs)`.
- Produces: `QuickConvertSettings.Defaults.RunInBackgroundDuringJobs == true`.
- Produces: legacy JSON without `runInBackgroundDuringJobs` loads that property as `true`, while explicit `false` round-trips unchanged.

- [ ] **Step 1: Add failing settings tests**

Extend the existing settings tests in `tests/QuickConvert.Tests/Program.cs` with these exact checks:

```csharp
TestSuite.Equal(true, settings.RunInBackgroundDuringJobs);
```

Add a legacy-file test that writes:

```json
{"qualityPreset":"Balanced","outputDirectoryMode":"Adjacent","openFolderOnCompletion":false}
```

and asserts `RunInBackgroundDuringJobs` is `true`. Update both explicit `new QuickConvertSettings(...)` calls to include `false`, then assert the loaded value remains `false`.

- [ ] **Step 2: Run the core test executable to verify RED**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Expected: compilation fails because `RunInBackgroundDuringJobs` and the fourth constructor parameter do not exist.

- [ ] **Step 3: Add the setting and a nullable storage DTO**

Change the record to:

```csharp
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
```

In `JsonSettingsStore`, deserialize into a private storage record whose `RunInBackgroundDuringJobs` is `bool?`. Normalize it with `stored.RunInBackgroundDuringJobs ?? true`, validate the enum values before constructing `QuickConvertSettings`, and continue serializing `QuickConvertSettings` directly. This distinguishes a missing legacy property from an explicitly saved `false`.

- [ ] **Step 4: Run settings tests to confirm GREEN**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Expected: all tests pass; malformed settings still fall back to defaults, legacy JSON produces `true`, and explicit `false` survives a save/load round trip.

- [ ] **Step 5: Commit settings persistence**

```powershell
git add src/QuickConvert.Core/Settings tests/QuickConvert.Tests/Program.cs
git commit -m "feat: persist background job preference"
```

---

### Task 3: Expose the setting in the UI and make settings initialization awaitable

**Files:**
- Modify: `src/QuickConvert.App/MainViewModel.cs`
- Modify: `src/QuickConvert.App/MainWindow.xaml`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Produces: `public bool RunInBackgroundDuringJobs { get; set; }` on `MainViewModel`.
- Produces: `public Task SettingsLoaded { get; }`, completed only after settings have been applied on the UI synchronization context, including fallback after handled load errors.
- Consumes: four-property `QuickConvertSettings` from Task 2.

- [ ] **Step 1: Add failing UI and source-contract tests**

In the existing XAML settings test require all of these strings:

```csharp
"IsChecked=\"{Binding RunInBackgroundDuringJobs}\""
"Pracuj w tle podczas zadań"
"Po zamknięciu okna aktywne zadania pozostaną w zasobniku."
```

Add a source-contract test that reads `src/QuickConvert.App/MainViewModel.cs` from the repository root and requires `public Task SettingsLoaded`, assignment of `RunInBackgroundDuringJobs = settings.RunInBackgroundDuringJobs`, and the fourth value in `new QuickConvertSettings(...)`.

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Expected: FAIL because the binding, copy, property, and initialization task are missing.

- [ ] **Step 3: Implement the view-model setting and completion signal**

Add `_runInBackgroundDuringJobs = true` and a `TaskCompletionSource` created with `RunContinuationsAsynchronously`. Expose:

```csharp
public Task SettingsLoaded => _settingsLoaded.Task;

public bool RunInBackgroundDuringJobs
{
    get => _runInBackgroundDuringJobs;
    set
    {
        if (Set(ref _runInBackgroundDuringJobs, value))
            SaveSettingsIfReady();
    }
}
```

Apply the stored property inside `LoadSettingsAsync`, complete `_settingsLoaded` after the UI callback has applied all four settings, and include the property in `SaveSettingsIgnoringErrorsAsync`. Use `TaskCompletionSource.TrySetResult()` in a `finally` path so the first IPC request cannot wait forever when loading fails.

- [ ] **Step 4: Add the checkbox and explanatory copy**

Add one row to the existing expanded settings grid after “Otwórz folder po zakończeniu”. The row contains a vertical stack with a dark checkbox bound to `RunInBackgroundDuringJobs` and the muted explanatory sentence directly below it. Move the lossless-format note to the following row and extend `RowDefinitions` accordingly.

- [ ] **Step 5: Run tests and build to confirm GREEN**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Run: `dotnet build QuickConvert.slnx --configuration Release`

Expected: all tests pass and the solution builds with zero warnings/errors.

- [ ] **Step 6: Commit the setting UI and initialization barrier**

```powershell
git add src/QuickConvert.App/MainViewModel.cs src/QuickConvert.App/MainWindow.xaml tests/QuickConvert.Tests/Program.cs
git commit -m "feat: add background mode setting"
```

---

### Task 4: Centralize activation, close, and tray decisions in a pure policy

**Files:**
- Create: `src/QuickConvert.App/BackgroundBehaviorPolicy.cs`
- Modify: `tests/QuickConvert.Tests/QuickConvert.Tests.csproj`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Produces: `internal enum WindowCloseAction { Close, HideToTray, KeepVisible }`.
- Produces: `BackgroundBehaviorPolicy.ShouldShowForEnvelope(string operation, bool runInBackgroundDuringJobs) : bool`.
- Produces: `BackgroundBehaviorPolicy.GetCloseAction(bool hasActiveJobs, bool runInBackgroundDuringJobs) : WindowCloseAction`.
- Produces: `BackgroundBehaviorPolicy.ShouldShowTray(bool hasActiveJobs, bool runInBackgroundDuringJobs) : bool`.

- [ ] **Step 1: Link the future policy into the test project and add failing matrix tests**

Add a linked `Compile` item for `BackgroundBehaviorPolicy.cs`. Add matrix assertions covering:

```csharp
TestSuite.Equal(false, BackgroundBehaviorPolicy.ShouldShowForEnvelope("download", true));
TestSuite.Equal(true, BackgroundBehaviorPolicy.ShouldShowForEnvelope("download", false));
TestSuite.Equal(true, BackgroundBehaviorPolicy.ShouldShowForEnvelope("convert", true));
TestSuite.Equal(true, BackgroundBehaviorPolicy.ShouldShowForEnvelope("activate", true));
TestSuite.Equal(WindowCloseAction.HideToTray, BackgroundBehaviorPolicy.GetCloseAction(true, true));
TestSuite.Equal(WindowCloseAction.KeepVisible, BackgroundBehaviorPolicy.GetCloseAction(true, false));
TestSuite.Equal(WindowCloseAction.Close, BackgroundBehaviorPolicy.GetCloseAction(false, true));
TestSuite.Equal(true, BackgroundBehaviorPolicy.ShouldShowTray(true, true));
TestSuite.Equal(false, BackgroundBehaviorPolicy.ShouldShowTray(true, false));
TestSuite.Equal(false, BackgroundBehaviorPolicy.ShouldShowTray(false, true));
```

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Expected: compilation fails because the policy file/types do not exist.

- [ ] **Step 3: Implement the minimal pure policy**

Create a dependency-free file containing the enum and static methods. `ShouldShowForEnvelope` returns false only for a case-insensitive `download` operation while background mode is enabled. `GetCloseAction` returns `Close` with no active jobs, otherwise `HideToTray` when enabled and `KeepVisible` when disabled. `ShouldShowTray` is exactly `hasActiveJobs && runInBackgroundDuringJobs`.

- [ ] **Step 4: Run the policy tests to confirm GREEN**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Expected: all policy matrix assertions pass.

- [ ] **Step 5: Commit the policy**

```powershell
git add src/QuickConvert.App/BackgroundBehaviorPolicy.cs tests/QuickConvert.Tests
git commit -m "feat: define window background policy"
```

---

### Task 5: Apply the policy and use the QuickConvert tray icon safely

**Files:**
- Modify: `src/QuickConvert.App/App.xaml.cs`
- Modify: `src/QuickConvert.App/MainWindow.xaml.cs`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Consumes: `MainViewModel.SettingsLoaded`, `MainViewModel.RunInBackgroundDuringJobs`, and all Task 4 policy methods.
- Produces: `MainWindow.RestoreWindow()` as an internal method callable from `App`.
- Produces: deterministic cleanup that unsubscribes view-model events and disposes `NotifyIcon`, its `ContextMenuStrip`, and the cloned tray `Icon` exactly once.

- [ ] **Step 1: Add failing source-contract tests for app routing and tray branding**

Read `App.xaml.cs` and `MainWindow.xaml.cs` in a repository-source test and require:

```csharp
"await _viewModel.SettingsLoaded"
"BackgroundBehaviorPolicy.ShouldShowForEnvelope"
"BackgroundBehaviorPolicy.GetCloseAction"
"BackgroundBehaviorPolicy.ShouldShowTray"
"Assets/quickconvert.ico"
```

Also assert that `MainWindow.xaml.cs` does not contain `SystemIcons.Application`, and that it contains disposal/unsubscription calls for `_notifyIcon`, `_notifyIcon.ContextMenuStrip`, `_trayIcon`, `PropertyChanged -=`, and `JobFinished -=`.

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Expected: FAIL because routing is unconditional and the tray still uses the generic system icon.

- [ ] **Step 3: Make IPC activation wait for settings and respect invocation type**

In `App.HandleEnvelopeAsync`, await `_viewModel.SettingsLoaded`, enqueue/prepare the envelope, and call `ShowMainWindow()` only when `BackgroundBehaviorPolicy.ShouldShowForEnvelope(envelope.Operation, _viewModel.RunInBackgroundDuringJobs)` is true. Keep explicit startup without `--background`, shell conversions, and `activate` envelopes visible. Call the now-internal `MainWindow.RestoreWindow()` from `ShowMainWindow()` so tray and WPF visibility state are updated in one place.

- [ ] **Step 4: Load and own the embedded product icon**

In `MainWindow`, load `pack://application:,,,/Assets/quickconvert.ico` through `Application.GetResourceStream`, create a cloned `System.Drawing.Icon`, keep it in `_trayIcon`, and assign it to the `NotifyIcon`. Dispose the source stream/icon promptly and keep the cloned icon alive until window cleanup. Throw a clear `InvalidOperationException` only if the packaged resource is genuinely missing.

- [ ] **Step 5: Apply close and tray behavior without canceling jobs**

Change `OnClosing` to switch on `BackgroundBehaviorPolicy.GetCloseAction(...)`:

- `HideToTray`: cancel closing, hide, show the branded tray icon, and show “Zadania nadal działają w tle.”
- `KeepVisible`: cancel closing, keep/show/activate the window, keep tray hidden, and do not cancel the queue.
- `Close`: allow normal shutdown.

On `HasActiveJobs` changes, set tray visibility only from `ShouldShowTray`. When the last hidden background job completes, show its final balloon, then use a cancellable two-second `DispatcherTimer` grace period before closing so `NotifyIcon` is not disposed before Windows receives the notification. If the user restores during the grace period, cancel the pending exit. When background mode changes from true to false during an active hidden job, restore the window immediately and hide the tray.

- [ ] **Step 6: Centralize cleanup and preserve completion notifications**

Create one idempotent cleanup method called on final close. It stops the grace timer, unsubscribes both view-model events, hides/disposes the notify icon and menu, and disposes `_trayIcon`. Keep `ViewModelOnJobFinished` notifications gated by background/tray state so a visible foreground job does not create a stray tray icon.

- [ ] **Step 7: Run tests and build to confirm GREEN**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Run: `dotnet build QuickConvert.slnx --configuration Release`

Expected: all tests pass, source contracts confirm the branded icon and policy integration, and the solution builds with zero warnings/errors.

- [ ] **Step 8: Perform a focused Windows smoke test**

Run the app from `src\QuickConvert.App\bin\Release\net8.0-windows10.0.19041.0\QuickConvert.exe` and verify:

1. Background ON + active job + X hides to the QuickConvert Q tray icon; double-click restores.
2. Background OFF + active job + X leaves the window visible and the job running.
3. Background ON + native download request starts without forcing a hidden window open.
4. Background OFF + native download request restores the window.
5. After the last hidden job, the completion notification appears before the process exits.

Expected: all five behaviors match; no generic Windows application icon appears.

- [ ] **Step 9: Commit app integration**

```powershell
git add src/QuickConvert.App/App.xaml.cs src/QuickConvert.App/MainWindow.xaml.cs tests/QuickConvert.Tests/Program.cs
git commit -m "feat: apply branded background tray behavior"
```

---

### Task 6: Set the v0.2.2 release contract and package both extensions

**Files:**
- Modify: `Directory.Build.props`
- Modify: `installer/QuickConvert.iss`
- Modify: `extensions/chrome/manifest.json`
- Modify: `extensions/firefox/manifest.json`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Produces: application and installer version `0.2.2`.
- Produces: Chrome and Firefox manifest version `0.2.2` without changing their stable IDs or permissions.
- Produces: installer `QuickConvert-0.2.2-win-x64-setup.exe` and Firefox package `quickconvert-firefox.xpi` through the existing release workflow.

- [ ] **Step 1: Change release-contract tests first**

Update the existing version-alignment test to require:

```csharp
TestSuite.Equal(true, buildProps.Contains("<Version>0.2.2</Version>", StringComparison.Ordinal));
TestSuite.Equal(true, installer.Contains("#define MyAppVersion \"0.2.2\"", StringComparison.Ordinal));
TestSuite.Equal("0.2.2", chromeManifest.RootElement.GetProperty("version").GetString());
TestSuite.Equal("0.2.2", firefoxManifest.RootElement.GetProperty("version").GetString());
TestSuite.Equal("quickconvert@local", firefoxManifest.RootElement
    .GetProperty("browser_specific_settings").GetProperty("gecko").GetProperty("id").GetString());
```

Keep the existing Chrome key/ID and permission assertions unchanged.

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj`

Expected: FAIL because all four version sources still report earlier versions.

- [ ] **Step 3: Set all package versions to 0.2.2**

Set `<Version>0.2.2</Version>` in `Directory.Build.props`, `#define MyAppVersion "0.2.2"` in `installer/QuickConvert.iss`, and `"version": "0.2.2"` in both manifests. Do not change the installer `AppId`, paths, extension key/IDs, permissions, or native-host registrations.

- [ ] **Step 4: Run the complete local verification suite**

Run: `npm run test:extensions`

Run: `npm run check:extensions`

Run: `dotnet build QuickConvert.slnx --configuration Release`

Run: `dotnet run --no-build --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj -- --integration`

Expected: every command exits `0`, including real FFmpeg fixture conversions.

- [ ] **Step 5: Commit the release contract**

```powershell
git add Directory.Build.props installer/QuickConvert.iss extensions/chrome/manifest.json extensions/firefox/manifest.json tests/QuickConvert.Tests/Program.cs
git commit -m "chore: prepare QuickConvert v0.2.2"
```

---

### Task 7: Publish and validate GitHub Release v0.2.2

**Files:**
- Modify only if required by verified workflow failure: `.github/workflows/release.yml`
- Generated and ignored: `artifacts/installer/QuickConvert-0.2.2-win-x64-setup.exe`
- External state: `origin/main`, annotated tag `v0.2.2`, GitHub Release `QuickConvert v0.2.2`

**Interfaces:**
- Consumes: green verification and a clean local `main` from Tasks 1–6.
- Produces: public installer, Chrome extension archive/folder artifact, Firefox XPI, `SHA256SUMS.txt`, source archives, and release notes describing the Zen fix and background setting.

- [ ] **Step 1: Run pre-publish verification from a clean tree**

Run:

```powershell
git status --short --branch
git log --oneline origin/main..HEAD
git tag --list v0.2.2
npm run test:extensions
npm run check:extensions
dotnet build QuickConvert.slnx --configuration Release
dotnet run --no-build --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj -- --integration
```

Expected: only the planned commits are ahead of `origin/main`, the worktree is clean, no `v0.2.2` tag exists, and all verification passes.

- [ ] **Step 2: Push main and create the annotated release tag**

```powershell
git push origin main
git tag -a v0.2.2 -m "QuickConvert v0.2.2"
git push origin v0.2.2
```

Expected: both pushes succeed and the existing release workflow starts for `v0.2.2`.

- [ ] **Step 3: Wait for the release workflow and inspect failures before retrying**

Run: `gh run list --workflow release.yml --limit 5`

Run: `gh run watch <run-id> --exit-status`

Expected: the tagged run completes successfully. If it fails, inspect with `gh run view <run-id> --log-failed`, fix only the demonstrated workflow defect with a new commit, move/recreate the not-yet-published tag only after confirming no public release exists, rerun the full verification, and push again.

- [ ] **Step 4: Validate release name and required assets**

Run: `gh release view v0.2.2 --json name,tagName,isDraft,isPrerelease,assets,url`

Expected: name `QuickConvert v0.2.2`, tag `v0.2.2`, neither draft nor prerelease, and assets include `QuickConvert-0.2.2-win-x64-setup.exe`, both browser-extension deliverables, and `SHA256SUMS.txt`.

- [ ] **Step 5: Verify the public installer checksum**

Create a new GUID-named directory under `[System.IO.Path]::GetTempPath()`, download the public installer and `SHA256SUMS.txt` using `gh release download v0.2.2`, compare `Get-FileHash -Algorithm SHA256` with the installer line in the manifest, then delete only that verified GUID temporary directory.

Expected: hashes match exactly.

- [ ] **Step 6: Confirm repository and tag alignment**

Run:

```powershell
git fetch origin main --tags
git status --short --branch
Write-Output "LOCAL=$(git rev-parse main)"
Write-Output "REMOTE=$(git rev-parse origin/main)"
Write-Output "TAG=$(git rev-list -n 1 v0.2.2)"
```

Expected: clean worktree and identical local main, origin/main, and peeled tag commits. Report the public release URL to the user.

