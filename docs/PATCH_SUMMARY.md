# Patch Summary

## Device Page

The patch routes Creality Print hosts to the bundled Creality `resources/web/deviceMgr/index.html` page instead of the stock Klipper/Fluidd style page.

The bridge in `src/slic3r/GUI/PrinterWebView.cpp` supplies the data and callbacks normally provided by Creality Print:

- device list and current device;
- machine list;
- theme and language;
- local printer capabilities;
- file export/download callback;
- WebRTC camera negotiation;
- camera watchdog reload if the embedded page gets stuck on loading.
- injected `Logo LED` control for the Creality Hi logo light using Moonraker `SET_PIN PIN=LED`.

The printer itself is not modified.

## CFS Sync And Upload

The patch restores the Creality upload dialog path in `src/slic3r/GUI/Plater.cpp` and adds `CrealityPrintHostSendDialog` in `PrintHostDialogs.*`.

That dialog shows the CFS mapping controls before upload, and passes `colorMatch_N` values through the print host upload data.

When the printer reports an external spool holder, the send dialog also shows a `Filament Device` choice for `CFS` or `Spool Holder`. Selecting `Spool Holder` maps the print to the external slot and uses the external-spool print command path instead of CFS color matching.

`src/slic3r/Utils/CrealityPrint.cpp` has a fallback mapping path so upload can still build a CFS color list when the dialog data is missing.

## Flush Volumes

The upstream CFS issue was that Orca did not emit `flush_volumes_changed = 1`, while Creality Print does.

This patch emits:

```text
; flush_volumes_changed = 1
; flush_volumes_matrix = ...
```

whenever a flush matrix is written into the G-code config block.

For Creality Print hosts, `WipeTowerDialog.cpp` bakes the UI flush multiplier into the stored matrix and resets the multiplier to `1`. This preserves the calculated flush values while avoiding Creality firmware/device-side handling problems with sub-1 multipliers.

`GCodeViewer.cpp` also shows a `Flushed` estimate in the Preview filament table when CFS/color changes exist. If the G-code processor did not record direct flush extrusion, the preview calculates the estimate from `filament_change_count_map` and `flush_volumes_matrix` so the user can see expected flush usage even without a prime tower.

## 2.3.2 Compatibility

The patch intentionally avoids OrcaSlicer 2.4-only settings such as:

- `brim_flow_ratio`
- `combine_brims`

Those options caused startup crashes when accidentally packaged into the 2.3.2 stable patch.

## Creality Hi Startup G-code

The bundled Creality Hi 0.4 and 0.6 nozzle profiles set the first-layer bed target before `START_PRINT`:

```gcode
M140 S[bed_temperature_initial_layer_single]
M104 S0
START_PRINT EXTRUDER_TEMP=[nozzle_temperature_initial_layer] BED_TEMP=[bed_temperature_initial_layer_single]
```

This avoids the printer reporting a bed target of `0` during the early park/nozzle-heat stage.

## Installer

The installer is a single-file WinForms app that embeds the payload ZIP, backs up overwritten files, then overlays the payload with `robocopy`.

The final completion state now resets the progress bar to a standard `0..100` range and sets it to `100`, preventing the bar from appearing stuck near the end.
