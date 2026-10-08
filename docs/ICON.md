# Setup icon — V1.0.2

Original vector kitty artwork matches the setup's pink/cyan/rainbow palette. It is supplied under the port's MIT license and contains no original game artwork.

- Editable artwork: src/Tka.Installer/Assets/techno-kitty.svg.
- Windows icon: src/Tka.Installer/Assets/techno-kitty.ico.
- Rebuild: tools/Build-Icon.ps1, invoked automatically before the installer build.
- Sizes: 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels, with alpha transparency.
- Native bootstrap: src/InstallerLauncher/setup.rc and CMake include directory.
- Managed setup: ApplicationIcon in the project and explicit Form.Icon from the executable.

In V1.0.2 the native play launcher was unchanged. V1.0.3 adds a separate simpler
cat face for the game; see [game icon, badge and migration](BUILD-BADGE.md). Source
packaging explicitly allows only these two original ICO files as binary artwork.
Game assets remain excluded.

Design brief: original happy techno kitty, bold white face, midnight violet silhouette, pink/cyan ears and whiskers, cyan eyes, rainbow arch and small sparkle; no text. Built-in image generation was attempted but returned a usage-limit error. Final artwork was created directly as vector paths and rendered with the reproducible local script; no API/CLI fallback was used.
