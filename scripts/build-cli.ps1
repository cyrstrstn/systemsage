param([string]$Destination=(Join-Path $PSScriptRoot '..\release'))
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path;$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe';if(!(Test-Path $csc)){$csc='C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'};if(!(Test-Path $csc)){throw 'The Windows .NET Framework compiler is unavailable.'}
$bundle=Join-Path $Destination 'SystemSage';if(Test-Path $bundle){Remove-Item $bundle -Recurse -Force};New-Item $bundle -ItemType Directory -Force|Out-Null
& $csc /nologo /optimize+ /target:exe /out:"$bundle\systemsage.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Management.dll /reference:System.Windows.Forms.dll "$root\native\SystemSageCli.cs" "$root\native\FullDiagnostic.cs" "$root\native\StyledPdfReport.cs" "$root\native\MoreScans.cs" "$root\native\ExtraScans.cs";if($LASTEXITCODE){throw 'Compilation failed.'}
Copy-Item "$root\LICENSE","$root\README.md" -Destination $bundle
Copy-Item "$root\scripts\install.ps1" -Destination $bundle
$zip=Join-Path $Destination 'SystemSage-Windows.zip';if(Test-Path $zip){Remove-Item $zip -Force};Compress-Archive $bundle $zip -CompressionLevel Optimal;Get-Item "$bundle\systemsage.exe",$zip|Select-Object FullName,Length
