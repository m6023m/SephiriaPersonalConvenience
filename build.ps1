param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria')
$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$managed = Join-Path $GameDir 'Sephiria_Data/Managed'
$refs = Get-ChildItem -LiteralPath $managed -Filter '*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$refs += @('BepInEx.dll','0Harmony.dll') | ForEach-Object { '/reference:"' + (Join-Path $GameDir ('BepInEx/core/' + $_)) + '"' }
$rsp = @('/nologo','/nostdlib+','/target:library','/optimize+','/utf8output',('/out:"' + (Join-Path $dist 'SephiriaPersonalConvenience.dll') + '"')) + $refs + (Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object { $_.Name -notlike '*Smoke.cs' -and $_.Name -ne 'UpdaterPatcher.cs' } | ForEach-Object { '"' + $_.FullName + '"' })
$response = Join-Path $dist 'build.rsp'
$rsp | Set-Content -LiteralPath $response -Encoding utf8
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /noconfig ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'Dice preview build failed' }
Get-FileHash -LiteralPath (Join-Path $dist 'SephiriaPersonalConvenience.dll')





& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:library /optimize+ /utf8output /reference:System.Runtime.Serialization.dll ('/reference:' + (Join-Path $GameDir 'BepInEx/core/BepInEx.dll')) ('/reference:' + (Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll')) ('/out:' + (Join-Path $dist 'SephiriaPersonalConvenience.Updater.dll')) (Join-Path $PSScriptRoot 'UpdateCore.cs') (Join-Path $PSScriptRoot 'UpdaterPatcher.cs')
if ($LASTEXITCODE -ne 0) { throw 'Updater build failed' }
