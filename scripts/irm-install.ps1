$repository='cyrstrstn/systemsage'
$installer=Join-Path $env:TEMP 'systemsage-install.ps1'
Invoke-WebRequest "https://raw.githubusercontent.com/$repository/main/scripts/install.ps1" -OutFile $installer
& $installer -Repository $repository
