# Security

Report a vulnerability privately with **Report a vulnerability** on the [Security tab](https://github.com/hazeliscoding/gil-sweep/security), not in a public issue. Fixes go into the latest release.

Gil Sweep has no accounts or servers. It runs on your PC and keeps its data in `%AppData%\GilSweep`. Its only network calls are:

- Universalis, for market data, and Saddlebag Exchange when it's turned on in Settings
- XIVAPI and Garland Tools, to check an item you add in Settings
- GitHub, for the update check

Releases are code-signed, and each one lists SHA-256 checksums in `SHA256SUMS`.
