; ServoERP Inno Setup 6 installer
; Build with: powershell -ExecutionPolicy Bypass -File .\Build-ServoERPInstaller.ps1

#include "ServoERP.version.iss"

#define AppName "ServoERP"
#define AppPublisher "ServoERP"
#define AppWebsite "https://servoerp.in"
#define AppExeName "HVAC_Pro_Desktop.exe"
#define AppCustomConfig "HVACPro.config"
#define AppRoot "C:\HVAC_PRO_MSE"
#define DotNetInstaller "NDP472-KB4054530-x86-x64-AllOS-ENU.exe"
#define SqlInstaller "SQLEXPR_x64_ENU.exe"
#define WebViewInstaller "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
#define AppIcon "..\Resources\Branding\servoerp_app.ico"
#define TermsFile "ServoERP-Terms.rtf"

[Setup]
AppId={{7B23C9B1-21A0-47A8-9E27-5F3987C32026}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppWebsite}
AppSupportURL={#AppWebsite}
AppUpdatesURL={#AppWebsite}
AppCopyright=Copyright (c) ServoERP 2026
DefaultDirName={#AppRoot}
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=..\..\installer_output
OutputBaseFilename=ServoERP_Setup_{#AppVersion}
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
LicenseFile={#TermsFile}
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
AppMutex=HVAC_PRO_MSE_MUTEX
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=ServoERP Setup
VersionInfoProductName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{app}"; Permissions: users-full
Name: "{app}\BACKUP"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\BACKUPS"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\CONFIG"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\DATABASE"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\Invoice"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\LOGS"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\LOGS\errors"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\MSE DATA"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\PAYROLL_EXPORTS"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\PAYSLIPS"; Permissions: users-full; Flags: uninsneveruninstall
Name: "{app}\RECEIPTS"; Permissions: users-full; Flags: uninsneveruninstall

[Files]
Source: "..\bin\Release\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\{#AppExeName}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\*.xml"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\bin\Release\x64\SQLite.Interop.dll"; DestDir: "{app}\x64"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\bin\Release\x86\SQLite.Interop.dll"; DestDir: "{app}\x86"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\bin\Release\app.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\bin\Release\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\{#AppCustomConfig}"; DestDir: "{app}"; Flags: onlyifdoesntexist ignoreversion
Source: "..\..\TOOLS\Provision-TenantDatabase.ps1"; DestDir: "{app}\TOOLS"; Flags: ignoreversion
Source: "Prerequisites\{#DotNetInstaller}"; Flags: dontcopy
Source: "Prerequisites\{#SqlInstaller}"; Flags: dontcopy
Source: "Prerequisites\{#WebViewInstaller}"; Flags: dontcopy

[Icons]
Name: "{commondesktop}\ServoERP"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app.ico"; WorkingDir: "{app}"; Comment: "ServoERP"
Name: "{commonprograms}\ServoERP"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app.ico"; WorkingDir: "{app}"

[Registry]
Root: HKLM; Subkey: "SOFTWARE\ServoERP\Installer"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekeyifempty
Root: HKLM; Subkey: "SOFTWARE\ServoERP\Installer"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "/firstrun"; Description: "Initialize ServoERP database"; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "Launch ServoERP"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM ""{#AppExeName}"""; Flags: runhidden; RunOnceId: "StopServoERP"

[Code]
var
  KeepUserData: Boolean;

function InstallLogPath(): string;
begin
  Result := ExpandConstant('{app}\LOGS\install.log');
end;

procedure LogInstall(const Message: string);
begin
  ForceDirectories(ExtractFileDir(InstallLogPath()));
  SaveStringToFile(
    InstallLogPath(),
    GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + ' | ' + Message + #13#10,
    True);
end;

function QueryReleaseValue(var ReleaseValue: Cardinal): Boolean;
begin
  Result :=
    RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) or
    RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) or
    RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue);
end;

function IsDotNet472OrLaterInstalled(): Boolean;
var
  ReleaseValue: Cardinal;
begin
  Result := QueryReleaseValue(ReleaseValue) and (ReleaseValue >= 461808);
end;

function IsSqlExpressInstalled(): Boolean;
begin
  Result :=
    RegValueExists(HKLM64, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL', 'SQLEXPRESS') or
    RegValueExists(HKLM32, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL', 'SQLEXPRESS') or
    RegValueExists(HKLM, 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL', 'SQLEXPRESS');
end;

function IsWebView2Installed(): Boolean;
begin
  Result :=
    RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
    RegKeyExists(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
    RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}');
end;

function RunInstaller(const FileName: string; const Parameters: string; const SuccessCodes: string; const DisplayName: string): Boolean;
var
  ResultCode: Integer;
begin
  LogInstall('Running ' + DisplayName + ': ' + FileName + ' ' + Parameters);
  Result := Exec(FileName, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if Result then
    Result := Pos(',' + IntToStr(ResultCode) + ',', ',' + SuccessCodes + ',') > 0;

  if Result then
    LogInstall(DisplayName + ' completed. ExitCode=' + IntToStr(ResultCode))
  else
  begin
    LogInstall(DisplayName + ' failed. ExitCode=' + IntToStr(ResultCode));
    MsgBox(DisplayName + ' installation failed. Setup cannot continue.' + #13#10 +
      'Exit code: ' + IntToStr(ResultCode), mbError, MB_OK);
  end;
end;

function EnsureSqlServiceRunning(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('cmd.exe', '/c sc config MSSQL$SQLEXPRESS start= auto', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := Exec('cmd.exe', '/c net start MSSQL$SQLEXPRESS', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  LogInstall('SQL service start attempt. ExitCode=' + IntToStr(ResultCode));
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): string;
begin
  Result := '';
  NeedsRestart := False;

  if not IsDotNet472OrLaterInstalled() then
  begin
    ExtractTemporaryFile('{#DotNetInstaller}');
    if not RunInstaller(
      ExpandConstant('{tmp}\{#DotNetInstaller}'),
      '/q /norestart',
      '0,3010',
      '.NET Framework 4.7.2') then
    begin
      Result := '.NET Framework 4.7.2 setup failed.';
      Exit;
    end;
    NeedsRestart := True;
  end
  else
    LogInstall('.NET Framework 4.7.2 or later already installed.');

  if not IsSqlExpressInstalled() then
  begin
    ExtractTemporaryFile('{#SqlInstaller}');
    if not RunInstaller(
      ExpandConstant('{tmp}\{#SqlInstaller}'),
      '/Q /IACCEPTSQLSERVERLICENSETERMS /ACTION=Install /FEATURES=SQLEngine ' +
      '/INSTANCENAME=SQLEXPRESS /SECURITYMODE=SQL /SAPWD=HvacPro@2026 ' +
      '/SQLSVCACCOUNT="NT AUTHORITY\NETWORK SERVICE" /SQLSYSADMINACCOUNTS="Builtin\Administrators" ' +
      '/TCPENABLED=1 /NPENABLED=1 /BROWSERSVCSTARTUPTYPE=Automatic',
      '0,3010',
      'SQL Server Express') then
    begin
      Result := 'SQL Server Express setup failed.';
      Exit;
    end;
    EnsureSqlServiceRunning();
  end
  else
    LogInstall('SQL Server Express already installed.');

  if not IsWebView2Installed() then
  begin
    ExtractTemporaryFile('{#WebViewInstaller}');
    if not RunInstaller(
      ExpandConstant('{tmp}\{#WebViewInstaller}'),
      '/silent /install',
      '0,3010',
      'Microsoft Edge WebView2 Runtime') then
    begin
      Result := 'Microsoft Edge WebView2 Runtime setup failed.';
      Exit;
    end;
  end
  else
    LogInstall('Microsoft Edge WebView2 Runtime already installed.');
end;

procedure ApplyDataFolderPermissions();
var
  ResultCode: Integer;
begin
  Exec('icacls.exe', '"' + ExpandConstant('{app}') + '" /grant Users:(OI)(CI)F /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  LogInstall('Applied data folder permissions. ExitCode=' + IntToStr(ResultCode));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    ApplyDataFolderPermissions();
end;

function InitializeUninstall(): Boolean;
var
  Answer: Integer;
begin
  Answer := SuppressibleMsgBox(
    'Do you want to keep ServoERP data, database, backups, invoices, receipts, and logs?' + #13#10#13#10 +
    'Choose Yes to keep business data.' + #13#10 +
    'Choose No to delete C:\HVAC_PRO_MSE after uninstall.',
    mbConfirmation,
    MB_YESNOCANCEL,
    IDYES);

  if Answer = IDCANCEL then
  begin
    Result := False;
    Exit;
  end;

  KeepUserData := Answer = IDYES;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if KeepUserData then
    begin
      if not UninstallSilent then
        MsgBox('ServoERP application files were removed. Your data remains at C:\HVAC_PRO_MSE.', mbInformation, MB_OK);
    end
    else
    begin
      DelTree(ExpandConstant('{app}'), True, True, True);
      if not UninstallSilent then
        MsgBox('ServoERP application files and local data were removed.', mbInformation, MB_OK);
    end;
  end;
end;
