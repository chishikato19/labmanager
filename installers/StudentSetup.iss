; Script Inno Setup cho LabManager Student App
#define MyAppName "LabManager Student"
#define MyAppVersion "1.3.7"
#define MyAppPublisher "LabManager Team"
#define MyAppExeName "LabManager.Student.App.exe"

[Setup]
AppId={{9F82B743-4C3D-4D30-8902-86E5E3D291F4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\LabManager\Student
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=LabManager_Student_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish\student\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LabManagerStudent"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue

[Run]
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""LabManager Student"" dir=in action=allow program=""{app}\{#MyAppExeName}"" enable=yes profile=any"; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""LabManager Student Out"" dir=out action=allow program=""{app}\{#MyAppExeName}"" enable=yes profile=any"; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#MyAppExeName}"; Flags: nowait; Check: WizardSilent

[UninstallRun]
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""LabManager Student"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""LabManager Student Out"""; Flags: runhidden
