# Build And Update Guide

This repo contains the patch artifacts, not a full OrcaSlicer fork.

## Current Base

- OrcaSlicer stable base: `2.3.2`
- Target platform: Windows x64
- Installer framework: .NET 8 WinForms, self-contained single-file publish

## Rebuilding The Current Installer

1. Build OrcaSlicer in Release x64 after applying `patches/creality-orca-2.3.2.patch`.
2. Copy the rebuilt files into the patch payload:

```powershell
$root = Resolve-Path .
$payload = Join-Path $root 'build\creality-orca-patcher-2.3.2-stable-release\payload\root'
Copy-Item 'build\src\Release\OrcaSlicer.dll' (Join-Path $payload 'OrcaSlicer.dll') -Force
Copy-Item 'build\src\Release\orca-slicer.exe' (Join-Path $payload 'orca-slicer.exe') -Force
```

3. Recreate the payload ZIP and embed it into the installer source:

```powershell
$zip = Join-Path $root 'build\creality-orca-patcher-2.3.2-stable-release.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $root 'build\creality-orca-patcher-2.3.2-stable-release\*') -DestinationPath $zip -CompressionLevel Optimal
Copy-Item $zip 'build\creality-orca-installer-wrapper-src\creality-orca-patcher-2.3.2-stable-release.zip' -Force
```

4. Publish the installer:

```powershell
dotnet publish 'build\creality-orca-installer-wrapper-src\CrealityOrcaPatchInstaller.csproj' `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o 'build\creality-orca-installer-2.3.2-stable-release'
```

## Updating For A New OrcaSlicer Version

1. Start from the new OrcaSlicer source release.
2. Apply `patches/creality-orca-2.3.2.patch`.
3. Resolve conflicts carefully. The most conflict-prone files are:

```text
src/slic3r/GUI/Plater.cpp
src/slic3r/GUI/PrinterWebView.cpp
src/slic3r/Utils/CrealityPrint.cpp
src/slic3r/GUI/PrintHostDialogs.cpp
src/libslic3r/GCode.cpp
src/slic3r/GUI/WipeTowerDialog.cpp
```

4. Before packaging, verify that the build does not include unknown future/dev options in a stable base.

For the 2.3.2 patch, these must not exist in the packaged DLL:

```powershell
Select-String -Path 'payload\root\OrcaSlicer.dll' -Pattern 'brim_flow_ratio' -SimpleMatch
Select-String -Path 'payload\root\OrcaSlicer.dll' -Pattern 'combine_brims' -SimpleMatch
```

5. Verify CFS flush metadata:

```powershell
Select-String -Path 'payload\root\OrcaSlicer.dll' -Pattern 'flush_volumes_changed' -SimpleMatch
```

6. Slice a multi-color CFS test file and confirm the generated G-code config block includes:

```text
; flush_multiplier = 1
; flush_volumes_changed = 1
; flush_volumes_matrix = ...
```

## Release Checklist

- OrcaSlicer launches without unknown-option crashes.
- Device tab opens directly to the configured Creality printer.
- Camera loads and recovers after leaving the Device page open.
- CFS sync button appears for Creality Print hosts.
- Upload dialog shows CFS filament mapping.
- Upload dialog shows the external spool holder option when the printer reports an external spool.
- Upload and upload-and-print still work.
- Device page file import and export work.
- Creality Hi generated G-code starts with `M140 S[bed_temperature_initial_layer_single]`, not `M140 S0`.
- Installer progress reaches 100 percent and shows `Done`.
