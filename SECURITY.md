# Security policy

The current **v0.9.0** portable release receives fixes on a best-effort basis. Third-party components retain their own security support policies.

## Report a vulnerability

Use this repository's **Security → Report a vulnerability** option to report a security issue privately. Include the build version, affected file, steps to reproduce and the likely impact. Please don't include game packages, imported assets, saves, credentials or unnecessary personal information.

For ordinary gameplay bugs or antivirus detections, use [Issues](https://github.com/CaiusCodes/Techno-Kitten-Adventure-XBLA-PC-Recomp/issues). For a detection, include the antivirus vendor, detection name and file SHA-256. Keep protection enabled while the report is investigated.

There is no guaranteed response time. Please allow the maintainer to investigate before publishing exploit details.

## Release checks

The portable release is checked for appropriate contents and scanned locally with Microsoft Defender. Results and limitations are recorded in [the security check](docs/SECURITY-CHECK.md). A clean scan is not a guarantee that every antivirus product will accept a build. The launcher is currently unsigned.
