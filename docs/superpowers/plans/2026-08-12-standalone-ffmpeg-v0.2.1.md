# QuickConvert Standalone FFmpeg v0.2.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish QuickConvert `v0.2.1` with real standalone `ffmpeg.exe` and `ffprobe.exe` files instead of unusable Chocolatey ShimGen executables.

**Architecture:** Move FFmpeg discovery, pair selection, smoke testing, and copying into a focused PowerShell module. Model discovered executables as candidate records so pair selection can be tested without real PE fixtures, and inject only the process runner in tests so pre-copy and post-copy validation remain deterministic. Keep the existing release workflow authoritative and refuse to create a release when tool validation fails.

**Tech Stack:** PowerShell 7, .NET 8, GitHub Actions, Chocolatey `ffmpeg-full`, Inno Setup 6, Git tags, GitHub REST API.

## Global Constraints

- Product version is exactly `0.2.1`; Git tag is exactly `v0.2.1`.
- GitHub Release name is exactly `QuickConvert v0.2.1`.
- Windows installer name is exactly `QuickConvert-0.2.1-win-x64-setup.exe`.
- Installer tools must be real standalone `ffmpeg.exe` and `ffprobe.exe`, never files whose `ProductName` contains `ShimGen generated shim`.
- `ffmpeg.exe` and `ffprobe.exe` must be selected from the same directory.
- Both executables must return exit code zero for `-version` before and after copying.
- Production process execution uses argument arrays and does not invoke a shell.
- Tests add no Pester dependency and do not depend on FFmpeg installed on the test machine.
- `v0.2.0` remains unchanged as a historical release.
- No converter presets, codecs, UI, user history, Authenticode, or Firefox signing behavior changes.

---

### Task 1: Testable FFmpeg candidate selection and validation

**Files:**
- Create: `tools/ffmpeg-tools.ps1`
- Create: `tools/tests/ffmpeg-tools.tests.ps1`

**Interfaces:**
- Produces `Test-QuickConvertShim -Path <string> -> bool`.
- Produces `Select-QuickConvertFfmpegPair -Candidates <object[]> -> object` with `FfmpegPath` and `FfprobePath`.
- Produces `Find-QuickConvertFfmpegPair -AdditionalRoots <string[]> -> object`.
- Produces `Test-QuickConvertExecutable -Path <string> -ExpectedName <string> [-ProcessRunner <scriptblock>] -> void`.
- Produces `Copy-QuickConvertFfmpegTools -OutputDirectory <string> [-AdditionalRoots <string[]>] [-CandidatePair <object>] [-ProcessRunner <scriptblock>] -> void`; production uses discovery, while the regression test supplies `CandidatePair`.

- [ ] **Step 1: Write the failing standalone PowerShell regression test**

Create `tools/tests/ffmpeg-tools.tests.ps1`. Dot-source the not-yet-existing module, create temporary candidate directories and ordinary marker files, and define these deterministic cases:

```powershell
$ErrorActionPreference = 'Stop'
$modulePath = Join-Path $PSScriptRoot '..\ffmpeg-tools.ps1'
. $modulePath

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "QuickConvertFfmpegTests-$([Guid]::NewGuid().ToString('N'))"
try {
    $shimRoot = Join-Path $tempRoot 'shim'
    $realRoot = Join-Path $tempRoot 'real'
    $incompleteRoot = Join-Path $tempRoot 'incomplete'
    New-Item -ItemType Directory -Force -Path $shimRoot, $realRoot, $incompleteRoot | Out-Null
    New-Item -ItemType File -Path (Join-Path $shimRoot 'ffmpeg.exe'), (Join-Path $shimRoot 'ffprobe.exe'), (Join-Path $realRoot 'ffmpeg.exe'), (Join-Path $realRoot 'ffprobe.exe'), (Join-Path $incompleteRoot 'ffmpeg.exe') | Out-Null

    $candidates = @(
        [pscustomobject]@{ Path = Join-Path $shimRoot 'ffmpeg.exe'; ToolName = 'ffmpeg.exe'; ProductName = 'ShimGen generated shim - Chocolatey Shim' },
        [pscustomobject]@{ Path = Join-Path $shimRoot 'ffprobe.exe'; ToolName = 'ffprobe.exe'; ProductName = 'ShimGen generated shim - Chocolatey Shim' },
        [pscustomobject]@{ Path = Join-Path $realRoot 'ffmpeg.exe'; ToolName = 'ffmpeg.exe'; ProductName = 'FFmpeg' },
        [pscustomobject]@{ Path = Join-Path $realRoot 'ffprobe.exe'; ToolName = 'ffprobe.exe'; ProductName = 'FFmpeg' },
        [pscustomobject]@{ Path = Join-Path $incompleteRoot 'ffmpeg.exe'; ToolName = 'ffmpeg.exe'; ProductName = 'FFmpeg' }
    )

    $pair = Select-QuickConvertFfmpegPair -Candidates $candidates
    Assert-True ($pair.FfmpegPath -eq (Join-Path $realRoot 'ffmpeg.exe')) 'Resolver selected a shim or incomplete pair.'
    Assert-True ($pair.FfprobePath -eq (Join-Path $realRoot 'ffprobe.exe')) 'Resolver did not keep the pair in one directory.'

    $copyRoot = Join-Path $tempRoot 'copied'
    $calls = [System.Collections.Generic.List[string]]::new()
    $runner = {
        param([string]$Path, [string[]]$Arguments)
        $calls.Add("$Path|$($Arguments -join ',')")
        return 0
    }
    Copy-QuickConvertFfmpegTools -OutputDirectory $copyRoot -CandidatePair $pair -ProcessRunner $runner
    Assert-True ($calls.Count -eq 4) 'Expected validation before and after copying both tools.'
    Assert-True ($calls[2].StartsWith((Join-Path $copyRoot 'ffmpeg.exe'))) 'Copied ffmpeg was not revalidated.'
    Assert-True ($calls[3].StartsWith((Join-Path $copyRoot 'ffprobe.exe'))) 'Copied ffprobe was not revalidated.'

    $failingRunner = { param([string]$Path, [string[]]$Arguments) return 1 }
    $failed = $false
    try { Test-QuickConvertExecutable -Path $pair.FfmpegPath -ExpectedName 'ffmpeg' -ProcessRunner $failingRunner } catch { $failed = $true }
    Assert-True $failed 'A nonzero -version result must stop tool preparation.'
} finally {
    if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
}
```

The production `Copy-QuickConvertFfmpegTools` signature also accepts the internal `-CandidatePair` parameter used by the deterministic test; normal callers use `-AdditionalRoots` and let the function discover the pair.

- [ ] **Step 2: Run the regression test and verify RED**

```powershell
pwsh -NoProfile -File .\tools\tests\ffmpeg-tools.tests.ps1
```

Expected: failure while dot-sourcing `tools/ffmpeg-tools.ps1` because the module does not exist.

- [ ] **Step 3: Implement the minimal module**

Create `tools/ffmpeg-tools.ps1` with these behaviors:

```powershell
function Test-QuickConvertShim {
    param([Parameter(Mandatory)][string]$Path)
    $productName = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)).ProductName
    return $productName -like '*ShimGen generated shim*'
}

function Select-QuickConvertFfmpegPair {
    param([Parameter(Mandatory)][object[]]$Candidates)
    $usable = $Candidates | Where-Object { $_.ProductName -notlike '*ShimGen generated shim*' }
    foreach ($group in ($usable | Group-Object { [System.IO.Path]::GetDirectoryName($_.Path) })) {
        $ffmpeg = $group.Group | Where-Object ToolName -EQ 'ffmpeg.exe' | Select-Object -First 1
        $ffprobe = $group.Group | Where-Object ToolName -EQ 'ffprobe.exe' | Select-Object -First 1
        if ($ffmpeg -and $ffprobe) {
            return [pscustomobject]@{ FfmpegPath = $ffmpeg.Path; FfprobePath = $ffprobe.Path }
        }
    }
    throw 'A complete standalone FFmpeg/FFprobe pair was not found.'
}
```

Implement `Find-QuickConvertFfmpegPair` by collecting all `Get-Command ffmpeg.exe -All` and `Get-Command ffprobe.exe -All` sources plus recursive matches below explicitly supplied roots and `$env:ChocolateyInstall\lib\ffmpeg*\tools`. Deduplicate full paths case-insensitively, read `ProductName` with `FileVersionInfo`, then call `Select-QuickConvertFfmpegPair`.

