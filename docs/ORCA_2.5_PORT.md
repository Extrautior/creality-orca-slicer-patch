# OrcaSlicer 2.5.0 port

Target: Windows x64 official **v2.5.0**, commit
`40ecc66f78ed72dc368e0c1ebe40d77f00cc8a19`.
This stable release was published November 7, 2026.
The installer accepts the exact Windows x64 stable core hashes from both the
portable ZIP and the installed release. These releases carry different signature
bytes: the installed executable/DLL have valid ORCASLICER PTE. LTD. signatures
and match the verified portable program bytes exactly after excluding only the
PE checksum, security-directory entry and certificate table. Both complete core
hash pairs are pinned; arbitrary changes and mixed pairs remain rejected.

## Compatibility changes

- Package the complete Creality vendor tree with the plaintext index. Official
  2.5 installers ship `.opc` caches; a partial plaintext override hides that cache
  and prevents Creality and dependent user presets from loading. Bundle version
  02.03.02.90 refreshes the earlier incomplete cache. Packaging verifies all
  1,379 index-referenced JSON files before building the installer.

- Keep 2.5's `LazyInstance<PrinterWebView>`, browser recreation, script-handler
  tracking and translated missing-connection page. Reinstall the JSON bridge on
  recreation and bind the download navigation handler on each new browser.
- Use `Sidebar::update_dynamic_filament_list()` after CFS sync so both physical
  filament choices and 2.5's mixed-filament choices update.
- Add F018/Hi to the shared upstream Creality capability/display table, preserving
  LAN discovery, K1 TEXT-mode frames, heartbeat acknowledgements and per-model
  `/usr/data` versus `/mnt/UDISK` print paths.
- Preserve beta asynchronous WebSocket connect/read/write/close deadlines and the
  batched start-command builder. Slot operations use that transport too.
- Keep beta Device-tab deduplication while identifying different Creality hosts
  even though they share the same local page URL. Propagate theme changes without a reload.
- Carry CFS slot parsing, material editing/reset/feed/retract, the external spool
  option, upload mapping, camera recovery and logo light control from 2.4.2.
- Keep the 2.5 timelapse generator and farthest-point implementation. Guard its
  shared generator with the patch's Off setting, including migration of `2`.
- Leave raw flush volumes and Default/Fast multipliers independent in project
  settings. Apply the active multiplier at G-code export and normalize both
  exported multiplier arrays to 1 for Creality hosts and Hi profiles, including
  offline file exports. Values below 1 are baked into the volumes; the G-code
  `flush_multiplier` always remains 1. The 2.4 dialog-time bake
  is deliberately absent: it would mix Default and Fast purge settings.
- Apply the same active multiplier in the fallback Flushed preview estimate;
  leave recorded flush statistics and unrelated printers' preview behavior intact.
- Preserve upstream profile changes while carrying the Hi 0.4/0.6 startup,
  flush-box and multicolor metadata corrections.

## Build

Use a clean checkout with Visual Studio 2022 C++ tools, CMake and native Windows Perl.
The Windows build script changed in 2.5; use `build_win.bat`, not the old release script.

```powershell
git clone --branch v2.5.0 --depth 1 https://github.com/OrcaSlicer/OrcaSlicer.git orca
./scripts/prepare-2.5.ps1 -OrcaSource orca -WorkDirectory build/downloads
Push-Location orca
./build_win.bat -d --arch x64 --msvc --msbuild --vs 2022 -j 3
./build_win.bat -s -i --arch x64 --msvc --ninja -j 4
Pop-Location
./scripts/package-2.5.ps1 -BuiltDirectory orca/build/OrcaSlicer -WorkDirectory build/package
```

Use fresh source/package work directories. Check each native build command's exit
code before continuing. Dependencies can take substantial time on a cold build.
Keep paths short; a short drive alias may be needed locally.

Preparation verifies the source commit, patch application and all inherited web
asset hashes. Web assets come from the existing pinned release archive; none of
its core binaries or old machine profiles are copied.

The patch disables the shallow wxWidgets clone so it can still retrieve the
exact pinned revision if the upstream branch moves.

Packaging verifies the official 2.5 portable archive's SHA-256, derives the exact
clean executable/DLL hashes, includes freshly built native DLLs, generates the
payload manifest, builds the installer, and tests a disposable installation.
Installer branding is derived from the manifest target; the default project
properties still build a 2.4.2 installer for existing payloads.

