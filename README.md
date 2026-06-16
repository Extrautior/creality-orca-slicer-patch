# Creality Hi / CFS Patch for OrcaSlicer

This repository packages an unofficial Windows compatibility patch that adds the Creality Hi, CFS, and Creality device-page workflow to OrcaSlicer.

## Current Release

- Target: OrcaSlicer `2.4.0-beta` Windows x64
- Exact upstream commit: `fc9a8aa93f7d341c3028d275781d77d2f385023e`
- Patch revision: `creality-cfs-orca-2.4.0-beta-r2-20260616`

Download the installer from the latest GitHub release:

`Creality-OrcaSlicer-2.4.0-beta-Patch-Installer.exe`

The installer validates the real OrcaSlicer executable and DLL hashes. It will not patch another beta, a development build, or a partially modified installation.

## Features

- Creality device page inside OrcaSlicer.
- Creality Hi and CFS discovery, mapping, sync, edit, feed, and retract support.
- External spool-holder selection.
- Creality camera/WebRTC bridge and camera recovery.
- Local-file export and logo-light controls.
- Creality print metadata, layer/time markers, and flush-matrix handling.
- Corrected Creality Hi 0.4 mm and 0.6 mm profiles.
- Compatibility with OrcaSlicer 2.4's printer web-handler architecture.
- Preservation of OrcaSlicer 2.4 beta webview behavior while applying the Creality bridge.

## Install

1. Install the official OrcaSlicer `2.4.0-beta` build.
2. Close OrcaSlicer.
3. Run the patch installer.
4. Confirm the OrcaSlicer folder, normally `C:\Program Files\OrcaSlicer`.
5. Select **Install / Verify**.

The installer creates a rollback backup under:

```text
%LOCALAPPDATA%\CrealityOrcaPatcher\Backups\2.4.0-beta
```

Run the same installer and select **Restore Backup** to undo the patch.

## Safety

- Embedded payload files are SHA-256 verified before installation.
- Core OrcaSlicer binaries must match the supported clean beta.
- Every overwritten file is backed up.
- Copies are written and verified before replacing their targets.
- The completed installation is verified against the payload manifest.
- Rollback restores files byte-for-byte and removes newly introduced files.
- Rollback refuses to overwrite an OrcaSlicer build changed after patching.
- A stale marker from the older 2.3.2 patch is ignored for version detection.

## Repository Layout

- `patches/creality-orca-2.4.0-beta.patch` - current source patch.
- `patches/creality-orca-2.3.2.patch` - previous stable patch.
- `installer/` - .NET 8 WinForms installer and transactional patch engine.
- `docs/BUILD_AND_UPDATE.md` - build and packaging guide.
- `docs/PATCH_SUMMARY.md` - implementation overview.
- `docs/payload-manifest-2.4.0-beta.json` - release payload hashes.
- `release/` - release notes and checksums.

## Scope

The patch does not modify printer firmware or files stored on the printer. It is not code-signed, so Windows SmartScreen may show an unknown-publisher warning.
