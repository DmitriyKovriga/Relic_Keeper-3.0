# Extract uses its AST cache and manifest to reuse unchanged files.
# Code-only deliberately prevents semantic model/API calls, including for documents.
& (Join-Path $PSScriptRoot 'Invoke-Graphify.ps1') extract . --code-only --max-workers 4
exit $LASTEXITCODE
