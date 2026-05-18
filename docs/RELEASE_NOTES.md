# Release Notes

## v2.3.2-creality-cfs-20260516

Final OrcaSlicer 2.3.2 stable patch package.

### Fixed

- Creality device page opens inside OrcaSlicer instead of the Klipper/Fluidd page.
- Device page jumps to the configured current printer.
- Camera/WebRTC offer handling matches the bundled Creality device page.
- Camera watchdog reloads the local device page if the embedded WebView gets stuck on loading.
- Device page file export bridge works through the Orca WebView message bridge.
- Device page includes a separate `Logo LED` toggle for the Creality Hi logo light.
- CFS sync button and CFS filament mapping dialog are present.
- Upload dialog includes the Creality CFS mapping and calibration option path.
- G-code emits `flush_volumes_changed = 1` before `flush_volumes_matrix`.
- Creality flush multiplier changes are baked into the matrix while generated multiplier stays `1`.
- Installer progress bar reaches 100 percent when installation completes.

### Known Scope

- Built for OrcaSlicer 2.3.2 stable on Windows x64.
- Not a full OrcaSlicer fork.
- Not tested against OrcaSlicer 2.4 development builds.
