param([string]$SourceZip, [string]$ReleaseZip)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $project
try {
    $version = [IO.File]::ReadAllText((Join-Path $project 'VERSION')).Trim()
    if (!$SourceZip -or !$ReleaseZip) { throw 'Supply the exact source and player ZIPs being reviewed.' }
    function Git-Read { & git -c ('safe.directory=' + $project.Replace('\','/')) @args }
    [xml]$props = Get-Content Directory.Build.props -Raw
    if ($props.Project.PropertyGroup.Version -ne $version -or $props.Project.PropertyGroup.FileVersion -ne ($version+'.0')) { throw 'Version metadata mismatch.' }
    $findings = [Collections.Generic.List[string]]::new()
    $forbidden = '(?i)(^|/)(private|out|\.tools|Game|userdata|logs|backups|generated|bin|obj)/|\.(exe|dll|xex|xnb|xma|wma|xwb|xsb|xgs|stfs|live|con|pirs|mgfxo|xenos|dmp)$|(^|/)(Helicopter\.[^/]+|GameInfo\.(xml|bin)|ScoreInfo)$'
    $sensitive = '(?i)C:[/\\]Users[/\\]|-----BEGIN (RSA |OPENSSH |EC )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|AKIA[A-Z0-9]{16}'
    $source = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($SourceZip))
    try {
        $entry = $source.GetEntry('source-manifest.json')
        if (!$entry) { throw 'Missing source manifest.' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($manifest.version -ne $version -or $manifest.assets_included) { throw 'Source manifest mismatch.' }
        foreach ($file in $manifest.files) {
            if ($file.path -match $forbidden) { $findings.Add('Forbidden source path: '+$file.path) }
            if ($file.path -in @('tools/Build-Baseline.ps1','tools/Extract-Baseline.ps1','tools/Test-DefaultDisplay.ps1')) { $findings.Add('Local development script: '+$file.path) }
            if ((Get-FileHash -LiteralPath $file.path).Hash -ne $file.sha256) { $findings.Add('Source archive differs from workspace: '+$file.path) }
            if ([IO.Path]::GetExtension($file.path) -notin @('.ico','.png')) {
                if ([IO.File]::ReadAllText((Join-Path $project $file.path)) -match $sensitive) { $findings.Add('Sensitive text pattern: '+$file.path) }
            }
        }
        if ($source.Entries.Count -ne $manifest.files.Count+1) { $findings.Add('Unexpected source ZIP entries.') }
    } finally { $source.Dispose() }
    $release = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ReleaseZip))
    try {
        foreach ($entry in $release.Entries) {
            if ($entry.FullName -notmatch '^Techno Kitten Adventure XBLA Recomp/(Setup Techno Kitten Adventure\.exe|README\.txt|licenses/[^/]+)$') { $findings.Add('Unexpected release entry: '+$entry.FullName) }
        }
        $notice = $release.GetEntry('Techno Kitten Adventure XBLA Recomp/README.txt')
        if (!$notice) { throw 'Missing release README.' }
        $reader = [IO.StreamReader]::new($notice.Open())
        try { if (!$reader.ReadToEnd().Contains('PC V'+$version)) { $findings.Add('Release README version mismatch.') } } finally { $reader.Dispose() }
        $releaseCount = $release.Entries.Count
    } finally { $release.Dispose() }
    $commits = @(Git-Read rev-list --all)
    $historyPaths = @(Git-Read rev-list --objects --all | ForEach-Object { if ($_ -match '^[a-f0-9]+ (.+)$') { $Matches[1] } } | Sort-Object -Unique)
    foreach ($path in $historyPaths) { if ($path -match $forbidden) { $findings.Add('Forbidden reachable-history path: '+$path) } }
    foreach ($commit in $commits) {
        $matches = @(Git-Read grep -I -l -E 'C:[/\\]Users[/\\]|-----BEGIN (RSA |OPENSSH |EC )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|AKIA[A-Z0-9]{16}' $commit)
        if ($LASTEXITCODE -gt 1) { throw 'History content scan failed.' }
        foreach ($match in $matches) { $findings.Add('Sensitive reachable-history text: '+$match) }
    }
    foreach ($path in @('out/audit-test.txt','private/test.xex','.tools/test.dll','Game/Helicopter.dll','tools/Build-Baseline.ps1','tools/Extract-Baseline.ps1','tools/Test-DefaultDisplay.ps1')) {
        $null = Git-Read check-ignore --no-index $path
        if ($LASTEXITCODE -ne 0) { $findings.Add('Ignore rule missing: '+$path) }
    }
    $png = [IO.File]::ReadAllBytes((Join-Path $project 'docs/social-preview.png'))
    function BigEndian([int]$at) { [int]$png[$at]*16777216+[int]$png[$at+1]*65536+[int]$png[$at+2]*256+[int]$png[$at+3] }
    if ((BigEndian 16) -ne 1280 -or (BigEndian 20) -ne 640) { $findings.Add('Social preview dimensions mismatch.') }
    $diff = @(Git-Read diff --check 2>$null)
    if ($LASTEXITCODE -ne 0) { $findings.Add('Git whitespace check failed.') }
    $tracked = @(Git-Read ls-files)
    foreach ($path in $tracked) { if ($path -match $forbidden) { $findings.Add('Forbidden tracked path: '+$path) } }
    $report = @{ version=$version; checked_utc=[DateTime]::UtcNow.ToString('O'); source_files=$manifest.files.Count;
        source_sha256=(Get-FileHash -LiteralPath $SourceZip).Hash; release_sha256=(Get-FileHash -LiteralPath $ReleaseZip).Hash;
        release_entries=$releaseCount; reachable_commits=$commits.Count; history_paths=$historyPaths.Count;
        social_preview='1280x640'; findings=@($findings); passed=($findings.Count -eq 0);
        limits=@('Pattern scans do not establish rights or detect every possible secret.','Unreachable Git objects and reflogs were not audited.','No clean-machine or fresh-checkout build was performed by this audit.') }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath out/publication-prep-audit.json
    if ($findings.Count) { throw ($findings -join [Environment]::NewLine) }
    Write-Output ('PASS: source '+$manifest.files.Count+' files; release '+$releaseCount+' entries; '+$commits.Count+' reachable commit(s); source/history pattern scans; ignore rules; version; social preview dimensions.')
} finally { Pop-Location }
