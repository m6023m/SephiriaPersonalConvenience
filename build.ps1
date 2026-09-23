param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria', [switch]$Force)
$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$managed = Join-Path $GameDir 'Sephiria_Data/Managed'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

function Invoke-CachedCompile([string]$Name, [string[]]$Arguments, [string[]]$Inputs, [string]$OutputFile) {
    $stateFile = Join-Path $dist ($Name + '.build-state.json')
    $responseFile = Join-Path $dist ($Name + '.rsp')
    $records = @($Arguments) + @(($Inputs + @($compiler, $PSCommandPath) | Sort-Object -Unique) | ForEach-Object {
        $_ + ':' + (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
    })
    $digest = [Security.Cryptography.SHA256]::Create()
    try { $signature = [BitConverter]::ToString($digest.ComputeHash([Text.Encoding]::UTF8.GetBytes(($records -join "`n")))).Replace('-', '') }
    finally { $digest.Dispose() }
    $old = $null
    if (Test-Path -LiteralPath $stateFile) { try { $old = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json } catch { $old = $null } }
    # Test compilation also consumes this file when the production DLL is reused.
    $Arguments | Set-Content -LiteralPath $responseFile -Encoding utf8
    # The DLL itself must still match the recorded build; a copied old DLL cannot be skipped.
    if (!$Force -and $old -and $old.signature -eq $signature -and (Test-Path -LiteralPath $OutputFile) -and (Get-FileHash -LiteralPath $OutputFile).Hash -eq $old.outputHash) {
        Write-Output "Unchanged inputs: reusing $Name"
        return
    }
    & $compiler /noconfig ('@' + $responseFile)
    if ($LASTEXITCODE -ne 0) { throw "$Name build failed" }
    @{ signature=$signature; outputHash=(Get-FileHash -LiteralPath $OutputFile).Hash; builtAt=(Get-Date).ToUniversalTime().ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath $stateFile -Encoding utf8
}

$referencePaths = @(Get-ChildItem -LiteralPath $managed -Filter '*.dll' | Sort-Object Name | ForEach-Object { $_.FullName })
$referencePaths += @('BepInEx.dll','0Harmony.dll') | ForEach-Object { Join-Path $GameDir ('BepInEx/core/' + $_) }
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object { $_.Name -notlike '*Smoke.cs' -and $_.Name -ne 'UpdaterPatcher.cs' } | Sort-Object Name | ForEach-Object { $_.FullName })
$thumbAsset = Join-Path $PSScriptRoot 'Assets/thumbs-up.png'
$thumbLicense = Join-Path $PSScriptRoot 'Assets/FONT-AWESOME-LICENSE.txt'
$output = Join-Path $dist 'SephiriaPersonalConvenience.dll'
$arguments = @('/nologo','/nostdlib+','/target:library','/optimize+','/utf8output',('/out:"' + $output + '"'),('/resource:"' + $thumbAsset + '",SephiriaPersonalConvenience.Assets.thumbs-up.png'),('/resource:"' + $thumbLicense + '",SephiriaPersonalConvenience.Assets.FONT-AWESOME-LICENSE.txt')) + @($referencePaths | ForEach-Object { '/reference:"' + $_ + '"' }) + @($sources | ForEach-Object { '"' + $_ + '"' })
# Keep the original response file name for the isolated test compiler.
Invoke-CachedCompile 'build' $arguments ($sources + $referencePaths + @($thumbAsset,$thumbLicense)) $output
Get-FileHash -LiteralPath $output

$updaterSources = @((Join-Path $PSScriptRoot 'UpdateCore.cs'), (Join-Path $PSScriptRoot 'UpdaterPatcher.cs'))
$updaterReferences = @((Join-Path $GameDir 'BepInEx/core/BepInEx.dll'), (Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll'))
$updaterOutput = Join-Path $dist 'SephiriaPersonalConvenience.Updater.dll'
$updaterArguments = @('/nologo','/target:library','/optimize+','/utf8output','/reference:System.dll','/reference:System.Xml.dll','/reference:System.Runtime.Serialization.dll',('/out:"' + $updaterOutput + '"')) + @($updaterReferences | ForEach-Object { '/reference:"' + $_ + '"' }) + @($updaterSources | ForEach-Object { '"' + $_ + '"' })
Invoke-CachedCompile 'updater' $updaterArguments ($updaterSources + $updaterReferences) $updaterOutput
