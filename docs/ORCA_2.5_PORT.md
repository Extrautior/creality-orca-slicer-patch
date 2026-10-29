# OrcaSlicer 2.5.0-beta port

Target: Windows x64 official **v2.5.0-beta**, commit
`345efec64c7b95a05ce5cf96df0dde66a158835c`.
2.4.2 is still the latest stable release. The beta was published October 27, 2026.
This target does not accept alpha, nightlies, ARM64, or a future 2.5 final build.

## Compatibility changes

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
git clone --branch v2.5.0-beta --depth 1 https://github.com/OrcaSlicer/OrcaSlicer.git orca
./scripts/prepare-2.5.ps1 -OrcaSource orca -WorkDirectory build/downloads
Push-Location orca
./build_win.bat -d --arch x64 --msvc --msbuild --vs 2022 -j 3
./build_win.bat -s -i --arch x64 --msvc --msbuild --vs 2022 -j 3
Pop-Location
./scripts/package-2.5.ps1 -BuiltDirectory orca/build/OrcaSlicer -WorkDirectory build/package
```

Use fresh source/package work directories. Check each native build command's exit
code before continuing. Dependencies can take substantial time on a cold build.
Keep paths short; a short drive alias may be needed locally.

Preparation verifies the source commit, patch application and all inherited web
asset hashes. Web assets come from the existing pinned release archive; none of
its core binaries or old machine profiles are copied.

Packaging verifies the official 2.5 portable archive's SHA-256, derives the exact
clean executable/DLL hashes, includes freshly built native DLLs, generates the
payload manifest, builds the installer, and tests a disposable installation.
Installer branding is derived from the manifest target; the default project
properties still build a 2.4.2 installer for existing payloads.

The Windows workflow uploads a build artifact only after packaging tests pass.
It does not publish a GitHub release or claim printer hardware validation.

## Validation status

Locally verified during this port:

- Exact source patch applies to a fresh official 2.5 beta checkout.
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
  installation and a newly recreated browser.
- Installer compiles with 2.5 branding against a synthetic test payload.
- Fixture tests pass for dry run, stale markers, install, every payload hash,
  idempotence, resource tampering, modified-core rollback protection, complete
  byte-for-byte rollback (including newly added files), and unsupported cores.

The synthetic fixture is not an Orca binary build. Native build status is tracked
on [PR #1](https://github.com/Extrautior/creality-orca-slicer-patch/pull/1).

Focused tests (the C++ test uses a local GCC-compatible compiler):

```powershell
python tests/test_native_protocol.py --source orca --compiler g++
node tests/test_webview_bridge.mjs orca
./tests/test-installer-fixture.ps1
```

Before publishing an installer, verify with a separate Orca `--datadir` and a real Hi/CFS:

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
