# =============================================================
#   Sentience V5 - Anima · One-Shot Build Script
# -------------------------------------------------------------
#   1. Builds the GTA5MOD2026 mod (Release, net48)
#   2. Builds the Sentience V5 launcher .exe (PyInstaller)
#   3. Copies artifacts into the deliverable folder layout:
#         SentienceV5-Anima/
#         ├── SentienceV5.exe         (launcher)
#         ├── ModFiles/               (DLLs to copy into GTA5\scripts)
#         ├── Runtime/llama.cpp/      (llama-server + dlls, already in place)
#         └── ...
# =============================================================
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$root = $PSScriptRoot
$modSrc = "D:\AI NPCS\GTA5MOD2026\GTA5MOD2026"
$modProject = Join-Path $modSrc "GTA5MOD2026.csproj"
$launcherDir = Join-Path $root "launcher"
$modFilesDst = Join-Path $root "ModFiles"
$installScripts = Join-Path $root "Install_To_GTA_scripts"

function Section($msg) {
    Write-Host ""
    Write-Host "=== $msg ===" -ForegroundColor Cyan
}

# -------------------------------------------------------------
# Step 1: Build mod (.NET Framework 4.8)
# -------------------------------------------------------------
Section "Step 1/3 · Build GTA5MOD2026 (Release)"

if (-not (Test-Path $modProject)) {
    throw "Mod project not found: $modProject"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "[WARN] dotnet CLI not found. Skipping mod build." -ForegroundColor Yellow
    Write-Host "       Install .NET SDK from https://dotnet.microsoft.com/download to enable." -ForegroundColor Yellow
} else {
    & dotnet build $modProject -c Release --nologo --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Mod build failed (exit $LASTEXITCODE)"
    }

    $modDllSearch = Get-ChildItem -Path (Join-Path $modSrc "bin\Release") -Filter "GTA5MOD2026.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $modDllSearch) {
        throw "GTA5MOD2026.dll not found in bin\Release after build"
    }
    $modDll = $modDllSearch.FullName
    Write-Host "[OK] Mod built: $modDll" -ForegroundColor Green

    # Copy mod DLL + Newtonsoft + NAudio runtime DLLs
    if (-not (Test-Path $modFilesDst)) { New-Item -ItemType Directory -Path $modFilesDst | Out-Null }
    Copy-Item -Path $modDll -Destination $modFilesDst -Force

    foreach ($extra in @("Newtonsoft.Json.dll", "NAudio.dll", "NAudio.Core.dll", "NAudio.Wasapi.dll", "NAudio.WinMM.dll")) {
        $found = Get-ChildItem -Path (Split-Path $modDll -Parent) -Filter $extra -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            Copy-Item -Path $found.FullName -Destination $modFilesDst -Force
        }
    }
    Write-Host "[OK] Copied mod DLLs to $modFilesDst" -ForegroundColor Green
}

# -------------------------------------------------------------
# Step 2: Build launcher .exe (PyInstaller)
# -------------------------------------------------------------
Section "Step 2/3 · Build Launcher (PyInstaller)"

if (-not (Test-Path (Join-Path $launcherDir "main.py"))) {
    throw "Launcher source missing: $launcherDir\main.py"
}

Push-Location $launcherDir
try {
    & powershell -NoProfile -ExecutionPolicy Bypass -File ".\build_exe.ps1"
    if ($LASTEXITCODE -ne 0) {
        throw "Launcher build failed (exit $LASTEXITCODE)"
    }
} finally {
    Pop-Location
}

$exeSrc = Join-Path $launcherDir "dist\SentienceV5.exe"
$exeDst = Join-Path $root "SentienceV5.exe"
if (Test-Path $exeSrc) {
    Copy-Item -Path $exeSrc -Destination $exeDst -Force
    Write-Host "[OK] Launcher exe → $exeDst" -ForegroundColor Green
} else {
    throw "Expected $exeSrc not found"
}

# -------------------------------------------------------------
# Step 3: Sync deliverable folder
# -------------------------------------------------------------
Section "Step 3/3 · Sync deliverable folder"

# Mirror ModFiles into Install_To_GTA_scripts (drop-in for GTA5\scripts\)
if (-not (Test-Path $installScripts)) { New-Item -ItemType Directory -Path $installScripts | Out-Null }
if (Test-Path $modFilesDst) {
    Get-ChildItem $modFilesDst -File | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $installScripts -Force
    }
    Write-Host "[OK] Synced ModFiles → Install_To_GTA_scripts" -ForegroundColor Green
}

Write-Host ""
Write-Host "BUILD COMPLETE" -ForegroundColor Green
Write-Host "Deliverable folder: $root" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Double-click SentienceV5.exe to launch."
Write-Host "  2. Copy contents of Install_To_GTA_scripts into GTA5\scripts\."
Write-Host "  3. Make sure your .gguf model is in Runtime\ or %USERPROFILE%\Documents\GTA5MOD2026\."