Implement the default process runner with `System.Diagnostics.ProcessStartInfo`: `UseShellExecute = $false`, `CreateNoWindow = $true`, `RedirectStandardOutput = $true`, `RedirectStandardError = $true`, and `ArgumentList.Add('-version')`. `Test-QuickConvertExecutable` throws when the file is absent, its base name does not match `ExpectedName`, the runner throws, or the exit code is nonzero.

Implement `Copy-QuickConvertFfmpegTools` so it resolves or accepts a candidate pair, validates both source files, creates the output directory, copies both files, and validates both destination files. Never catch validation errors to fall back to another shim.

- [ ] **Step 4: Run the regression test and verify GREEN**

```powershell
pwsh -NoProfile -File .\tools\tests\ffmpeg-tools.tests.ps1
```

Expected: exit code zero; the selected paths point to the `real` directory and the injected runner records four validations.

- [ ] **Step 5: Review and commit the resolver**

```powershell
git diff --check
git add tools/ffmpeg-tools.ps1 tools/tests/ffmpeg-tools.tests.ps1
git commit -m "fix: select standalone FFmpeg tools"
```

---

### Task 2: Protect local and GitHub release builds

**Files:**
- Modify: `tools/prepare-tools.ps1`
- Modify: `.github/workflows/ci.yml`
- Modify: `.github/workflows/release.yml`
- Modify: `tests/QuickConvert.Tests/Program.cs`

**Interfaces:**
- Consumes `Copy-QuickConvertFfmpegTools` from Task 1.
- Produces tool preparation that blocks on a shim or failed `-version` smoke test.
- Produces CI and release jobs that run `tools/tests/ffmpeg-tools.tests.ps1` before packaging.

- [ ] **Step 1: Add failing repository contract tests**

Add a neighboring test in `tests/QuickConvert.Tests/Program.cs`:

```csharp
tests.Run("release builds validate standalone FFmpeg tools", () =>
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var prepare = File.ReadAllText(Path.Combine(root, "tools", "prepare-tools.ps1"));
    var ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
    var release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

    TestSuite.Equal(true, prepare.Contains("ffmpeg-tools.ps1", StringComparison.Ordinal));
    TestSuite.Equal(true, prepare.Contains("Copy-QuickConvertFfmpegTools", StringComparison.Ordinal));
    TestSuite.Equal(false, prepare.Contains("Get-Command $name", StringComparison.Ordinal));
    TestSuite.Equal(true, ci.Contains("./tools/tests/ffmpeg-tools.tests.ps1", StringComparison.Ordinal));
    TestSuite.Equal(true, release.Contains("./tools/tests/ffmpeg-tools.tests.ps1", StringComparison.Ordinal));
});
```

- [ ] **Step 2: Run the .NET suite and verify RED**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
```

Expected: the new repository-contract test fails because `prepare-tools.ps1` still copies the first PATH result and workflows do not run the PowerShell regression test.

- [ ] **Step 3: Replace the unsafe copy loop**

At the beginning of `tools/prepare-tools.ps1`, dot-source the module and replace the FFmpeg loop:

```powershell
. (Join-Path $PSScriptRoot 'ffmpeg-tools.ps1')

New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-QuickConvertFfmpegTools -OutputDirectory $target
```

Keep the existing repository-boundary check and verified `yt-dlp.exe` download unchanged.

- [ ] **Step 4: Run the regression test in both workflows**

After Node/.NET setup in `.github/workflows/ci.yml`, add:

```yaml
      - shell: pwsh
        run: ./tools/tests/ffmpeg-tools.tests.ps1
