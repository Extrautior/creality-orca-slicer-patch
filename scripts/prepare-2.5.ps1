param([Parameter(Mandatory)][string]$OrcaSource, [Parameter(Mandatory)][string]$WorkDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$config = Get-Content (Join-Path $repo 'config/orca-2.5.0-beta.json') -Raw | ConvertFrom-Json
$source = (Resolve-Path $OrcaSource).Path
if ((git -C $source rev-parse HEAD) -ne $config.Commit) { throw 'Wrong Orca source commit.' }
if (git -C $source status --porcelain) { throw 'Use a clean Orca source checkout.' }
git -C $source apply --check (Join-Path $repo 'patches/creality-orca-2.5.0-beta.patch')
if ($LASTEXITCODE -ne 0) { throw 'Source patch check failed.' }
git -C $source apply (Join-Path $repo 'patches/creality-orca-2.5.0-beta.patch')
if ($LASTEXITCODE -ne 0) { throw 'Source patch failed.' }
New-Item -ItemType Directory -Force $WorkDirectory | Out-Null
$work = (Resolve-Path $WorkDirectory).Path
$archive = Join-Path $work 'web-assets-source.zip'
if (!(Test-Path $archive)) {
    Invoke-WebRequest $config.WebAssetsArchive -OutFile $archive
}
if ((Get-FileHash $archive).Hash -ne $config.WebAssetsArchiveSha256) { throw 'Web asset archive checksum mismatch.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $payloadEntry = $zip.GetEntry('installer/payload.zip')
    if (!$payloadEntry) { throw 'Pinned source archive is missing payload.zip.' }
    $payloadZip = Join-Path $work 'previous-payload.zip'
    [IO.Compression.ZipFileExtensions]::ExtractToFile($payloadEntry, $payloadZip, $true)
} finally { $zip.Dispose() }
$payload = [IO.Compression.ZipFile]::OpenRead($payloadZip)
$manifest = Get-Content (Join-Path $repo 'docs/payload-manifest-2.4.2.json') -Raw | ConvertFrom-Json
try {
    # Copy only the pinned Creality web resources. Never reuse old executable code or profiles.
    foreach ($file in $manifest.Files) {
        if (!$file.RelativePath.StartsWith('resources/web/deviceMgr/') -and
            $file.RelativePath -ne 'resources/profiles/Creality/machineList.json') { continue }
        $entry = $payload.GetEntry('payload/root/' + $file.RelativePath)
        if (!$entry) { throw "Missing web resource: $($file.RelativePath)" }
        $dest = Join-Path $source $file.RelativePath
        New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dest, $true)
        if ((Get-FileHash $dest).Hash -ne $file.Sha256 -or (Get-Item $dest).Length -ne $file.Size) {
            throw "Invalid web resource: $($file.RelativePath)"
        }
    }
} finally { $payload.Dispose() }
if (!(Test-Path (Join-Path $source 'resources/web/deviceMgr/index.html'))) { throw 'Device page missing.' }
Write-Host "Prepared exact $($config.Version) source and verified Creality web assets."
