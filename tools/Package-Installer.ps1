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
    $folder = 'Techno Kitten Adventure XBLA Recomp/'
    $files = @('Setup Techno Kitten Adventure.exe', 'README.txt') +
        @(Get-ChildItem -LiteralPath (Join-Path $releaseRoot 'licenses') -Recurse -File | ForEach-Object {
            [IO.Path]::GetRelativePath($releaseRoot, $_.FullName).Replace('\','/')
        })
    foreach ($name in $files) {
        if (-not (Test-Path -LiteralPath (Join-Path $releaseRoot $name) -PathType Leaf)) { throw ('Missing release file: ' + $name) }
    }
    if (-not (Get-Content -LiteralPath (Join-Path $releaseRoot 'README.txt') -Raw).Contains('PC V' + $version))
        { throw 'Packaged README disagrees with VERSION.' }
    $file = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($file, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($name in $files) {
                $source = [IO.Path]::GetFullPath((Join-Path $releaseRoot $name))
                if (-not $source.StartsWith($releaseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid release path.' }
                $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, $folder + $name, [IO.Compression.CompressionLevel]::Optimal)
            }
        } finally { $archive.Dispose() }
    } finally { $file.Dispose() }
    # Only Setup, README and notices are archived. Runtime files and the
    # manifest are embedded in Setup and extracted into Game after import.
    $check = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        if ($check.Entries.Count -ne $files.Count -or
            @($check.Entries | Where-Object { $_.FullName -notlike ($folder + '*') -or $_.FullName -match '(?i)\.(xnb|wma|xwb|xsb|xgs|xenos|mgfxo)$|(^|/)(Helicopter\.(exe|dll)|GameInfo\.(xml|bin)|ScoreInfo)$' }).Count -gt 0)
            { throw 'Unexpected ZIP inventory.' }
    } finally { $check.Dispose() }
    Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
} finally { Pop-Location }
