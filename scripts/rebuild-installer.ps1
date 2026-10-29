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
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.TargetVersion -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$' -or
    $manifest.TargetCommit -notmatch '^[a-f0-9]{40}$') {
    throw 'Payload target version/commit is missing or invalid.'
}
$assemblyVersion = ($manifest.TargetVersion -split '-')[0] + '.1'

$embeddedZip = Join-Path $repoRoot "installer\payload.zip"
if (Test-Path $embeddedZip) {
    Remove-Item $embeddedZip -Force
}

# Always store payload/ at the ZIP root, independently of the input folder name.
$staging = Join-Path $repoRoot ('build\embed-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item -LiteralPath $payloadDirectory -Destination (Join-Path $staging 'payload') -Recurse
Compress-Archive -Path (Join-Path $staging 'payload') -DestinationPath $embeddedZip -CompressionLevel Optimal

try {
    dotnet publish (Join-Path $repoRoot "installer\CrealityOrcaPatchInstaller.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        "-p:PatchTargetVersion=$($manifest.TargetVersion)" `
        "-p:PatchTargetCommit=$($manifest.TargetCommit)" `
        "-p:PatchAssemblyVersion=$assemblyVersion" `
        -o (Join-Path $repoRoot $OutputDirectory)
    if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
} finally {
    Remove-Item $embeddedZip -Force -ErrorAction SilentlyContinue
    # $staging is a fresh GUID directory under this repository's build directory.
    $expectedParent = [IO.Path]::GetFullPath((Join-Path $repoRoot 'build'))
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($staging)) -eq $expectedParent) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
