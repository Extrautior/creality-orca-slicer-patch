# Creality Hi / CFS Patch for OrcaSlicer

Unofficial Windows x64 patch for **OrcaSlicer 2.5.0 stable**, upstream commit
`40ecc66f78ed72dc368e0c1ebe40d77f00cc8a19`.

## Download

[Download the latest patch installer](https://github.com/Extrautior/creality-orca-slicer-patch/releases/latest/download/Creality-OrcaSlicer-2.5.0-Patch-Installer.exe)

The current release is **2.5.0 revision 4**. Older 2.4 installers remain in the
[release archive](https://github.com/Extrautior/creality-orca-slicer-patch/releases).
Use the matching official OrcaSlicer version; other versions are rejected.

## Install

1. Install official **OrcaSlicer 2.5.0 stable, Windows x64** (installer or portable).
2. Close OrcaSlicer normally and run the patch installer.
3. Select the OrcaSlicer folder and choose **Install / Verify**. Approve Windows
   administrator permission when installing under Program Files.
4. Optionally check **Install Extrautior Hi printer, filament and print presets**.
   This is **unchecked by default**. The device page, CFS and flushing fixes install
   with either choice. Existing user presets are not replaced. Presets installed
   by an earlier patch are retained when this box is left unchecked.
5. Reopen OrcaSlicer. Set your printer connection to **Creality Print** and enter
   your own address. The Device tab opens the Creality page. In Prepare, use
   **Synchronize filament list from AMS/CFS** beside the Filament heading.

When selected, the optional bundle adds **Creality Hi 0.4 nozzle - Extrautior**,
four process presets (0.08/0.12/0.20/0.28 mm) and six filament presets: Duramic PLA,
Inslogic PLA PRO, Inslogic WoodFill PLA, Sunlu HS PLA+ 2.0, Generic PETG and Yasin ABS.
Addresses, credentials and personal preset metadata are excluded.

Run the same installer and choose **Restore Backup** to undo the last installation
or preset addition. Backups are stored under `%LOCALAPPDATA%\CrealityOrcaPatcher`.

## Features and validation

- Creality device page, Hi/CFS discovery and filament sync, mapping, slot controls,
  external spool selection, camera bridge, local-file export and logo-light controls.
- Exported Creality **flush_multiplier always stays 1**. The chosen factor is baked
  into exported volumes, including factors below 1. Saved project matrices stay unbaked.
- Orca 2.5 webview lifecycle and asynchronous printer-query behavior are preserved.
- Complete Creality preset bundle for Orca 2.5's `.opc` cache packaging.
- Optional public presets; existing user profiles are preserved.

The full native Windows build and real G-code exports passed. The Creality Device
page was observed on a Hi, and the running patched app read four CFS slots. Both
installer choices pass installation, payload verification, tamper rejection and
exact rollback tests. Physical feed/retract, camera video and supervised printing
still require owner validation. The patch changes no printer firmware.

The installer verifies exact supported core hashes, backs up changed files and
verifies the complete installed payload. Modified or unsupported builds are rejected.
It is not code-signed; Windows or endpoint protection may prompt for review.

## Source

- [2.5 source patch](patches/creality-orca-2.5.0.patch)
- [Build and port notes](docs/ORCA_2.5_PORT.md)
- [Revision 4 release notes](release/RELEASE_NOTES_2.5.0_r4.md)
- `installer/` contains the .NET WinForms installer and transactional patch engine.
- `scripts/package-2.5.ps1` builds and tests the release from a native Orca build.
