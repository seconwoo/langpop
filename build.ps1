# Builds bin\LangPop.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
#   -Version 1.2.3   stamps the version into the exe's file properties (default 0.0.0 for local builds)
param([string]$Version = '0.0.0')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.2.3 (got '$Version')" }

$out = Join-Path $root 'bin'
$obj = Join-Path $root 'obj'
New-Item -ItemType Directory -Force $out, $obj | Out-Null

# Version resource shown in Explorer > Properties > Details
$versionInfo = Join-Path $obj 'VersionInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyTitle("LangPop")]
[assembly: AssemblyProduct("LangPop")]
[assembly: AssemblyDescription("Shows your input language next to the text cursor when you switch it")]
[assembly: AssemblyCopyright("Licensed under the Apache License 2.0")]
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
[assembly: AssemblyInformationalVersion("$Version")]
"@ | Set-Content -Encoding UTF8 $versionInfo

& $csc /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 `
    "/out:$out\LangPop.exe" `
    "/win32manifest:$root\app.manifest" `
    "/win32icon:$root\assets\langpop.ico" `
    "/resource:$root\assets\langpop.ico,LangPop.langpop.ico" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Accessibility.dll `
    "$root\src\*.cs" $versionInfo
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }
Write-Host "Built $out\LangPop.exe ($Version)"
