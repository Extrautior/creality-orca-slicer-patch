param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$CleanDirectory,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [switch]$SmokeTest
)
$ErrorActionPreference = 'Stop'
$installer = (Resolve-Path $Installer).Path
$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
if (Test-Path $WorkDirectory) { throw 'Tests require a fresh work directory.' }
New-Item -ItemType Directory $WorkDirectory | Out-Null
$work = (Resolve-Path $WorkDirectory).Path
$target = Join-Path $work 'OrcaSlicer'
Copy-Item -LiteralPath (Resolve-Path $CleanDirectory).Path -Destination $target -Recurse
$data = Join-Path $work 'backups'
function Run-Patcher([string[]]$Operation, [int]$Expected = 0) {
    $testLog = Join-Path $work ([Guid]::NewGuid().ToString('N') + '.log')
    $arguments = @($Operation) + @('--install-dir', "`"$target`"", '--data-root', "`"$data`"", '--no-elevate', '--log-file', "`"$testLog`"")
    $proc = Start-Process -FilePath $installer -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($proc.ExitCode -ne $Expected) {
        if (Test-Path $testLog) { Get-Content $testLog | Write-Host }
        throw "Patcher $Operation returned $($proc.ExitCode), expected $Expected."
    }
}
function Snapshot {
    $result = @{}
    Get-ChildItem $target -File -Recurse | ForEach-Object {
        $result[[IO.Path]::GetRelativePath($target, $_.FullName)] = (Get-FileHash $_.FullName).Hash
    }
    return $result
}
function Assert-Snapshot($Before) {
    $after = Snapshot
    if ($Before.Count -ne $after.Count) { throw 'Rollback changed the file count.' }
    foreach ($key in $Before.Keys) {
        if ($Before[$key] -ne $after[$key]) { throw "Rollback changed $key." }
    }
}
# A stale marker must not override clean-core detection, and must survive rollback.
$marker = Join-Path $target '.creality-orca-patch/current.json'
New-Item -ItemType Directory -Force (Split-Path -Parent $marker) | Out-Null
'{"PatchId":"old-2.3.2-marker"}' | Set-Content $marker
$before = Snapshot
Run-Patcher @('--verify')
Run-Patcher @('--install', '--dry-run')
Assert-Snapshot $before
Run-Patcher @('--install')
Run-Patcher @('--verify')
foreach ($file in $manifest.Files) {
    if ((Get-FileHash (Join-Path $target $file.RelativePath)).Hash -ne $file.Sha256) {
        throw "Installed hash mismatch: $($file.RelativePath)"
    }
}
Run-Patcher @('--install') # idempotent exact-patch verification
if ($SmokeTest) {
    # Load the real rebuilt core and its runtime DLLs in a separate data directory.
    $appData = Join-Path $work 'isolated-orca-data'
    $smoke = Start-Process -FilePath (Join-Path $target 'orca-slicer.exe') `
        -ArgumentList @('--help', '--datadir', "`"$appData`"") `
        -WorkingDirectory $target -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $work 'orca-help.txt') `
        -RedirectStandardError (Join-Path $work 'orca-help-errors.txt')
    if (!$smoke.WaitForExit(45000)) {
        $smoke.Kill()
        throw 'Patched Orca CLI startup timed out.'
    }
    $smoke.Refresh()
    if ($smoke.ExitCode -ne 0) { throw "Patched Orca CLI could not load (exit $($smoke.ExitCode))." }
}
# Modified resources must fail verification.
$resource = $manifest.Files | Where-Object RelativePath -Like 'resources/*' | Select-Object -First 1
$resourcePath = Join-Path $target $resource.RelativePath
$resourceBytes = [IO.File]::ReadAllBytes($resourcePath)
[IO.File]::AppendAllText($resourcePath, 'tampered')
Run-Patcher @('--verify') 1
[IO.File]::WriteAllBytes($resourcePath, $resourceBytes)
# A newer core must prevent rollback without changing the rest of the installation.
$dll = Join-Path $target 'OrcaSlicer.dll'
$patchedDll = [IO.File]::ReadAllBytes($dll)
[IO.File]::AppendAllText($dll, 'newer-build')
$modified = Snapshot
Run-Patcher @('--restore') 1
Assert-Snapshot $modified
[IO.File]::WriteAllBytes($dll, $patchedDll)
Run-Patcher @('--restore')
Assert-Snapshot $before
Run-Patcher @('--verify')
# Unsupported/partially modified clean binaries must not be patched.
[IO.File]::AppendAllText($dll, 'unsupported-build')
$unsupported = Snapshot
Run-Patcher @('--install') 1
Assert-Snapshot $unsupported
Write-Host 'PASS: dry run, stale marker, install, hashes, idempotence, tampering, rollback protection, exact restore, unsupported build.'
