param([string]$Repository,[string]$PackagePath)
$ErrorActionPreference='Stop'
$target=Join-Path $env:LOCALAPPDATA 'Programs\SystemSage'
$source=$PSScriptRoot
if($Repository){
  $release=Invoke-RestMethod "https://api.github.com/repos/$Repository/releases/latest"
  $asset=$release.assets|Where-Object name -eq 'SystemSage-Windows.zip'|Select-Object -First 1
  if(!$asset){throw 'Latest release does not contain SystemSage-Windows.zip.'}
  $PackagePath=Join-Path $env:TEMP 'SystemSage-Windows.zip'
  Invoke-WebRequest $asset.browser_download_url -OutFile $PackagePath
}
if($PackagePath){
  $stage=Join-Path $env:TEMP ('SystemSage-'+[guid]::NewGuid().ToString('N'))
  Expand-Archive $PackagePath $stage -Force
  $source=Join-Path $stage 'SystemSage'
}
if(!(Test-Path (Join-Path $source 'systemsage.exe'))){throw 'systemsage.exe was not found in the installation package.'}
$sourcePath=[IO.Path]::GetFullPath($source);$targetPath=[IO.Path]::GetFullPath($target)
if((Test-Path $targetPath)-and $sourcePath -ne $targetPath){Remove-Item $targetPath -Recurse -Force}
New-Item $targetPath -ItemType Directory -Force|Out-Null
Copy-Item (Join-Path $sourcePath 'systemsage.exe'),(Join-Path $sourcePath 'LICENSE'),(Join-Path $sourcePath 'README.md') -Destination $targetPath -Force
$path=[Environment]::GetEnvironmentVariable('Path','User')
if(($path-split';')-notcontains $targetPath){[Environment]::SetEnvironmentVariable('Path',(($path.TrimEnd(';')+';'+$targetPath).Trim(';')),'User')}
$env:Path="$targetPath;$env:Path"
Write-Host 'SystemSage installed successfully.' -ForegroundColor Green
Write-Host 'Open a new terminal and run: systemsage'
& (Join-Path $targetPath 'systemsage.exe') --version
