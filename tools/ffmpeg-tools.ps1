$ErrorActionPreference = "Stop"

function Test-QuickConvertShim {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Tool candidate does not exist: $Path"
    }

    $productName = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)).ProductName
    return $productName -like "*ShimGen generated shim*"
}

function Select-QuickConvertFfmpegPair {
    param([Parameter(Mandatory = $true)][object[]]$Candidates)

    $usable = @($Candidates | Where-Object {
        $_.ProductName -notlike "*ShimGen generated shim*"
    })

    foreach ($group in ($usable | Group-Object {
        [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($_.Path))
    })) {
        $ffmpeg = $group.Group | Where-Object { $_.ToolName -ieq "ffmpeg.exe" } | Select-Object -First 1
        $ffprobe = $group.Group | Where-Object { $_.ToolName -ieq "ffprobe.exe" } | Select-Object -First 1
        if ($null -ne $ffmpeg -and $null -ne $ffprobe) {
            return [pscustomobject]@{
                FfmpegPath = [System.IO.Path]::GetFullPath($ffmpeg.Path)
                FfprobePath = [System.IO.Path]::GetFullPath($ffprobe.Path)
            }
        }
    }

    throw "A complete standalone FFmpeg/FFprobe pair was not found. Chocolatey shims cannot be packaged."
}

function Find-QuickConvertFfmpegPair {
    param([string[]]$AdditionalRoots = @())

    $candidatePaths = [System.Collections.Generic.List[string]]::new()
    foreach ($toolName in @("ffmpeg.exe", "ffprobe.exe")) {
        foreach ($command in @(Get-Command $toolName -All -ErrorAction SilentlyContinue)) {
            $source = if ($command.Source) { $command.Source } else { $command.Path }
            if ($source) {
                $candidatePaths.Add($source)
            }
        }
    }

    $searchRoots = [System.Collections.Generic.List[string]]::new()
    foreach ($root in @($AdditionalRoots)) {
        if ($root) {
            $searchRoots.Add($root)
        }
    }

    if ($env:ChocolateyInstall) {
        $chocolateyLib = Join-Path $env:ChocolateyInstall "lib"
        if (Test-Path -LiteralPath $chocolateyLib -PathType Container) {
            foreach ($packageDirectory in @(Get-ChildItem -LiteralPath $chocolateyLib -Directory -Filter "ffmpeg*" -ErrorAction SilentlyContinue)) {
                $toolsRoot = Join-Path $packageDirectory.FullName "tools"
                if (Test-Path -LiteralPath $toolsRoot -PathType Container) {
                    $searchRoots.Add($toolsRoot)
                }
            }
        }
    }

    foreach ($root in $searchRoots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            continue
        }

        foreach ($toolName in @("ffmpeg.exe", "ffprobe.exe")) {
            foreach ($file in @(Get-ChildItem -LiteralPath $root -File -Recurse -Filter $toolName -ErrorAction SilentlyContinue)) {
                $candidatePaths.Add($file.FullName)
            }
        }
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $candidates = [System.Collections.Generic.List[object]]::new()
    foreach ($path in $candidatePaths) {
        $fullPath = [System.IO.Path]::GetFullPath($path)
        if (-not $seen.Add($fullPath) -or -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            continue
        }

        $candidates.Add([pscustomobject]@{
            Path = $fullPath
            ToolName = [System.IO.Path]::GetFileName($fullPath)
            ProductName = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($fullPath)).ProductName
        })
    }

    return Select-QuickConvertFfmpegPair -Candidates $candidates.ToArray()
}

function Test-QuickConvertExecutable {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedName,
        [scriptblock]$ProcessRunner
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$ExpectedName executable does not exist: $Path"
    }

    $actualName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    if (-not $actualName.Equals($ExpectedName, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Expected $ExpectedName executable, found $actualName at $Path"
    }

    if ($null -eq $ProcessRunner) {
        $ProcessRunner = {
            param([string]$Path, [string[]]$Arguments)

            $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
            $startInfo.FileName = $Path
            $startInfo.Arguments = "-version"
            $startInfo.UseShellExecute = $false
            $startInfo.CreateNoWindow = $true
            $process = [System.Diagnostics.Process]::Start($startInfo)
            $process.WaitForExit()
            return $process.ExitCode
        }
    }

    $exitCode = & $ProcessRunner -Path $Path -Arguments @("-version")
    if ($exitCode -ne 0) {
        throw "$ExpectedName failed its -version smoke test with exit code $exitCode."
    }
}

function Copy-QuickConvertFfmpegTools {
    param(
        [Parameter(Mandatory = $true)][string]$OutputDirectory,
        [string[]]$AdditionalRoots = @(),
        [object]$CandidatePair,
        [scriptblock]$ProcessRunner
    )

    $pair = if ($null -ne $CandidatePair) {
        $CandidatePair
    }
    else {
        Find-QuickConvertFfmpegPair -AdditionalRoots $AdditionalRoots
    }

    if ((Test-QuickConvertShim -Path $pair.FfmpegPath) -or (Test-QuickConvertShim -Path $pair.FfprobePath)) {
        throw "Chocolatey ShimGen executables cannot be packaged as FFmpeg tools."
    }

    Test-QuickConvertExecutable -Path $pair.FfmpegPath -ExpectedName "ffmpeg" -ProcessRunner $ProcessRunner
    Test-QuickConvertExecutable -Path $pair.FfprobePath -ExpectedName "ffprobe" -ProcessRunner $ProcessRunner

    $target = [System.IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $targetFfmpeg = Join-Path $target "ffmpeg.exe"
    $targetFfprobe = Join-Path $target "ffprobe.exe"
    Copy-Item -LiteralPath $pair.FfmpegPath -Destination $targetFfmpeg -Force
    Copy-Item -LiteralPath $pair.FfprobePath -Destination $targetFfprobe -Force

    if ((Test-QuickConvertShim -Path $targetFfmpeg) -or (Test-QuickConvertShim -Path $targetFfprobe)) {
        throw "Copied FFmpeg tools unexpectedly contain a Chocolatey ShimGen executable."
    }

    Test-QuickConvertExecutable -Path $targetFfmpeg -ExpectedName "ffmpeg" -ProcessRunner $ProcessRunner
    Test-QuickConvertExecutable -Path $targetFfprobe -ExpectedName "ffprobe" -ProcessRunner $ProcessRunner
}
