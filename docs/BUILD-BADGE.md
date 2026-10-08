# V1.0.3 launchers, icon and title badge

Public setup is `Setup Techno Kitten Adventure.exe`. Installation emits
`Techno Kitten Adventure.exe` beside Setup and a matching managed host inside
`Game`. The play launcher remains embedded until installation. All quoted process
paths support spaces. Source archives contain original icon artwork, never game assets.

The game icon is original editable `src/Tka.Host/Assets/game-kitty.svg` artwork
under the project's MIT license. `tools/Build-Icon.ps1` regenerates its transparent
16–256 pixel Windows icon. Native `game.rc` and managed ApplicationIcon embed the
same icon. The host assigns it to the actual MonoGame WinForms game window.

`TitleBuildBadge.cs` draws a small box in logical coordinates (16, 676), height 28,
with a 16 pixel label. It obtains the build version from AssemblyInformationalVersion,
so the shared project version updates the displayed text automatically.

Independently verified original `Helicopter.Game1.Draw` (token 06000009, original
return IL_0109) already calls `PcDisplay.EndFrame`; the badge uses that existing
bridge before presentation. No new original-assembly edits are required.
Original `DrawMenu` gates Press Start on `gameState == GameState.OPENING` (enum 0)
and `splashScreen == false`. The cached reflection fields use those exact names
and enum value. `Helicopter.Global.spriteFont` supplies the user's imported font.
The badge excludes splash screens, other menus and gameplay. The existing owned
canvas transform handles internal supersampling and output aspect fitting.

Upgrade validates both old and new play filenames before conversion. Only the
current payload or independently hashed known predecessors can be replaced.
V1.0.2's native play launcher SHA256 is
`7E0521168FBFE1709AE3BEBBFA655538FA8C8244224323C712D8283F712924E7`.
At commit, the old filename moves to a unique `backups/Launcher-*.exe`. A failed
launcher promotion restores both the previous Game folder and old play filename.
The old managed host is retained in the previous Game backup. Userdata is copied
before promotion, preserving saves/settings; unknown conflicting files are rejected.

Verification checklist:

- Fresh asset-free ZIP has Setup and no top-level play EXE before installation.
- Import emits the new play filename and launches from an unrelated working directory.
- Both game PE icons and the live window show the cat face.
- Press Start shows the build badge; splash screens, main menu and gameplay do not.
- Upgrade a private V1.0.2 fixture with `Test-Installer.ps1 -LegacyName`; exercise
  cancellation, damaged package, both promotion failures and save-preserving success.
- Repeat setup DPI checks, display aspect checks and Popaganda gameplay smoke test.
- Check binary/source archive allowlists, package integrity and retained notices.
