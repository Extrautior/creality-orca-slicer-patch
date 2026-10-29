param([string]$WorkDirectory = 'build/installer-fixture')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (Test-Path $WorkDirectory) { throw 'Use a fresh fixture directory.' }
New-Item -ItemType Directory $WorkDirectory | Out-Null
$work = (Resolve-Path $WorkDirectory).Path
$clean = Join-Path $work 'clean'
# Exercise the packager's folder-name-independent embedded ZIP layout too.
$payload = Join-Path $work 'differently-named-payload'
$root = Join-Path $payload 'root'
$manifest = [ordered]@{
    PatchId = 'synthetic-test-fixture-only'
    Name = 'Synthetic fixture, not Orca binaries'
    TargetVersion = '2.5.0-beta'
    TargetCommit = '345efec64c7b95a05ce5cf96df0dde66a158835c'
    ExpectedCleanCore = [ordered]@{}
    Files = @()
}
foreach ($name in @('orca-slicer.exe','OrcaSlicer.dll','resources/existing.txt','resources/new.txt')) {
    $destination = Join-Path $root $name
    New-Item -ItemType Directory -Force (Split-Path -Parent $destination) | Out-Null
    "patched fixture $name" | Set-Content $destination
    $manifest.Files += [ordered]@{ RelativePath = $name; Size = (Get-Item $destination).Length; Sha256 = (Get-FileHash $destination).Hash }
    if ($name -ne 'resources/new.txt') {
        $original = Join-Path $clean $name
        New-Item -ItemType Directory -Force (Split-Path -Parent $original) | Out-Null
        "original fixture $name" | Set-Content $original
        if ($name -notlike 'resources/*') { $manifest.ExpectedCleanCore[$name] = (Get-FileHash $original).Hash }
    }
}
$manifestPath = Join-Path $payload 'manifest.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content $manifestPath -Encoding utf8NoBOM
$publish = Join-Path $work 'published'
& (Join-Path $repo 'scripts/rebuild-installer.ps1') -PayloadDirectory $payload -OutputDirectory $publish
& (Join-Path $repo 'scripts/test-installer.ps1') -Installer (Join-Path $publish 'Creality-OrcaSlicer-2.5.0-beta-Patch-Installer.exe') -CleanDirectory $clean -ManifestPath $manifestPath -WorkDirectory (Join-Path $work 'roundtrip')
