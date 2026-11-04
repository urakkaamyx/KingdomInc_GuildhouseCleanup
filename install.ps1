param([string]$GameRoot = (Join-Path $PSScriptRoot '..\..'), [switch]$StageForNextLaunch)
$ErrorActionPreference = 'Stop'
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
if (!(Test-Path -LiteralPath (Join-Path $GameRoot 'KingdomInc.exe'))) { throw 'Choose the Kingdom Inc game directory.' }
$gameRunning = [bool](Get-Process -Name KingdomInc -ErrorAction SilentlyContinue)
if ($gameRunning -and !$StageForNextLaunch) { throw 'Close Kingdom Inc before installing GuildhouseCleanup, or use -StageForNextLaunch for a first installation.' }
$source = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\GuildhouseCleanup.dll'
if (!(Test-Path -LiteralPath $source)) { throw 'Build the Release project first.' }
$destination = Join-Path $GameRoot 'BepInEx\plugins\GuildhouseCleanup'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$target = Join-Path $destination 'GuildhouseCleanup.dll'
if (Test-Path -LiteralPath $target) {
    if ($gameRunning) { throw 'An installed DLL already exists. Close Kingdom Inc before replacing it.' }
    $backup = Join-Path $PSScriptRoot ('_Backup\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination $backup
}
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw 'Installed DLL verification failed.' }
Write-Output "Installed GuildhouseCleanup: $target"
if ($gameRunning) { Write-Output 'First installation staged. Restart Kingdom Inc to load GuildhouseCleanup.' }
