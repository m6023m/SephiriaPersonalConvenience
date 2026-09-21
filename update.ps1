param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria', [string]$Version, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$repo = 'm6023m/SephiriaPersonalConvenience'
if (-not $Version) { $Version = (Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers @{'User-Agent'='SephiriaConvenience-Updater'}).tag_name }
if ($Version -notmatch '^v\d+\.\d+\.\d+$') { throw 'Invalid release version' }
$base = "https://github.com/$repo/releases/download/$Version"
$manifest = Invoke-RestMethod -Uri "$base/update.json"
if ($manifest.schema -ne 1 -or $manifest.version -ne $Version.Substring(1) -or $manifest.filename -ne 'SephiriaPersonalConvenience.dll' -or $manifest.updaterFilename -ne 'SephiriaPersonalConvenience.Updater.dll') { throw 'Invalid manifest' }
$root = [IO.Path]::GetFullPath($GameDir)
if (-not (Test-Path -LiteralPath (Join-Path $root 'Sephiria.exe')) -or -not (Test-Path -LiteralPath (Join-Path $root 'BepInEx/plugins'))) { throw 'Specify a Sephiria installation with BepInEx using -GameDir' }
$cache = Join-Path ([IO.Path]::GetTempPath()) ('SephiriaConvenience-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $cache | Out-Null
$files = @(@{Name=$manifest.filename;Hash=$manifest.sha256;Size=$manifest.size;Folder='plugins'},@{Name=$manifest.updaterFilename;Hash=$manifest.updaterSha256;Size=$manifest.updaterSize;Folder='patchers'})
try {
    foreach ($file in $files) {
        if ($file.Hash -notmatch '^[a-fA-F0-9]{64}$' -or $file.Size -le 0 -or $file.Size -gt 16777216) { throw 'Invalid asset metadata' }
        $download = Join-Path $cache $file.Name
        Invoke-WebRequest -Uri "$base/$($file.Name)" -OutFile $download -UseBasicParsing
        if ((Get-Item -LiteralPath $download).Length -ne $file.Size -or (Get-FileHash -LiteralPath $download).Hash -ne $file.Hash) { throw 'Download integrity check failed' }
    }
    Write-Output "Verified $Version (plugin and updater)"
    if ($CheckOnly) { return }
    if (Get-Process Sephiria -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $root 'Sephiria.exe') }) { throw 'Close Sephiria normally before updating' }
    $backup = Join-Path $root ('BepInEx/mod-backups/PersonalConvenience/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    foreach ($file in $files) {
        $folder = Join-Path $root ('BepInEx/' + $file.Folder)
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        $target = Join-Path $folder $file.Name
        if (Test-Path -LiteralPath $target) {
            if ((Get-FileHash -LiteralPath $target).Hash -eq $file.Hash) { continue }
            New-Item -ItemType Directory -Force -Path $backup | Out-Null
            Copy-Item -LiteralPath $target -Destination (Join-Path $backup $file.Name)
        }
        Copy-Item -LiteralPath (Join-Path $cache $file.Name) -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target).Hash -ne $file.Hash) { throw 'Installed hash mismatch; previous files are in mod-backups/PersonalConvenience' }
    }
    Write-Output "Installed $Version. Start the game to load it."
}
finally {
    foreach ($file in $files) { $download = Join-Path $cache $file.Name; if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download } }
    Remove-Item -LiteralPath $cache
}