The Windows workflow uploads a build artifact only after packaging tests pass.
It does not publish a GitHub release or claim printer hardware validation.

## Bundled public presets

Select **Creality Hi 0.4 nozzle - Extrautior** for the owner's saved printer tuning.
The bundle includes 0.08, 0.12, 0.20 and 0.28 mm print presets and six filament presets:
Duramic PLA, Inslogic PLA PRO, Inslogic WoodFill PLA, Sunlu HS PLA+ 2.0,
Generic PETG and Yasin ABS. Defaults select the custom 0.20 mm process and Sunlu.
The ABS preset declares ABS as its material type; the saved calibration values remain.
The printer's single junction-deviation limit is repeated for normal and silent modes.

Addresses, URLs, credentials, account identifiers and personal preset metadata are
excluded. System preset IDs are generated with Orca's profile tool. The printer uses
the Creality Print host type with no saved address.

The saved **No sparse layers** option is preserved. Orca's generic validation cube
does not satisfy its tower-clearance rule: 1,329 of 1,330 slicing cases passed and
this layout-dependent case failed. Position the tower/model appropriately when
using that option. Preset loading and full-tree schema/identity checks pass.

## Validation status

Locally verified during this port:

- Exact source patch applies to a fresh official 2.5 stable checkout.
- Static profile checks pass across all 72 vendors, with no errors or warnings;
  the patched Creality bundle version advances from 02.03.02.88 to 02.03.02.89.
- Every inherited web asset matches the pinned 2.4.2 manifest.
- A read-only live F018 response was parsed into four loaded CFS slots and an
  external spool entry. No printer settings or motion commands were changed.
- Compiled tests of the actual C++ slot parser pass for connected/disconnected
  boxes, gaps in loaded slots, multiple boxes, metadata, color conversion,
  external spools and malformed responses.
- Compiled tests of the actual export matrix block pass for multipliers 0,
  0.35, 0.7, 1, 1.3 and 2. Exported Creality/Hi multipliers always remain 1;
  Default does not compound with Fast, and unrelated hosts retain upstream behavior.
- JavaScript bridge tests pass for startup, delayed availability, repeated script
  installation, a newly recreated browser, and alias restoration after repeated
  Edge/WebKit reloads without duplicate command delivery.
- Replacing the patched export block with upstream's block makes the compiled
  regression fail specifically on the required multiplier of 1.
- Full Windows x64 dependencies and patched application compiled locally with
  Visual Studio 2022, MSVC and Ninja against official 2.5.0 stable.
- Real slicing/export tests pass at multipliers 0, 0.35, 0.7, 1, 1.3 and 2:
  exported multiplier is always 1, volumes are scaled once, and project values
  are preserved.
- Fixture tests pass for dry run, stale markers, install, every payload hash,
  idempotence, resource tampering, modified-core rollback protection, complete
  byte-for-byte rollback (including newly added files), and unsupported cores.

The locally built Windows installer includes the rebuilt native modules and all
11 public presets. Device-page rendering and physical printing still require
owner testing on a Hi/CFS; compilation and CLI tests do not establish those results.

Focused tests (the C++ test uses a local GCC-compatible compiler):

```powershell
python tests/test_native_protocol.py --source orca --compiler g++
node tests/test_webview_bridge.mjs orca
./tests/test-installer-fixture.ps1
```

Printer hardware checks for the owner, using a separate Orca `--datadir` and a real Hi/CFS:

1. Launch, select the Hi profile and Creality Print host, and open Device. Resize,
   switch tabs/printers, reload, and check both themes.
2. Check camera startup/recovery, local-file export and logo light.
3. Sync loaded CFS slots, confirm material/color and per-object filament choices;
   edit/reset a slot and test feed/retract while the printer is idle.
4. Map CFS slots in the print dialog. Select an external spool for a single-color
   job and confirm it cannot be mixed with multicolor mapping.
5. Slice a two-color model at different Default multipliers. Compare exported
   matrix values, Flushed estimates, layer/time metadata and timelapse Off after reload.
6. Check Fast purge only on a profile that enables it. Switching modes must not
   compound a previously selected Default multiplier.
7. Upload a small job, confirm printer-side mapping, then perform a supervised print.
8. Test a non-Creality printer web UI and verify its handler/API key still works.
9. Restore the official installation and compare the entire file snapshot.
