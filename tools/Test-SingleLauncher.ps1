param([string]$Release,[ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Fixture = 'single-launcher-check',[string]$PackagePath)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $project
try {
    if (!$Release) { throw 'Supply the portable build root.' }
    $template = [IO.Path]::GetFullPath($Release)
    if (Test-Path -LiteralPath (Join-Path $template 'Techno Kitten Adventure')) { $template = Join-Path $template 'Techno Kitten Adventure' }
    $testRoot = Join-Path $project ('private/'+$Fixture)
    if (Test-Path -LiteralPath $testRoot) { throw 'Use a fresh fixture name.' }
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    if (!$PackagePath) { $PackagePath = Join-Path $project '../XBLA package Techno Kitten Adventure!/584E07D2/00000002/D1BDA9ABE3E4FABFF0DC2FC779D2AF61D975840C58' }
    $packageHash = (Get-FileHash -LiteralPath $PackagePath).Hash
    $results = [ordered]@{}
    function Fixture([string]$name) { $path = Join-Path $testRoot $name; Copy-Item -LiteralPath $template -Destination $path -Recurse; return $path }
    function Run([string]$root,[string[]]$arguments,[int]$expected = 0) {
        $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $root 'Techno Kitten Adventure.exe'))
        $start.UseShellExecute = $false; $start.WorkingDirectory = $project
        foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::Start($start)
        try { if (!$process.WaitForExit(120000)) { $process.Kill($true); throw 'Bounded test timed out.' }; if ($process.ExitCode -ne $expected) { throw ('Unexpected exit '+$process.ExitCode+' for '+($arguments -join ' ')) } }
        finally { $process.Dispose() }
    }
    function UserHashes([string]$data) { $values = @{}; Get-ChildItem -LiteralPath (Join-Path $data 'userdata') -Recurse -File | ForEach-Object { $values[[IO.Path]::GetRelativePath($data,$_.FullName)] = (Get-FileHash -LiteralPath $_.FullName).Hash }; return $values }
    function SameUsers($before,[string]$data) { foreach ($entry in $before.GetEnumerator()) { if ((Get-FileHash -LiteralPath (Join-Path $data $entry.Key)).Hash -ne $entry.Value) { throw ('Changed userdata: '+$entry.Key) } } }
    $auto = Fixture 'auto'
    New-Item -ItemType Directory -Path (Join-Path $auto 'Game Package') | Out-Null
    Copy-Item -LiteralPath $PackagePath -Destination (Join-Path $auto 'Game Package/source-package')
    $invalid = Join-Path $auto 'unsupported-package'; $file = [IO.File]::Create($invalid); $file.SetLength(83353600); $file.Dispose()
    Run $auto @('--ui-auto-test')
    $results.auto_detect_and_wait_for_play = $true
    Run $auto @('--test-package-picker'); $results.browse_root_and_flags = $true
    Run $auto @('--install',$invalid,$auto) 1; $results.unsupported_rejected = $true
    $data = Join-Path $auto 'resources/data'
    New-Item -ItemType Directory -Path (Join-Path $data 'userdata/settings') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $data 'userdata/settings/display.json'),'{"Fullscreen":false,"Resolution":0,"InternalScale":2,"CustomSetting":"keep"}')
    [IO.File]::WriteAllText((Join-Path $data 'userdata/preservation.txt'),'Preserve this save/settings sentinel.')
    $before = UserHashes $data
    $dllHash = (Get-FileHash -LiteralPath (Join-Path $data 'Helicopter.dll')).Hash
    Run $auto @('--install',$PackagePath,$auto,'--test-cancel') 1
    SameUsers $before $data
    if ((Get-FileHash -LiteralPath (Join-Path $data 'Helicopter.dll')).Hash -ne $dllHash) { throw 'Cancelled import damaged working game.' }
    $results.cancellation_preserves_installation = $true
    Run $auto @('--install',$PackagePath,$auto,'--test-rollback') 1
    SameUsers $before $data
    $results.promotion_rollback = $true
    $lost = Join-Path $data 'Content/Effects/effect4.xnb'
    Move-Item -LiteralPath $lost -Destination (Join-Path $testRoot 'preserved-effect4.xnb')
    Run $auto @('--test-ready') 2
    Run $auto @('--install',$PackagePath,$auto)
    SameUsers $before $data
    $results.missing_file_reimport_preserves_userdata = $true
    $results.manual_setup = $true; Run $auto @('--setup','--ui-smoke')
    $setupLogsBefore = @(Get-ChildItem -LiteralPath (Join-Path $auto 'resources/logs') -File).Count
    # No package is copied into the relocated folder; launch must depend only on imported data.
    $relocated = Join-Path $testRoot 'relocated'
    New-Item -ItemType Directory -Path $relocated | Out-Null
    foreach ($name in @('Techno Kitten Adventure.exe','resources','README.txt','licenses')) { Copy-Item -LiteralPath (Join-Path $auto $name) -Destination $relocated -Recurse }
    Run $relocated @('--game-test','31','--fixed60','--scripted','--popaganda-test')
    $log = Get-ChildItem -LiteralPath (Join-Path $relocated 'resources/data/logs') -Filter 'baseline-*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $logText = Get-Content -LiteralPath $log.FullName -Raw
    if ($logText -notmatch 'Game.Run returned normally' -or $logText -notmatch '-PLAY.png' -or $logText -notmatch 'Camera probe: effectIndex=0') { throw 'Relocated Popaganda gameplay did not finish.' }
    if (@(Get-ChildItem -LiteralPath (Join-Path $relocated 'resources/logs') -File).Count -ne $setupLogsBefore) { throw 'Subsequent launch opened setup.' }
    $results.relocation_without_package_and_direct_gameplay = $true
    $beforePlayLogs = @(Get-ChildItem -LiteralPath (Join-Path $relocated 'resources/data/logs') -Filter 'baseline-*.log' | ForEach-Object Name)
    Run $relocated @('--ui-play-test')
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 200
        $latest = Get-ChildItem -LiteralPath (Join-Path $relocated 'resources/data/logs') -Filter 'baseline-*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $played = $latest.Name -notin $beforePlayLogs -and (Get-Content -LiteralPath $latest.FullName -Raw) -match 'Game.Run returned normally'
    } while (!$played -and [DateTime]::UtcNow -lt $deadline)
    if (!$played) { throw 'Play-button game launch did not finish.' }
    $settings = Get-Content -LiteralPath (Join-Path $relocated 'resources/data/userdata/settings/display.json') -Raw | ConvertFrom-Json
    if (!$settings.Fullscreen -or $settings.CustomSetting -ne 'keep' -or $settings.InternalScale -ne 2) { throw 'Play fullscreen changed unrelated settings.' }
    $results.play_button_and_fullscreen = $true
    $shortcut = Join-Path $testRoot 'test-shortcut.lnk'
    Run $relocated @('--test-extras',$shortcut)
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    if ($link.TargetPath -ne (Join-Path $relocated 'Techno Kitten Adventure.exe')) { throw 'Shortcut does not target root launcher.' }
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    $shortcutHash = (Get-FileHash -LiteralPath $shortcut).Hash
    Run $relocated @('--test-extras',$shortcut) 1
    if ((Get-FileHash -LiteralPath $shortcut).Hash -ne $shortcutHash) { throw 'Existing shortcut overwritten.' }
    $results.shortcut_targets_root_and_no_overwrite = $true
    $multi = Fixture 'multi'
    Copy-Item -LiteralPath $PackagePath -Destination (Join-Path $multi 'first-package')
    Copy-Item -LiteralPath $PackagePath -Destination (Join-Path $multi 'second-package')
    Run $multi @('--ui-choice-test'); $results.multiple_packages_require_choice = $true
    $drop = Fixture 'drop'
    $input = Join-Path $testRoot 'input'; New-Item -ItemType Directory -Path $input | Out-Null
    Copy-Item -LiteralPath $PackagePath -Destination (Join-Path $input 'package')
    Run $drop @('--ui-drop-test',$input); $results.folder_drop_through_actual_handlers = $true
    $migration = Fixture 'migration'
    $oldGame = Join-Path $migration 'Game'
    Copy-Item -LiteralPath $data -Destination $oldGame -Recurse
    $old = UserHashes $oldGame
    Run $migration @('--migrate')
    SameUsers $old (Join-Path $migration 'resources/data')
    SameUsers $old $oldGame
    if (!(Test-Path -LiteralPath (Join-Path $oldGame 'Helicopter.dll'))) { throw 'Old installation was removed.' }
    $results.migration_reuses_assets_preserves_old_installation = $true
    if ((Get-FileHash -LiteralPath $PackagePath).Hash -ne $packageHash) { throw 'Original input package changed.' }
    $results.original_package_unchanged = $true
    $results.hardware_dpi_and_real_drag_dialog = 'NOT TESTED: native dialog selection and physical cross-monitor DPI are manual; drop handlers and effective DPI are scripted.'
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $project ('out/'+$Fixture+'-results.json'))
    Write-Output 'Single-launcher integration checks passed.'
} finally { Pop-Location }
