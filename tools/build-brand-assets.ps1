param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\assets\branding")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc(
        $X + $Width - $diameter,
        $Y + $Height - $diameter,
        $diameter,
        $diameter,
        0,
        90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-LogoPngBytes {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = $null
    $backgroundPath = $null
    $backgroundBrush = $null
    $symbolPen = $null
    $stream = $null

    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality =
            [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        $scale = [single]($Size / 64.0)
        $graphics.ScaleTransform($scale, $scale)

        $backgroundPath = New-RoundedRectanglePath -X 3 -Y 3 -Width 58 -Height 58 -Radius 16
        $backgroundBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.PointF]::new(3, 3),
            [System.Drawing.PointF]::new(61, 61),
            [System.Drawing.ColorTranslator]::FromHtml("#9B8CFF"),
            [System.Drawing.ColorTranslator]::FromHtml("#5145D6"))
        $graphics.FillPath($backgroundBrush, $backgroundPath)

        $symbolPen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 6)
        $symbolPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $symbolPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $symbolPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

        $graphics.DrawArc($symbolPen, 14, 14, 36, 36, 38, 292)
        $arrow = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(40.5, 25.2),
            [System.Drawing.PointF]::new(48.6, 15),
            [System.Drawing.PointF]::new(57.4, 24.6),
            [System.Drawing.PointF]::new(52.1, 24.8),
            [System.Drawing.PointF]::new(46, 25)
        )
        $graphics.FillPolygon([System.Drawing.Brushes]::White, $arrow)
        $graphics.DrawLine($symbolPen, 34, 35, 48, 49)

        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        if ($stream) { $stream.Dispose() }
        if ($symbolPen) { $symbolPen.Dispose() }
        if ($backgroundBrush) { $backgroundBrush.Dispose() }
        if ($backgroundPath) { $backgroundPath.Dispose() }
        if ($graphics) { $graphics.Dispose() }
        $bitmap.Dispose()
    }
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[object]]::new()
foreach ($size in $sizes) {
    $frames.Add([pscustomobject]@{
        Size = $size
        Bytes = New-LogoPngBytes -Size $size
    })
}

$iconPath = Join-Path $resolvedOutput "quickconvert.ico"
$fileStream = [System.IO.File]::Create($iconPath)
$writer = $null
try {
    $writer = [System.IO.BinaryWriter]::new($fileStream)
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) {
        $writer.Write([byte[]]$frame.Bytes)
    }
}
finally {
    if ($writer) { $writer.Dispose() } else { $fileStream.Dispose() }
}

$frame64 = $frames | Where-Object Size -eq 64
$frame256 = $frames | Where-Object Size -eq 256
[System.IO.File]::WriteAllBytes(
    (Join-Path $resolvedOutput "quickconvert-wizard-small.png"),
    [byte[]]$frame64.Bytes)
[System.IO.File]::WriteAllBytes(
    (Join-Path $resolvedOutput "quickconvert-256.png"),
    [byte[]]$frame256.Bytes)

Write-Host "Brand assets generated in $resolvedOutput"

