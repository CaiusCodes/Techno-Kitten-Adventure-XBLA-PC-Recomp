TECHNO KITTEN ADVENTURE! — PC V0.9.0

A free, unofficial .NET 8 / MonoGame Windows PC port.
You'll need your own supported Xbox 360 game package. No original game
program or assets are included. No separate .NET installation is needed.

FIRST PLAY
1. Extract the complete Techno Kitten Adventure folder to a short, writable
   location. Keep Techno Kitten Adventure.exe beside resources.
2. Open Techno Kitten Adventure.exe.
3. Setup looks only beside the launcher and in Game Package/ (up to four
   nested folders). One compatible package is imported automatically.
   If several are found, choose one. Otherwise use Browse Package, or drag
   your original package or its containing folder onto the setup window.
4. Wait for Ready to play, then click Play. Import never launches the game.

Optional Extras, unchecked by default:
- Start in fullscreen: applied when Play is clicked; other settings are kept.
- Create desktop shortcut: targets the root launcher. An existing shortcut
  is left unchanged. Recreate it if you later move the portable folder.

NEXT PLAY
Open the same Techno Kitten Adventure.exe. The game starts immediately.
The package is no longer needed. To reopen setup, run the launcher with
--setup (for example from a shortcut's arguments).

PORTABLE FILES
resources/game/        internal game runtime
resources/setup/       internal setup and conversion service runtime
resources/compiler/    shader compiler
resources/translator/  shader translator and its required adjacent DLLs
resources/data/        imported game assembly, Content and installation marker
resources/data/userdata/  saves, settings and caches
resources/data/logs/   game logs
resources/logs/        import/setup logs
resources/backups/     previous imported data retained after reimport
resources/release-manifest.json  redistributed runtime hashes

Keep the whole folder together when moving or backing it up. There is no
administrator or registry installation. Don't share resources/data: it
contains converted material from your own game copy.

If essential imported files are missing, setup opens again. Reimport keeps
saves/settings and retains the old data as a backup. If runtime software is
missing, extract the complete download again while keeping resources/data.
Valid older Game/ or runtime/ installations beside the launcher can be copied
into resources/data without a package. Their original folders are retained.
Older score-gated kitten unlocks in your save are preserved.

SUPPORTED PACKAGE
Only the previously supported 83,353,600-byte LIVE package is accepted.
Header title ID: 584E07D2
SHA-256:
A472E517EF37A35A5C956C6ED558FE918F8D4E0C775995188CC17671FBB50EE5
Other revisions, repacked or loose files, and separate DLC are unsupported.
Your source package is read-only and is never moved, changed or deleted.
All existing content packs in this supported package are retained.

CONTROLS
Arrows / WASD: menu navigation. Space: select / hold to rise, release to fall.
Enter: Start in menus. Esc: Back in menus / pause during gameplay.
S: original Start / pause shortcut. B: Back / resume.
Mouse: hover menus, left click selects (and advances from Press Start),
right click goes back. Cursor is hidden during active play.
Original gamepad controls are preserved.

DISPLAY
Windowed/borderless fullscreen and 720p/1080p/1440p/4K output retain 16:9.
Internal rendering remains 2x, independent of output size. VSync is off;
game updates use fixed 60 Hz. Existing settings take priority unless you
select Start in fullscreen and click Play.

LICENCES
Original port code and project-owned artwork are MIT-licensed; see
licenses/Port-Code.txt. Other components retain their notices in licenses/.
No rights to the original game, its artwork, music or trademarks are granted.
This is an unofficial project, with no affiliation or endorsement.

This is the portable single-launcher v0.9.0 update. Use short installation
paths. Broader hardware, physical DPI
transitions and long sessions still need testing.
