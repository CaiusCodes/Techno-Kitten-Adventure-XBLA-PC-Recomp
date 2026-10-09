param([string]$Fixture = 'v110-mouse-targets-check')
$ErrorActionPreference = 'Stop'
if ($Fixture -notmatch '^v110-mouse-targets-[a-zA-Z0-9_-]+$') { throw 'Use a dedicated mouse-targets test fixture.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$game = Join-Path $projectRoot ('private/' + $Fixture + '/Techno Kitten Adventure XBLA Recomp/Game')
$start = [Diagnostics.ProcessStartInfo]::new((Join-Path $game 'Techno Kitten Adventure.exe'))
$start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $projectRoot
foreach ($argument in @((Join-Path $game 'Helicopter.dll'), (Join-Path $game 'Content'), '9', '--fixed60', '--scripted', '--pc-input-test', '--mouse-menu-test')) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Bounded mouse test timed out.' }
$latest = Get-ChildItem -LiteralPath (Join-Path $game 'logs') -Filter baseline-*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$log = Get-Content -LiteralPath $latest.FullName -Raw
if ($process.ExitCode -ne 0 -or $log -notmatch 'Mouse menu integration PASS' -or $log -notmatch 'Game.Run returned normally') {
    throw ('Mouse integration failed; see ' + $latest.FullName)
}
$log -split '\r?\n' | Where-Object { $_ -match 'Mouse .*PASS' }
