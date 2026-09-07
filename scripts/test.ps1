$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot '..\release\SystemSage\systemsage.exe'
if(!(Test-Path $exe)){& (Join-Path $PSScriptRoot 'build-cli.ps1')}
$commands=@(@('--version'),@('doctor'),@('processes'),@('storage'),@('security'),@('battery'),@('network'),@('startup'),@('system'))
foreach($arguments in $commands){$output=& $exe @arguments 2>&1;if($LASTEXITCODE -ne 0){throw "Command failed: $($arguments -join ' ')`n$output"};if(!$output){throw "Command returned no output: $($arguments -join ' ')"};Write-Host "PASS systemsage $($arguments -join ' ')" -ForegroundColor Green}
