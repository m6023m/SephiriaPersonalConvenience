param(
    [Parameter(Mandatory=$true)][string]$RunDirectory,
    [string]$Ledger = 'PersonalConvenience/evidence/damage-details/validation-ledger.json'
)
$ErrorActionPreference = 'Stop'
$run = (Resolve-Path -LiteralPath $RunDirectory).Path
$resultsPath = Join-Path $run 'results.json'
if (!(Test-Path -LiteralPath $resultsPath)) { throw 'No runtime results were produced.' }
$results = Get-Content -LiteralPath $resultsPath -Raw | ConvertFrom-Json
if (!$results.complete) { throw 'Runtime checks are incomplete. Do not record partial results as a completed run.' }
& python -X utf8 (Join-Path $PSScriptRoot 'incremental-validation.py') record --plan (Join-Path $run 'plan.json') --results $resultsPath --ledger $Ledger
if ($LASTEXITCODE -ne 0) { throw 'Validation results were not accepted.' }
$failed = @($results.results | Where-Object { $_.status -ne 'passed' })
Write-Output "Recorded $(@($results.results).Count - $failed.Count) passing cases; $($failed.Count) cases remain unverified."
