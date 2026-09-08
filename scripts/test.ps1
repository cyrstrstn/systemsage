$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot '..\release\SystemSage\systemsage.exe'
if(!(Test-Path $exe)){& (Join-Path $PSScriptRoot 'build-cli.ps1')}
$commands=@(@('--version'),@('doctor'),@('memory'),@('gpu'),@('diskhealth'),@('processes'),@('storage'),@('wifi'),@('boot'),@('display'),@('audio'),@('browsers'),@('temp'),@('outlook'),@('security'),@('battery'),@('network'),@('startup'),@('system'),@('json'))
# updates can take up to ~20s (COM search); run separately with soft failure tolerance
foreach($arguments in $commands){$output=& $exe @arguments 2>&1;if($LASTEXITCODE -ne 0){throw "Command failed: $($arguments -join ' ')`n$output"};if(!$output){throw "Command returned no output: $($arguments -join ' ')"};Write-Host "PASS systemsage $($arguments -join ' ')" -ForegroundColor Green}
$output=& $exe @('updates') 2>&1;if($LASTEXITCODE -ne 0){throw "Command failed: updates`n$output"};Write-Host "PASS systemsage updates" -ForegroundColor Green
