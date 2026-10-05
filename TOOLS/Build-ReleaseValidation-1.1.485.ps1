$ErrorActionPreference = 'Stop'
Set-Location C:\HVAC_PRO_MSE
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:release\ReleaseValidation-1.1.485.exe /reference:SOURCE_CODE\bin\Release\HVAC_Pro_Desktop.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:SOURCE_CODE\bin\Release\EPPlus.dll TOOLS\ReleaseValidation-1.1.485.cs
if ($LASTEXITCODE -ne 0) { throw 'Validation harness compile failed' }
Copy-Item -LiteralPath SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe -Destination release/HVAC_Pro_Desktop.exe
Copy-Item -LiteralPath SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe.config -Destination release/ReleaseValidation-1.1.485.exe.config
Start-Process -FilePath C:\HVAC_PRO_MSE\release\ReleaseValidation-1.1.485.exe -WindowStyle Hidden -Wait
Get-Content release/validation-native-1.1.485.log
