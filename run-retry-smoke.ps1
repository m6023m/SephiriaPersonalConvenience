param([string]$Prepared = 'CombatTestArena/prepared-ddfeb3926fd3483e9c5d0d61884b2f5a')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Prepared).Path
$runtime = Join-Path $root 'runtime'
$testExe = Join-Path $runtime 'Sephiria.exe'
if (Get-CimInstance Win32_Process -Filter "name='Sephiria.exe'" | Where-Object { $_.ExecutablePath -eq $testExe }) { throw 'The isolated runtime is already running.' }
$disabled = Join-Path $root 'disabled-test-plugins'
New-Item -ItemType Directory -Force $disabled | Out-Null
foreach ($other in @('RetrySmoke.dll','ConvenienceSmoke.dll','DiceSmoke.dll','OtherSmoke.dll')) {
    $old = Join-Path $runtime ('BepInEx/plugins/' + $other)
    if ($other -ne 'RetrySmoke.dll' -and (Test-Path -LiteralPath $old)) { Move-Item -LiteralPath $old -Destination (Join-Path $disabled $other) -Force }
}
$rsp = Get-Content (Join-Path $PSScriptRoot 'dist/build.rsp') | Where-Object { $_ -notlike '/out:*' -and $_ -notmatch '\.cs"$' }
$rsp += '/reference:"' + (Join-Path $PSScriptRoot 'dist/SephiriaPersonalConvenience.dll') + '"'
$rsp += '/out:"' + (Join-Path $PSScriptRoot 'dist/RetrySmoke.dll') + '"'
$rsp += '"' + (Join-Path $PSScriptRoot 'RetrySmoke.cs') + '"'
$rsp | Set-Content (Join-Path $PSScriptRoot 'dist/smoke.rsp') -Encoding utf8
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /noconfig ('@' + (Join-Path $PSScriptRoot 'dist/smoke.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Smoke build failed' }
Copy-Item (Join-Path $PSScriptRoot 'dist/SephiriaPersonalConvenience.dll') (Join-Path $runtime 'BepInEx/plugins')
Copy-Item (Join-Path $PSScriptRoot 'dist/RetrySmoke.dll') (Join-Path $runtime 'BepInEx/plugins')
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $runtime 'Sephiria.exe'
$start.WorkingDirectory = $runtime
$start.UseShellExecute = $false
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
foreach ($arg in @('-batchmode','-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',(Join-Path $root 'retry-unity.log'))) { $start.ArgumentList.Add($arg) }
$start.Environment['SEPHIRIA_COMBAT_TEST_ROOT'] = $root
$start.Environment['SEPHIRIA_COMBAT_TEST_MUTE'] = '1'
$process = [Diagnostics.Process]::Start($start)
@{ProcessId=$process.Id;Root=$root} | ConvertTo-Json | Set-Content (Join-Path $root 'retry-process.json')
Write-Output "Started isolated bundled retry test PID=$($process.Id)"






