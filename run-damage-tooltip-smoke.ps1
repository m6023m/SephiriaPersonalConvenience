param([switch]$LayoutOnly, [switch]$UiOnly, [switch]$Audit, [string]$Prepared = 'CombatTestArena/prepared-ddfeb3926fd3483e9c5d0d61884b2f5a', [string]$ValidationPlan)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Prepared).Path
$runtime = Join-Path $root 'runtime'
$testExe = Join-Path $runtime 'Sephiria.exe'
if (Get-Process -Name Sephiria -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $testExe }) { throw 'The isolated runtime is already running.' }
$testSource = Join-Path $PSScriptRoot 'DamageTooltipSmoke.cs'
if ($Audit) { $testSource = Join-Path $PSScriptRoot 'AuditDamageSmoke.cs' }
if ($LayoutOnly) { $testSource = Join-Path $PSScriptRoot 'UiLayoutSmoke.cs' }
$runDirectory = $root
$copiedPlan = $null
if ($ValidationPlan) {
    if ($UiOnly) { throw 'UiOnly and ValidationPlan cannot be combined.' }
    $planPath = (Resolve-Path -LiteralPath $ValidationPlan).Path
    $plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
    if (@($plan.selected).Count -eq 0) { Write-Output 'No changed cases: skipped build and game launch.'; return }
    $runDirectory = Join-Path $root ('validation-runs/' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    $copiedPlan = Join-Path $runDirectory 'plan.json'
    Copy-Item -LiteralPath $planPath -Destination $copiedPlan
    $testSource = Join-Path $PSScriptRoot 'SelectedDamageSmoke.cs'
}
# Compile current sources; unchanged production inputs are reused by build.ps1.
& (Join-Path $PSScriptRoot 'build.ps1')
$disabled = Join-Path $root 'disabled-test-plugins'
New-Item -ItemType Directory -Force $disabled | Out-Null
foreach ($other in @('StageSmoke.dll','UpdateSmoke.dll','RetrySmoke.dll','DamageTooltipSmoke.dll','ConvenienceSmoke.dll','DiceSmoke.dll','OtherSmoke.dll')) {
    $old = Join-Path $runtime ('BepInEx/plugins/' + $other)
    if ($other -ne 'DamageTooltipSmoke.dll' -and (Test-Path -LiteralPath $old)) { Move-Item -LiteralPath $old -Destination (Join-Path $disabled $other) -Force }
}
$rsp = Get-Content (Join-Path $PSScriptRoot 'dist/build.rsp') | Where-Object { $_ -notlike '/out:*' -and $_ -notmatch '\.cs"$' }
$rsp += '/reference:"' + (Join-Path $PSScriptRoot 'dist/SephiriaPersonalConvenience.dll') + '"'
$rsp += '/out:"' + (Join-Path $PSScriptRoot 'dist/DamageTooltipSmoke.dll') + '"'
$rsp += '"' + $testSource + '"'
$rsp | Set-Content (Join-Path $PSScriptRoot 'dist/smoke.rsp') -Encoding utf8
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /noconfig ('@' + (Join-Path $PSScriptRoot 'dist/smoke.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Smoke build failed' }
Copy-Item (Join-Path $PSScriptRoot 'dist/SephiriaPersonalConvenience.dll') (Join-Path $runtime 'BepInEx/plugins')
Copy-Item (Join-Path $PSScriptRoot 'dist/DamageTooltipSmoke.dll') (Join-Path $runtime 'BepInEx/plugins')
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $runtime 'Sephiria.exe'
$start.WorkingDirectory = $runtime
$start.UseShellExecute = $false
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
foreach ($arg in @('-batchmode','-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',(Join-Path $runDirectory 'damage-tooltip-unity.log'))) { $start.ArgumentList.Add($arg) }
$start.Environment['SEPHIRIA_COMBAT_TEST_ROOT'] = $root
$start.Environment['SEPHIRIA_COMBAT_TEST_MUTE'] = '1'
if ($UiOnly) { $start.Environment['STAGE_UI_ONLY'] = '1' }
if ($copiedPlan) { $start.Environment['SEPHIRIA_DAMAGE_VALIDATION_PLAN'] = $copiedPlan }
$process = [Diagnostics.Process]::Start($start)
@{ProcessId=$process.Id;Root=$root;StartedUtc=$process.StartTime.ToUniversalTime().ToString('o');RunDirectory=$runDirectory;Plan=$copiedPlan;TestDllHash=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'dist/SephiriaPersonalConvenience.dll')).Hash} | ConvertTo-Json | Set-Content (Join-Path $root 'damage-tooltip-process.json')
Write-Output "Started isolated damage tooltip test PID=$($process.Id)"
Write-Output "Evidence directory: $runDirectory"
