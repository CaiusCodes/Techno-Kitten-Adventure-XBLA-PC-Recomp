param([ValidateSet('display','reload','options')][string]$Scenario = 'display', [string]$Game = 'out/display-menu-test')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $projectRoot
try {
    $gameRoot = [IO.Path]::GetFullPath($Game)
    if (-not $gameRoot.StartsWith((Join-Path $projectRoot 'out/display-menu-test'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a dedicated display-menu-test fixture.' }
    $settings = Join-Path $gameRoot 'userdata/settings/display.json'
    if ($Scenario -eq 'display') {
        New-Item -ItemType Directory -Path (Split-Path $settings) -Force | Out-Null
        [IO.File]::WriteAllText($settings, '{"Fullscreen":false,"Resolution":0}')
    }
    $seconds = if ($Scenario -eq 'display') { 44 } elseif ($Scenario -eq 'reload') { 31 } else { 33 }
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $gameRoot 'Techno Kitten Adventure.exe'))
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $gameRoot
    foreach ($item in @((Join-Path $gameRoot 'Helicopter.dll'), (Join-Path $gameRoot 'Content'), [string]$seconds, '--scripted', '--fixed60')) { $start.ArgumentList.Add($item) }
    if ($Scenario -ne 'reload') { $start.ArgumentList.Add('--' + $Scenario + '-test') }
    $process = [Diagnostics.Process]::Start($start)
    $process.WaitForExit()
    $logFile = Get-ChildItem -LiteralPath (Join-Path $gameRoot 'logs') -Filter 'baseline-*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $log = Get-Content -LiteralPath $logFile.FullName -Raw
    if ($process.ExitCode -ne 0 -or $log -notmatch 'Game.Run returned normally.' -or $log -match 'Display change failed') { throw ('Failed game run; see ' + $logFile.FullName) }
    $saved = Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json
    if ($Scenario -eq 'display') {
        foreach ($mode in @('Windowed', 'Fullscreen')) {
            foreach ($size in @('1280 Y:720','1920 Y:1080','2560 Y:1440','3840 Y:2160')) {
                if (-not $log.Contains(('Display applied: ' + $mode + '; output {X:' + $size + '}'))) { throw ('Missing mode: ' + $mode + ' ' + $size) }
            }
        }
        if ($saved.Fullscreen -or $saved.Resolution -ne 1) { throw 'Display setting save mismatch.' }
    } elseif ($Scenario -eq 'reload') {
        if ($log -notmatch 'Display applied: Windowed; output \{X:1920 Y:1080\}' -or $log -notmatch 'State: PLAY' -or $log -notmatch 'State: PAUSE') { throw 'Reload/gameplay coverage failed.' }
    } else {
        if ($log -notmatch 'musicOn=False' -or $log -notmatch 'sfxOn=False' -or $log -notmatch 'vibrationOn=False' -or $log -notmatch 'State: CREDITS') { throw 'Original options coverage failed.' }
        $lastState = [regex]::Matches($log, 'State: (\w+)') | Select-Object -Last 1
        if ($lastState.Groups[1].Value -ne 'MAIN_MENU') { throw 'Original Back item failed.' }
    }
    @{ scenario = $Scenario; passed = $true; log = $logFile.FullName; saved = $saved; tested_utc = (Get-Date).ToUniversalTime().ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $gameRoot ($Scenario + '-test-results.json'))
    Write-Output ('PASS: ' + $Scenario + '; ' + $logFile.FullName)
} finally { Pop-Location }
