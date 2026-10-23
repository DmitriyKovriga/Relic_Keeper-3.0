[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $GraphifyArguments
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$pythonPath = Join-Path $projectRoot '.graphify/venv/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $pythonPath)) {
    throw 'Graphify is not installed here. Run Tools/Graphify/Install-Graphify.ps1 first.'
}

$env:PYTHONUTF8 = '1'
$env:GRAPHIFY_OUT = Join-Path $projectRoot '.graphify/index'
$env:GRAPHIFY_QUERY_LOG_DISABLE = '1'
Push-Location -LiteralPath $projectRoot
try {
    & $pythonPath -m graphify @GraphifyArguments
    $graphifyExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $graphifyExitCode
