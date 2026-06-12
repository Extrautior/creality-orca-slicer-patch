param(
    [Parameter(Mandatory = $true)]
    [string]$PayloadDirectory,
    [string]$OutputDirectory = "build\installer-publish"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$payloadDirectory = (Resolve-Path $PayloadDirectory).Path
$manifestPath = Join-Path $payloadDirectory "manifest.json"
$payloadRoot = Join-Path $payloadDirectory "root"

if (-not (Test-Path $manifestPath -PathType Leaf) -or
    -not (Test-Path $payloadRoot -PathType Container)) {
    throw "PayloadDirectory must contain manifest.json and root\."
}

$embeddedZip = Join-Path $repoRoot "installer\payload.zip"
if (Test-Path $embeddedZip) {
    Remove-Item $embeddedZip -Force
}

Compress-Archive -Path $payloadDirectory -DestinationPath $embeddedZip -CompressionLevel Optimal

try {
    dotnet publish (Join-Path $repoRoot "installer\CrealityOrcaPatchInstaller.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o (Join-Path $repoRoot $OutputDirectory)
} finally {
    Remove-Item $embeddedZip -Force -ErrorAction SilentlyContinue
}
