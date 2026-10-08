param([string]$Release, [string]$Zip)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION file.' }
if (-not $Release) { $Release = 'out/TKA-PC-Installer-v' + ($version -replace '\.', '') }
if (-not $Zip) { $Zip = 'out/Techno-Kitten-Adventure-XBLA-PC-Recomp-v' + $version + '.zip' }
Push-Location $projectRoot
try {
    $releaseRoot = [IO.Path]::GetFullPath($Release)
    $zipPath = [IO.Path]::GetFullPath($Zip)
    if (Test-Path -LiteralPath $zipPath) { throw 'ZIP already exists; choose a new filename.' }
    $manifest = Get-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.version -ne $version -or $manifest.assets_included -ne $false) { throw 'Release manifest disagrees with VERSION or includes game assets.' }
    $file = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($file, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($entry in $manifest.files) {
                $source = [IO.Path]::GetFullPath((Join-Path $releaseRoot $entry.path))
                if (-not $source.StartsWith($releaseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid release path.' }
                if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.sha256) { throw ('Release file changed: ' + $entry.path) }
                $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, $entry.path.Replace('\', '/'), [IO.Compression.CompressionLevel]::Optimal)
            }
            $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $releaseRoot 'release-manifest.json'), 'release-manifest.json')
        } finally { $archive.Dispose() }
    } finally { $file.Dispose() }
    # Only the build-time allowlist was archived. Test logs, imported games and
    # backups created beside an installer after its build cannot enter this ZIP.
    $check = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        if ($check.Entries.Count -ne $manifest.files.Count + 1 -or @($check.Entries | Where-Object { $_.FullName -match '(?i)^(Game|runtime|logs|backups)/|\.(xnb|wma|xwb|xsb|xgs|xenos|mgfxo)$|(^|/)(Helicopter\.(exe|dll)|GameInfo\.(xml|bin)|ScoreInfo)$' }).Count -gt 0) { throw 'Unexpected ZIP inventory.' }
    } finally { $check.Dispose() }
    Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
} finally { Pop-Location }
