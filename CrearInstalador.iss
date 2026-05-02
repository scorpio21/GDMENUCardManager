[Setup]
; Información básica de la aplicación
AppName=GD MENU Card Manager
AppVersion=1.5.3
AppPublisher=ATeam
AppPublisherURL=https://github.com/sonik-br/GDMENUCardManager
AppSupportURL=https://github.com/sonik-br/GDMENUCardManager/issues
AppUpdatesURL=https://github.com/sonik-br/GDMENUCardManager/releases

; Configuración de las carpetas de instalación
DefaultDirName={autopf}\GD MENU Card Manager
DefaultGroupName=GD MENU Card Manager
DisableProgramGroupPage=yes

; Iconos e interfaz
SetupIconFile=k:\GDMENUCardManager\src\GDMENUCardManager\Assets\GDMENUCardManager.ico
UninstallDisplayIcon={app}\GDMENUCardManager.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Ubicación donde se generará el archivo "Setup.exe"
OutputDir=k:\GDMENUCardManager\Instalador
OutputBaseFilename=GDMENUCardManager_Setup_v1.5.3

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copiamos todos los archivos generados en la carpeta "publish"
Source: "k:\GDMENUCardManager\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTA: ¡No uses "Flags: ignoreversion" en ningún archivo del sistema compartido!

[Icons]
; Acceso directo en el menú de inicio
Name: "{group}\GD MENU Card Manager"; Filename: "{app}\GDMENUCardManager.exe"; IconFilename: "{app}\Assets\GDMENUCardManager.ico"
; Acceso directo de desinstalación
Name: "{group}\{cm:UninstallProgram,GD MENU Card Manager}"; Filename: "{uninstallexe}"
; Acceso directo opcional en el escritorio
Name: "{autodesktop}\GD MENU Card Manager"; Filename: "{app}\GDMENUCardManager.exe"; Tasks: desktopicon; IconFilename: "{app}\Assets\GDMENUCardManager.ico"

[Run]
; Dar la opción de ejecutar el programa justo después de instalar
Filename: "{app}\GDMENUCardManager.exe"; Description: "{cm:LaunchProgram,GD MENU Card Manager}"; Flags: nowait postinstall skipifsilent
