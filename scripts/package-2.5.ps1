param(
    [Parameter(Mandatory)][string]$BuiltDirectory,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [string]$OutputDirectory = 'release/2.5.0-beta'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$config = Get-Content (Join-Path $repo 'config/orca-2.5.0-beta.json') -Raw | ConvertFrom-Json
$built = (Resolve-Path $BuiltDirectory).Path
New-Item -ItemType Directory -Force $WorkDirectory, $OutputDirectory | Out-Null
$work = (Resolve-Path $WorkDirectory).Path
$output = (Resolve-Path $OutputDirectory).Path
$archive = Join-Path $work 'official.zip'
if (!(Test-Path $archive)) { Invoke-WebRequest $config.OfficialArchive -OutFile $archive }
if ((Get-FileHash $archive).Hash -ne $config.OfficialArchiveSha256) { throw 'Official archive checksum mismatch.' }
$clean = Join-Path $work 'official'
if (Test-Path $clean) { throw 'Use a fresh package work directory.' }
Expand-Archive -LiteralPath $archive -DestinationPath $clean
$officialExe = @(Get-ChildItem $clean -Filter orca-slicer.exe -Recurse)
if ($officialExe.Count -ne 1) { throw 'Cannot locate unique official OrcaSlicer executable.' }
$clean = $officialExe[0].DirectoryName
$payload = Join-Path $work 'payload'
$root = Join-Path $payload 'root'
New-Item -ItemType Directory -Force $root | Out-Null
$paths = @('orca-slicer.exe', 'OrcaSlicer.dll',
    'resources/profiles/Creality.json',
    'resources/profiles/Creality/machine/Creality Hi 0.4 nozzle.json',
    'resources/profiles/Creality/machine/Creality Hi 0.6 nozzle.json',
    'resources/profiles/Creality/machineList.json')
# Include the native runtime DLLs from the same build as OrcaSlicer.dll.
$paths += Get-ChildItem $built -Filter '*.dll' -File | ForEach-Object Name
$paths += Get-ChildItem (Join-Path $built 'resources/web/deviceMgr') -File -Recurse |
    ForEach-Object { [IO.Path]::GetRelativePath($built, $_.FullName).Replace('\','/') }
$paths = @($paths | Sort-Object -Unique)
$manifest = [ordered]@{
    PatchId = $config.PatchId
    Name = "Creality Hi / CFS Patch for OrcaSlicer $($config.Version)"
    TargetVersion = $config.Version
    TargetCommit = $config.Commit
    ExpectedCleanCore = [ordered]@{}
    Files = @()
}
foreach ($core in @('orca-slicer.exe', 'OrcaSlicer.dll')) {
    $manifest.ExpectedCleanCore[$core] = (Get-FileHash (Join-Path $clean $core)).Hash
}
if ((Get-FileHash (Join-Path $built 'OrcaSlicer.dll')).Hash -eq $manifest.ExpectedCleanCore['OrcaSlicer.dll']) {
    throw 'The supplied build is unpatched.'
}
foreach ($path in $paths) {
    $source = Join-Path $built $path
    $dest = Join-Path $root $path
    New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
    Copy-Item -LiteralPath $source -Destination $dest
    $manifest.Files += [ordered]@{
        RelativePath = $path
        Size = (Get-Item $dest).Length
        Sha256 = (Get-FileHash $dest).Hash
    }
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $payload 'manifest.json') -Encoding utf8NoBOM
& (Join-Path $PSScriptRoot 'rebuild-installer.ps1') -PayloadDirectory $payload -OutputDirectory $output
$installer = Join-Path $output "Creality-OrcaSlicer-$($config.Version)-Patch-Installer.exe"
if (!(Test-Path $installer)) { throw 'Installer was not generated.' }
& (Join-Path $PSScriptRoot 'test-installer.ps1') -Installer $installer -CleanDirectory $clean -ManifestPath (Join-Path $payload 'manifest.json') -WorkDirectory (Join-Path $work 'test') -SmokeTest
Copy-Item (Join-Path $payload 'manifest.json') (Join-Path $output 'payload-manifest.json')
Copy-Item (Join-Path $repo 'patches/creality-orca-2.5.0-beta.patch') $output
Get-ChildItem $output -File | Where-Object Name -ne 'SHA256SUMS.txt' |
    ForEach-Object { "$((Get-FileHash $_.FullName).Hash)  $($_.Name)" } |
    Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
