# Renders the LangPop lollipop logo to assets/langpop.ico (multi-size) and docs/logo.png.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot 'Lollipop.cs') -ReferencedAssemblies System.Drawing

$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force (Join-Path $root 'assets'), (Join-Path $root 'docs') | Out-Null

# .ico with PNG-compressed images (supported since Windows Vista)
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($s in $sizes) {
    $bmp = [Lollipop]::Draw($s)
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , $ms.ToArray()
}

$ico = Join-Path $root 'assets\langpop.ico'
$out = New-Object IO.BinaryWriter([IO.File]::Create($ico))
$out.Write([uint16]0); $out.Write([uint16]1); $out.Write([uint16]$sizes.Count)   # ICONDIR
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {                                         # ICONDIRENTRY
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $out.Write([byte]$dim); $out.Write([byte]$dim); $out.Write([byte]0); $out.Write([byte]0)
    $out.Write([uint16]1); $out.Write([uint16]32)
    $out.Write([uint32]$images[$i].Length); $out.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $out.Write($img) }
$out.Close()
Write-Host "Wrote $ico"

$logo = [Lollipop]::Draw(256)
$png = Join-Path $root 'docs\logo.png'
$logo.Save($png, [Drawing.Imaging.ImageFormat]::Png); $logo.Dispose()
Write-Host "Wrote $png"
