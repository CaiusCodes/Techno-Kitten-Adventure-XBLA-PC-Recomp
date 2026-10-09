param(
    [string]$Zip = 'out/Techno-Kitten-Adventure-XBLA-PC-Recomp-v0.9.0.zip',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Fixture = 'embedded-launcher-check',
    [string]$PackagePath
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $projectRoot ('private/' + $Fixture)
if (Test-Path -LiteralPath $testRoot) { throw 'Use a new fixture name; existing installs are preserved.' }
$zipPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $Zip))
$package = if ($PackagePath) { [IO.Path]::GetFullPath($PackagePath) } else { Join-Path $projectRoot '../XBLA package Techno Kitten Adventure!/584E07D2/00000002/D1BDA9ABE3E4FABFF0DC2FC779D2AF61D975840C58' }
$sourceHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
function Run-Checked([string]$Exe, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Exe)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    # Deliberately start outside the installer folder to verify EXE-relative paths.
    $start.WorkingDirectory = $projectRoot
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw ('Process failed: ' + $Exe + ' (' + $process.ExitCode + ')') }
    $process.Dispose()
}
[IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $testRoot)
$zipRoot = Join-Path $testRoot 'Techno Kitten Adventure XBLA Recomp'
$zipEntries = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $names = @($zipEntries.Entries | ForEach-Object FullName)
    if (-not ($names -contains 'Techno Kitten Adventure XBLA Recomp/Setup Techno Kitten Adventure.exe') -or
        -not ($names -contains 'Techno Kitten Adventure XBLA Recomp/README.txt') -or
        @($names | Where-Object { $_ -notmatch '^Techno Kitten Adventure XBLA Recomp/(Setup Techno Kitten Adventure\.exe|README\.txt|licenses/[^/]+)$' }).Count -gt 0)
        { throw 'Unexpected initial ZIP layout.' }
} finally { $zipEntries.Dispose() }
$launcher = Join-Path $zipRoot 'Setup Techno Kitten Adventure.exe'
if (Test-Path -LiteralPath (Join-Path $zipRoot 'Game')) { throw 'Game must not be in the initial ZIP.' }
Run-Checked $launcher @('--install-here', $package)
$game = Join-Path $zipRoot 'Game'
if (!(Test-Path -LiteralPath (Join-Path $game 'Techno Kitten Adventure.exe')) -or
    (Test-Path -LiteralPath (Join-Path $zipRoot 'runtime')) -or
    (Test-Path -LiteralPath (Join-Path $zipRoot 'resources'))) { throw 'Incorrect install location.' }
$manifest = Get-Content -LiteralPath (Join-Path $game 'release-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.assets_included -or $manifest.format -ne 2) { throw 'Invalid installed runtime manifest.' }
foreach ($entry in $manifest.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $game $entry.path)).Hash -ne $entry.sha256) { throw ('Runtime hash mismatch: ' + $entry.path) }
}
Write-Output 'Fresh EXE-relative installation and runtime hashes passed. Checking gameplay and local saves.'
Run-Checked (Join-Path $game 'Techno Kitten Adventure.exe') @((Join-Path $game 'Helicopter.dll'), (Join-Path $game 'Content'), '31', '--fixed60', '--scripted', '--popaganda-test')
$gameLog = Get-ChildItem -LiteralPath (Join-Path $game 'logs') -Filter 'baseline-*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$logText = Get-Content -LiteralPath $gameLog.FullName -Raw
if ($logText -notmatch 'Game.Run returned normally' -or $logText -notmatch '-PLAY.png' -or $logText -notmatch 'Camera probe: effectIndex=0') { throw 'Popaganda gameplay check did not complete.' }
$save = @(Get-ChildItem -LiteralPath (Join-Path $game 'userdata') -Filter ScoreInfo -Recurse -File)
if ($save.Count -eq 0) { throw 'No local game save was created.' }
$sentinel = Join-Path $game 'userdata/portable-test.txt'
Set-Content -LiteralPath $sentinel -Value 'Preserve portable userdata on reinstall.'
$preserve = @($save + (Get-Item -LiteralPath $sentinel) | ForEach-Object {
    @{ Path = [IO.Path]::GetRelativePath($game, $_.FullName); Hash = (Get-FileHash -LiteralPath $_.FullName).Hash }
})
Run-Checked $launcher @('--install-here', $package)
$backups = @(Get-ChildItem -LiteralPath (Join-Path $zipRoot 'backups') -Directory)
if ($backups.Count -ne 1) { throw 'Expected one previous-install backup.' }
$backup = $backups[0]
foreach ($entry in $preserve) {
    foreach ($folder in @($game, $backup.FullName)) {
        if ((Get-FileHash -LiteralPath (Join-Path $folder $entry.Path)).Hash -ne $entry.Hash) { throw ('Userdata changed: ' + $entry.Path) }
    }
}
if ((Get-FileHash -LiteralPath $package).Hash -ne $sourceHash) { throw 'Original package changed.' }
if (@(Get-ChildItem -LiteralPath $zipRoot -Directory -Filter '.install-*').Count -ne 0) { throw 'Staging was not cleaned up.' }
@{
    zip_files_verified = $names.Count
    installed_runtime_files_verified = $manifest.files.Count
    installer_root = $zipRoot
    installation_beside_setup = $true
    game_folder_play_launcher = $true
    play_launcher_absent_before_install = $true
    different_working_directory = $true
    popaganda_normal_exit = $true
    local_save_paths = @($save | ForEach-Object { [IO.Path]::GetRelativePath($game, $_.FullName) })
    reinstall_preserved_userdata_and_backup = $true
    source_package_unchanged = $true
    zip_bytes = (Get-Item -LiteralPath $zipPath).Length
    zip_sha256 = (Get-FileHash -LiteralPath $zipPath).Hash
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot ('out/' + $Fixture + '-results.json'))
Write-Output 'Portable installer checks passed.'
