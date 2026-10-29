# Creality Hi / CFS Patch for OrcaSlicer

This repository packages an unofficial Windows compatibility patch that adds the Creality Hi, CFS, and Creality device-page workflow to OrcaSlicer.

## Current Release

The published installer below is for **2.4.2 stable**. The **2.5.0-beta source port**
is in [PR #1](https://github.com/Extrautior/creality-orca-slicer-patch/pull/1).
It targets official commit `345efec64c7b95a05ce5cf96df0dde66a158835c`.
Orca 2.5 is currently a prerelease; it is a separate target, not an update accepted by the 2.4.2 installer.

The 2.5 port retains the device page and CFS controls while adapting lazy webview recreation,
filament-list refresh, Creality discovery, timelapse Off, and Default/Fast flush-multiplier export.
See [the 2.5 build guide](docs/ORCA_2.5_PORT.md) for build status, packaging, and the remaining runtime checks.
Do not install the old binaries into 2.5 or interpret patch application as proof of runtime compatibility.

- Target: OrcaSlicer `2.4.2` Windows x64
- Exact upstream commit: `8500fcdccaa10b5099ac20d252af3a7c560046f1`
- Patch revision: `creality-cfs-orca-2.4.2-r1-20260709`

Download the installer from the latest GitHub release:

`Creality-OrcaSlicer-2.4.2-Patch-Installer.exe`

The installer validates the real OrcaSlicer executable and DLL hashes. It will not patch another version, a development build, or a partially modified installation.

## Features

- Creality device page inside OrcaSlicer.
- Creality Hi and CFS discovery, mapping, sync, edit, feed, and retract support.
- External spool-holder selection.
- Creality camera/WebRTC bridge and camera recovery.
- Local-file export and logo-light controls.
- Creality print metadata, layer/time markers, and flush-matrix handling.
- Corrected Creality Hi 0.4 mm and 0.6 mm profiles.
- Compatibility with OrcaSlicer 2.4's printer web-handler architecture.
- Preservation of OrcaSlicer 2.4.2 webview behavior while applying the Creality bridge.
- Timelapse control remains visible for Creality/time-lapse-capable printers and includes an **Off** option.
- Timelapse **Off** persists on reload in OrcaSlicer 2.4.2.

## Install

1. Install the official OrcaSlicer `2.4.2` build.
2. Close OrcaSlicer.
3. Run the patch installer.
4. Confirm the OrcaSlicer folder, normally `C:\Program Files\OrcaSlicer`.
5. Select **Install / Verify**.

The installer creates a rollback backup under:

```text
%LOCALAPPDATA%\CrealityOrcaPatcher\Backups\2.4.2
```

Run the same installer and select **Restore Backup** to undo the patch.

## Safety

- Embedded payload files are SHA-256 verified before installation.
- Core OrcaSlicer binaries must match the supported clean 2.4.2 release.
- Every overwritten file is backed up.
- Copies are written and verified before replacing their targets.
- The completed installation is verified against the payload manifest.
- Rollback restores files byte-for-byte and removes newly introduced files.
- Rollback refuses to overwrite an OrcaSlicer build changed after patching.
- A stale marker from the older 2.3.2 patch is ignored for version detection.

## Repository Layout

- `patches/creality-orca-2.4.2.patch` - current source patch.
- `patches/creality-orca-2.5.0-beta.patch` - 2.5 beta source port, pending native build/runtime verification.
- `patches/creality-orca-2.4.0-beta.patch` - previous 2.4 beta patch.
- `patches/creality-orca-2.3.2.patch` - previous stable patch.
- `installer/` - .NET 8 WinForms installer and transactional patch engine.
- `docs/BUILD_AND_UPDATE.md` - build and packaging guide.
- `docs/PATCH_SUMMARY.md` - implementation overview.
- `docs/payload-manifest-2.4.2.json` - current release payload hashes.
- `release/` - release notes and checksums.

## Scope

The patch does not modify printer firmware or files stored on the printer. It is not code-signed, so Windows SmartScreen may show an unknown-publisher warning.
