param([switch]$VerifyApplied, [switch]$UiOnly, [string]$Prepared = 'CombatTestArena/prepared-ddfeb3926fd3483e9c5d0d61884b2f5a')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Prepared).Path
$runtime = Join-Path $root 'runtime'
$testExe = Join-Path $runtime 'Sephiria.exe'
if (Get-CimInstance Win32_Process -Filter "name='Sephiria.exe'" | Where-Object { $_.ExecutablePath -eq $testExe }) { throw 'The isolated runtime is already running.' }
$disabled = Join-Path $root 'disabled-test-plugins'
New-Item -ItemType Directory -Force $disabled | Out-Null
foreach ($other in @('StageSmoke.dll','RetrySmoke.dll','UpdateSmoke.dll','ConvenienceSmoke.dll','DiceSmoke.dll','OtherSmoke.dll')) {
    $old = Join-Path $runtime ('BepInEx/plugins/' + $other)
    if ($other -ne 'UpdateSmoke.dll' -and (Test-Path -LiteralPath $old)) { Move-Item -LiteralPath $old -Destination (Join-Path $disabled $other) -Force }
}
$rsp = Get-Content (Join-Path $PSScriptRoot 'dist/build.rsp') | Where-Object { $_ -notlike '/out:*' -and $_ -notmatch '\.cs"$' }
$rsp += '/reference:"' + (Join-Path $PSScriptRoot 'dist/SephiriaPersonalConvenience.dll') + '"'
$rsp += '/out:"' + (Join-Path $PSScriptRoot 'dist/UpdateSmoke.dll') + '"'
$rsp += '"' + (Join-Path $PSScriptRoot 'UpdateSmoke.cs') + '"'
$rsp | Set-Content (Join-Path $PSScriptRoot 'dist/smoke.rsp') -Encoding utf8
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /noconfig ('@' + (Join-Path $PSScriptRoot 'dist/smoke.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Smoke build failed' }
if (-not $VerifyApplied) {
    $oldFixture = Join-Path $PSScriptRoot 'dist/updater-old-fixture'
    New-Item -ItemType Directory -Force $oldFixture | Out-Null
    Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object { $_.Name -notlike '*Smoke.cs' } | Copy-Item -Destination $oldFixture
    Copy-Item (Join-Path $PSScriptRoot 'build.ps1') $oldFixture
    $oldPlugin = Join-Path $oldFixture 'PersonalConveniencePlugin.cs'
    $oldSource = Get-Content -LiteralPath $oldPlugin -Raw
    $currentVersion = [regex]::Match($oldSource, 'public const string Version = "([0-9.]+)"').Groups[1].Value
    if (-not $currentVersion) { throw 'Plugin version not found' }
    $oldSource.Replace($currentVersion,'1.0.2') | Set-Content -LiteralPath $oldPlugin -Encoding utf8
    & (Join-Path $oldFixture 'build.ps1')
    Copy-Item (Join-Path $oldFixture 'dist/SephiriaPersonalConvenience.dll') (Join-Path $runtime 'BepInEx/plugins')
}
Copy-Item (Join-Path $PSScriptRoot 'dist/UpdateSmoke.dll') (Join-Path $runtime 'BepInEx/plugins')
New-Item -ItemType Directory -Force (Join-Path $runtime 'BepInEx/patchers') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'dist/SephiriaPersonalConvenience.Updater.dll') (Join-Path $runtime 'BepInEx/patchers')
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $runtime 'Sephiria.exe'
$start.WorkingDirectory = $runtime
$start.UseShellExecute = $false
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
foreach ($arg in @('-batchmode','-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',(Join-Path $root 'update-unity.log'))) { $start.ArgumentList.Add($arg) }
$start.Environment['SEPHIRIA_COMBAT_TEST_ROOT'] = $root
$start.Environment['SEPHIRIA_COMBAT_TEST_MUTE'] = '1'
if ($VerifyApplied) { $start.Environment['UPDATER_EXPECT_LATEST'] = '1' }
if ($UiOnly) { $start.Environment['STAGE_UI_ONLY'] = '1' }
$process = [Diagnostics.Process]::Start($start)
@{ProcessId=$process.Id;Root=$root} | ConvertTo-Json | Set-Content (Join-Path $root 'update-process.json')
Write-Output "Started isolated bundled retry test PID=$($process.Id)"






