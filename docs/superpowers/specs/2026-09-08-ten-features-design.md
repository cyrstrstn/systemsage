# SystemSage: Ten diagnostic extensions

Date: 2026-09-08  
Scope: GPU, disk health, Wi‑Fi, boot, Windows Update, display, audio, browser cache, per-check PDF, JSON export.  
Constraints: no NuGet, no telemetry, read-only except existing confirmed temp clean, fast local APIs only.

## Commands

| Command | Data source |
|---------|-------------|
| `gpu` | `Win32_VideoController` (+ note if GPU temp unavailable) |
| `diskhealth` | `Win32_DiskDrive` + `MSStorageDriver_FailurePredictStatus` when present |
| `wifi` | `netsh wlan show interfaces` parse (fallback adapter list) |
| `boot` | OS last boot / uptime + startup entries |
| `updates` | Windows Update COM session + AU registry last success |
| `display` | `System.Windows.Forms.Screen` + WMI monitor names when available |
| `audio` | `Win32_SoundDevice` |
| `browsers` | Edge/Chrome/Firefox cache folder sizes (preview only) |
| `storage\|battery --pdf` | Same optional PDF pattern as memory |
| `--json` / `json` | Machine-readable dump of key scans OR per-command `--json` |

## Menu

Add items; keep Exit last. Pad labels consistently.

## Success

`scripts/test.ps1` covers new commands; binary stays dependency-free.
