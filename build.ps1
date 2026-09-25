# Builds bin\LanguageIndicator.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

& $csc /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 `
    "/out:$out\LanguageIndicator.exe" `
    "/win32manifest:$root\app.manifest" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Accessibility.dll `
    "$root\src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }
Write-Host "Built $out\LanguageIndicator.exe"
