# Creality Hi / CFS Patch for OrcaSlicer 2.5.0 — revision 4

Supports official OrcaSlicer 2.5.0 stable Windows x64, both signed installer and
portable ZIP builds. Close Orca normally before running the patcher.

## Optional profiles

The new **Install Extrautior Hi printer, filament and print presets** checkbox is
unchecked by default. The Creality Device page, CFS controls/sync and flushing fix
install either way. Check it to add the public Hi 0.4 mm printer, four process
presets and six filament presets. No connection address or credentials are bundled.
Existing user presets are untouched. Earlier installed Extrautior presets are
retained; leaving the box unchecked does not remove them. Profiles can also be
added later by running the installer again with the box checked. Each update has
its own rollback backup.

## Fixes included

- Accept exact official signed-installer and portable core hash pairs.
- Ship the complete Creality vendor bundle, avoiding incomplete plaintext indexes
  that hide the official `.opc` cache and dependent custom presets.
- Use separate complete vendor indexes/defaults for each profile choice. Version
  02.03.02.91 is patch-only; 02.03.02.92 includes the optional profiles.
- Keep exported Creality `flush_multiplier` at 1 while baking chosen factors into
  volumes once, including factors below 1; preserve raw project values.

## Verified

Full local Windows native build; real G-code export at factors 0, 0.35, 0.7, 1,
1.3 and 2; installed Creality Device page on a Hi; four loaded CFS slots parsed by
the running app; complete profile indexes for both installer choices; installation,
hashes, idempotence, tamper rejection, exact rollback and existing-profile preservation.

Physical feed/retract, camera video and supervised printing remain hardware checks.
The owner's saved No sparse layers option is preserved and requires appropriate
model/tower placement. The installer is unsigned; review OS/security prompts.
