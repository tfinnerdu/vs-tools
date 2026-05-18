<#
.SYNOPSIS
  Build, test, and optionally pack DoaneDevTools.

.PARAMETER Configuration
  Debug or Release (default: Debug)

.PARAMETER Pack
  Pack the NuGet package after build

.PARAMETER Install
  Copy VSIX to VS2022 extension folder for local testing

.EXAMPLE
  .\build.ps1
  .\build.ps1 -Configuration Release -Pack
  .\build.ps1 -Install
#>

param(
    [string]$Configuration = "Debug",
    [switch]$Pack,
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Find-MSBuild {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        $msbuild = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path $msbuild) { return $msbuild }
    }
    throw "MSBuild not found. Install Visual Studio 2022 with the 'Desktop development with C++' or '.NET desktop development' workload."
}

$msbuild = Find-MSBuild
Write-Host "Using MSBuild: $msbuild" -ForegroundColor Cyan

# Restore
Write-Host "`n[1/3] Restoring NuGet packages..." -ForegroundColor Yellow
& nuget restore "$root\DoaneDevTools.sln"
if ($LASTEXITCODE -ne 0) { throw "nuget restore failed" }

# Build
Write-Host "`n[2/3] Building ($Configuration)..." -ForegroundColor Yellow
& $msbuild "$root\DoaneDevTools.sln" `
    /p:Configuration=$Configuration `
    /p:Platform="Any CPU" `
    /m /v:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed" }

# Pack
if ($Pack) {
    Write-Host "`n[3/3] Packing NuGet..." -ForegroundColor Yellow
    & $msbuild "$root\DoaneDevTools.Analyzers.NuGet\DoaneDevTools.Analyzers.NuGet.csproj" `
        /p:Configuration=Release /t:Pack /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "NuGet pack failed" }
    $nupkg = Get-ChildItem "$root\DoaneDevTools.Analyzers.NuGet\bin\Release\*.nupkg" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($nupkg) { Write-Host "Package: $($nupkg.FullName)" -ForegroundColor Green }
}

# Install VSIX locally
if ($Install) {
    $vsix = Get-ChildItem "$root\DoaneDevTools.Vsix\bin\$Configuration\*.vsix" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $vsix) { throw "VSIX not found in bin/$Configuration — build first" }
    $installer = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\VSIXInstaller.exe"
    if (-not (Test-Path $installer)) { $installer = "VSIXInstaller.exe" }
    Write-Host "Installing $($vsix.Name)..." -ForegroundColor Yellow
    & $installer /quiet "$($vsix.FullName)"
    Write-Host "Installed. Restart Visual Studio to activate." -ForegroundColor Green
}

Write-Host "`nBuild complete." -ForegroundColor Green
