#requires -Version 5.1
<#
    Builds bin\MyMchose.exe using the C# compiler that ships with the .NET Framework.
    No .NET SDK, NuGet or Visual Studio required.

    Usage:
        .\build.ps1                       # 0.0.1
        .\build.ps1 -Version 1.2.3        # stamp a version
#>
param(
    [string]$Version = '0.0.1'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'csc.exe (.NET Framework 4.x) not found.'
}

$outDir = Join-Path $root 'bin'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$objDir = Join-Path $root 'obj'
if (-not (Test-Path $objDir)) { New-Item -ItemType Directory -Path $objDir | Out-Null }

# Assembly version must be numeric, four parts.
$numeric = ($Version.TrimStart('v', 'V') -split '[^0-9]+' | Where-Object { $_ -ne '' })
while ($numeric.Count -lt 4) { $numeric += '0' }
$asmVersion = ($numeric[0..3] -join '.')
$versionFile = Join-Path $objDir 'VersionInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyVersion("$asmVersion")]
[assembly: AssemblyFileVersion("$asmVersion")]
[assembly: AssemblyInformationalVersion("$Version")]
[assembly: AssemblyTitle("MyMchose")]
[assembly: AssemblyProduct("MyMchose")]
[assembly: AssemblyCompany("Bpazy")]
"@ | Set-Content -Path $versionFile -Encoding UTF8

$out = Join-Path $outDir 'MyMchose.exe'
$source = Join-Path $root 'src\MyMchose.cs'

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    "/out:$out" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "$source" "$versionFile"

if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
Write-Host "Built $out (version $asmVersion)"
