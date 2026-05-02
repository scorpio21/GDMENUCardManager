; Inno Setup script for GD MENU Card Manager
; Requires Inno Setup 6.x

#define MyAppName "GD MENU Card Manager"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "ATeam"
#define MyAppExeName "GDMENUCardManager.exe"

; Update this path if needed to point to the published single-file folder
#define PublishDir "publish\\win-x64-singlefile"

; Ensure the output directory is set correctly
#pragma parseroption -p-

[Setup]
AppId={{D1A3B6C5-17E0-4A9C-9A48-9E9C2C3F8C8A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={pf64}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=no
OutputBaseFilename=GDMENUCardManager_{#MyAppVersion}_Setup
OutputDir=.\publish
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=src\GDMENUCardManager\Assets\GDMENUCardManager.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear icono en el escritorio"; GroupDescription: "Tareas adicionales:"; Flags: unchecked

[Files]
; Incluir los archivos de la aplicaciÃ³n
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\GDMENUCardManager.ico"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\GDMENUCardManager.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Ejecutar {#MyAppName}"; Flags: nowait postinstall skipifsilent
