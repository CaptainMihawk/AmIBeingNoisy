#define AppName "LoudEnv"
#define AppPublisher "CaptainMihawk"
#define AppExeName "LoudEnv.Tray.exe"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{93E07CDE-F867-41DB-AAFF-A9A2B76F9287}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\LoudEnv
DefaultGroupName=LoudEnv
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=LoudEnv-Setup-{#AppVersion}
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
AppMutex=LoudEnv.Tray.SingleInstance
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "..\publish\tray\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LoudEnv"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar LoudEnv"; Filename: "{uninstallexe}"
Name: "{autodesktop}\LoudEnv"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar o LoudEnv"; Flags: nowait postinstall skipifsilent
