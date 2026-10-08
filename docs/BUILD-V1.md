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
cmake --build .tools/XenosRecomp-build --config Release
./tools/dotnet.ps1 tool install dotnet-mgfxc --version 3.8.4.1 --tool-path .tools/mgfxc
./tools/Build-Installer.ps1 -Name TKA-PC-Installer-v103
./tools/Package-Installer.ps1 -Release out/TKA-PC-Installer-v103
```

Each output name must be fresh. The build deduplicates identical runtime payloads and adapts the private MonoGame copy automatically. Never run an ordinary publish over an installed Game directory: that would replace its patched MonoGame dependency. Keep translator `dxcompiler.dll` and `dxil.dll` adjacent to XenosRecomp, and preserve the complete installer resources tree.

## Validation

```powershell
./tools/Test-PortableInstaller.ps1 -Zip 'out/Techno-Kitten-Adventure-XBLA-PC-Recomp-v1.0.3.zip' -Fixture aspect-v1-check -PackagePath 'PATH-TO-YOUR-PACKAGE'
./tools/Test-DisplayAspect.ps1 -Game private/aspect-v1-check/Game
./tools/Test-InternalScale.ps1 -Fixture aspect-v1-check
./tools/Test-Installer.ps1 -Release out/TKA-PC-Installer-v103 -FixtureName aspect-v1-check -PackagePath 'PATH-TO-YOUR-PACKAGE'
./tools/Test-InstallerDpi.ps1
./tools/Test-BuildPresentation.ps1 -Fixture aspect-v1-check
```

The first check creates a fresh isolated fixture and refuses existing directories. Fault tests require that fixture and must never target a personal installation. GPU probes open bounded game windows and save local results. Internal-render tests verify camera effects, true 2x rasterization and zero render-bridge allocations after warmup. Tests never upload anything.

The dependency installation procedure is documented for fresh machines; the V1 release was built and tested with the pinned dependencies already installed. A fully clean dependency bootstrap on another machine remains to be verified.

## Source archive / GitHub

`VERSION` is the release version. Build and packaging scripts check it against the .NET assembly metadata and packaged README; the release manifest and default ZIP name derive from it. Run `./tools/Package-Source.ps1` to create a fresh source archive using an explicit file allowlist. Its GitHub-ready root includes README, licences, artwork, source, lock files, build/test tools and selected documentation. It excludes private assets, local dependencies, generated outputs, old development notes and logs. The original social preview can be rerendered with `python tools/Render-SocialPreview.py` after installing Pillow; this is not required to build the game. Later, attach the installer ZIP to a release tagged `v1.0.3`. Do not upload the workspace parent or an installed Game folder.
