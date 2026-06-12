# Release Notes

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
