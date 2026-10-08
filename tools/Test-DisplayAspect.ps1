param([string]$Game = 'private/aspect-fixed-check/Game')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $projectRoot
try {
    $root = [IO.Path]::GetFullPath($Game)
    if (-not $root.StartsWith((Join-Path $projectRoot 'private/aspect-'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a dedicated private/aspect- fixture.' }
    $settings = Join-Path $root 'userdata/settings/display.json'
    $previous = if (Test-Path -LiteralPath $settings) { [IO.File]::ReadAllBytes($settings) } else { $null }
    New-Item -ItemType Directory -Path (Split-Path $settings) -Force | Out-Null
    try {
        [IO.File]::WriteAllText($settings, '{"Fullscreen":false,"Resolution":0,"InternalScale":2}')
        $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $root 'Techno Kitten Adventure.exe'))
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $root
        foreach ($arg in @((Join-Path $root 'Helicopter.dll'), (Join-Path $root 'Content'), '30', '--fixed60', '--display-aspect-test')) { $start.ArgumentList.Add($arg) }
        $began = Get-Date
        $process = [Diagnostics.Process]::Start($start)
        $process.WaitForExit()
        $resultFile = Get-Item -LiteralPath (Join-Path $root 'logs/display-aspect-results.json')
        $result = Get-Content -LiteralPath $resultFile.FullName -Raw | ConvertFrom-Json
        if ($process.ExitCode -ne 0 -or $resultFile.LastWriteTime -lt $began -or -not $result.passed -or $result.cases.Count -ne 8) { throw 'Display aspect regression failed.' }
        Write-Output ('PASS: all eight display modes retain 16:9 and a square remains square. ' + $resultFile.FullName)
    } finally {
        if ($null -ne $previous) { [IO.File]::WriteAllBytes($settings, $previous) }
        elseif (Test-Path -LiteralPath $settings) { Remove-Item -LiteralPath $settings }
    }
} finally { Pop-Location }
