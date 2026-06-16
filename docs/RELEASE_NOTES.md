# Release Notes

## v2.4.0-beta-creality-cfs-r3-20260616

Timelapse visibility and disable-control fix for the OrcaSlicer 2.4.0-beta Creality Hi / CFS patch.

### Fixed

- Show the **Timelapse** row for printers that provide `time_lapse_gcode`, including Creality Hi profiles.
- Add an explicit **Off** value to the OrcaSlicer 2.4 beta Timelapse setting.
- Suppress Creality/non-BBL timelapse G-code emission when Timelapse is set to **Off**.

### Preserved

- All previous Creality Hi / CFS workflow fixes from r1 and r2.
- OrcaSlicer 2.4 beta printer web-handler and webview behavior.

## v2.4.0-beta-creality-cfs-r2-20260616

Feature-preservation refresh for the OrcaSlicer 2.4.0-beta Creality Hi / CFS patch.

### Fixed

- Restored OrcaSlicer's upstream 2.4 beta webview resize workaround in the patched printer webview path.
- Re-applied that workaround after the Creality page resets injected scripts, so the Creality bridge no longer drops upstream webview behavior.

### Checked

- The visible OrcaSlicer 2.4 beta timelapse settings remain present in the patched source tree.
- The Creality device-page bundle still includes timelapse/performance/camera strings and controls from the bundled page.
- Source patch applies cleanly to the exact upstream beta commit `fc9a8aa9`.

## v2.4.0-beta-creality-cfs-r1-20260612

First Creality Hi / CFS patch for OrcaSlicer 2.4.0-beta.

### Added

- Port of the working 2.3.2 Creality device page and CFS workflow.
- Compatibility with OrcaSlicer's new 2.4 printer web-handler implementation.
- Creality Hi/CFS mapping, sync, material edit, feed, and retract support.
- External spool-holder selection.
- Camera/WebRTC, local-file export, and logo-light bridges.
- Creality layer/time and flush metadata.
- Corrected 0.4 mm and 0.6 mm Hi profiles.

### Installer Safety

- Exact clean-beta executable and DLL verification.
- SHA-256 verification of all 207 payload files.
- Transactional backup and post-install verification.
- Byte-for-byte rollback.
- Stale 2.3.2 marker handling.
- Unsupported-build rejection.
- Rollback protection after an OrcaSlicer update.

### Validation

- Full Windows Release build succeeded.
- Source patch applies cleanly to the upstream beta tag.
- Patched OrcaSlicer launched and remained responsive.
- Install and rollback were tested against a disposable copy.

Physical printer/CFS printing was not performed as part of the automated validation.

## Previous Release

The OrcaSlicer 2.3.2 stable patch remains available in the earlier tagged releases and in `patches/creality-orca-2.3.2.patch`.
