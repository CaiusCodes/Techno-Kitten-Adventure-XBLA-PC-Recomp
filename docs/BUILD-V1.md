# Building V1

Use PowerShell 7 from the source root on Windows x64. The public source builds the asset-free installer without a game package. Running the game tests requires your own supported package. No game assets or development dependencies are in the source archive.

## Pinned dependencies

- Visual Studio 2022 C++ build tools, Windows SDK, CMake, Git, and ClangCL (for XenosRecomp).
- .NET SDK 8.0.424 unpacked directly into `.tools/dotnet`.
- NuGet packages: MonoGame WindowsDX 3.8.4.1 and Mono.Cecil 0.11.6, plus dependencies recorded in `packages.lock.json`. Installer builds use locked restore.
- XenosRecomp commit `990d03b28a27b50277ee5d8d942e1c5f873869d1`, including its recursive submodules.
- dotnet-mgfxc 3.8.4.1.

SDK archive: https://dotnetcli.blob.core.windows.net/dotnet/Sdk/8.0.424/dotnet-sdk-8.0.424-win-x64.zip

Verified SDK SHA-512: `1787ab90635c2950672ed7c6507b000e1b212ea7d9a22fcef37061344d37c64d4c4eda12b8742601eff5b45c8736485b31c55613892f240c300190e4e88a58b0`.

After installing the SDK in the local directory:

```powershell
git clone https://github.com/hedge-dev/XenosRecomp.git .tools/XenosRecomp
git -C .tools/XenosRecomp checkout 990d03b28a27b50277ee5d8d942e1c5f873869d1
git -C .tools/XenosRecomp submodule update --init --recursive
cmake -S .tools/XenosRecomp -B .tools/XenosRecomp-build -G 'Visual Studio 17 2022' -A x64 -T ClangCL
cmake --build .tools/XenosRecomp-build --config Release --target XenosRecomp
./tools/dotnet.ps1 tool install dotnet-mgfxc --version 3.8.4.1 --tool-path .tools/mgfxc
./tools/Build-Installer.ps1 -Name TKA-PC-Installer-v090
./tools/Package-Installer.ps1 -Release out/TKA-PC-Installer-v090
```

Each output name must be fresh. The build embeds an asset-free, hash-verified runtime payload in a self-contained single-file Setup and adapts its private MonoGame copy automatically. The download ZIP contains one `Techno Kitten Adventure XBLA Recomp/` folder with only Setup, README.txt and licences. Setup creates `Game/` beside itself, containing the play EXE, `resources/`, release manifest and files converted from the user's package. Never run an ordinary publish over an installed Game directory: that would replace its patched MonoGame dependency. Keep translator `dxcompiler.dll` and `dxil.dll` adjacent to XenosRecomp within the embedded payload.

## Validation

```powershell
./tools/Test-PortableInstaller.ps1 -Zip 'out/Techno-Kitten-Adventure-XBLA-PC-Recomp-v0.9.0.zip' -Fixture v090-check -PackagePath 'PATH-TO-YOUR-PACKAGE'
./tools/Test-DisplayAspect.ps1 -Game 'private/v090-check/Techno Kitten Adventure XBLA Recomp/Game'
./tools/Test-Installer.ps1 -Release out/TKA-PC-Installer-v090 -FixturePath 'private/v090-check/Techno Kitten Adventure XBLA Recomp' -PackagePath 'PATH-TO-YOUR-PACKAGE'
./tools/Test-InstallerDpi.ps1 -Installer 'out/TKA-PC-Installer-v090/Setup Techno Kitten Adventure.exe' -Output out/installer-dpi-check-v090
```

The first check creates a fresh isolated fixture and refuses existing directories. Fault tests require that fixture and must never target a personal installation. GPU probes open bounded game windows and save local results. Internal-render tests verify camera effects, true 2x rasterization and zero render-bridge allocations after warmup. Tests never upload anything.

Keep installation fixtures at a short path. A fixture nested inside the fresh-source test directory exceeded Windows executable-path limits while launching XenosRecomp. The installer rejected that import without promoting the installation; moving the test to a shorter fixture path allowed conversion to proceed. This is a current installation-path limitation, not a missing source dependency.

On 9 October 2026 the reviewed source archive was built in a fresh source directory, with fresh project outputs and a freshly compiled XenosRecomp target. The pinned SDK, NuGet cache, mgfxc installation and upstream source checkout were reused. This validates the source inventory and build instructions, not a clean-machine dependency bootstrap. Build only the required XenosRecomp target: building every upstream target also attempts unused zstd utilities, one of which failed resource compilation in this environment.

## Source archive / GitHub

`VERSION` is the release version. Build and packaging scripts check it against the .NET assembly metadata and packaged README; the release manifest and default ZIP name derive from it. Run `./tools/Package-Source.ps1` to create a fresh source archive using an explicit file allowlist. Its GitHub-ready root includes README, licences, artwork, source, lock files, build/test tools and selected documentation. It excludes private assets, local dependencies, generated outputs, old development notes and logs. The original social preview can be rerendered with `python tools/Render-SocialPreview.py` after installing Pillow; this is not required to build the game. Later, attach the installer ZIP to a release tagged `v0.9.0`. Do not upload the workspace parent or an installed Game folder.
