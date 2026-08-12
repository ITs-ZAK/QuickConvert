param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\dist\extensions")
)

$ErrorActionPreference = "Stop"
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\extensions")).Path
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if (-not $outputRoot.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output directory must stay inside the repository."
}

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

foreach ($browser in @("chrome", "firefox")) {
    $target = Join-Path $outputRoot $browser
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -LiteralPath (Join-Path (Join-Path $sourceRoot $browser) "manifest.json") -Destination $target
    Copy-Item -Path (Join-Path $sourceRoot "shared\*") -Destination $target -Recurse
}

$zipPath = Join-Path $outputRoot "quickconvert-firefox-unsigned.zip"
$xpiPath = Join-Path $outputRoot "quickconvert-firefox-unsigned.xpi"
Compress-Archive -Path (Join-Path $outputRoot "firefox\*") -DestinationPath $zipPath -Force
Move-Item -LiteralPath $zipPath -Destination $xpiPath
Write-Host "Extensions built in $outputRoot"
