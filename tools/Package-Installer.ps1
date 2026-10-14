param([string]$Release,[string]$Zip)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
if (-not $Release) { throw 'Supply the fresh portable build directory.' }
if (-not $Zip) { $Zip = 'out/Techno-Kitten-Adventure-XBLA-PC-Recomp-v' + $version + '.zip' }
Push-Location $projectRoot
try {
    $root = [IO.Path]::GetFullPath($Release)
    if (Test-Path -LiteralPath (Join-Path $root 'Techno Kitten Adventure')) { $root = Join-Path $root 'Techno Kitten Adventure' }
    $zipPath = [IO.Path]::GetFullPath($Zip)
    if (Test-Path -LiteralPath $zipPath) { throw 'Choose a new ZIP filename; existing releases are preserved.' }
    if (-not (Test-Path -LiteralPath (Join-Path $root 'Techno Kitten Adventure.exe'))) { throw 'Portable launcher missing.' }
    $files = @(Get-ChildItem -LiteralPath $root -Recurse -File)
    foreach ($file in $files) {
        $name = [IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $name -notmatch '^(Techno Kitten Adventure\.exe|README\.txt|licenses/[^/]+|resources/(game|setup|compiler|translator)/.+|resources/release-manifest\.json)$' -or
            $name -match '(?i)\.(xnb|wma|xwb|xsb|xgs|xex|live|stfs|con|pirs|xenos|mgfxo|pdb|log|zip)$|(^|/)(Helicopter\.(exe|dll)|GameInfo\.(xml|bin)|ScoreInfo)$') { throw ('Forbidden release entry: '+$name) }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $root 'resources/release-manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.version -ne $version -or $manifest.assets_included -or $manifest.format -ne 3) { throw 'Release manifest/version mismatch.' }
    foreach ($entry in $manifest.files) { if ((Get-FileHash -LiteralPath (Join-Path $root $entry.path)).Hash -ne $entry.sha256) { throw ('Runtime hash mismatch: '+$entry.path) } }
    $stream = [IO.File]::Open($zipPath,[IO.FileMode]::CreateNew)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
        try { foreach ($file in $files) { $name = [IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/'); $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,'Techno Kitten Adventure/'+$name,[IO.Compression.CompressionLevel]::Optimal) } }
        finally { $archive.Dispose() }
    } finally { $stream.Dispose() }
    $check = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        if ($check.Entries.Count -ne $files.Count) { throw 'ZIP inventory mismatch.' }
        foreach ($entry in $check.Entries) {
            $relative = $entry.FullName.Substring('Techno Kitten Adventure/'.Length)
            $inputStream = $entry.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputStream)) }
            finally { $inputStream.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $root $relative)).Hash) { throw ('ZIP hash mismatch: '+$relative) }
        }
    } finally { $check.Dispose() }
    $digest = (Get-FileHash -LiteralPath $zipPath).Hash
    [IO.File]::WriteAllText(($zipPath+'.sha256'),$digest+'  '+[IO.Path]::GetFileName($zipPath)+[Environment]::NewLine)
    Write-Output ('Verified portable ZIP: '+$files.Count+' files; '+(Get-Item -LiteralPath $zipPath).Length+' bytes; SHA-256 '+$digest)
} finally { Pop-Location }
