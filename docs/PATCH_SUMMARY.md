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

The printer itself is not modified.

## CFS Sync And Upload

The patch restores the Creality upload dialog path in `src/slic3r/GUI/Plater.cpp` and adds `CrealityPrintHostSendDialog` in `PrintHostDialogs.*`.

That dialog shows the CFS mapping controls before upload, and passes `colorMatch_N` values through the print host upload data.

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

## 2.3.2 Compatibility

The patch intentionally avoids OrcaSlicer 2.4-only settings such as:

- `brim_flow_ratio`
- `combine_brims`

Those options caused startup crashes when accidentally packaged into the 2.3.2 stable patch.

## Installer

The installer is a single-file WinForms app that embeds the payload ZIP, backs up overwritten files, then overlays the payload with `robocopy`.

The final completion state now resets the progress bar to a standard `0..100` range and sets it to `100`, preventing the bar from appearing stuck near the end.
