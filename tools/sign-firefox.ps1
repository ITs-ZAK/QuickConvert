param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot "..\dist\extensions\firefox"),
    [string]$ArtifactsDirectory = (Join-Path $PSScriptRoot "..\dist\extensions")
)

$ErrorActionPreference = "Stop"
if (-not $env:WEB_EXT_API_KEY -or -not $env:WEB_EXT_API_SECRET) {
    throw "WEB_EXT_API_KEY and WEB_EXT_API_SECRET are required."
}

$arguments = @("--yes", "web-ext", "sign", "--source-dir", $SourceDirectory, "--artifacts-dir", $ArtifactsDirectory, "--channel", "unlisted", "--api-key", $env:WEB_EXT_API_KEY, "--api-secret", $env:WEB_EXT_API_SECRET)
& npx @arguments
if ($LASTEXITCODE -ne 0) { throw "Firefox signing failed." }

$signed = Get-ChildItem -LiteralPath $ArtifactsDirectory -Filter "*.xpi" |
    Where-Object { $_.Name -notlike "*unsigned*" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $signed) { throw "Signed Firefox XPI was not produced." }
$target = Join-Path $ArtifactsDirectory "quickconvert-firefox.xpi"
Copy-Item -LiteralPath $signed.FullName -Destination $target -Force
Get-ChildItem -LiteralPath $ArtifactsDirectory -Filter "*.xpi" |
    Where-Object { $_.FullName -ne $target } |
    Remove-Item -Force
Write-Host "Signed Firefox extension: $target"
