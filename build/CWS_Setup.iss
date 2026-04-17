#define MyAppName "CWS"
#define MyAppVersion "1.0.0.0" 
#define MyAppPublisher "NekoCat"
#define MyAppURL "https://github.com/Colorful-Palette/CWS"
#define MyAppExeName "CWS.exe"

[Setup]
AppId={{D3B2E911-A5F1-4F9A-B0C4-7C91F5D2E123}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesAssociations=no
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=.
OutputBaseFilename=CWS_Setup
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Languages\EnglishBritish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "開機自動啟動 CWS"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "../release/{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "../release/CWS.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "../release/*.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

