# Creality OrcaSlicer 2.3.2 Patch

This repository packages the Creality Hi / CFS support patch for the stable OrcaSlicer 2.3.2 Windows install.

It is meant to patch a normal OrcaSlicer install in place.

## What This Adds

- Creality Print style Device page inside OrcaSlicer.
- Direct jump to the current Creality printer when one printer is configured.
- Creality CFS filament sync and CFS-aware upload dialog.
- Creality CFS filament mapping during print upload.
- Camera/WebRTC bridge fixes for the embedded Creality device page.
- Local file export bridge for the device page.
- CFS flush volume compatibility:
  - emits `flush_volumes_changed = 1`;
  - bakes Creality flush multiplier changes into `flush_volumes_matrix`;
  - keeps generated Creality G-code multiplier at `1`.

## Download

Use the installer from the latest GitHub release:

`Creality-OrcaSlicer-2.3.2-Patch-Installer-FINAL-20260516-233148.exe`

The checksum is stored in `release/SHA256SUMS.txt`.

## Install

1. Install OrcaSlicer 2.3.2 stable normally.
2. Close OrcaSlicer.
3. Run the patch installer as administrator.
4. Point it at the OrcaSlicer install directory, usually:

```text
C:\Program Files\OrcaSlicer
```

5. Click `Install Patch`.

The installer backs up overwritten files under:

```text
%LOCALAPPDATA%\CrealityOrcaPatcher\Backups
```

## Repository Layout

- `patches/creality-orca-2.3.2.patch` - source patch against OrcaSlicer 2.3.2.
- `installer/` - WinForms wrapper source for the single-file installer.
- `docs/BUILD_AND_UPDATE.md` - how to rebuild or update this for a newer OrcaSlicer.
- `docs/PATCH_SUMMARY.md` - overview of the important code areas.
- `docs/payload-manifest.txt` - payload files overlaid into OrcaSlicer.
- `release/` - SHA256 checksum for the release installer.

## Notes

This is an unofficial compatibility patch. It is focused on the Creality Hi and CFS workflow that was missing from stock OrcaSlicer.
