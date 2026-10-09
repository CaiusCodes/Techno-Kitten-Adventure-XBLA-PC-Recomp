# Techno Kitten Adventure PC Port v0.9

The first public release of this unofficial **.NET 8 / MonoGame Windows PC port** of Techno Kitten Adventure! Bring your own supported Xbox 360 package, run Setup, and play from a portable game folder.

> **Your own game copy is required.** The download contains no original game executable, music, textures or other playable game data. Setup reads and validates the package you supply and converts it locally. This port does not use ReXGlue or static PowerPC recompilation.

![Techno Kitten Adventure main menu](https://raw.githubusercontent.com/CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp/v0.9.0/docs/screenshots/main-menu.png)

## Highlights

- Custom Silver Explorer pixel-art Setup and game icons.
- Mouse navigation in menus, including clickable Options values and level-select arrows. Left click also advances from the Press Start screen.
- Keyboard controls alongside the original gamepad controls.
- Windowed or borderless fullscreen, with 720p, 1080p, 1440p and 4K output options that preserve the image's proportions.
- 2× internal rendering by default, independent of the window size. Fixed 60 Hz game updates, with VSync off by default.
- Portable saves and display settings inside `Game/userdata/`.
- Controller Select/Back prompts and flight instructions appear only while a gamepad is connected.
- Optional Setup extra to unlock score-gated kittens in your portable save. All five stages are already available in full mode.
- Package imports preserve the original file; reinstalling preserves saves and retains a backup of the previous installation.

## Download and install

1. Download **`Techno-Kitten-Adventure-XBLA-PC-Recomp-v0.9.0.zip`**. This is the player download; GitHub's source-code downloads are for development.
2. Extract the complete **Techno Kitten Adventure XBLA Recomp** folder to a writable location.
3. Run **`Setup Techno Kitten Adventure.exe`** and choose your supported game package. The file picker starts in Setup's folder.
4. Optionally enable **Extras: Unlock all levels and kittens**, then select **Install game**.
5. Select **Play now**. For subsequent sessions, launch **`Game/Techno Kitten Adventure.exe`**.

Setup creates `Game/` beside itself. Keep the complete folder together when moving or backing it up. Do not share the installed `Game/` folder: it contains converted data from your own copy.

### Why is the ZIP about 170 MB?

The release ZIP is **169,615,507 bytes — 169.6 MB / 161.8 MiB**. It carries the .NET 8 runtimes for Setup and the game, MonoGame, and the shader conversion and compilation tools used during installation. **No separate .NET installation is required.**

A small importer can be only a few megabytes when it relies on prerequisites already installed on the PC. This self-contained release includes those dependencies, which accounts for its larger size. It does not include game assets. The installed folder will be larger after the runtime is extracted and your package is converted.

## Requirements and supported package

- Windows x64 and a Direct3D 11-capable graphics card. Windows 11 has been tested locally; broader compatibility needs testing.
- A writable installation folder.
- Your own supported **83,353,600-byte LIVE package**, header title ID **`584E07D2`**.

Supported package SHA-256:

```text
A472E517EF37A35A5C956C6ED558FE918F8D4E0C775995188CC17671FBB50EE5
```

Only that exact package is currently accepted. Other revisions, loose extracted files and separate DLC packages are not supported. Setup does not download proprietary game files or modify your source package.

## Controls

| Input | Action |
| --- | --- |
| Mouse | Hover and left click to select menu items; right click to go back |
| Left click on Press Start | Advance to the main menu |
| Arrow keys / WASD | Navigate menus |
| Space | Select / fly |
| Enter | Start in menus |
| Esc | Back in menus; pause during gameplay |
| S | Original Start / pause shortcut |
| B | Back / resume |

The mouse cursor is visible in menus and hidden during active gameplay. Original gamepad controls remain available.

## Screenshots

User-supplied captures of the PC port. Game artwork remains the property of its respective owners; screenshots are not covered by the port-code MIT licence.

### Setup

![Silver Explorer Setup with package picker and optional kitten unlocks](https://raw.githubusercontent.com/CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp/main/docs/screenshots/setup.png)

### PC display options

![PC display options in Opshunz](https://raw.githubusercontent.com/CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp/v0.9.0/docs/screenshots/options.png)

### Adventure selection

![Adventure selection](https://raw.githubusercontent.com/CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp/v0.9.0/docs/screenshots/adventure-select.png)

### Lava and Dream gameplay

![Lava gameplay](https://raw.githubusercontent.com/CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp/v0.9.0/docs/screenshots/lava.png)

![Dream gameplay](https://raw.githubusercontent.com/CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp/v0.9.0/docs/screenshots/dream.png)

### Local scores

![Local scores](https://raw.githubusercontent.com/CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp/v0.9.0/docs/screenshots/scores.png)

## Known limitations

- Only one verified package revision is supported.
- Extract to a short writable path. Deeply nested folders can prevent Windows from starting the shader conversion tool.
- The music-spectrum visualization remains inactive.
- VSync and FPS-display controls are not available in the game's Options menu.
- More testing is needed on physical controllers, different GPUs and Windows versions, and during longer sessions. The project owner reports a successful play test on a friend's PC; a fully documented clean-machine install/reinstall and hardware matrix remain unverified.
- The original game's own save writes are not crash-atomic.

For reports, include the build version, Windows version, GPU, input device and reproduction steps. Review `Game/logs/` for personal paths before sharing excerpts. Do not attach game packages, converted assets or saves.

## Download verification

Release ZIP SHA-256:

```text
87D58CB5AC388E2F5DBAE49E198E8919AC673EFD3D574E3AB813B240BE765DD6
```

See the repository README for build instructions, licence details and third-party credits. The repository retains the name **Techno-Kitten-Adventure-XBLA-PC-Recomp**, while the implementation is a managed PC port.

If you enjoy the project, [Ko-fi tips](https://ko-fi.com/caiuscodes) are optional. The port and releases remain free.

This is an unofficial fan project, with no affiliation or endorsement from the game's owners or Microsoft.
