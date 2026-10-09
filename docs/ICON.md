# Original arcade kitten artwork — V1.1.0

The selected Silver Explorer pixel artwork is supplied under the port's MIT license and contains no extracted game artwork. The game uses a grey 32×32 kitten face with pink ears and cyan eyes; Setup uses the matching 64×64 flying kitten with a jetpack and rainbow trail.

- Editable artwork: src/Tka.Installer/Assets/techno-kitty.svg.
- Windows icon: src/Tka.Installer/Assets/techno-kitty.ico.
- Rebuild: tools/Build-Icon.ps1, invoked automatically before the installer build.
- Sizes: 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels, with alpha transparency.
- Native bootstrap: src/InstallerLauncher/setup.rc and CMake include directory.
- Managed setup: ApplicationIcon in the project and explicit Form.Icon from the executable.

The game artwork is editable in src/Tka.Host/Assets/game-kitty.svg. Its ICO is embedded in both the native play launcher and managed host, and is applied to the live game window; see [game icon, badge and migration](BUILD-BADGE.md).

Build-Icon.ps1 reads each SVG's data-pixel-grid attribute, renders its original integer-grid paths without smoothing and scales with nearest-neighbour sampling. It also creates a 512-pixel transparent Setup illustration in out/techno-kitty-icon.png, embedded by the installer build. Setup uses the selected Sunset Explorer layout: original sunset artwork down the left, cream/pink pixel lettering, a cyan divider and the package picker on the right. Body text stays readable using the Windows-supplied Consolas font. No extra fonts, official logos, sprites, reference images or screenshots are embedded.

Source packaging explicitly allows only the two original ICO files and the original social preview as binary artwork. Generated previews stay in out/ and game assets remain excluded.
