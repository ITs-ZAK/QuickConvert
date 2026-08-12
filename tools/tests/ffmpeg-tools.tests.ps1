$ErrorActionPreference = "Stop"

$modulePath = Join-Path $PSScriptRoot "..\ffmpeg-tools.ps1"
. $modulePath

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $threw = $false
    try {
        & $Action
    }
    catch {
        $threw = $true
    }

    Assert-True -Condition $threw -Message $Message
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "QuickConvertFfmpegTests-$([Guid]::NewGuid().ToString("N"))"
try {
    $shimRoot = Join-Path $tempRoot "shim"
    $realRoot = Join-Path $tempRoot "real"
    $incompleteRoot = Join-Path $tempRoot "incomplete"
    New-Item -ItemType Directory -Force -Path $shimRoot, $realRoot, $incompleteRoot | Out-Null

    $paths = @(
        (Join-Path $shimRoot "ffmpeg.exe"),
        (Join-Path $shimRoot "ffprobe.exe"),
        (Join-Path $realRoot "ffmpeg.exe"),
        (Join-Path $realRoot "ffprobe.exe"),
        (Join-Path $incompleteRoot "ffmpeg.exe")
    )
    foreach ($path in $paths) {
        New-Item -ItemType File -Path $path | Out-Null
    }

    $candidates = @(
        [pscustomobject]@{ Path = Join-Path $shimRoot "ffmpeg.exe"; ToolName = "ffmpeg.exe"; ProductName = "ShimGen generated shim - Chocolatey Shim" },
        [pscustomobject]@{ Path = Join-Path $shimRoot "ffprobe.exe"; ToolName = "ffprobe.exe"; ProductName = "ShimGen generated shim - Chocolatey Shim" },
        [pscustomobject]@{ Path = Join-Path $incompleteRoot "ffmpeg.exe"; ToolName = "ffmpeg.exe"; ProductName = "FFmpeg" },
        [pscustomobject]@{ Path = Join-Path $realRoot "ffmpeg.exe"; ToolName = "ffmpeg.exe"; ProductName = "FFmpeg" },
        [pscustomobject]@{ Path = Join-Path $realRoot "ffprobe.exe"; ToolName = "ffprobe.exe"; ProductName = "FFmpeg" }
    )

    $pair = Select-QuickConvertFfmpegPair -Candidates $candidates
    Assert-True -Condition ($pair.FfmpegPath -eq (Join-Path $realRoot "ffmpeg.exe")) -Message "Resolver selected a shim or incomplete pair."
    Assert-True -Condition ($pair.FfprobePath -eq (Join-Path $realRoot "ffprobe.exe")) -Message "Resolver did not keep the pair in one directory."

    Assert-Throws -Message "An incomplete pair must be rejected." -Action {
        Select-QuickConvertFfmpegPair -Candidates @($candidates | Where-Object { $_.Path.StartsWith($incompleteRoot) })
    }

    $copyRoot = Join-Path $tempRoot "copied"
    $calls = [System.Collections.Generic.List[string]]::new()
    $runner = {
        param([string]$Path, [string[]]$Arguments)
        $null = $calls.Add("$Path|$($Arguments -join ",")")
        return 0
    }

    Copy-QuickConvertFfmpegTools -OutputDirectory $copyRoot -CandidatePair $pair -ProcessRunner $runner
    Assert-True -Condition ($calls.Count -eq 4) -Message "Expected validation before and after copying both tools."
    Assert-True -Condition ($calls[0] -eq "$($pair.FfmpegPath)|-version") -Message "Source ffmpeg was not validated."
    Assert-True -Condition ($calls[1] -eq "$($pair.FfprobePath)|-version") -Message "Source ffprobe was not validated."
    Assert-True -Condition ($calls[2] -eq "$(Join-Path $copyRoot "ffmpeg.exe")|-version") -Message "Copied ffmpeg was not revalidated."
    Assert-True -Condition ($calls[3] -eq "$(Join-Path $copyRoot "ffprobe.exe")|-version") -Message "Copied ffprobe was not revalidated."

    $failingRunner = {
        param([string]$Path, [string[]]$Arguments)
        return 1
    }
    Assert-Throws -Message "A nonzero -version result must stop tool preparation." -Action {
        Test-QuickConvertExecutable -Path $pair.FfmpegPath -ExpectedName "ffmpeg" -ProcessRunner $failingRunner
    }

    Write-Host "PASS: standalone FFmpeg tool tests"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
