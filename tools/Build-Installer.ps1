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
    $payloadRoot = Join-Path $release 'payload'
    $resources = Join-Path $payloadRoot 'resources'
    New-Item -ItemType Directory -Path $resources | Out-Null
    function Invoke-LocalDotnet {
        & "$PSScriptRoot/dotnet.ps1" @args
        if ($LASTEXITCODE -ne 0) { throw ('Build tool failed: ' + $LASTEXITCODE) }
    }
    # Compile the play launcher first so the managed installer embeds this build.
    & "$PSScriptRoot/Build-Icon.ps1"
    cmake -S src/InstallerLauncher -B out/installer-launcher-build -G 'Visual Studio 17 2022' -A x64
    if ($LASTEXITCODE -ne 0) { throw 'Launcher configuration failed.' }
    cmake --build out/installer-launcher-build --config Release --target TkaGameLauncher
    if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed.' }
    Invoke-LocalDotnet publish tools/Tka.AssemblyTool -c Release -r win-x64 --self-contained true '-p:RestoreLockedMode=true' '-p:DebugType=None' '-p:DebugSymbols=false' -o (Join-Path $release 'build-tools')
    Invoke-LocalDotnet publish src/Tka.Host -c Release -r win-x64 --self-contained true '-p:RestoreLockedMode=true' '-p:DebugType=None' '-p:DebugSymbols=false' -o (Join-Path $resources 'game')
    Invoke-LocalDotnet (Join-Path $release 'build-tools/Tka.AssemblyTool.dll') --runtime (Join-Path $release 'build-tools/MonoGame.Framework.dll') (Join-Path $resources 'game/MonoGame.Framework.dll')
    Copy-Item -LiteralPath 'out/installer-launcher-build/Release/Techno Kitten Adventure.exe' -Destination $payloadRoot
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
    # Fail closed before embedding anything. The payload contains only the
    # play launcher and runtime, never a user's program or converted assets.
    $forbidden = Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | Where-Object {
        $_.Extension -in @('.xnb', '.wma', '.xwb', '.xsb', '.xgs', '.xenos', '.mgfxo') -or
        $_.Name -in @('Helicopter.exe', 'Helicopter.dll', 'GameInfo.xml', 'GameInfo.bin', 'ScoreInfo')
    }
    if ($forbidden) { throw 'Asset-free payload check failed.' }
    $manifest = Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | ForEach-Object {
        @{ path = [IO.Path]::GetRelativePath($payloadRoot, $_.FullName).Replace('\','/'); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    @{ format = 2; version = $version; port_code_license = 'MIT'; built_utc = (Get-Date).ToUniversalTime().ToString('O'); assets_included = $false; files = @($manifest) } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $payloadRoot 'release-manifest.json')
    $payloadArchive = Join-Path $release 'installer-payload.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($payloadRoot, $payloadArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
    Invoke-LocalDotnet publish src/Tka.Installer -c Release -r win-x64 --self-contained true '-p:RestoreLockedMode=true' '-p:DebugType=None' '-p:DebugSymbols=false' '-p:PublishSingleFile=true' '-p:EnableSingleFileAnalyzer=false' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:EnableCompressionInSingleFile=true' ('-p:TkaPayloadArchive=' + $payloadArchive) -o (Join-Path $release 'installer-build')
    Copy-Item -LiteralPath (Join-Path $release 'installer-build/Setup Techno Kitten Adventure.exe') -Destination $release
    Copy-Item -LiteralPath packaging/licenses -Destination $release -Recurse
    Copy-Item -LiteralPath packaging/README.txt -Destination $release
    Copy-Item -LiteralPath LICENSE -Destination (Join-Path $release 'licenses/Port-Code.txt')
    Write-Output ('Asset-free single-file Setup ready: ' + $release)
} finally { Pop-Location }
