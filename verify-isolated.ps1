$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$verification = Join-Path $PSScriptRoot 'Verification'
$runtime = Join-Path $verification ('Runtime-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
foreach ($name in @('KingdomInc.exe','UnityPlayer.dll','UnityCrashHandler64.exe','winhttp.dll','doorstop_config.ini','.doorstop_version')) {
    New-Item -ItemType HardLink -Path (Join-Path $runtime $name) -Value (Join-Path $gameRoot $name) | Out-Null
}
foreach ($name in @('KingdomInc_Data','MonoBleedingEdge','D3D12')) {
    New-Item -ItemType Junction -Path (Join-Path $runtime $name) -Value (Join-Path $gameRoot $name) | Out-Null
}
$bep = Join-Path $runtime 'BepInEx'
New-Item -ItemType Directory -Path (Join-Path $bep 'plugins\GuildhouseCleanup'),(Join-Path $bep 'config') -Force | Out-Null
New-Item -ItemType Junction -Path (Join-Path $bep 'core') -Value (Join-Path $gameRoot 'BepInEx\core') | Out-Null
Copy-Item -LiteralPath (Join-Path $gameRoot 'BepInEx\config\BepInEx.cfg') -Destination (Join-Path $bep 'config\BepInEx.cfg')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\GuildhouseCleanup.dll') -Destination (Join-Path $bep 'plugins\GuildhouseCleanup\GuildhouseCleanup.dll')
$logPath = Join-Path $runtime 'diagnostic.log'
$resultPath = Join-Path $runtime '_Mods\GuildhouseCleanup\Verification\runtime-tests.json'
$exe = Join-Path $runtime 'KingdomInc.exe'
$owned = Start-Process -FilePath $exe -WorkingDirectory $runtime -WindowStyle Hidden -ArgumentList @('-guildhouse-cleanup-smoke','-screen-width','1600','-screen-height','1000','-screen-fullscreen','0','-logFile',('"' + $logPath + '"')) -PassThru
try {
    $deadline = (Get-Date).AddSeconds(55)
    while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $resultPath)) { Start-Sleep -Milliseconds 500 }
    if (-not (Test-Path -LiteralPath $resultPath)) {
        if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 60 }
        throw "Diagnostic did not complete. See $logPath"
    }
    Copy-Item -LiteralPath $resultPath -Destination (Join-Path $verification 'runtime-tests.json') -Force
    Get-ChildItem -LiteralPath (Split-Path -Parent $resultPath) -File -Filter '*.png' | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $verification $_.Name) -Force
    }
    Copy-Item -LiteralPath $logPath -Destination (Join-Path $verification 'unity-isolated.log') -Force
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    $result | Select-Object passed,version,count,gameVersion,unityVersion,bepInExVersion,error | ConvertTo-Json
    if (-not $result.passed) { throw "Diagnostic failed. See $logPath" }
    $owned.WaitForExit(3000) | Out-Null
} finally {
    $owned.Refresh()
    if (-not $owned.HasExited) { Stop-Process -Id $owned.Id }
    foreach ($replacement in Get-CimInstance Win32_Process -Filter "Name='KingdomInc.exe'") {
        if ($replacement.ExecutablePath -eq $exe -and $replacement.CommandLine.Contains($logPath)) { Stop-Process -Id $replacement.ProcessId }
    }
}
