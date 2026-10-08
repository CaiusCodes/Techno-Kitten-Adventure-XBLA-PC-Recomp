param([string]$Name)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = [IO.File]::ReadAllText((Join-Path $root 'VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION file.' }
if (-not $Name) { $Name = 'Techno Kitten Adventure Source - V' + $version }
if ($Name -notmatch '^[a-zA-Z0-9][a-zA-Z0-9 .-]*$') { throw 'Use a plain archive name.' }
$destination = Join-Path $root ('out/' + $Name)
$zip = $destination + '.zip'
if ((Test-Path -LiteralPath $destination) -or (Test-Path -LiteralPath $zip)) { throw 'Choose a fresh source output name.' }
$files = [Collections.Generic.List[string]]::new()
foreach ($path in @('.gitignore','.gitattributes','VERSION','Directory.Build.props','global.json','NuGet.Config','README.md','LICENSE','THIRD_PARTY.md','RELEASE_NOTES.md',
    'docs/BUILD-V1.md','docs/V1.md','docs/DPI-VSYNC.md','docs/ICON.md','.github/ISSUE_TEMPLATE/bug_report.md','packaging/README.txt',
    'src/Tka.Installer/Assets/techno-kitty.svg','src/Tka.Installer/Assets/techno-kitty.ico','src/InstallerLauncher/setup-version.rc.in','tools/Build-Icon.ps1',
    'src/Tka.Host/Assets/game-kitty.svg','src/Tka.Host/Assets/game-kitty.ico','docs/BUILD-BADGE.md',
    'docs/social-preview.svg','docs/social-preview.png','.github/FUNDING.yml','tools/Render-SocialPreview.py',
    'tools/dotnet.ps1','tools/Build-Installer.ps1','tools/Package-Installer.ps1','tools/Package-Source.ps1',
    'tools/Test-PortableInstaller.ps1','tools/Test-Installer.ps1','tools/Test-InstallerDpi.ps1','tools/Test-BuildPresentation.ps1','tools/Test-DisplayAspect.ps1','tools/Test-DisplayMenu.ps1','tools/Test-InternalScale.ps1')) { $files.Add($path) }
foreach ($directory in @('src','tools/Tka.AssemblyTool','tools/Tka.AssetTool','packaging/licenses')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root $directory) -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\','/')
        if ($relative -match '/(bin|obj)/') { continue }
        if ($file.Extension -in @('.cs','.csproj','.cpp','.h','.rc') -or $file.Name -in @('CMakeLists.txt','packages.lock.json') -or
            ($directory -eq 'packaging/licenses' -and $file.Extension -eq '.txt')) { $files.Add($relative) }
    }
}
$manifest = @(foreach ($relative in $files | Sort-Object -Unique) {
    $source = Join-Path $root $relative
    if ((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Source link rejected.' }
    if ($relative -notin @('src/Tka.Installer/Assets/techno-kitty.ico', 'src/Tka.Host/Assets/game-kitty.ico', 'docs/social-preview.png')) {
        $content = [IO.File]::ReadAllText($source)
        if ($content.Contains([char]0) -or $content -match '(?i)C:[/\\]Users[/\\]|-----BEGIN (RSA |OPENSSH |EC )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}') { throw ('Unexpected private/binary data: ' + $relative) }
    }
    @{ path = $relative; sha256 = (Get-FileHash -LiteralPath $source).Hash; bytes = (Get-Item -LiteralPath $source).Length }
})
New-Item -ItemType Directory -Path $destination | Out-Null
foreach ($entry in $manifest) {
    $target = Join-Path $destination $entry.path
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $entry.path) -Destination $target
}
@{ version = $version; port_code_license = 'MIT'; files = $manifest; assets_included = $false } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'source-manifest.json')
[IO.Compression.ZipFile]::CreateFromDirectory($destination, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    if ($archive.Entries.Count -ne $manifest.Count + 1) { throw 'Source archive inventory mismatch.' }
    foreach ($entry in $manifest) {
        $stream = $archive.GetEntry($entry.path).Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
        if ($hash -ne $entry.sha256) { throw ('Source archive hash mismatch: ' + $entry.path) }
    }
} finally { $archive.Dispose() }
Write-Output ('Verified asset-free source files: ' + $manifest.Count)
Get-FileHash -LiteralPath $zip
