param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Name = ('TKA-Setup-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = [IO.File]::ReadAllText((Join-Path $projectRoot 'VERSION')).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION file.' }
[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw
if ($buildProperties.Project.PropertyGroup.Version -ne $version -or
    $buildProperties.Project.PropertyGroup.FileVersion -ne ($version + '.0') -or
    -not [IO.File]::ReadAllText((Join-Path $projectRoot 'packaging/README.txt')).Contains('PC V' + $version)) {
    throw 'VERSION, .NET assembly version and packaged README disagree.'
}
Push-Location $projectRoot
try {
    $release = Join-Path $projectRoot ('out/' + $Name)
    if (Test-Path -LiteralPath $release) { throw 'Choose a new release name; existing output is preserved.' }
    $resources = Join-Path $release 'resources'
    New-Item -ItemType Directory -Path $resources | Out-Null
    function Invoke-LocalDotnet {
        & "$PSScriptRoot/dotnet.ps1" @args
        if ($LASTEXITCODE -ne 0) { throw ('Build tool failed: ' + $LASTEXITCODE) }
    }
    # Compile the play launcher first so the managed installer embeds this build.
    & "$PSScriptRoot/Build-Icon.ps1"
    cmake -S src/InstallerLauncher -B out/installer-launcher-build -G 'Visual Studio 17 2022' -A x64
    if ($LASTEXITCODE -ne 0) { throw 'Launcher configuration failed.' }
    cmake --build out/installer-launcher-build --config Release
    if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed.' }
    Invoke-LocalDotnet publish src/Tka.Installer -c Release -r win-x64 --self-contained true '-p:RestoreLockedMode=true' '-p:DebugType=None' '-p:DebugSymbols=false' -o (Join-Path $resources 'installer')
    Invoke-LocalDotnet publish src/Tka.Host -c Release -r win-x64 --self-contained true '-p:RestoreLockedMode=true' '-p:DebugType=None' '-p:DebugSymbols=false' -o (Join-Path $resources 'game')
    Invoke-LocalDotnet (Join-Path $resources 'installer/Tka.AssemblyTool.dll') --runtime (Join-Path $resources 'installer/MonoGame.Framework.dll') (Join-Path $resources 'game/MonoGame.Framework.dll')
    # Preserve both runtime layouts when installed, but ship byte-identical
    # files only once. Hashes are checked again before staging the user's game.
    $gameRoot = [IO.Path]::GetFullPath((Join-Path $resources 'game'))
    $sharedRoot = Join-Path $resources 'installer'
    $sharedFiles = @(foreach ($file in Get-ChildItem -LiteralPath $gameRoot -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($gameRoot, $file.FullName)
        $other = Join-Path $sharedRoot $relative
        if (Test-Path -LiteralPath $other -PathType Leaf) {
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            if ($hash -eq (Get-FileHash -LiteralPath $other -Algorithm SHA256).Hash) {
                @{ Path = $relative; Sha256 = $hash; Bytes = $file.Length }
                $resolved = [IO.Path]::GetFullPath($file.FullName)
                if (-not $resolved.StartsWith($gameRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid duplicate payload path.' }
                Remove-Item -LiteralPath $resolved
            }
        }
    })
    ConvertTo-Json -InputObject $sharedFiles -Depth 3 | Set-Content -LiteralPath (Join-Path $resources 'game-shared-files.json')
    Copy-Item -LiteralPath 'out/installer-launcher-build/Release/Setup Techno Kitten Adventure.exe' -Destination $release
    $translator = Join-Path $resources 'translator'; New-Item -ItemType Directory -Path $translator | Out-Null
    foreach ($file in @('XenosRecomp.exe', 'dxcompiler.dll', 'dxil.dll')) {
        Copy-Item -LiteralPath (Join-Path '.tools/XenosRecomp-build/XenosRecomp/Release' $file) -Destination $translator
    }
    Copy-Item -LiteralPath '.tools/XenosRecomp/XenosRecomp/shader_common.h' -Destination $translator
    $compilerSource = '.tools/mgfxc/.store/dotnet-mgfxc/3.8.4.1/dotnet-mgfxc/3.8.4.1/tools/net8.0/any'
    $compiler = Join-Path $resources 'compiler'; New-Item -ItemType Directory -Path $compiler | Out-Null
    # Compiler program files only; no input/output content is copied here.
    foreach ($file in Get-ChildItem -LiteralPath $compilerSource -File | Where-Object { $_.Extension -in @('.dll', '.json') }) {
        Copy-Item -LiteralPath $file.FullName -Destination $compiler
    }
    Copy-Item -LiteralPath packaging/licenses -Destination $release -Recurse
    Copy-Item -LiteralPath packaging/README.txt -Destination $release
    Copy-Item -LiteralPath LICENSE -Destination (Join-Path $release 'licenses/Port-Code.txt')
    # Fail closed if a retail payload or conversion artifact slipped into the
    # publish outputs. The release is built from source, never a private game.
    $forbidden = Get-ChildItem -LiteralPath $release -Recurse -File | Where-Object {
        $_.Extension -in @('.xnb', '.wma', '.xwb', '.xsb', '.xgs', '.xenos', '.mgfxo') -or
        $_.Name -in @('Helicopter.exe', 'Helicopter.dll', 'GameInfo.xml', 'GameInfo.bin', 'ScoreInfo')
    }
    if ($forbidden) { throw 'Asset-free release check failed.' }
    if (Test-Path -LiteralPath (Join-Path $release 'Techno Kitten Adventure.exe')) { throw 'Play launcher must be embedded, not exposed before installation.' }
    $manifest = Get-ChildItem -LiteralPath $release -Recurse -File | ForEach-Object {
        @{ path = [IO.Path]::GetRelativePath($release, $_.FullName); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    @{ format = 1; version = $version; port_code_license = 'MIT'; built_utc = (Get-Date).ToUniversalTime().ToString('O'); assets_included = $false; files = @($manifest) } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $release 'release-manifest.json')
    Write-Output ('Asset-free installer folder ready: ' + $release)
} finally { Pop-Location }
