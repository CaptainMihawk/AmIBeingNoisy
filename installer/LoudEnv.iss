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
Name: "startup"; Description: "Iniciar o LoudEnv com o Windows"; GroupDescription: "Opções:"; Flags: checkedonce

[Files]
Source: "..\publish\tray\*"; DestDir: "{app}"; Excludes: "*.pdb,LoudEnv.runtimeconfig.json"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LoudEnv"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Icons]
Name: "{group}\LoudEnv"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar LoudEnv"; Filename: "{uninstallexe}"
Name: "{autodesktop}\LoudEnv"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar o LoudEnv"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LoudEnv');
end;
