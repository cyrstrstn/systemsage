<div align="center">

# SystemSage

### Understand your Windows PC without installing a heavyweight monitoring suite.

[![Latest release](https://img.shields.io/github/v/release/cyrstrstn/systemsage?style=flat-square&color=b7ff3c)](https://github.com/cyrstrstn/systemsage/releases/latest)
[![Release build](https://img.shields.io/github/actions/workflow/status/cyrstrstn/systemsage/release.yml?style=flat-square&label=release)](https://github.com/cyrstrstn/systemsage/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/github/license/cyrstrstn/systemsage?style=flat-square)](LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows)](https://github.com/cyrstrstn/systemsage/releases/latest)
[![No telemetry](https://img.shields.io/badge/telemetry-none-success?style=flat-square)](#privacy-and-safety)
[![Native CLI](https://img.shields.io/badge/runtime-no%20download-required-171815?style=flat-square)](#why-systemsage)

**A tiny, native, open-source Windows diagnostic CLI with readable results, an interactive menu, and zero background services.**

Interactive launches check the latest GitHub release and offer an optional one-key update. Offline use always continues normally.

[Install](#one-command-install) · [Scripts blocked?](#if-scripts-are-blocked-executionpolicy) · [Commands](#commands) · [Build](#build-from-source) · [Releases](https://github.com/cyrstrstn/systemsage/releases)

</div>

---

## One-command install

Open **PowerShell** and paste:

```powershell
irm https://raw.githubusercontent.com/cyrstrstn/systemsage/main/scripts/irm-install.ps1 | iex
```

If your PC blocks scripts (`running scripts is disabled` / ExecutionPolicy), use this instead — still one paste, no permanent policy change:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/cyrstrstn/systemsage/main/scripts/irm-install.ps1 | iex"
```

Open a new terminal, then launch the interactive menu:

```powershell
systemsage
```

> [!NOTE]
> Installation is per-user and does not require Administrator access. SystemSage is installed under `%LOCALAPPDATA%\Programs\SystemSage` and added to your user `PATH`. The installer runs in memory and does not require changing your system ExecutionPolicy permanently.

### Update

Run the same installation command again. It downloads and installs the latest GitHub release.

### Manual installation

1. Download [`SystemSage-Windows.zip`](https://github.com/cyrstrstn/systemsage/releases/latest/download/SystemSage-Windows.zip).
2. Extract the archive.
3. From that folder in PowerShell run:
   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
   ```

## Why SystemSage?

| | SystemSage |
|---|---|
| Distribution | One native `systemsage.exe` |
| Download size | Approximately 27 KB for v1.2.0 |
| Runtime download | None |
| Background service | None |
| Accounts or cloud | None |
| Telemetry | None |
| Default behavior | Read-only diagnostics |
| License | MIT |

SystemSage uses Windows' existing .NET Framework and management interfaces. It does **not** bundle Node.js, Python, Electron, Chromium, a database, or another application runtime.

## What it checks

- Live CPU and physical memory usage, plus per-module vendor, type (DDR4/DDR5), speed, and part numbers
- Battery charge, power state, estimated runtime, and capacity health
- Connected-drive capacity and available space
- Highest-memory running processes
- Antivirus products registered with Windows Security Center
- Active TCP network connections
- Current-user and machine-wide startup entries
- Windows version, device manufacturer, model, architecture, processors, and uptime
- Recent Windows warning and error events from System and Application logs
- Physical RAM modules, graphics adapters, disk devices, and device error codes
- Temporary-file cleanup preview, with optional confirmed cleanup for files older than 7 days
- Local Outlook account identities and on-disk OST/PST/cache size (not server mailbox quota)
- The ten largest files found under the user profile
- A visual PDF health dashboard with a score, metric bars, plain-English guidance, recommendations, and a technical appendix

## Commands

| Command | Purpose |
|---|---|
| `systemsage` | Open the interactive keyboard menu |
| `systemsage doctor` | Run the complete PC overview and generate a PDF |
| `systemsage memory [--pdf] [--json]` | RAM usage, modules, vendor, DDR type, speed |
| `systemsage gpu [--pdf] [--json]` | Graphics adapters, VRAM, driver |
| `systemsage diskhealth [--pdf] [--json]` | Disk status and failure-predict when available |
| `systemsage storage [--pdf] [--json]` | Connected-drive capacity |
| `systemsage wifi [--pdf] [--json]` | SSID, signal, link rates |
| `systemsage boot [--pdf] [--json]` | Last boot, uptime, startup list |
| `systemsage updates [--pdf] [--json]` | Windows Update last success + pending count |
| `systemsage display [--pdf] [--json]` | Monitors and resolution |
| `systemsage audio [--pdf] [--json]` | Sound devices |
| `systemsage browsers [--pdf] [--json]` | Edge/Chrome/Firefox/Brave cache sizes (preview) |
| `systemsage printers [--pdf] [--json]` | Installed printers and default |
| `systemsage bluetooth [--pdf] [--json]` | Bluetooth adapter / paired devices |
| `systemsage usb [--pdf] [--json]` | USB devices |
| `systemsage firewall [--pdf] [--json]` | Firewall profile state |
| `systemsage bitlocker [--pdf] [--json]` | Volume encryption status (may need elevation) |
| `systemsage restore [--pdf] [--json]` | System restore points |
| `systemsage apps [--pdf] [--json]` | Top installed apps by estimated size |
| `systemsage thermal [--pdf] [--json]` | ACPI thermal zones when exposed |
| `systemsage proxy [--pdf] [--json]` | Proxy settings and hosts entries |
| `systemsage baseline --save` | Save metric baseline snapshot |
| `systemsage compare` | Diff current metrics vs baseline |
| `systemsage processes` | Highest-memory processes |
| `systemsage temp` | Temp files older than 7 days (preview) |
| `systemsage temp --clean --yes` | Delete unlocked old temp files |
| `systemsage outlook [--pdf]` | Outlook accounts and on-disk storage |
| `systemsage security` | Registered antivirus |
| `systemsage battery [--pdf] [--json]` | Charge, runtime, estimated health |
| `systemsage network` | Active TCP connections |
| `systemsage startup` | Startup programs |
| `systemsage system` | Windows and hardware details |
| `systemsage json` | Multi-section JSON export to Documents |
| `systemsage report` | Full PDF report |
| `systemsage --version` | Print version |
| `systemsage --help` | Show help |

The interactive menu uses arrow-key navigation and real stage-by-stage percentage progress. Redirected output is stable and animation-free for scripts, logs, and CI.

## Privacy and safety

- Every diagnostic runs locally on the current PC.
- SystemSage does not upload system information.
- There are no analytics, advertisements, accounts, or tracking identifiers.
- Nothing runs automatically when Windows starts.
- There is no resident agent or background service.
- Current diagnostic commands do not delete, repair, or modify system settings unless you explicitly run temp cleanup and confirm with `YES` / `--yes`.
- Reports remain on the user's device unless the user shares them.

## Build from source

SystemSage deliberately avoids a package manager. On Windows:

```powershell
git clone https://github.com/cyrstrstn/systemsage.git
cd systemsage
.\scripts\build-cli.ps1
```

The script uses the .NET Framework C# compiler included with supported Windows installations and produces:

```text
release/
├── SystemSage-Windows.zip
└── SystemSage/
    ├── systemsage.exe
    ├── install.ps1
    ├── LICENSE
    └── README.md
```

Run the full command smoke test:

```powershell
.\scripts\test.ps1
```

## Project structure

```text
native/SystemSageCli.cs       Application, menu, and core scans
native/MoreScans.cs           GPU, disk, Wi-Fi, boot, updates, display, audio, browsers, JSON
native/ExtraScans.cs          Printers, Bluetooth, USB, firewall, BitLocker, restore, apps, thermal, proxy, baseline
native/FullDiagnostic.cs      Full doctor collection
native/StyledPdfReport.cs     Visual PDF dashboard
scripts/build-cli.ps1         Reproducible native Windows build
scripts/install.ps1           Local and GitHub release installer
scripts/irm-install.ps1       Public one-command installation entry point
scripts/test.ps1              Command-level smoke tests
.github/workflows/release.yml Tagged-release automation
```

## Contributing

Issues and pull requests are welcome. Please keep additions aligned with the project principles:

1. Remain lightweight and dependency-free for end users.
2. Prefer Windows-native information sources.
3. Keep diagnostics read-only unless an action is explicit and safely confirmed.
4. Return useful human-readable output and stable redirected output.
5. Never add telemetry or hidden network communication.

## License

SystemSage is open source under the [MIT License](LICENSE).

<div align="center">

Built for fast, understandable Windows diagnostics.

</div>
