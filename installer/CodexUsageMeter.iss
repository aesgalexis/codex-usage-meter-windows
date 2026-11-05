#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define MyAppName "Codex Usage Meter for Windows"
#define MyAppPublisher "aesgalexis"
#define MyAppExeName "CodexUsageMeter.exe"
#define MyAppUrl "https://github.com/aesgalexis/codex-usage-meter-windows"

[Setup]
AppId={{9B7B432F-36A6-4E80-AB97-6D020F2F0CAB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\Codex Usage Meter
DefaultGroupName=Codex Usage Meter
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=CodexUsageMeter-Setup-win-x64
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Codex Usage Meter"; Filename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\CodexUsageMeter"; ValueType: string; ValueName: "InstallLanguage"; ValueData: "{language}"; Flags: uninsdeletevalue; Check: not WizardSilent

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Codex Usage Meter}"; Flags: nowait postinstall skipifsilent

[Code]
const
  StartupKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupValue = 'CodexUsageMeter';

var
  UpdateRelaunched: Boolean;

function IsAutomaticUpdate: Boolean;
begin
  Result := WizardSilent and (ExpandConstant('{param:AUTOMATICUPDATE|0}') = '1');
end;

function StartupTargetsExecutable(const Command, Executable: String): Boolean;
begin
  Result := (Executable <> '') and
    (CompareText(RemoveQuotes(Trim(Command)), Executable) = 0);
end;

procedure PreserveStartupRegistration;
var
  Command, InstalledExe, PreviousExe: String;
begin
  if not RegQueryStringValue(HKCU, StartupKey, StartupValue, Command) then
    Exit;

  InstalledExe := ExpandConstant('{app}\{#MyAppExeName}');
  PreviousExe := ExpandConstant('{param:RESTARTAPP|}');
  if StartupTargetsExecutable(Command, InstalledExe) or
    (IsAutomaticUpdate and
      (CompareText(ExtractFileName(PreviousExe), '{#MyAppExeName}') = 0) and
      StartupTargetsExecutable(Command, PreviousExe)) then
  begin
    if not RegWriteStringValue(HKCU, StartupKey, StartupValue, '"' + InstalledExe + '"') then
      Log('Could not preserve the start-with-Windows setting.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
    PreserveStartupRegistration;

  if (CurStep = ssDone) and IsAutomaticUpdate then
  begin
    UpdateRelaunched := ExecAsOriginalUser(ExpandConstant('{app}\{#MyAppExeName}'),
      '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    if not UpdateRelaunched then
      Log('Could not reopen the updated application: ' + IntToStr(ResultCode));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if (CurUninstallStep = usPostUninstall) and
    RegQueryStringValue(HKCU, StartupKey, StartupValue, Command) and
    StartupTargetsExecutable(Command, ExpandConstant('{app}\{#MyAppExeName}')) then
  begin
    if not RegDeleteValue(HKCU, StartupKey, StartupValue) then
      Log('Could not remove this installation''s start-with-Windows setting.');
  end;
end;

procedure DeinitializeSetup;
var
  PreviousExe: String;
  ResultCode: Integer;
begin
  if IsAutomaticUpdate and not UpdateRelaunched then
  begin
    PreviousExe := ExpandConstant('{param:RESTARTAPP|}');
    if (CompareText(ExtractFileName(PreviousExe), '{#MyAppExeName}') = 0) and FileExists(PreviousExe) then
    begin
      Log('Reopening the previous application after an automatic update failure.');
      if not ExecAsOriginalUser(PreviousExe, '--update-failed', '', SW_SHOWNORMAL, ewNoWait, ResultCode) then
        Log('Could not reopen the previous application: ' + IntToStr(ResultCode));
    end;
  end;
end;
