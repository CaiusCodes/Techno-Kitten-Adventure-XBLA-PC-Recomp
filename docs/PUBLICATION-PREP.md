# GitHub publication preparation — 9 October 2026

This records the checks completed before the first public release. Repository name remains `CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp`. The project is a .NET 8 / MonoGame PC port, not a ReXGlue or static PowerPC recompilation. The owner authorised GitHub publication on 9 October 2026 after reporting a successful play test on a friend's PC.

## Current candidate

- Version: `0.9.0`; title screen displays `v0.9`.
- First public tag: `v0.9.0`.
- Release title: **Techno Kitten Adventure PC Port — v0.9**.
- Player archive: `Techno-Kitten-Adventure-XBLA-PC-Recomp-v0.9.0.zip`.
- Player archive SHA-256: `87D58CB5AC388E2F5DBAE49E198E8919AC673EFD3D574E3AB813B240BE765DD6`.
- Player archive size: 169,615,507 bytes.
- History reviewed before the publication commit: one reachable commit, `38bae4b98f054d97b5ea33d7fe2df9702b369b85`. The publication commit adds the subsequently developed features and reviewed presentation files without rewriting the original history. Git author metadata uses the existing CaiusCodes public noreply identity.

Suggested description:

> Unofficial Windows PC port of Techno Kitten Adventure (XBLA), built with .NET 8 and MonoGame. Requires your own supported game copy.

Suggested topics: `techno-kitten-adventure`, `xbla`, `xbox-360`, `pc-port`, `monogame`, `dotnet`, `windows`, `arcade`. Avoid ReXGlue/static-recompilation topics because they do not describe this implementation.

## Proposed public contents

```text
.github/                  funding and bug-report template
.gitattributes
.gitignore
LICENSE                   MIT for original project code and original artwork
README.md
RELEASE_NOTES.md
THIRD_PARTY.md
VERSION
Directory.Build.props
global.json
NuGet.Config
docs/                     selected build/input/icon/publication documentation
  social-preview.svg      original editable artwork
  social-preview.png      1280 × 640; PC Port wording
packaging/
  README.txt
  licenses/               retained upstream notices
src/
  InstallerLauncher/      native play launcher and legacy setup bridge
  Tka.Compatibility/      normal source for input, storage, display and menu bridges
  Tka.Host/               MonoGame host and bounded opt-in test probes
  Tka.Installer/          transactional package importer and Setup
tools/                    allowlisted build/package/test tools and original patch utilities
  Tka.AssemblyTool/       reproducible hash-checked IL/runtime patches
  Tka.AssetTool/          local conversion of user-supplied assets
```

Use the explicit source archive allowlist, rather than copying the workspace parent. The parent contains reference projects and proprietary package inputs. `.tools/`, `private/`, `out/`, installed `Game/`, caches, saves, logs and generated game outputs stay local. The three local development scripts `Build-Baseline.ps1`, `Extract-Baseline.ps1` and `Test-DefaultDisplay.ps1` are explicitly ignored and excluded from the source archive. Files are preserved on disk. The six owner-supplied screenshots in `docs/screenshots/` document the port, carry a separate game-artwork notice and are excluded from the player ZIP. The source manifest explicitly identifies documentation screenshots separately from playable game data.

There is no ReXGlue dependency or project submodule to pin. .NET and NuGet versions are pinned in source. XenosRecomp is an external local build dependency pinned to `990d03b28a27b50277ee5d8d942e1c5f873869d1`; the current local checkout matches and has no reported working changes. Follow `docs/BUILD-V1.md` to obtain it and its submodules. MonoGame adaptations and game-specific IL bridges are independently tracked in normal source, not in vendored SDK binaries.

## Draft release notes

Free, unofficial Windows x64 PC port of Techno Kitten Adventure! Supply your own supported original Xbox 360 package. No original game program or assets are included or downloaded.

Extract the archive's `Techno Kitten Adventure XBLA Recomp` folder to a writable location. Run `Setup Techno Kitten Adventure.exe`, choose your package, and install. Afterwards, launch `Game/Techno Kitten Adventure.exe`. Assets, runtime resources and portable saves remain in `Game/`. The optional Setup extra unlocks score-gated kittens; all five stages are available in full mode.

