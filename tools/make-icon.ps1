# Renders package\icon.png (256x256, required by Thunderstore) with System.Drawing.
param([string]$Out = (Join-Path $PSScriptRoot '..\package\icon.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}
function C([int]$a, [int]$r, [int]$gr, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $gr, $b) }

# Background: dusk sky
$bg = New-RoundedRect 0 0 256 256 44
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 0, 256), (C 255 58 70 120), (C 255 24 28 52)
$g.FillPath($bgBrush, $bg)

# Orbit: dashed ellipse around the scene with an arrow head
$orbitPen = New-Object System.Drawing.Pen (C 220 255 200 120), 6
$orbitPen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawArc($orbitPen, 22, 120, 212, 96, 200, 300)
$arrow = New-Object System.Drawing.Drawing2D.GraphicsPath
$arrow.AddPolygon(@((New-Object System.Drawing.PointF 38, 132), (New-Object System.Drawing.PointF 62, 118), (New-Object System.Drawing.PointF 58, 146)))
$g.FillPath((New-Object System.Drawing.SolidBrush (C 255 255 200 120)), $arrow)

# Small character in the middle of the orbit
$cream = New-Object System.Drawing.SolidBrush (C 255 250 238 222)
$ink = New-Object System.Drawing.SolidBrush (C 255 34 44 68)
$g.FillPath($cream, (New-RoundedRect 112 168 32 40 14))
$g.FillEllipse($cream, 108, 136, 40, 40)
$g.FillEllipse($ink, 119, 152, 5, 7)
$g.FillEllipse($ink, 132, 152, 5, 7)

# Camera above, looking down
$body = New-RoundedRect 72 28 112 72 16
$g.FillPath((New-Object System.Drawing.SolidBrush (C 255 236 240 248)), $body)
$g.FillPath((New-Object System.Drawing.SolidBrush (C 255 236 240 248)), (New-RoundedRect 100 16 40 20 6))
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 255 34 44 68)), 102, 38, 52, 52)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 255 120 190 255)), 112, 48, 32, 32)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 200 255 255 255)), 118, 53, 10, 10)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 255 255 120 110)), 160, 38, 12, 12)

$g.Dispose()
$full = [System.IO.Path]::GetFullPath($Out)
$bmp.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Icon: $full"