```

After `choco install ffmpeg-full innosetup -y` in `.github/workflows/release.yml`, add the same step. Running after Chocolatey installation reproduces the PATH condition that caused `v0.2.0`.

- [ ] **Step 5: Verify GREEN and commit**

```powershell
pwsh -NoProfile -File .\tools\tests\ffmpeg-tools.tests.ps1
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
git diff --check
git add tools/prepare-tools.ps1 .github/workflows/ci.yml .github/workflows/release.yml tests/QuickConvert.Tests/Program.cs
git commit -m "ci: reject FFmpeg shims in release builds"
```

Expected: the PowerShell and .NET contract tests pass.

---

### Task 3: Set the v0.2.1 release contract

**Files:**
- Modify: `tests/QuickConvert.Tests/Program.cs`
- Modify: `Directory.Build.props`
- Modify: `installer/QuickConvert.iss`

**Interfaces:**
- Produces application version `0.2.1` and installer filename `QuickConvert-0.2.1-win-x64-setup.exe`.
- Preserves the current icon, wizard image, output basename pattern, and per-user installation behavior.

- [ ] **Step 1: Change the existing release test first**

Rename `v0.2.0 release version and installer branding stay aligned` to `v0.2.1 release version and installer branding stay aligned`, and change its exact assertions to:

```csharp
TestSuite.Equal(true, buildProps.Contains("<Version>0.2.1</Version>", StringComparison.Ordinal));
TestSuite.Equal(true, installer.Contains("#define MyAppVersion \"0.2.1\"", StringComparison.Ordinal));
```

Keep all filename and branding assertions in that test.

- [ ] **Step 2: Run the test and verify RED**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
```

Expected: exactly the updated release-contract assertions fail because both version sources remain `0.2.0`.

- [ ] **Step 3: Set version 0.2.1 in both sources**

In `Directory.Build.props` set `<Version>0.2.1</Version>`. In `installer/QuickConvert.iss` set `#define MyAppVersion "0.2.1"`. Do not alter `AppId`, installation path, branding, or `OutputBaseFilename`.

- [ ] **Step 4: Verify GREEN and commit**

```powershell
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj
dotnet build QuickConvert.slnx --configuration Release --no-restore
git diff --check
git add Directory.Build.props installer/QuickConvert.iss tests/QuickConvert.Tests/Program.cs
git commit -m "chore: prepare QuickConvert v0.2.1"
```

---

### Task 4: Full local artifact verification

**Files:**
- Generated and ignored: `artifacts/publish/tools/ffmpeg.exe`
- Generated and ignored: `artifacts/publish/tools/ffprobe.exe`
- Generated and ignored: `artifacts/installer/QuickConvert-0.2.1-win-x64-setup.exe`
- Generated and ignored: `artifacts/SHA256SUMS.txt`

**Interfaces:**
- Consumes all source changes from Tasks 1–3.
- Produces locally validated release artifacts without source changes.

- [ ] **Step 1: Run all automated suites**

```powershell
pwsh -NoProfile -File .\tools\tests\ffmpeg-tools.tests.ps1
dotnet run --configuration Release --project tests\QuickConvert.Tests\QuickConvert.Tests.csproj -- --integration
dotnet build QuickConvert.slnx --configuration Release --no-restore
npm run test:extensions
npm run check:extensions
```

Expected: PowerShell regression test, all .NET tests including real media conversions, solution build, and browser extension tests pass.

- [ ] **Step 2: Build the complete local release**

```powershell
.\tools\build-release.ps1
```

Expected: tool preparation, app publishing, extension packaging, and Inno Setup complete; the exact `0.2.1` installer exists.

- [ ] **Step 3: Inspect and execute the packaged tools**

```powershell
$tools = @('artifacts\publish\tools\ffmpeg.exe', 'artifacts\publish\tools\ffprobe.exe')
foreach ($tool in $tools) {
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path $tool))
    if ($info.ProductName -like '*ShimGen generated shim*') { throw "$tool is a Chocolatey shim" }
    & $tool -version | Select-Object -First 1
    if ($LASTEXITCODE -ne 0) { throw "$tool failed -version" }
}
```

Expected: neither ProductName contains ShimGen and both commands exit zero from inside `artifacts\publish\tools`.

- [ ] **Step 4: Verify exact installer and checksum**

```powershell
$installer = 'artifacts\installer\QuickConvert-0.2.1-win-x64-setup.exe'
if (-not (Test-Path -LiteralPath $installer)) { throw 'v0.2.1 installer missing' }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = Get-Content -Raw -LiteralPath 'artifacts\SHA256SUMS.txt'
if (-not $manifest.Contains($hash, [StringComparison]::OrdinalIgnoreCase)) { throw 'Installer checksum missing' }
Get-Item -LiteralPath $installer | Select-Object Name, Length
Write-Output "SHA256=$hash"
```

