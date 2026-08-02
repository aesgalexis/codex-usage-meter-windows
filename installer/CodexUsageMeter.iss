#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
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
