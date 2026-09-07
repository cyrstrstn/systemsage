# SystemSage

SystemSage is a tiny, open-source Windows diagnostic CLI. It turns useful PC health data into readable terminal summaries without installing Node.js, Python, Electron, a database, a background service, or a telemetry agent.

## Install from GitHub

After this repository has a tagged release, install it with one command:

```powershell
irm https://raw.githubusercontent.com/cyrstrstn/systemsage/main/scripts/irm-install.ps1 | iex
```

Open a new terminal and run:

```powershell
systemsage
```

The installer downloads the latest `SystemSage-Windows.zip` release, installs it per-user under `%LOCALAPPDATA%\Programs\SystemSage`, and adds that directory to the user PATH. Administrator permission is not required.

## Commands

```text
systemsage                 Interactive keyboard menu
systemsage doctor          CPU, memory, battery, and drive overview
systemsage processes       Highest-memory processes
systemsage storage         Connected-drive capacity
systemsage security        Antivirus registered with Windows
systemsage battery         Charge, power state, runtime, and health
systemsage network         Active TCP connections
systemsage startup         Current-user and machine startup entries
systemsage system          Windows, hardware, architecture, and uptime
systemsage report          Save a readable text report to Documents
systemsage --version       Print the installed version
```

Interactive operations have an animated progress indicator. Redirected output is stable and animation-free for scripts and CI.

## Build

SystemSage uses the .NET Framework compiler included with supported Windows installations:

```powershell
.\scripts\build-cli.ps1
```

The release is created at `release\SystemSage-Windows.zip`. It contains one native console executable, the MIT license, this README, and the local installer.

## Release

Push a tag such as `v1.0.0`. The included GitHub Actions workflow builds the executable and publishes the ZIP to a GitHub Release automatically.

## Privacy and safety

- All checks run locally.
- No telemetry, analytics, accounts, or network uploads.
- No always-running background process or service.
- Diagnostic commands are read-only.
- Source code is available under the MIT License.
