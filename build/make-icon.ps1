# 生成多尺寸 .ico（16/24/32/48/64/128/256，32bpp DIB，兼容 Inno Setup 与 csc /win32icon）
param(
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [double]$size
    $pad = [double]($s * 0.02)
    $bodyW = $s - 2 * $pad

    # 外框：蓝色圆角卡片
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = [float]($bodyW * 0.22)
    $rect = New-Object System.Drawing.RectangleF([float]$pad, [float]$pad, [float]$bodyW, [float]$bodyW)
    $d = $r * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect,
        [System.Drawing.Color]::FromArgb(255, 96, 208, 255),
        [System.Drawing.Color]::FromArgb(255, 38, 160, 232), 90.0)
    $g.FillPath($brush, $path)

    # 显示屏
    $dispX = [float]($pad + $bodyW * 0.13)
    $dispY = [float]($pad + $bodyW * 0.13)
    $dispH = [float]($bodyW * 0.24)
    $dispW = [float]($bodyW * 0.74)
    $dark = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 14, 38, 52))
    $dispPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $dr = [float]($dispH * 0.22)
    $dispPath.AddArc($dispX, $dispY, $dr * 2, $dr * 2, 180, 90)
    $dispPath.AddArc($dispX + $dispW - $dr * 2, $dispY, $dr * 2, $dr * 2, 270, 90)
    $dispPath.AddArc($dispX + $dispW - $dr * 2, $dispY + $dispH - $dr * 2, $dr * 2, $dr * 2, 0, 90)
    $dispPath.AddArc($dispX, $dispY + $dispH - $dr * 2, $dr * 2, $dr * 2, 90, 90)
    $dispPath.CloseFigure()
    $g.FillPath($dark, $dispPath)

    # 按键点阵 3 x 2
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $dot = [float]($bodyW * 0.105)
    $gapX = [float](($bodyW * 0.74 - 3 * $dot) / 2.0)
    $startX = [float]($dispX)
    $startY = [float]($dispY + $dispH + $bodyW * 0.10)
    $gapY = [float]($bodyW * 0.14)
    for ($row = 0; $row -lt 2; $row++) {
        for ($col = 0; $col -lt 3; $col++) {
            $x = $startX + $col * ($dot + $gapX)
            $y = $startY + $row * ($dot + $gapY)
            $g.FillEllipse($white, $x, $y, $dot, $dot)
        }
    }

    $g.Dispose()
    return $bmp
}

function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    # BITMAPINFOHEADER
    $bw.Write([int]40)
    $bw.Write([int]$w)
    $bw.Write([int]($h * 2))
    $bw.Write([int16]1)
    $bw.Write([int16]32)
    $bw.Write([int]0)
    $bw.Write([int]($w * $h * 4))
    $bw.Write([int]0); $bw.Write([int]0)
    $bw.Write([int]0); $bw.Write([int]0)
    # XOR 数据：自下而上 BGRA
    for ($y = $h - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B)
            $bw.Write([byte]$c.G)
            $bw.Write([byte]$c.R)
            $bw.Write([byte]$c.A)
        }
    }
    # AND 掩码（alpha 通道已足够，掩码全 0），每行 4 字节对齐
    $maskRow = [int]([Math]::Floor(($w + 31) / 32) * 4)
    $mask = New-Object byte[] ($maskRow * $h)
    $bw.Write($mask)
    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Dispose()
    $ms.Dispose()
    return $bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap -size $size
    $images += , @($size, (Get-DibBytes -bmp $bmp))
    $bmp.Dispose()
}

$dir = Split-Path -Parent $Out
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter($fs)
$w.Write([int16]0)
$w.Write([int16]1)
$w.Write([int16]$images.Count)

$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $size = $img[0]
    $data = $img[1]
    $dim = if ($size -eq 256) { 0 } else { $size }
    $w.Write([byte]$dim)
    $w.Write([byte]$dim)
    $w.Write([byte]0)
    $w.Write([byte]0)
    $w.Write([int16]1)
    $w.Write([int16]32)
    $w.Write([int]$data.Length)
    $w.Write([int]$offset)
    $offset += $data.Length
}
foreach ($img in $images) {
    $w.Write([byte[]]$img[1])
}
$w.Flush()
$w.Dispose()
$fs.Dispose()

Write-Output "icon written: $Out"
