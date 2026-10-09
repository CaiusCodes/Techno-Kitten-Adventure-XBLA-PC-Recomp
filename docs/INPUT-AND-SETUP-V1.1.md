# V1.1 installer and PC input bridge

## Controller-dependent prompts (current V0.9)

`ControllerPrompts.Sample` checks all four MonoGame gamepad slots once per rendered frame. Any connected gamepad enables the original prompts. No input binding or gameplay update body is changed. `ControllerPromptPatches.cs` runs only after the full original program hash check, and requires exact method and draw-overload signatures plus the two original flight instruction strings.

| Original member | Metadata token | Original IL offset | Bridge |
| --- | ---: | ---: | --- |
| `StageSelectMenu.DrawBackground(SpriteBatch)` | `0x060001B9` | 39 | `ControllerPrompts.DrawBackground` |
| `CatSelectMenu.DrawBackground(SpriteBatch, int)` | `0x060000D4` | 36, 94 | `ControllerPrompts.DrawBackground` |
| `Game1.DisplayInstructions()` | `0x0600001F` | 0 | `ControllerPrompts.Visible` entry guard |

The verified cat footer regions are (130,587,201,73) and (988,587,165,73) in the 1280×720 menu. Stage reconstruction is restricted to (118,575,218,100) and (980,575,182,100); circular masks of radius 44 around (166,625)/(1116,625) include the icon glows, plus tight text masks (198,608,130,31)/(992,608,91,31). Unmasked cloud pixels stay unchanged. Cat-select has transparent footer art, so disconnected rendering omits those two regions. Stage-select has opaque cloud art with the prompts baked in; two small cached RAM textures reconstruct the covered background from unchanged boundary colours using harmonic interpolation. That reconstruction approximates the obscured clouds. The original atlas is never written or replaced, and reconnecting draws it unchanged. No derived texture is included in the release or public source. All fields are resolved by exact managed names (`Global.selectStageTex`, `Global.selectCatTex`); there are no CLR memory offsets.

`tools/Test-ControllerPrompts.ps1` runs the bounded `--controller-prompts-test` rendering probe. It compares connected/disconnected/reconnected states, requires unchanged pixels outside the footer regions, and requires an empty disconnected flight-instruction layer. These synthetic connection checks do not certify physical USB/Bluetooth hot-plug behaviour; that remains a manual hardware check.

The ZIP contains one `Techno Kitten Adventure XBLA Recomp/` directory with Setup, README.txt and third-party licences. Setup embeds the asset-free runtime in a single EXE. At import time it checks the embedded manifest, relative paths, lengths and SHA-256 values, then converts the user's supported package into a staged `Game/` directory. The previous `Game/` and save are backed up before promotion. The play launcher is inside `Game/`; the MonoGame host and other DLLs remain in `Game/resources/game/`. `Game/release-manifest.json` lists only redistributed runtime files, not game-derived outputs.

The optional Extras checkbox sets `seaHigh_`, `cloudHigh_`, `lavaHigh_`, `meatHigh_` and `ronHigh_` to at least 80,000 in the staged `ScoreInfo` XML. The verified original `CatSelectMenu` gates its kittens at 40,000, 60,000 and 80,000 per stage; the full game already exposes all five stages. No game instruction or menu callback is changed for unlocking. A reinstall keeps the previous save in `backups/`.

The installer verifies the entire input program SHA-256 `12C05F08878273BB6BF9C006D379F81C81AAF07FCF32EB3AF2DCDBB1FAB6848D` before any IL edit. `PcInputPatches.cs` then requires exact declaring types, method names, parameter types and return types. Each method has exactly one return. It adds a return bridge; original controller and keyboard results are still consulted. The offsets below are original IL offsets from the V1.1.0 import report:

| Original member | Metadata token | IL offset | Bridge |
| --- | ---: | ---: | --- |
| `InputState.Update()` | `0x060001E1` | 52 | `PcInput.Sample` |
| `InputState.EndUpdate()` | `0x060001E2` | 25 | `PcInput.End` |
| `InputState.IsButtonPressed(Buttons)` | `0x060001E3` | 676 | `PcInput.Pressed` |
| `InputState.IsButtonUp(Buttons)` | `0x060001E4` | 201 | `PcInput.Up` |
| `InputState.IsButtonDown(Buttons)` | `0x060001E5` | 201 | `PcInput.Down` |
| `Menu.Update(float, InputState)` | `0x060000B8` | 311 | `PcInput.MenuHover` |
| `StageSelectMenu.Update(float, InputState, ref GameState)` | `0x060001B6` | 0 | `PcInput.StageArrows` (entry) |

Stage paging requires its bridge at entry because the original derived method checks D-pad directions before calling `Menu.Update`. It selects the edge item and emits one direction per click, consuming mouse A for that frame. Hidden arrows do not receive targets. Exact managed members are `StageSelectMenu.startingIndex`, `maxIndex` and inherited `Menu.index_`; no CLR memory offsets are used. The original atlas arrows are 73×106 pixels centred at (70,280) and (1210,280), with ±5 pixels of horizontal animation. Click rectangles are (20,219,100,122) and (1160,219,100,122) in the logical 1280×720 canvas.

Options ON/OFF targets come from the verified atlas draws at x=900/1060, y=171/234/298, with eight pixels of padding. ON emits Left; OFF emits Right, consuming mouse A to prevent a second toggle. Display and Resolution targets use the same measured text, scale and positions as `DrawValues`; their left chevron moves backward, while the value and right chevron advance. The same letterbox-aware coordinates used for ordinary menu items apply to these targets.

`tools/Test-MenuMouse.ps1` exercises actual imported menu update bodies with synthetic snapshots in a disposable fixture. The opt-in `--mouse-menu-test` host probe runs once after normal player selection. Physical pointer testing on another PC remains manual. Setup's picker uses the directory of `Application.ExecutablePath` and restores the working directory; `--test-package-picker` checks those properties when launched from a different directory.

The original `Menu.menuItems_`, `Menu.index_` and `MenuItem.CollisionRect` managed members define hover targets. There are no native structure offsets or unverified game addresses. Mouse coordinates map from the actual window client area to the game's 1280×720 logical menu, including 16:9 letterboxing. The existing Options menu modification separately verifies its atlas and item positions. Keyboard mappings are Enter to Start in menus, Esc to B in menus and Start in PLAY, WASD to D-pad directions. Left click acts as Start only in OPENING, and as A elsewhere; right click maps to B. The MonoGame cursor is visible in OPENING and menus, including pause, and hidden during PLAY. Original arrows, Space, B, S and controller input remain.

MonoGame 3.8.4.1 initially resolves XNA audio banks relative to its managed host EXE in `Game/resources/game/`. `Tka.Host` verifies and sets the runtime's private `TitleContainer.Location` to `Game/` before game initialization. This keeps converted Content beside the play launcher without duplicating or relocating game assets. The host rejects an unexpected MonoGame member signature.

Scripted import/gameplay checks and the optional-unlock save load passed locally. The installer rendered without clipping at simulated 96–288 DPI, initial and complete states. Mouse and keyboard behavior on additional physical devices remains a manual check; simulated DPI is not a clean-machine 4K test.
