#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
[Setup]
AppId={{B43E7F47-FDCE-49D2-B07C-87FA31111906}
AppName=Team Reminder
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\TeamReminder
DefaultGroupName=Team Reminder
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=TeamReminder-Setup-{#AppVersion}-x64
SetupIconFile=..\src\clients\TeamReminder.Windows\Assets\app.ico
UninstallDisplayIcon={app}\TeamReminder.Windows.exe
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "startup"; Description: "Iniciar com o Windows (na bandeja, sem abrir a janela)"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Team Reminder"; Filename: "{app}\TeamReminder.Windows.exe"

[Registry]
; Always record uninstall cleanup, including startup enabled later from Settings.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "TeamReminder"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TeamReminder"; ValueData: """{app}\TeamReminder.Windows.exe"" --tray"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "TeamReminder"; Flags: deletevalue; Tasks: not startup

[Run]
Filename: "{app}\TeamReminder.Windows.exe"; Description: "Abrir Team Reminder"; Flags: nowait postinstall skipifsilent
; Automatic updates run silently and pass /relaunch=--tray (or --updated when the user asked to update now).
Filename: "{app}\TeamReminder.Windows.exe"; Parameters: "{param:relaunch|--tray}"; Flags: nowait; Check: WizardSilent
