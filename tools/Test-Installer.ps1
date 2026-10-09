param([string]$Release = 'out/TKA-PC-Installer-v103',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$FixtureName = 'embedded-launcher-check',
    [string]$FixturePath,
    [switch]$Legacy,
    [switch]$LegacyName,
    [string]$PackagePath)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $projectRoot
try {
    $installer = [IO.Path]::GetFullPath((Join-Path $Release 'Setup Techno Kitten Adventure.exe'))
    $package = [IO.Path]::GetFullPath($(if ($PackagePath) { $PackagePath } else { '../XBLA package Techno Kitten Adventure!/584E07D2/00000002/D1BDA9ABE3E4FABFF0DC2FC779D2AF61D975840C58' }))
    $fixture = if ($FixturePath) { [IO.Path]::GetFullPath($FixturePath) } else { Join-Path $projectRoot ('private/' + $FixtureName) }
    if (-not $fixture.StartsWith((Join-Path $projectRoot 'private') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
        { throw 'Installer fault fixture must stay in the local private folder.' }
    $game = Join-Path $fixture $(if ($Legacy) { 'runtime' } else { 'Game' })
    if (-not (Test-Path -LiteralPath (Join-Path $game 'tka-install.json'))) { throw 'Create the private installer fixture first. Do not point this test at a personal installation.' }
    function Invoke-InstallerTest([string]$InputFile, [string]$Fault = '') {
        $start = [Diagnostics.ProcessStartInfo]::new($installer)
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true
        foreach ($item in @('--install', $InputFile, $fixture)) { $start.ArgumentList.Add($item) }
        if ($Fault) { $start.ArgumentList.Add($Fault) }
        $process = [Diagnostics.Process]::Start($start)
        $process.WaitForExit()
        return $process.ExitCode
    }
    function Get-TreeFingerprint([string]$Path) {
        $records = Get-ChildItem -LiteralPath $Path -Recurse -File | Sort-Object FullName | ForEach-Object {
            [IO.Path]::GetRelativePath($Path, $_.FullName) + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
        return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($records -join "`n"))))
    }
    $packageBefore = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    $sentinel = Join-Path $game 'userdata/import-preservation-check.txt'
    [IO.File]::WriteAllText($sentinel, 'Installer regression fixture; must survive all import outcomes.')
    $savePath = Join-Path $game 'userdata/profile/Techno Kitten Adventure/ScoreInfo'
    $saveBefore = (Get-FileHash -LiteralPath $savePath -Algorithm SHA256).Hash
    $treeBefore = Get-TreeFingerprint $game
    $rootLauncher = Join-Path $fixture $(if ($LegacyName) { 'TechnoKittenAdventure.exe' } else { 'Techno Kitten Adventure.exe' })
    $launcherExisted = Test-Path -LiteralPath $rootLauncher
    $launcherBefore = if ($launcherExisted) { (Get-FileHash -LiteralPath $rootLauncher).Hash } else { $null }
    $results = [Collections.Generic.List[object]]::new()
    # An unrelated file at either new or old name must never be overwritten.
    $conflict = Join-Path $fixture $(if ($LegacyName) { 'Techno Kitten Adventure.exe' } else { 'TechnoKittenAdventure.exe' })
    if (Test-Path -LiteralPath $conflict) { throw 'Expected unused conflict-test filename.' }
    [IO.File]::WriteAllText($conflict, 'Unrelated file; preserve unchanged.')
    $conflictHash = (Get-FileHash -LiteralPath $conflict).Hash
    try {
        $code = Invoke-InstallerTest $package
        if ($code -ne 1 -or (Get-TreeFingerprint $game) -ne $treeBefore -or (Get-FileHash -LiteralPath $conflict).Hash -ne $conflictHash) { throw 'FAILED: unrelated launcher conflict' }
        $results.Add(@{ test = 'unrelated launcher conflict'; exit = $code; previous_game_unchanged = $true; unrelated_file_unchanged = $true })
        Write-Output 'PASS: unrelated launcher conflict'
    } finally {
        if ((Get-FileHash -LiteralPath $conflict).Hash -eq $conflictHash) { Remove-Item -LiteralPath $conflict }
    }
    $bad = Join-Path $fixture 'damaged-package-test.bin'
    $badBytes = [IO.File]::ReadAllBytes($package); $badBytes[1000] = $badBytes[1000] -bxor 1
    [IO.File]::WriteAllBytes($bad, $badBytes)
    foreach ($test in @(@('damaged package', $bad, ''), @('cancel during conversion', $package, '--test-cancel'), @('promotion rollback', $package, '--test-rollback'), @('launcher promotion rollback', $package, '--test-launcher-rollback'))) {
        $code = Invoke-InstallerTest $test[1] $test[2]
        $preserved = (Get-TreeFingerprint $game) -eq $treeBefore
        if ($launcherExisted -and (Get-FileHash -LiteralPath $rootLauncher).Hash -ne $launcherBefore) { throw 'Failed install changed the previous launcher.' }
        $stagingCleared = @(Get-ChildItem -LiteralPath $fixture -Directory -Force | Where-Object { $_.Name -like '.install-*' }).Count -eq 0
        if ($code -ne 1 -or -not $preserved -or -not $stagingCleared -or
            (Test-Path -LiteralPath $rootLauncher) -ne $launcherExisted -or
            ($Legacy -and (Test-Path -LiteralPath (Join-Path $fixture 'Game'))) -or
            ($LegacyName -and (Test-Path -LiteralPath (Join-Path $fixture 'Techno Kitten Adventure.exe')))) { throw ('FAILED: ' + $test[0]) }
        $results.Add(@{ test = $test[0]; exit = $code; previous_game_unchanged = $preserved; staging_cleared = $stagingCleared })
        Write-Output ('PASS: ' + $test[0])
    }
    $code = Invoke-InstallerTest $package
    $game = Join-Path $fixture 'Game'
    $savePath = Join-Path $game 'userdata/profile/Techno Kitten Adventure/ScoreInfo'
    $sentinel = Join-Path $game 'userdata/import-preservation-check.txt'
    $savePreserved = (Get-FileHash -LiteralPath $savePath -Algorithm SHA256).Hash -eq $saveBefore
    $backup = Get-ChildItem -LiteralPath (Join-Path $fixture 'backups') -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $backupPreserved = $backup -and (Get-TreeFingerprint $backup.FullName) -eq $treeBefore
    if ($code -ne 0 -or -not $savePreserved -or -not (Test-Path -LiteralPath $sentinel) -or -not $backupPreserved -or
        -not (Test-Path -LiteralPath (Join-Path $game 'Techno Kitten Adventure.exe')) -or
        (Test-Path -LiteralPath $rootLauncher) -or
        ($LegacyName -and (Test-Path -LiteralPath $rootLauncher)) -or ($Legacy -and (Test-Path -LiteralPath (Join-Path $fixture 'runtime')))) { throw 'FAILED: reinstall preservation' }
    $results.Add(@{ test = 'successful reinstall'; exit = $code; save_preserved = $savePreserved; previous_game_backup_unchanged = [bool]$backupPreserved })
    if ($launcherExisted -and @((Get-ChildItem -LiteralPath (Join-Path $fixture 'backups') -File -Filter 'Launcher-*.exe') | Where-Object { (Get-FileHash -LiteralPath $_.FullName).Hash -eq $launcherBefore }).Count -eq 0) { throw 'Previous launcher backup missing.' }
    if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $packageBefore) { throw 'Original package changed.' }
    @{ tested_utc = (Get-Date).ToUniversalTime().ToString('O'); legacy_migration = [bool]$Legacy; filename_migration = [bool]$LegacyName; source_package_unchanged = $true; tests = @($results.ToArray()) } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fixture 'installer-test-results.json')
    Write-Output 'PASS: reinstall, backup, save preservation and original package integrity'
} finally { Pop-Location }
