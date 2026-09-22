param([string]$GameDir='C:\Program Files (x86)\Steam\steamapps\common\Sephiria')
$ErrorActionPreference='Stop'
$game=(Resolve-Path -LiteralPath $GameDir).Path
if (!(Test-Path -LiteralPath (Join-Path $game 'Sephiria.exe'))) { throw 'Sephiria.exe was not found.' }
if (Get-Process Sephiria -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $game 'Sephiria.exe') }) { throw 'Close Sephiria normally before removing the test modules.' }
$plugins=Join-Path $game 'BepInEx/plugins'
$backup=Join-Path $game ('BepInEx/mod-backups/RemovedCombatTestArena/'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
foreach ($name in @('SephiriaCombatTestArena.dll','SephiriaCombatTestEntry.dll')) {
    $source=Join-Path $plugins $name
    if (Test-Path -LiteralPath $source) {
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        $hash=(Get-FileHash -LiteralPath $source).Hash
        $destination=Join-Path $backup $name
        Move-Item -LiteralPath $source -Destination $destination
        if ((Get-FileHash -LiteralPath $destination).Hash -ne $hash -or (Test-Path -LiteralPath $source)) { throw "Removal verification failed: $name" }
        Write-Output "Removed $name (backup: $destination)"
    }
}
Write-Output 'Combat simulation entry and test arena are absent from this game install. Other mods and saves were not changed.'
