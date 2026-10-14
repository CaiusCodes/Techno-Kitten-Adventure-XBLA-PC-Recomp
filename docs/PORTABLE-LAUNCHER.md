# Portable single-launcher candidate — 13 October 2026

Version remains **0.9.0**. The owner authorised publication of this portable update on 13 October 2026 after the local checks. The original `v0.9.0` tag remains its original release snapshot; the attached current source archive and `main` contain this update.

## Release and installed layout

```text
Techno Kitten Adventure/
  Techno Kitten Adventure.exe
  README.txt
  licenses/
  resources/
    game/                 unchanged patched MonoGame host/runtime
    setup/                conventional self-contained PortServices runtime
    compiler/             mgfxc libraries
    translator/           XenosRecomp with dxcompiler.dll and dxil.dll
    release-manifest.json runtime and launcher hashes, format 3
    data/                 created by import or migration
      Helicopter.dll
      Helicopter.dll.retarget.json
      Content/
      tka-install.json
      userdata/           saves, settings and import caches
      logs/               game logs
    logs/                 setup logs
    backups/              previous data retained after reimport
```

`data/`, userdata, logs and backups are absent from the release ZIP. No original game assembly, package, music or assets are distributed. Native and managed executable files remain conventional files; no executable payload archive, single-file self-extraction, packing or obfuscation is used. Legacy embedded-payload source files are retained on disk for reference but excluded from the setup project build.

## Startup and setup

The root native launcher resolves its own directory and performs file-existence checks plus a bounded small-marker check. A ready installation starts `resources/game/Techno Kitten Adventure.exe` directly, with `--game-root` and working directory set to `resources/data/`. The existing host already supports that root override, so its audio banks, saves, caches and display settings stay relative to imported data. No normal-launch package scanning, runtime hashing or setup-process startup occurs.

Missing imported essentials, or `--setup`, open the Silver Explorer WinForms GUI via `resources/setup/PortServices.exe`. The GUI checks only files beside the launcher and the nearby `Game Package/` tree, limited to four nested directory levels, 256 directories and 128 package-sized candidates. Redirected paths are skipped. Files must pass the existing exact size/SHA-256 restriction; names and extensions do not establish compatibility.

One compatible candidate begins importing automatically. Multiple candidates are displayed with filename first and require a choice. Browse Package opens the native file dialog rooted beside the launcher. Select a package normally, or navigate into its containing folder and choose Open while leaving `Use this folder` in the filename box. This is resolved to the current folder and uses the same bounded search as drag-and-drop. No computer-wide scanning or extra folder-picker button is introduced.

After import, Browse is disabled and Play is enabled. The window stays open until Play is clicked. The two Extras start unchecked: fullscreen merges only `Fullscreen=true` into existing JSON; a shortcut targets the root launcher and is created with Windows' existing shell component. Existing shortcuts are left unchanged, including a race-safe final file promotion. Unchecked fullscreen preserves the saved mode. Moving the portable folder requires recreating any desktop shortcut because Windows shortcut targets are absolute.

## Import and preservation

Package and original managed assembly restrictions are unchanged. The existing IL/runtime/texture/font/effect conversion tools and all supplied content/music are retained; no separate DLC importer exists. Earlier score-gated unlocks in existing saves remain intact. The previous unlock checkbox is replaced by the two requested launcher Extras; the existing opt-in development import switch is retained for regression compatibility.

Runtime hashes and version are verified before import or migration, not every launch. Imports take an exclusive local file lock and stage under `resources/.install-<id>/`. Only imported data is promoted; the distributed runtime is untouched. Existing userdata is copied into staging before promotion. The old data directory is retained in `resources/backups/`; failed promotion restores it. Cancellation is cooperative between conversion stages, and only newly created staging is cleaned up after redirected-path checks. Interrupted staging is never treated as a ready installation and does not replace the working data.

Recognised older `Game/` or `runtime/` installations beside the launcher are copied into a fresh data directory. Package markers and/or independently verified retarget reports establish ownership. Imported Content and userdata are reused without the original package. Old folders and files are retained. Migration does not silently modify or remove an old installation. Unknown folders are rejected.

