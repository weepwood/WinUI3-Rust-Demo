# WinUI 3 Rust System Center

A native Windows system information and settings application built with:

- **Microsoft.UI.Reactor** for a declarative WinUI 3 interface
- **Rust** for system inventory, diagnostics, report export, and approved system commands
- **GitHub Actions** for x64 and ARM64 build artifacts and tagged releases

> Microsoft.UI.Reactor is currently experimental. This repository pins the preview package version so builds remain reproducible.

## Features

- Windows, device, CPU, memory, disk, uptime, and process information
- Manual and configurable automatic refresh
- Light, dark, and system theme modes
- Compact layout and hostname privacy controls
- Optional current-user startup registration
- Shortcuts to Windows display, storage, network, update, and privacy settings
- Rust bridge diagnostics
- JSON system report export to the user's Documents folder
- DNS cache refresh through an explicit allow-listed command
- Self-contained x64 and ARM64 release packages

## Architecture

```text
src/SystemCenter/   C# Microsoft.UI.Reactor application
src/sysbridge/      Rust command-line bridge bundled beside the app
scripts/            local packaging scripts
.github/workflows/  CI and tag-based GitHub Releases
```

The UI never forwards arbitrary shell input to Rust. The bridge accepts only the fixed commands implemented in `src/sysbridge/src/main.rs`.

## Requirements

- Windows 10 version 1809 or later
- .NET 10 SDK
- Rust stable with the MSVC toolchain
- Visual Studio Build Tools 2022/2026 with Desktop development with C++

## Run locally

```powershell
rustup target add x86_64-pc-windows-msvc
dotnet run --project .\src\SystemCenter\SystemCenter.csproj -p:Platform=x64
```

For ARM64:

```powershell
rustup target add aarch64-pc-windows-msvc
dotnet run --project .\src\SystemCenter\SystemCenter.csproj -p:Platform=ARM64
```

## Build a distributable package

```powershell
.\scripts\package.ps1 -Architecture x64
.\scripts\package.ps1 -Architecture ARM64
```

Artifacts are written to `artifacts/` as ZIP files with SHA-256 checksum files.

## Publish a GitHub Release

Push a semantic version tag. The release workflow builds both architectures and creates the release automatically.

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The workflow can also be started manually from the Actions page; manual runs upload build artifacts but do not create a tagged release.

## Safety model

- Startup registration writes only to the current user's `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run` entry and is reversible.
- Windows settings shortcuts use documented `ms-settings:` URIs.
- DNS refresh invokes only `ipconfig /flushdns`.
- No arbitrary command execution, registry editor, service control, or privilege escalation is exposed.
