# Builds a release and packages it into dist\:
#   LangPop-<version>-win.zip   (LangPop.exe + README.md + LICENSE)
#   LangPop.exe                 (standalone, for people who just want the exe)
#   SHA256SUMS.txt
param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$Version = $Version.TrimStart('v')

& (Join-Path $root 'build.ps1') -Version $Version

$dist  = Join-Path $root 'dist'
$stage = Join-Path $dist "LangPop-$Version"
if (Test-Path $dist) { Get-ChildItem $dist | Remove-Item -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

Copy-Item (Join-Path $root 'bin\LangPop.exe'), (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE') $stage
$zip = Join-Path $dist "LangPop-$Version-win.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Copy-Item (Join-Path $root 'bin\LangPop.exe') $dist
Remove-Item -Recurse -Force $stage

$sums = Get-ChildItem $dist -File | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name
}
$sums | Set-Content -Encoding ASCII (Join-Path $dist 'SHA256SUMS.txt')
Get-ChildItem $dist | ForEach-Object { Write-Host ("  {0,-28} {1,8:N0} bytes" -f $_.Name, $_.Length) }
