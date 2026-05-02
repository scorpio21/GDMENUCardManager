# Guía: Compilar, Publicar, ZIP Portable y Generar Instalador (Inno Setup)

Proyecto: GDMENUCardManager (WPF .NET 8)
Ejecutable principal: GDMENUCardManager.exe
Versión instalador: 1.5.3
Carpeta del proyecto: k:\GDMENUCardManager

------------------------------------------------------------
1) Preparar cambios (código y recursos)
------------------------------------------------------------

- Verifica que el proyecto compila correctamente antes de generar nada:
  1. Abre PowerShell en:
     k:\GDMENUCardManager
  2. Ejecuta:
     dotnet build

------------------------------------------------------------
2) Publicar binarios (Release, self-contained, single-file)
------------------------------------------------------------

Ejecutar en PowerShell desde la carpeta raíz del proyecto:
  k:\GDMENUCardManager

# (Opcional) limpiar publicación previa
Remove-Item -Path .\publish\* -Recurse -Force -ErrorAction SilentlyContinue

# Publicar binarios actualizados en single-file, win-x64
dotnet publish src/GDMENUCardManager/GDMENUCardManager.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish/win-x64-singlefile

# Verificar el ejecutable publicado
Get-Item .\publish\win-x64-singlefile\GDMENUCardManager.exe |
  Select-Object Name, LastWriteTime, Length

------------------------------------------------------------
3) Generar el instalador con Inno Setup
------------------------------------------------------------

Requisitos:
- Inno Setup 6 instalado.
  Rutas típicas:
  - C:\Program Files (x86)\Inno Setup 6\ISCC.exe
  - C:\Program Files\Inno Setup 6\ISCC.exe

Desde k:\GDMENUCardManager:

$ErrorActionPreference = 'Stop'

# Localizar ISCC
$iss = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
if (!(Test-Path $iss)) { $iss = 'C:\Program Files\Inno Setup 6\ISCC.exe' }

# Compilar installer.iss y generar el .exe en .\publish
# Nota: puedes pasar la versión dinámicamente usando /DMyAppVersion=1.5.3
& "$iss" /Sspawn=0 /V=1 /DMyAppVersion=1.5.3 /O"$pwd\publish" "$pwd\installer.iss"

Salida esperada del instalador:
  k:\GDMENUCardManager\publish\GDMENUCardManager_1.5.3_Setup.exe

------------------------------------------------------------
3b) Crear versión portable (ZIP)
------------------------------------------------------------

Desde la misma carpeta del proyecto:

$ErrorActionPreference = 'Stop'

$out = 'publish/GDMENUCardManager_1.5.3_Portable.zip'
if (Test-Path $out) { Remove-Item $out -Force }

Compress-Archive -Path 'publish/win-x64-singlefile/*' -DestinationPath $out -Force

Salida esperada del portable:
  k:\GDMENUCardManager\publish\GDMENUCardManager_1.5.3_Portable.zip

------------------------------------------------------------
3c) Crear ZIP win-x64 (single-file)
------------------------------------------------------------

Desde la misma carpeta del proyecto:

$ErrorActionPreference = 'Stop'

$out = 'publish/GDMENUCardManager_1.5.3_win-x64_singlefile.zip'
if (Test-Path $out) { Remove-Item $out -Force }

Compress-Archive -Path 'publish/win-x64-singlefile/*' -DestinationPath $out -Force

Salida esperada del ZIP win-x64 single-file:
  k:\GDMENUCardManager\publish\GDMENUCardManager_1.5.3_win-x64_singlefile.zip

------------------------------------------------------------
4) Fallback: limpieza completa si algo queda obsoleto
------------------------------------------------------------

Si sospechas que el instalador o el portable llevan archivos antiguos o arrastran errores:

Remove-Item -Path .\publish\* -Recurse -Force -ErrorAction SilentlyContinue

dotnet clean

dotnet restore

dotnet publish src/GDMENUCardManager/GDMENUCardManager.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish/win-x64-singlefile

$iss = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
if (!(Test-Path $iss)) { $iss = 'C:\Program Files\Inno Setup 6\ISCC.exe' }
& "$iss" /Sspawn=0 /V=1 /DMyAppVersion=1.5.3 /O"$pwd\publish" "$pwd\installer.iss"
