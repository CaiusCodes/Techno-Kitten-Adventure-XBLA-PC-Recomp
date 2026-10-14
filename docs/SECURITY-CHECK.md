# Release security check — 14 October 2026

**Microsoft Defender reported no threats** in the clean portable release folder and the player ZIP. This is evidence for this build and scanner state, not a guarantee that every antivirus will accept it.

- Project version: `0.9.0`.
- Player ZIP: `Techno-Kitten-Adventure-XBLA-PC-Recomp-v0.9.0.zip`.
- Published ZIP SHA-256: `5EEA493CD7F36F82FBA5692B619145EEEC7009E2C4878E541FE4A72798B2FC99`.
- Defender engine: `1.1.26080.3`.
- Defender security intelligence: `1.459.691.0`.
- Defender platform: `4.18.26080.4-0`.
- Defender status: Normal; antivirus and real-time protection enabled.
- Custom scans of the release directory and exact ZIP returned exit code 0 and explicitly reported no threats.
- Scans used `-DisableRemediation`; no exclusion, protection setting or quarantine configuration was changed.
- The project-owned root launcher is **NotSigned**. No trusted code-signing identity was applied.

The source review found conventional native/managed launchers and runtime files, package hashes and runtime-manifest validation, bounded local discovery, data-only staged imports, opt-in desktop shortcuts and portable settings. The application does not implement a game downloader, autorun registry entry, service installation, process injection, packed executable payload or obfuscation. Generic network libraries included by the self-contained .NET runtime do not imply an application downloader. The original package is read-only and not distributed.

Runtime, gameplay, migration, cancellation/rollback, save preservation, relocation and effective-DPI checks are recorded in [PORTABLE-LAUNCHER.md](PORTABLE-LAUNCHER.md). The release archive is separately checked for allowed files, all file hashes and absence of original game data and development artifacts. These reviews are limited checks, not a penetration test or complete dependency-vulnerability assessment.

## Antivirus detections and Windows warnings

An unsigned or newly released app can still produce a reputation warning. SmartScreen evaluates file and publisher reputation; even code signing does not promise that a new build will avoid warnings. See [Microsoft's SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

No VirusTotal/multiple-engine scan or clean-machine SmartScreen reputation test was performed. No malware-free certification is claimed. If a product reports a named threat, keep protection enabled and report the detection name, vendor, file path, build version and SHA-256. Do not assume a detection is a false positive solely because a local scan was clean, and do not attach game packages or imported assets.

Scan method: [Microsoft Defender command-line documentation](https://learn.microsoft.com/en-us/defender-endpoint/command-line-arguments-microsoft-defender-antivirus).
