#requires -Version 5.1
<#
    Builds bin\MyMchose.exe using the C# compiler that ships with the .NET Framework.
    No .NET SDK, NuGet or Visual Studio required.
#>
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

$out = Join-Path $outDir 'MyMchose.exe'
$source = Join-Path $root 'src\MyMchose.cs'

& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    "/out:$out" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "$source"

if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
Write-Host "Built $out"