- [ ] **Step 5: Confirm repository readiness**

```powershell
git diff --check
git status --short --branch
git log -5 --oneline --decorate
git tag --list v0.2.1
```

Expected: clean worktree, implementation commits present, and no existing `v0.2.1` tag.

---

### Task 5: Publish and validate GitHub Release v0.2.1

**Files:**
- External state: `origin/main`
- External state: annotated tag `v0.2.1`
- External state: GitHub Actions Release run
- External state: GitHub Release `v0.2.1`
- Temporary downloaded installer under the system temporary directory

**Interfaces:**
- Consumes the clean, locally verified commit from Task 4.
- Produces a public release whose installer contains executable standalone FFmpeg tools.

- [ ] **Step 1: Push main with a fast-forward safety check**

```powershell
git fetch origin
if ((git rev-parse origin/main) -ne (git merge-base origin/main main)) { throw 'origin/main is not an ancestor of local main' }
git push origin main
if ((git rev-parse main) -ne (git rev-parse origin/main)) { throw 'origin/main does not match local main' }
```

- [ ] **Step 2: Create and push the annotated tag**

```powershell
if (git tag --list v0.2.1) { throw 'v0.2.1 already exists' }
git tag -a v0.2.1 -m "QuickConvert v0.2.1"
git push origin v0.2.1
```

Expected: `.github/workflows/release.yml` starts from the verified commit.

- [ ] **Step 3: Wait for the workflow and public release**

Use `gh run list --workflow release.yml --limit 5` and `gh run watch <run-id> --exit-status`, or poll the public API every 15 seconds for no more than 20 minutes. Do not upload artifacts manually if the workflow fails.

```powershell
$headers = @{ 'User-Agent' = 'QuickConvert-release-verifier' }
$deadline = [DateTimeOffset]::UtcNow.AddMinutes(20)
do {
    try { $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/ITs-ZAK/QuickConvert/releases/tags/v0.2.1' -Headers $headers } catch { $release = $null }
    if ($null -eq $release) { Start-Sleep -Seconds 15 }
} while ($null -eq $release -and [DateTimeOffset]::UtcNow -lt $deadline)
if ($null -eq $release) { throw 'v0.2.1 release did not appear within 20 minutes' }
```

- [ ] **Step 4: Validate public identity and required assets**

```powershell
if ($release.name -ne 'QuickConvert v0.2.1') { throw "Unexpected release name: $($release.name)" }
$assetNames = @($release.assets | ForEach-Object name)
if ($assetNames -notcontains 'QuickConvert-0.2.1-win-x64-setup.exe') { throw 'Installer missing' }
if ($assetNames -notcontains 'SHA256SUMS.txt') { throw 'Checksums missing' }
$release.assets | Select-Object name, size, browser_download_url
```

- [ ] **Step 5: Download and verify the public installer checksum**

Download the public installer and `SHA256SUMS.txt` to a newly created GUID directory under `[System.IO.Path]::GetTempPath()`. Compare the actual installer SHA-256 with the `installer/QuickConvert-0.2.1-win-x64-setup.exe` line in the public manifest. Remove only that GUID temporary directory after verification.

- [ ] **Step 6: Install silently and verify installed tools**

Run the downloaded installer with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`, then inspect `%LocalAppData%\Programs\QuickConvert\tools\ffmpeg.exe` and `ffprobe.exe`. Assert that neither ProductName contains ShimGen and both `-version` invocations return zero. This upgrades the existing per-user installation and preserves `%LocalAppData%\QuickConvert` history/settings.

- [ ] **Step 7: Final ref and repository verification**

```powershell
git fetch origin --tags
Write-Output "LOCAL=$(git rev-parse main)"
Write-Output "REMOTE=$(git rev-parse origin/main)"
Write-Output "TAG=$(git rev-list -n 1 v0.2.1)"
git status --short --branch
```

Expected: local main, origin/main, and peeled `v0.2.1` tag are identical; worktree is clean; public installed FFmpeg and FFprobe both execute successfully.
