param(
    [switch]$SkipInstaller,
    [switch]$SkipExtensions
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifacts = Join-Path $repositoryRoot "artifacts"
$publish = Join-Path $artifacts "publish"
if (Test-Path -LiteralPath $artifacts) {
    Remove-Item -LiteralPath $artifacts -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publish | Out-Null

$appProject = Join-Path $repositoryRoot "src\QuickConvert.App\QuickConvert.App.csproj"
$hostProject = Join-Path $repositoryRoot "src\QuickConvert.NativeHost\QuickConvert.NativeHost.csproj"
$publishOptions = @("-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:DebugType=None", "-o", $publish)
& dotnet publish $appProject @publishOptions
if ($LASTEXITCODE -ne 0) { throw "Application publish failed." }
& dotnet publish $hostProject @publishOptions
if ($LASTEXITCODE -ne 0) { throw "Native Host publish failed." }

& (Join-Path $PSScriptRoot "prepare-tools.ps1") -OutputDirectory (Join-Path $publish "tools")
if (-not $SkipExtensions) {
    & (Join-Path $PSScriptRoot "build-extensions.ps1")
}

if (-not $SkipInstaller) {
    $isccCommand = Get-Command iscc.exe -ErrorAction SilentlyContinue
    $isccPath = if ($isccCommand) { $isccCommand.Source } else { $null }
    if (-not $isccPath) {
        $candidateRoots = @(
            [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData),
            [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles),
            [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
        )
        foreach ($root in $candidateRoots) {
            $relativePath = if ($root -eq [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) {
                "Programs\Inno Setup 6\ISCC.exe"
            } else {
                "Inno Setup 6\ISCC.exe"
            }
            $candidate = Join-Path $root $relativePath
            if (Test-Path -LiteralPath $candidate) {
                $isccPath = $candidate
                break
            }
        }
    }
    if (-not $isccPath) { throw "Inno Setup 6 (ISCC.exe) is required." }
    & $isccPath (Join-Path $repositoryRoot "installer\QuickConvert.iss")
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }
}

$checksums = Get-ChildItem -Path $artifacts -File -Recurse |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.FullName.Substring($artifacts.Length + 1).Replace('\', '/'))"
    }
$checksums | Set-Content -LiteralPath (Join-Path $artifacts "SHA256SUMS.txt") -Encoding ASCII
Write-Host "Release artifacts built in $artifacts"