Features include native menu mouse selection, keyboard shortcuts, windowed/borderless fullscreen output with 16:9 fitting, four output resolutions, 2× internal rendering by default, VSync off, fixed 60 Hz updates, original Silver Explorer pixel icons and a small title-only version label. Controller selection prompts and flight instructions follow controller connection status.

Only the exact 83,353,600-byte LIVE package documented in README is accepted. Loose extracted folders, repacked packages and other revisions are not supported. Separate DLC importing is not implemented. Music-spectrum visualization is inactive. Controller hot-plug rendering was tested with synthetic connection states; more physical-device testing is needed.

## Audit and validation evidence

- Current source archive uses an explicit file allowlist and per-file SHA-256 manifest; source packaging checks for unexpected binaries, local machine paths and selected credential patterns.
- Player ZIP contains 16 entries: Setup, README and third-party notices only. The embedded runtime is produced separately from user assets. The prior fresh-install test verified 542 redistributed runtime files against the embedded manifest; original game outputs are generated afterwards from the supplied package.
- Fresh import, unchanged source package, Popaganda gameplay/normal exit, portable saves and save-preserving transactional reinstall passed for the exact player ZIP hash above. Evidence is local in `out/v090-controller-prompts-release-check-results.json`.
- Controller prompt rendering passed for connected/disconnected/reconnected states, with no menu pixel changes outside the documented regions. Physical USB/Bluetooth hot-plug remains NOT TESTED.
- Title-only version label and live window icon checks passed during the feature work. DPI checks passed at simulated 96–288 DPI during installer work. The owner reported successful play on a friend's PC; its hardware, dependency state and detailed install/reinstall coverage were not recorded. A documented clean second-PC/4K test remains NOT TESTED.
- The banner/icon artwork is original project artwork with editable SVG sources. Six owner-supplied gameplay/menu screenshots are included solely as documentation, with the original game's artwork excluded from MIT licensing. Exported textures, atlases and playable game data remain private.
- Root MIT licence covers original project work only. Upstream notices remain in `packaging/licenses/`; original game assets, programs and trademarks retain their separate rights. Hash/signature constants and IL patch tools are distinct from generated game-derived output.
- `tools/Test-Publication.ps1` rechecks source/release inventories, source hashes, current version metadata, tracked and reachable-history paths/text patterns, ignore behaviour, whitespace and preview dimensions. Its local report is `out/publication-prep-audit.json`. Pattern scans do not establish rights or detect every possible secret. Unreachable Git objects/reflogs are not audited.

## Fresh-source reproduction and remaining coverage

The source archive was extracted into a fresh project directory. Its source hashes were verified; the installer and XenosRecomp target built with fresh outputs. The pinned SDK, NuGet cache, mgfxc installation and upstream checkout were reused. All 62 current build inputs match the tested source, including regenerated icons. Fresh-build installation, Popaganda gameplay, unchanged input package, runtime manifest checks, portable saves and save-preserving reinstall passed in an isolated fixture.

Building every upstream XenosRecomp target attempted an unused zstd utility that failed resource compilation. Building the required `XenosRecomp` target passed; the build instructions now name that target. A deeply nested installation fixture exceeded Windows executable-path limits during tool startup; the same fresh build passed at a shorter path. README and release notes document this installation-path limitation. No gameplay change was made during publication preparation.

The published player ZIP remains the already tested candidate identified above. The fresh build is validation evidence rather than an untested replacement download.

Still NOT TESTED: a fresh-machine dependency bootstrap, a documented full second-machine install/reinstall matrix, and physical USB/Bluetooth controller hot-plug across hardware. Pattern scans do not certify all licensing questions or secrets. Third-party notices are retained; no upstream work is claimed under the port's MIT licence. Screenshots show copyrighted game artwork and are separately identified.

Ko-fi `https://ko-fi.com/caiuscodes` matches the GoldenEye reference README and the local funding file. Release presentation follows GoldenEye's short introduction, highlights and own-copy notice, while documenting Techno Kitten's actual implementation and limitations. It includes the 169.6 MB self-contained runtime explanation and uses the original 1280×640 social preview.

Status: **VALIDATED FOR FIRST PUBLIC RELEASE WITH DOCUMENTED LIMITATIONS**. Publication is authorised by the owner; this report does not imply clean-machine or broad hardware certification.
