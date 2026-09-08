# SystemSage: Memory, Temp Cleanup, Outlook

Date: 2026-09-08  
Status: approved-for-build (user chose D, lightweight console + optional PDF)  
Principles: no new deps, no telemetry, read-only by default, explicit confirm for deletes, fast WMI/filesystem only.

## Goal

Ship three features in one contribution line, phased A → B → C, without bloating the ~27 KB native CLI.

## Phase A — Memory command

### Commands / menu

- `systemsage memory` — console report
- `systemsage memory --pdf` — same data + small PDF under `Documents\SystemSage Reports`
- Interactive menu item: **Memory modules**
- Optional interactive prompt: `P` save PDF (same as `--pdf`)

### Data (local WMI only)

**Summary**

- Used / total RAM (GB + %)
- Module count
- Combined capacity

**Per-module table**

| Column | Source |
|--------|--------|
| Slot | `Win32_PhysicalMemory.DeviceLocator` |
| Vendor | `Manufacturer` (fallback "Unknown") |
| Capacity | `Capacity` |
| Type | Map `SMBIOSMemoryType` (prefer) else `MemoryType` → DDR3/DDR4/DDR5/… |
| Speed | `ConfiguredClockSpeed` if present else `Speed` (MHz) |
| Part number | `PartNumber` |

Reuse `Scan.Memory()` for live usage. Enrich doctor PDF “Physical memory” section with Type column (same mapper). No separate always-on PDF for other checks in this PR set (optional PDF pattern can extend later).

### Out of scope (A)

- Per-process deep memory graphs
- Changing pagefile / standby list

## Phase B — Temp cleanup

### Commands / menu

- `systemsage temp` — preview only (default)
- `systemsage temp --clean` — requires second confirm
- Menu: **Temporary files** → show preview → ask `C` to clean, then type `YES`

### Behavior

- Targets (same as doctor preview): user `%TEMP%`, Windows `\Temp` (best-effort)
- Default age: files older than **7 days** (match existing preview)
- Skip locked files; count skipped; never fail whole run on one file
- Print before/after recoverable size
- Redirected/non-interactive: `--clean` alone is **not** enough — require `--clean --yes` so scripts stay safe
- No recursion into unrelated folders; no browser cache in v1

### Out of scope (B)

- Disk Cleanup COM / cleanmgr profiles
- Recycle Bin empty

## Phase C — Outlook storage (read-only)

### Commands / menu

- `systemsage outlook`
- Menu: **Outlook accounts & storage**
- Optional `--pdf`

### Data (filesystem + HKCU only; no Graph, no mail bodies)

1. Detect classic vs new Outlook by known folders under `%LOCALAPPDATA%` / `%USERPROFILE%`
2. Account identities from Outlook registry profiles when readable (email display names only)
3. OST/PST (and new-Outlook local store if found): path, size, last write
4. Totals: file count + bytes used on disk

If Outlook not installed / folders missing → clear “Outlook not found” message, exit 0.

### Never

- Message contents, credentials, tokens, password fields
- Network calls to Microsoft

## Shared mechanics

- Wire commands in `App.Command`, menu labels, `Help`, `scripts/test.ps1`
- Keep single-file / few-file edits in `native/` following existing `Scan` + `Ui` patterns
- PDF: reuse lightweight text PDF path (`PdfReport` / thin wrapper) for section reports — avoid new dependencies
- Version bump only if release workflow expects it (follow repo convention)

## Success criteria

1. `systemsage memory` shows usage + module vendor/type/GB in &lt;2s typical
2. `systemsage temp` never deletes without confirm / `--yes`
3. `systemsage outlook` shows accounts/sizes or clean unavailable — no crash
4. `.\scripts\test.ps1` passes with new commands added
5. Binary stays dependency-free; no telemetry added

## Implementation order

1. A — Scan helpers + command + menu + tests  
2. B — preview command + confirmed clean  
3. C — registry/path probe + command  
4. README command table update  
5. One PR from fork `FutureVisionMobDev/systemsage` → `cyrstrstn/systemsage`
