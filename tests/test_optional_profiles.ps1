param(
 [Parameter(Mandatory)][string]$Installer,
 [Parameter(Mandatory)][string]$CleanDirectory,
 [Parameter(Mandatory)][string]$WorkDirectory,
 [string]$PreviousInstaller
)
$ErrorActionPreference='Stop'
if(Test-Path $WorkDirectory) {throw 'Use a fresh disposable test directory.'}
New-Item -ItemType Directory $WorkDirectory | Out-Null
$work=(Resolve-Path $WorkDirectory).Path
$target=Join-Path $work 'OrcaSlicer'
Copy-Item -LiteralPath (Resolve-Path $CleanDirectory).Path -Destination $target -Recurse
$new=(Resolve-Path $Installer).Path
$state=Join-Path $work 'backups'
$sentinel=Join-Path $target 'data_dir/user/default/machine/Existing custom printer.json'
New-Item -ItemType Directory -Force (Split-Path -Parent $sentinel) | Out-Null
'{"name":"Existing custom printer","private_setting":"preserve-me"}' | Set-Content $sentinel
$before=(Get-FileHash $sentinel).Hash
function Run([string]$Exe,[string[]]$ArgsList) {
 New-Item -ItemType Directory -Force $state | Out-Null
 $log=Join-Path $state ([Guid]::NewGuid().ToString('N')+'.log')
 $argsAll=@($ArgsList)+@('--install-dir',('"'+$target+'"'),'--data-root',('"'+$state+'"'),'--no-elevate','--log-file',('"'+$log+'"'))
 $p=Start-Process -FilePath $Exe -ArgumentList $argsAll -WindowStyle Hidden -Wait -PassThru
 if($p.ExitCode -ne 0) { Get-Content $log; throw 'Transition operation failed.' }
}
function OwnerCount { return @(Get-ChildItem (Join-Path $target 'resources/profiles/Creality') -Filter 'Extrautior *.json' -File -Recurse).Count }
Run $new @('--verify')
if($PreviousInstaller) {
 $old=(Resolve-Path $PreviousInstaller).Path
 Run $old @('--install')
 Run $new @('--install')
 Run $new @('--verify')
 if((OwnerCount) -ne 11) {throw 'Earlier presets not retained.'}
 Run $new @('--restore')
 Run $old @('--restore')
}
Run $new @('--install')
if((OwnerCount) -ne 0) {throw 'Default added presets.'}
Run $new @('--install','--include-profiles')
Run $new @('--install')
if((OwnerCount) -ne 11) {throw 'Opt-in did not persist.'}
Run $new @('--restore')
Run $new @('--verify')
if((OwnerCount) -ne 0) {throw 'Opt-in rollback did not restore base selection.'}
Run $new @('--restore')
Run $new @('--verify')
if((Get-FileHash $sentinel).Hash -ne $before) {throw 'Existing user profile changed.'}
'PASS: default skips profiles; later opt-in persists; both rollback levels restore; existing user profile unchanged.'
