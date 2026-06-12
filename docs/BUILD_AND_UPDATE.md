# Build And Update Guide

## Current Base

- OrcaSlicer version: `2.4.0-beta`
- Upstream commit: `fc9a8aa93f7d341c3028d275781d77d2f385023e`
- Platform: Windows x64
- Toolchain: Visual Studio 2022, CMake, .NET 8

## Build OrcaSlicer

1. Check out the exact upstream commit.
2. Apply `patches/creality-orca-2.4.0-beta.patch`.
3. Build the Windows dependencies and Release configuration.

The official beta script is:

```powershell
build_release_vs2022.bat deps
build_release_vs2022.bat slicer
```

Long Windows paths can break dependency builds. Use a short drive alias when necessary:

```powershell
subst R: C:\path\to\OrcaSlicer
R:
build_release_vs2022.bat deps
build_release_vs2022.bat slicer
```

OpenSSL requires Windows-native Perl, such as Strawberry Perl. Git for Windows' MSYS Perl is not accepted by OpenSSL's Windows configure step.

## Assemble The Payload

Create a directory with this structure:

```text
payload/
  manifest.json
  root/
    orca-slicer.exe
    OrcaSlicer.dll
    resources/profiles/Creality/machineList.json
    resources/profiles/Creality/machine/Creality Hi 0.4 nozzle.json
    resources/profiles/Creality/machine/Creality Hi 0.6 nozzle.json
    resources/web/deviceMgr/...
```

`manifest.json` must contain:

- a unique patch ID;
- the exact target version and commit;
- expected clean SHA-256 hashes for `orca-slicer.exe` and `OrcaSlicer.dll`;
- every payload file's relative path, size, and SHA-256 hash.

The release manifest is stored in `docs/payload-manifest-2.4.0-beta.json`.

Compress the payload directory as `installer/payload.zip`. The ZIP is intentionally excluded from Git history and distributed in the GitHub release/source archive.

## Build The Installer

```powershell
dotnet publish installer/CrealityOrcaPatchInstaller.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

## Required Verification

- Apply the source patch with `git apply --check` against the exact upstream tag.
- Build OrcaSlicer Release successfully.
- Verify all embedded payload hashes.
- Verify clean-build detection with any stale older marker present.
- Install into a disposable copy of OrcaSlicer.
- Verify every installed payload file.
- Launch the patched application with a separate `--datadir`.
- Restore and compare every path with its pre-install hash.
- Confirm modified or unsupported core binaries are rejected.
- Confirm rollback refuses to overwrite a newer or unrelated core binary.

## Updating For Another Orca Release

Port the patch from the new upstream release rather than reusing compiled binaries. Review these areas first:

```text
src/slic3r/GUI/PrinterWebView.*
src/slic3r/GUI/PrinterWebViewHandler.*
src/slic3r/Utils/CrealityPrint.*
src/slic3r/GUI/Plater.*
src/slic3r/GUI/PrintHostDialogs.*
src/libslic3r/GCode.cpp
src/libslic3r/GCode/GCodeProcessor.*
src/libslic3r/Print.cpp
```

Update the expected clean hashes and patch ID before packaging. Never broaden version acceptance without rebuilding and testing against that exact binary release.
