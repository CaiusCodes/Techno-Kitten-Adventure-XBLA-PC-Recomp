# V1.0.1 — setup DPI and VSync

The installer previously mixed WinForms automatic scaling with custom painting scaled directly by DeviceDpi. At higher Windows scaling, painted cards and icons could grow while control positions stayed at their design coordinates, causing the reported overlapping text and clipped rainbow.

InstallerForm now owns one 800x560 logical layout. Automatic control scaling is disabled for this form; control bounds, pixel-unit fonts, button shapes and custom painting all use the same DPI factor. The process uses PerMonitorV2 awareness; OnLoad and OnDpiChanged apply the layout from the original coordinates, never by accumulating scale multiplications. Progress is stored as a percentage and resized with its track. Fonts are cached for the form lifetime because WinForms can retain a previous equal Font instance. Header text boxes have enough height for complete line metrics.

The bounded `--preview-dpi DPI output.png [complete]` mode renders actual WinForms controls. Each invocation exercises 96 -> 192 -> 144 -> 96 -> requested DPI, checking label text height and client containment. `tools/Test-InstallerDpi.ps1` covers 100%, 125%, 150%, 175%, 200%, 250% and 300%, initial and completed states. These are simulated effective-DPI layout checks on the local desktop, not a physical 4K/mixed-DPI monitor test. A friend's hardware retest remains useful.

PcDisplay.Configure explicitly sets SynchronizeWithVerticalRetrace=false before every ApplyChanges. Original Game1 already requested false, but the PC-owned path now makes the policy explicit across startup and mode changes. The display GPU probe requires PresentInterval.Immediate in every mode. Internal 2x rendering and 60 game updates per second are preserved. Borderless Windows composition or driver overrides can still affect final presentation pacing; VSync off does not change the game's update rate or guarantee uncapped FPS.

No game IL bridges, conversion logic, package acceptance, import transaction or runtime DLL placement changed. Release remains asset-free and port code remains MIT-licensed.
