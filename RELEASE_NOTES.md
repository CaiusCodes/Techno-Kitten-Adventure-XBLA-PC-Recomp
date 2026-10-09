# V0.9 — 0.9.0

The current build is numbered V0.9 (package and Windows product version 0.9.0). This renumbers the current port without rolling back fixes. The title-only version label now uses original cream pixel lettering with a navy outline and a small cyan/pink underline, displaying `v0.9` without the previous box.

Stage/kitten Select and Back controller prompts and the flight A-button instructions now follow gamepad connection status, including connection changes during play. Mouse, keyboard and controller bindings are unchanged.

# Development build — formerly V1.1.0

The asset-free ZIP now opens to one `Techno Kitten Adventure XBLA Recomp` folder containing Setup, README.txt and licences. Setup carries the Windows runtime and creates `Game/Techno Kitten Adventure.exe`, `Game/resources/`, `Game/release-manifest.json` and the converted files from your own supported package.

- Added mouse hover selection, a visible menu cursor, left-click Start on the opening screen, left-click A and right-click B in menus, Enter Start in menus, Esc Back in menus and Esc Pause during play. WASD supplements the existing arrow keys. The cursor hides during active play.
- Setup's Choose file picker starts beside the Setup EXE. Opshunz ON/OFF values and display/resolution controls are clickable, and visible level-select arrows scroll their respective direction without selecting a pack.
- Added an optional Setup extra to unlock score-gated kittens in the portable save. All five stages were already available in full mode. Reinstallation retains the previous save in a backup.
- Added the selected Silver Explorer pixel-art kitten to game and Setup icons. Setup uses a sunset side panel, cream/pink pixel lettering, a cyan divider and arcade-style buttons.
- Preserved the original game program, menu callbacks, controller controls and transactional package import.

Only the exact audited Xbox 360 package is supported. Additional physical input and hardware testing is still welcome. No game assets are included in the download.

# V1.0.3

First planned public release of the free, asset-free .NET 8 / MonoGame PC port of the Techno Kitten Adventure! XBLA game. Supply the exact supported Xbox 360 package yourself; Setup reads it locally and prepares the game in a portable `Game` folder.

- Renamed Setup and the installed game EXE with clear names and spaces.
- Added an original simple cat-face icon to the play launcher, host and game window.
- Added a small automatic build-version badge only on the Press Start screen.
- Transactionally migrate the old play filename while retaining backups and saves.
- Existing display options remain: windowed/borderless fullscreen, four output sizes, 2× internal rendering by default, and fixed 60 Hz updates.

Known limits: only one exact package is accepted; no separate DLC importer, music-spectrum visualization, VSync/FPS menu controls or broad hardware certification. This managed XNA port does not use ReXGlue or static PowerPC recompilation. The original game and converted assets are not distributed.

# V1.0.2

- Added an original neon kitty icon to the setup bootstrap, managed installer and setup window.
- Includes transparent Windows icon sizes from 16 through 256 pixels and editable SVG artwork in source.

# V1.0.1

- Fixed setup controls, text and painted artwork using different scales on high-DPI displays.
- Enabled per-monitor DPI handling with one shared logical layout and cached pixel fonts.
- Added initial/completed layout checks from 100% through 300%, including repeated DPI transitions.
- Made VSync off explicit at startup and after every display-mode change; 60-update timing and internal 2x quality are preserved.

# V1 — 1.0.0

First numbered Windows release of the Techno Kitten Adventure! PC port.

The new port code is free and open source under the MIT license. Third-party notices are retained; original game assets remain excluded.

- Custom package-picker installer with the approved neon design.
- Portable Game folder, local saves/settings and a top-level play EXE created during installation.
- Transactional imports and update backups; original package remains unchanged.
- Native Windowed/Fullscreen and 720p/1080p/1440p/4K options, with correct aspect ratio on ultrawide displays.
- True 2x internal rendering independent of output size.
- Corrected Popaganda shader inputs.
- Allocation-free render-target lookup in the per-batch display bridge.
- Versioned, self-contained, asset-free installer and a separate GitHub source archive.

V1 retains the known limitations listed in README.md. The optimization reduces temporary garbage; no measured FPS improvement is claimed. Installer runtime deduplication remains enabled. Debug symbols are omitted from the player download; required runtime DLLs stay in their expected locations.
