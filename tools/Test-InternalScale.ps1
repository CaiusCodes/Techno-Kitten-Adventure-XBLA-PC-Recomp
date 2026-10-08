param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Fixture = 'internal-2x-final-check')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $projectRoot ('private/' + $Fixture)
$game = Join-Path $root 'Game'
$settings = Join-Path $game 'userdata/settings/display.json'
if (Test-Path -LiteralPath $settings) { throw 'Use a fresh private fixture with no display settings.' }
New-Item -ItemType Directory -Force -Path (Split-Path $settings) | Out-Null
$results = @()
try {
    foreach ($case in @(
        @{ name = 'default internal 2x, window 720p'; json = $null; stage = '2560x1440'; output = '1280x720' },
        @{ name = 'old saved 1080p retains internal 2x'; json = '{"Fullscreen":false,"Resolution":1}'; stage = '2560x1440'; output = '1920x1080' },
        @{ name = 'explicit internal 1x baseline'; json = '{"Fullscreen":false,"Resolution":0,"InternalScale":1}'; stage = '1280x720'; output = '1280x720' },
        @{ name = 'invalid internal scale defaults safely'; json = '{"Fullscreen":false,"Resolution":0,"InternalScale":99}'; stage = '2560x1440'; output = '1280x720' }
    )) {
        if ($case.json) { Set-Content -LiteralPath $settings -Value $case.json }
        $hashBefore = if ($case.json) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
        $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $root 'Techno Kitten Adventure.exe'))
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $projectRoot
        foreach ($argument in @((Join-Path $game 'Helicopter.dll'), (Join-Path $game 'Content'), '0', '--internal-render-test')) { $start.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::Start($start); $process.WaitForExit()
        $logFile = Get-ChildItem -LiteralPath (Join-Path $game 'logs') -File -Filter 'baseline-*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $log = Get-Content -LiteralPath $logFile.FullName -Raw
        if ($process.ExitCode -ne 0 -or -not $log.Contains('Internal stage created: ' + $case.stage) -or
            -not $log.Contains('backbuffer ' + $case.output) -or $log -notmatch 'Internal render GPU checks passed' -or $log -notmatch 'Game.Run returned normally') { throw ('Failed: ' + $case.name + '; see ' + $logFile.FullName) }
        if ($case.json -and (Get-FileHash -LiteralPath $settings).Hash -ne $hashBefore) { throw 'Loading settings unexpectedly overwrote them.' }
        if (-not $case.json -and (Test-Path -LiteralPath $settings)) { throw 'First launch unexpectedly wrote settings.' }
        $results += @{ case = $case.name; passed = $true; stage = $case.stage; output = $case.output; log = $logFile.FullName }
        Write-Output ('PASS: ' + $case.name)
    }
} finally {
    # This file was created above exclusively in the private regression fixture.
    if (Test-Path -LiteralPath $settings) { Remove-Item -LiteralPath $settings }
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $projectRoot 'out/internal-scale-results.json')
