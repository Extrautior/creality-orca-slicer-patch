param(
    [string]$OrcaBuildRoot = ".",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path $OrcaBuildRoot
$payloadRoot = Join-Path $root "build\creality-orca-patcher-2.3.2-stable-release\payload\root"
$wrapperRoot = Join-Path $root "build\creality-orca-installer-wrapper-src"
$zipPath = Join-Path $root "build\creality-orca-patcher-2.3.2-stable-release.zip"
$publishDir = Join-Path $root "build\creality-orca-installer-2.3.2-stable-release"

Copy-Item (Join-Path $root "build\src\$Configuration\OrcaSlicer.dll") (Join-Path $payloadRoot "OrcaSlicer.dll") -Force
Copy-Item (Join-Path $root "build\src\$Configuration\orca-slicer.exe") (Join-Path $payloadRoot "orca-slicer.exe") -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $root "build\creality-orca-patcher-2.3.2-stable-release\*") -DestinationPath $zipPath -CompressionLevel Optimal
Copy-Item $zipPath (Join-Path $wrapperRoot "creality-orca-patcher-2.3.2-stable-release.zip") -Force

dotnet publish (Join-Path $wrapperRoot "CrealityOrcaPatchInstaller.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outFile = Join-Path $root "build\Creality-OrcaSlicer-2.3.2-Patch-Installer-$stamp.exe"
Copy-Item (Join-Path $publishDir "CrealityOrcaPatchInstaller.exe") $outFile -Force
Get-FileHash $outFile -Algorithm SHA256
