# Renders docs/demo.gif and docs/themes.png using the app's own popup renderer.
# Requires a build (build.ps1) and Python with Pillow (pip install pillow) for the GIF.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$exe  = Join-Path $root 'bin\LanguageIndicator.exe'
if (-not (Test-Path $exe)) { & (Join-Path $root 'build.ps1') }
$docs = Join-Path $root 'docs'
New-Item -ItemType Directory -Force $docs | Out-Null

$asm      = [Reflection.Assembly]::LoadFrom($exe)
$stateT   = $asm.GetType('LanguageIndicator.InputState')
$themeT   = $asm.GetType('LanguageIndicator.Theme')
$renderFn = $asm.GetType('LanguageIndicator.OsdWindow').GetMethod('Render', [Reflection.BindingFlags]'NonPublic,Static')
$marginFn = $asm.GetType('LanguageIndicator.OsdWindow').GetMethod('ShadowMargin', [Reflection.BindingFlags]'NonPublic,Static')

function New-State([int]$lang, [bool]$native) {
    $s = [Activator]::CreateInstance($stateT)
    $stateT.GetField('LangId').SetValue($s, $lang)
    $stateT.GetField('Native').SetValue($s, $native)
    $s
}
function Render-Popup($state, [single]$scale, [string]$theme, [int]$opacity) {
    $renderFn.Invoke($null, @($state, $scale, $themeT.GetMethod('Get').Invoke($null, @($theme)), $opacity))
}
function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); $p
}
function Draw-Alpha($g, $bmp, [int]$x, [int]$y, [single]$alpha) {
    $cm = New-Object Drawing.Imaging.ColorMatrix; $cm.Matrix33 = $alpha
    $ia = New-Object Drawing.Imaging.ImageAttributes; $ia.SetColorMatrix($cm)
    $g.DrawImage($bmp, (New-Object Drawing.Rectangle $x, $y, $bmp.Width, $bmp.Height), 0, 0, $bmp.Width, $bmp.Height, 'Pixel', $ia)
    $ia.Dispose()
}

# ---------------------------------------------------------------- demo.gif
$W = 760; $H = 330; $fps = 20; $scale = [single]1.25; $theme = 'Dark'; $opacity = 45
$popups = @{
    'zh' = Render-Popup (New-State 0x804 $true)  $scale $theme $opacity
    'en' = Render-Popup (New-State 0x804 $false) $scale $theme $opacity
}
$margin = $marginFn.Invoke($null, @($scale))

# Builds a string from code points so this script works regardless of file encoding.
function Cjk { -join ($args | ForEach-Object { [char]$_ }) }

# Timeline (ms). Text runs: [start, end, text, cjk]
$runs = @(
    @(200, 1400, 'Hello, world! ', $false),
    @(2900, 3500, (Cjk 0x4F60 0x597D 0x4E16 0x754C), $true),
    @(4700, 5300, ' IME ', $false),
    @(6600, 7000, (Cjk 0x518D 0x89C1), $true)
)
$events = @(   # popup start, popup key, keycap label
    @(1700, 'zh', 'Win + Space'),
    @(3900, 'en', 'Shift'),
    @(5700, 'zh', 'Shift')
)
$total = 8600

$codeFont = New-Object Drawing.Font('Consolas', 20, [Drawing.GraphicsUnit]::Pixel)
$cjkFont  = New-Object Drawing.Font('Microsoft YaHei UI', 19, [Drawing.GraphicsUnit]::Pixel)
$uiFont   = New-Object Drawing.Font('Segoe UI', 13, [Drawing.GraphicsUnit]::Pixel)
$keyFont  = New-Object Drawing.Font('Segoe UI Semibold', 14, [Drawing.GraphicsUnit]::Pixel)
$typo     = New-Object Drawing.StringFormat([Drawing.StringFormat]::GenericTypographic)
$typo.FormatFlags = $typo.FormatFlags -bor [Drawing.StringFormatFlags]::MeasureTrailingSpaces

$frames = Join-Path ([IO.Path]::GetTempPath()) ('li-demo-' + [guid]::NewGuid())
New-Item -ItemType Directory $frames | Out-Null

$winX = 40; $winY = 34; $winW = $W - 80; $winH = $H - 84
$textX = $winX + 24; $lineY = $winY + 96

