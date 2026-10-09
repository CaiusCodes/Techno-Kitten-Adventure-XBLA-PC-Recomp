param([string]$Fixture = 'v090-controller-prompts-check')
$ErrorActionPreference = 'Stop'
if ($Fixture -notmatch '^v090-controller-prompts-[a-zA-Z0-9_-]+$') { throw 'Use a dedicated controller-prompt fixture.' }
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$game=Join-Path $project ('private/'+$Fixture+'/Techno Kitten Adventure XBLA Recomp/Game')
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $game 'Techno Kitten Adventure.exe'))
$start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.WorkingDirectory=$project
foreach($arg in @((Join-Path $game 'Helicopter.dll'),(Join-Path $game 'Content'),'6','--fixed60','--controller-prompts-test')) { $start.ArgumentList.Add($arg) }
$process=[Diagnostics.Process]::Start($start)
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Prompt test timed out.' }
$latest=Get-ChildItem -LiteralPath (Join-Path $game 'logs') -Filter baseline-*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$log=Get-Content -LiteralPath $latest.FullName -Raw
if ($process.ExitCode -ne 0 -or $log -notmatch 'Controller prompt integration PASS' -or $log -notmatch 'Game.Run returned normally') { throw ('Prompt test failed: '+$latest.FullName) }
$log -split '\r?\n' | Where-Object { $_ -match 'Controller prompt .*PASS' }
