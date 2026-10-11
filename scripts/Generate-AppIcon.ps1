$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoPath = Split-Path -Parent $PSScriptRoot
$iconPath = Join-Path $repoPath 'src/clients/TeamReminder.Windows/Assets/app.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(67, 56, 202))
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $amber = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(251, 191, 36))
    $stroke = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(67, 56, 202), 10)
    $stroke.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $stroke.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    try {
        $graphics.FillEllipse($background, 4, 4, 248, 248)
        # Alarm bell and clock: drawn geometry remains legible at tray sizes.
        $bell = [System.Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $bell.AddBezier(68, 155, 81, 140, 79, 106, 82, 96)
            $bell.AddBezier(82, 96, 91, 56, 155, 56, 165, 96)
            $bell.AddBezier(165, 96, 169, 118, 166, 142, 181, 155)
            $bell.AddLine(181, 155, 68, 155)
            $bell.CloseFigure()
            $graphics.FillPath($white, $bell)
        } finally { $bell.Dispose() }
        $graphics.FillEllipse($white, 110, 157, 28, 24)
        $graphics.FillEllipse($white, 115, 49, 18, 18)
        $graphics.FillEllipse($amber, 148, 139, 68, 68)
        $graphics.DrawLine($stroke, 182, 156, 182, 175)
        $graphics.DrawLine($stroke, 182, 175, 195, 184)
        $memory = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
            $images += ,$memory.ToArray()
        } finally { $memory.Dispose() }
    } finally {
        $graphics.Dispose(); $bitmap.Dispose(); $background.Dispose(); $white.Dispose(); $amber.Dispose(); $stroke.Dispose()
    }
}
$stream = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($png in $images) { $writer.Write([byte[]]$png) }
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output $iconPath