Missing distributed runtime files require re-extracting the portable download while preserving `resources/data/`. Reimport can recover missing imported assets but cannot recreate missing .NET/runtime binaries. There is no separate repair mode.

## Build and verification

Use the existing pinned SDK and dependencies from [BUILD-V1.md](BUILD-V1.md):

```powershell
./tools/Build-Installer.ps1 -Name portable-v090-check
./tools/Package-Installer.ps1 -Release out/portable-v090-check -Zip out/Techno-Kitten-Adventure-portable-v0.9.0.zip
./tools/Test-SingleLauncher.ps1 -Release out/portable-v090-check -Fixture short-check -PackagePath 'PATH-TO-YOUR-PACKAGE'
./tools/Test-InstallerDpi.ps1 -Installer 'out/portable-v090-check/Techno Kitten Adventure/Techno Kitten Adventure.exe' -Output out/portable-dpi
```

Use a fresh output and fixture name; scripts preserve prior outputs. Keep paths short due to Windows' executable-path limits. `Package-Installer.ps1` uses a release-file allowlist, rejects game-derived outputs and debug artifacts, verifies all runtime manifest hashes and ZIP entry hashes, and writes a SHA-256 sidecar. Third-party notices and original artwork licensing remain unchanged. There are no new external dependencies.

The integration probe uses the real setup message loop, drag/drop handlers, importer, Play button, root launcher and host. Optional test flags are bounded and do not affect normal startup. It covers auto-detection, ambiguous inputs, unsupported input, cancellation, promotion rollback, recovery, save/settings preservation, relocation without the package, Popaganda play/exit, fullscreen, shortcut targeting/no overwrite and legacy migration. Effective DPI checks cover initial/ready states from 96 to 288 DPI.

Manual coverage still needed: native file-dialog interaction and physical OS drag/drop, controller hardware/hot-plug, actual cross-monitor DPI transitions, long sessions and a clean second-machine test of this new candidate. Audio banks and music are preserved and initialized during gameplay; no new listening-equivalence or full-level certification is claimed. Existing `Test-PortableInstaller.ps1` and `Test-Installer.ps1` describe the previous release layout; use `Test-SingleLauncher.ps1` for this candidate.

## Completed candidate checks

- Final build: `out/portable-v090-20261013-r7/Techno Kitten Adventure`; clean redistribution copy: `out/portable-v090-final-r7/Techno Kitten Adventure`.
- ZIP: `out/Techno-Kitten-Adventure-portable-v0.9.0.zip`, 167,168,680 bytes, 1,094 files. Its adjacent `.sha256` file is generated by the packaging script.
- Player ZIP SHA-256: `03639EB25DB65563AF03833D40E00237A303673446E72839EA1869BF1CF9339C`.
- Final integration report: `out/sl16-results.json`; all automated integration checks passed, including the real drop event handlers and bounded Play-button launch. File-dialog defaults and folder-path resolution passed scripted checks.
- Final DPI previews: `out/portable-v090-dpi-r7/`, all 14 initial/ready states passed at 96–288 DPI. The 96-DPI initial preview was inspected and saved as the current Setup documentation screenshot.
- Additional actual previous-release fixture: imported assembly, Content and userdata copied from the tested earlier v0.9.0 installation, migrated into `resources/data`, and launched successfully without the package. The original `Game/` remained intact.
- An extracted final-layout ZIP fixture also passed normal no-argument launch, game-window initialization and normal close, with no package present and no setup process/logs created.
- Source audits cover the explicit source allowlist, selected sensitive-data patterns, reachable history, version agreement, root layout, ZIP exclusions, all archived file hashes and retained third-party notices. These checks are not a blanket rights/security certification.

No game/compatibility source or original IL patch implementation changed. The existing tag is preserved. Temporary build outputs and earlier candidates remain local; they are not included in the clean ZIP. See [SECURITY-CHECK.md](SECURITY-CHECK.md) for the scan evidence and published ZIP checksum; the earlier candidate checksum above records pre-publication testing.
