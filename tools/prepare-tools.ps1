param([Parameter(Mandatory = $true)][string]$OutputDirectory)

$ErrorActionPreference = "Stop"
$target = [System.IO.Path]::GetFullPath($OutputDirectory)
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $target.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Tool output must stay inside the repository."
}

New-Item -ItemType Directory -Force -Path $target | Out-Null
foreach ($name in @("ffmpeg.exe", "ffprobe.exe")) {
    $command = Get-Command $name -ErrorAction Stop
    Copy-Item -LiteralPath $command.Source -Destination (Join-Path $target $name) -Force
}

$ytDlpPath = Join-Path $target "yt-dlp.exe"
$checksumsPath = Join-Path $target "SHA2-256SUMS"
Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe" -OutFile $ytDlpPath
Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS" -OutFile $checksumsPath
$expectedLine = Get-Content -LiteralPath $checksumsPath | Where-Object { $_ -match "\syt-dlp\.exe$" } | Select-Object -First 1
if (-not $expectedLine) { throw "Official yt-dlp checksum was not found." }
$expected = ($expectedLine -split "\s+")[0].ToUpperInvariant()
$actual = (Get-FileHash -LiteralPath $ytDlpPath -Algorithm SHA256).Hash
if ($actual -ne $expected) {
    Remove-Item -LiteralPath $ytDlpPath -Force
    throw "yt-dlp checksum mismatch."
}
Remove-Item -LiteralPath $checksumsPath -Force
Write-Host "Prepared verified tools in $target"
