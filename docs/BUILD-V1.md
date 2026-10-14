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

Each output name must be fresh. The current local candidate uses the single-launcher layout in [PORTABLE-LAUNCHER.md](PORTABLE-LAUNCHER.md), with conventional self-contained runtime files under resources and no embedded executable archive. Keep translator DLLs adjacent to XenosRecomp. Version remains 0.9.0; the published previous layout has not been updated.

## Validation

Use `tools/Test-SingleLauncher.ps1` for the current portable layout. It creates fresh isolated fixtures and uses your private supported package. [PORTABLE-LAUNCHER.md](PORTABLE-LAUNCHER.md) includes current build, packaging, integration and DPI commands. The older installer-layout test scripts are retained for the previous release, not the new root launcher.

Pinned dependencies were already installed locally. Fresh project builds and local import/gameplay/reinstall checks are recorded in the portable notes; a fully clean dependency bootstrap and physical hardware matrix remain separate tests.
## Source archive / GitHub

`VERSION` is the release version. Build and packaging scripts check it against the .NET assembly metadata and packaged README; the release manifest and default ZIP name derive from it. Run `./tools/Package-Source.ps1` to create a fresh source archive using an explicit file allowlist. Its GitHub-ready root includes README, licences, artwork, source, lock files, build/test tools and selected documentation. It excludes private assets, local dependencies, generated outputs, old development notes and logs. The original social preview can be rerendered with `python tools/Render-SocialPreview.py` after installing Pillow; this is not required to build the game. Later, attach the installer ZIP to a release tagged `v0.9.0`. Do not upload the workspace parent or imported resources/data. Publication of this candidate requires explicit approval; do not move the existing v0.9.0 tag.