$n = [int]($total / 1000 * $fps)
for ($f = 0; $f -lt $n; $f++) {
    $t = $f * 1000.0 / $fps
    $bmp = New-Object Drawing.Bitmap $W, $H
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'

    # Desktop wallpaper
    $wall = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Rectangle 0, 0, $W, $H),
        [Drawing.Color]::FromArgb(40, 70, 120), [Drawing.Color]::FromArgb(150, 90, 140), [single]25)
    $g.FillRectangle($wall, 0, 0, $W, $H); $wall.Dispose()

    # Editor window
    $win = New-RoundRect $winX $winY $winW $winH 10
    $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 30, 30, 32))), $win)
    $bar = New-RoundRect $winX $winY $winW 38 10
    $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 44, 44, 48))), $bar)
    $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 44, 44, 48))), $winX, $winY + 20, $winW, 18)
    $g.DrawString('notes.txt - Notepad', $uiFont, (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(200, 230, 230, 230))), $winX + 16, $winY + 11)
    $g.DrawString('// taskbar hidden - where am I typing?', $codeFont, (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 106, 153, 85))), $textX, $winY + 56, $typo)

    # Typed text
    $x = [single]$textX
    foreach ($r in $runs) {
        if ($t -lt $r[0]) { break }
        $chars = $r[2].Length
        $shown = [Math]::Min($chars, [int][Math]::Floor(($t - $r[0]) / (($r[1] - $r[0]) / $chars)) + 1)
        $txt = $r[2].Substring(0, $shown)
        $font = if ($r[3]) { $cjkFont } else { $codeFont }
        $dy = if ($r[3]) { 1 } else { 0 }
        $g.DrawString($txt, $font, (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 220, 220, 220))), $x, $lineY + $dy, $typo)
        $x += $g.MeasureString($txt, $font, 2000, $typo).Width
    }
    # Caret (blinks when idle)
    if ([int]($t / 530) % 2 -eq 0 -or ($runs | Where-Object { $t -ge $_[0] -and $t -le $_[1] + 300 })) {
        $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 235, 235, 235))), $x + 1, $lineY, 2, 24)
    }

    # Popup and keycap for the active event
    foreach ($e in $events) {
        $dt = $t - $e[0]
        if ($dt -ge -150 -and $dt -lt 900) {
            $kw = $g.MeasureString($e[2], $keyFont).Width + 24
            $kp = New-RoundRect (($W - $kw) / 2) ($H - 42) $kw 30 7
            $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(200, 20, 20, 24))), $kp)
            $g.DrawPath((New-Object Drawing.Pen ([Drawing.Color]::FromArgb(70, 255, 255, 255))), $kp)
            $g.DrawString($e[2], $keyFont, [Drawing.Brushes]::White, ($W - $kw) / 2 + 12, $H - 36)
        }
        if ($dt -lt 0 -or $dt -gt 1340) { continue }
        # Same easing as OsdWindow: 160 ms cubic-out in, 900 ms hold, 280 ms smoothstep out.
        if ($dt -lt 160) { $v = $dt / 160; $ease = 1 - [Math]::Pow(1 - $v, 3) }
        elseif ($dt -lt 1060) { $ease = 1 }
        else { $v = 1 - ($dt - 1060) / 280; $ease = $v * $v * (3 - 2 * $v) }
        $p = $popups[$e[1]]
        $px = [int]($x - $margin); $py = [int]($lineY + 24 + 8 * $scale - $margin + (1 - $ease) * 8 * $scale)
        Draw-Alpha $g $p $px $py ([single]$ease)
    }

    $g.Dispose()
    $bmp.Save((Join-Path $frames ('f{0:D4}.png' -f $f))); $bmp.Dispose()
}

$gif = Join-Path $docs 'demo.gif'
& python (Join-Path $PSScriptRoot 'encode_gif.py') $frames $gif ([int](1000 / $fps))
if ($LASTEXITCODE -ne 0) { throw 'GIF encoding failed' }
Remove-Item -Recurse -Force $frames
Write-Host "Wrote $gif"

# ---------------------------------------------------------------- themes.png
$names = $themeT.GetField('Names').GetValue($null) | Where-Object { $_ -ne 'Auto' }
$rowH = 84; $GW = 820; $GH = $rowH * $names.Count + 24
$bmp = New-Object Drawing.Bitmap $GW, $GH
$g = [Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
$wall = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Rectangle 0, 0, $GW, $GH),
    [Drawing.Color]::FromArgb(235, 238, 245), [Drawing.Color]::FromArgb(35, 60, 110), [single]15)
$g.FillRectangle($wall, 0, 0, $GW, $GH)
$labelFont = New-Object Drawing.Font('Segoe UI Semibold', 15, [Drawing.GraphicsUnit]::Pixel)
$row = 0
foreach ($name in $names) {
    $y = 12 + $rowH * $row
    $chip = New-RoundRect 14 ($y + 25) 96 30 8
    $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(170, 20, 20, 28))), $chip)
    $g.DrawString($name, $labelFont, [Drawing.Brushes]::White, 24, $y + 30)
    $x = 118
    foreach ($s in @((New-State 0x409 $false), (New-State 0x804 $true), (New-State 0x804 $false))) {
        $p = Render-Popup $s ([single]1.25) $name 45
        $g.DrawImage($p, $x, $y); $x += $p.Width - 6; $p.Dispose()
    }
    $row++
}
$g.Dispose()
$png = Join-Path $docs 'themes.png'
$bmp.Save($png, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
Write-Host "Wrote $png"
