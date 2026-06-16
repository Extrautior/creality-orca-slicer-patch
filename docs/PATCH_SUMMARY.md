# Patch Summary

## OrcaSlicer 2.4 Port

The current patch is based on OrcaSlicer `2.4.0-beta` commit `fc9a8aa9`. It preserves the beta's new printer web-handler abstraction while routing Creality printers through the local Creality device-page bridge.

Revision `r2-20260616` also restores the upstream 2.4 beta webview resize workaround that was accidentally dropped during the Creality bridge port.

Revision `r3-20260616` keeps the beta Timelapse option visible for Creality/time-lapse-capable printers, adds an explicit `Off` value, and gates Creality/non-BBL timelapse G-code emission on that setting.

## Device Page

`PrinterWebView.*` supplies the local Creality page with:

- device and machine metadata;
- theme and language state;
- printer capabilities;
- local-file export and download callbacks;
- WebRTC camera negotiation and watchdog recovery;
- Creality Hi logo-light controls.

Non-Creality messages continue through OrcaSlicer's 2.4 printer-specific web handlers.

## CFS

`CrealityPrint.*`, `Plater.*`, and `PrintHostDialogs.*` provide:

- robust CFS slot queries;
- color/material mapping;
- CFS sync and material editing;
- filament feed and retract operations;
- external spool-holder selection;
- upload and start-print metadata.

Supported Creality model codes include `F008`, `F012`, `F018`, `F021`, and `F022`.

## G-code And Flush Handling

The patch adds Creality-compatible layer and elapsed-time markers, flush metadata, and matrix handling. For Creality hosts, selected flush multipliers are baked into the emitted matrix while the generated multiplier remains compatible with the printer.

The Preview filament table includes a `Flushed` estimate for color changes.

## Profiles

The Creality Hi 0.4 mm and 0.6 mm profiles include:

- startup bed target before `START_PRINT`;
- prime and flush-box values;
- default flush multiplier;
- multicolor method metadata.

The upstream beta's hard-coded private print-host URL was removed from both profiles.

## Installer

The .NET 8 WinForms installer uses `PatchEngine.cs` for:

- exact clean-core hash validation;
- embedded payload validation;
- transactional backup and atomic replacement;
- post-install hash verification;
- byte-for-byte rollback;
- stale-marker handling;
- rollback protection when core binaries changed after installation.
