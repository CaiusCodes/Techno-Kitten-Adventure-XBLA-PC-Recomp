param([string]$Installer = 'out/TKA-PC-Installer-v103/resources/installer/Setup Techno Kitten Adventure.exe', [string]$Output = 'out/installer-dpi-check-v103')
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $project
try {
    $exe = [IO.Path]::GetFullPath($Installer)
    $destination = [IO.Path]::GetFullPath($Output)
    if (-not $destination.StartsWith((Join-Path $project 'out') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a local out preview folder.' }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $results = @()
    foreach ($dpi in @(96,120,144,168,192,240,288)) {
        foreach ($state in @('initial','complete')) {
            $file = Join-Path $destination "$dpi-$state.png"
            $start = [Diagnostics.ProcessStartInfo]::new($exe)
            $start.UseShellExecute = $false; $start.CreateNoWindow = $true
            foreach ($arg in @('--preview-dpi',[string]$dpi,$file)) { $start.ArgumentList.Add($arg) }
            if ($state -eq 'complete') { $start.ArgumentList.Add('complete') }
            $process = [Diagnostics.Process]::Start($start)
            if (-not $process.WaitForExit(15000)) { $process.Kill(); throw "Preview timed out: $dpi $state" }
            if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $file)) { throw "Preview failed: $dpi $state; see installer logs." }
            $results += @{ dpi=$dpi; percent=$dpi/96*100; state=$state; passed=$true; image=$file }
            Write-Output "PASS: $dpi DPI $state"
        }
    }
    $results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $destination 'results.json')
} finally { Pop-Location }
