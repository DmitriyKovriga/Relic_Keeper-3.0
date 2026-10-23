[CmdletBinding()]
param([string] $Python)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $Python) {
    $bundledPython = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    if (Test-Path -LiteralPath $bundledPython) { $Python = $bundledPython }
    else {
        $candidate = Get-Command python -ErrorAction SilentlyContinue
        if ($candidate -and $candidate.Source -notmatch 'WindowsApps') { $Python = $candidate.Source }
    }
}
if (-not $Python) { throw 'Provide Python 3.10 or newer with -Python C:\Path\To\python.exe.' }

$localRoot = Join-Path $projectRoot '.graphify'
$venvPython = Join-Path $localRoot 'venv/Scripts/python.exe'
$taskTemp = Join-Path $localRoot 'temp'
New-Item -ItemType Directory -Force -Path $taskTemp | Out-Null
$previousTemp = $env:TEMP; $previousTmp = $env:TMP
try {
    $env:TEMP = $taskTemp; $env:TMP = $taskTemp
    if (-not (Test-Path -LiteralPath $venvPython)) {
        & $Python -m venv (Join-Path $localRoot 'venv')
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the local Python environment.' }
    }
    & $venvPython -m ensurepip --upgrade --default-pip
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize pip.' }
    & $venvPython -m pip install --disable-pip-version-check --cache-dir (Join-Path $localRoot 'pip-cache') -r (Join-Path $PSScriptRoot 'requirements.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Graphify installation failed.' }
    & $venvPython -m pip check
    if ($LASTEXITCODE -ne 0) { throw 'Graphify dependencies are inconsistent.' }
}
finally {
    $env:TEMP = $previousTemp; $env:TMP = $previousTmp
}
