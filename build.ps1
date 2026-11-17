# Builds Output\AirBridge_Setup_v<version>.exe
#   .\build.ps1            (version comes from AirBridge.csproj)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$version = ([xml](Get-Content AirBridge.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$iscc = @("$env:ProgramFiles\Inno Setup 7\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", 'G:\APPS\Inno Setup 7\ISCC.exe') |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) not found. Install Inno Setup 6 or 7.' }
$python = 'engine\venv\Scripts\python.exe'
if (-not (Test-Path $python)) { throw 'Engine venv missing. See README > Building.' }

Write-Host "== AirBridge ${version}: engine (PyInstaller)"
& $python -m PyInstaller --noconfirm --clean AirBridgeEngine.spec
if ($LASTEXITCODE) { throw 'PyInstaller failed' }

Write-Host '== Tray app (dotnet publish, self-contained)'
if (Test-Path dist\app) { Remove-Item -Recurse -Force dist\app }
dotnet publish AirBridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o dist\app
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

Write-Host '== Installer (Inno Setup)'
& $iscc "/DMyAppVersion=$version" installer\installer.iss
if ($LASTEXITCODE) { throw 'ISCC failed' }

Get-Item "Output\AirBridge_Setup_v$version.exe" | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,1)}}
