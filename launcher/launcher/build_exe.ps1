# ===========================================================
#   build_exe.ps1
#   Pack the launcher into a standalone Windows executable
#   using PyInstaller. Output: launcher\dist\SentienceV5.exe
# ===========================================================
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Assert-Python {
    $py = Get-Command python -ErrorAction SilentlyContinue
    if (-not $py) {
        throw "Python is not on PATH. Install Python 3.10+ first."
    }
    return $py.Source
}

$systemPython = Assert-Python

# Pick a Python that already has flet 0.85+, flet_desktop, flet_cli, PyInstaller.
# We avoid pip install here (build host may be offline). Caller is expected to
# have run `pip install -r requirements.txt` once with network access.
$venvPython = Join-Path $PSScriptRoot ".venv\Scripts\python.exe"
$pythonExe = $null
$probe = "import flet.version, flet_desktop, flet_cli, PyInstaller, sys; v=getattr(flet.version,'__version__',None) or getattr(flet.version,'flet_version','0'); parts=[int(x) for x in v.split('.')[:2]]; sys.exit(0 if parts >= [0,85] else 2)"
foreach ($candidate in @($venvPython, $systemPython)) {
    if (-not (Test-Path $candidate)) { continue }
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $candidate -c $probe *> $null
    } catch {
        # Ignore stderr-as-error from native invocations
    } finally {
        $ErrorActionPreference = $prev
    }
    if ($LASTEXITCODE -eq 0) {
        $pythonExe = $candidate
        Write-Host "[*] Using Python: $candidate" -ForegroundColor Cyan
        break
    }
}

if (-not $pythonExe) {
    Write-Host "[!] No Python found with flet[all]>=0.85 + pyinstaller. Attempting offline pip install into .venv..." -ForegroundColor Yellow
    if (-not (Test-Path $venvPython)) {
        Write-Host "[*] Creating virtual environment..." -ForegroundColor Cyan
        & $systemPython -m venv .venv
    }
    & $venvPython -m pip install -r requirements.txt
    if ($LASTEXITCODE -ne 0) {
        throw "pip install failed. Run: python -m pip install -r requirements.txt manually, then retry."
    }
    $pythonExe = $venvPython
}

# Clean previous build
foreach ($d in @("build", "dist")) {
    if (Test-Path $d) { Remove-Item $d -Recurse -Force }
}
Get-ChildItem -Filter "*.spec" -ErrorAction SilentlyContinue | Remove-Item -Force

# Seed FLET_VIEW_PATH so flet's PyInstaller hook bundles the local desktop
# runtime instead of downloading it from GitHub (which fails offline).
# The hook copies the contents of $env:FLET_VIEW_PATH into the bundle as
# `_internal/flet_desktop/app/`, so we point it at the dir that already
# contains the `flet/` subdir (with flet.exe).
if (-not $env:FLET_VIEW_PATH) {
    $offlineRuntime = Resolve-Path "$PSScriptRoot\..\_internal\flet_desktop\app" -ErrorAction SilentlyContinue
    if ($offlineRuntime -and (Test-Path (Join-Path $offlineRuntime "flet\flet.exe"))) {
        $env:FLET_VIEW_PATH = $offlineRuntime.Path
        Write-Host "[*] Using offline Flet runtime: $($offlineRuntime.Path)" -ForegroundColor Cyan
    }
}

Write-Host "[*] Running PyInstaller..." -ForegroundColor Cyan
$iconArg = @()
if (Test-Path "$PSScriptRoot\assets\NexusV.ico") {
    $iconArg = @("--icon", "assets/NexusV.ico")
}
$assetsArg = @()
if (Test-Path "$PSScriptRoot\assets") {
    $assetsArg = @("--add-data", "assets;assets")
}
& $pythonExe -m PyInstaller `
    --name SentienceV5 `
    --noconfirm `
    --windowed `
    @iconArg `
    @assetsArg `
    --hidden-import flet `
    --hidden-import flet.canvas `
    --hidden-import flet_desktop `
    --collect-all flet `
    --collect-all flet_desktop `
    main.py

if ($LASTEXITCODE -ne 0) {
    throw "PyInstaller failed with exit code $LASTEXITCODE"
}

$exePath = Join-Path $PSScriptRoot "dist\SentienceV5\SentienceV5.exe"
if (Test-Path $exePath) {
    $sizeMB = [Math]::Round((Get-Item $exePath).Length / 1MB, 1)
    Write-Host "" 
    Write-Host "[OK] Built dist\SentienceV5\SentienceV5.exe ($sizeMB MB)" -ForegroundColor Green
} else {
    throw "Expected dist\SentienceV5\SentienceV5.exe not found"
}

# Deploy onedir output to the project root so the layout looks like:
#   <root>\SentienceV5.exe
#   <root>\_internal\...
$deployRoot = Resolve-Path "$PSScriptRoot\.."
$distDir = Join-Path $PSScriptRoot "dist\SentienceV5"
Write-Host "[*] Deploying to $deployRoot" -ForegroundColor Cyan
Copy-Item -Path (Join-Path $distDir "SentienceV5.exe") -Destination $deployRoot -Force
$rootInternal = Join-Path $deployRoot "_internal"
if (Test-Path $rootInternal) {
    Remove-Item $rootInternal -Recurse -Force
}
Copy-Item -Path (Join-Path $distDir "_internal") -Destination $deployRoot -Recurse -Force
Write-Host "[OK] Deployed SentienceV5.exe + _internal\ to $deployRoot" -ForegroundColor Green
