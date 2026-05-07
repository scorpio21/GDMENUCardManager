# Changelog

## v2.0.5 - 2026-05-08

### Añadido
- **Localización Completa al Español**: Traducción total de todas las ventanas, diálogos y mensajes del sistema en las versiones WPF y AvaloniaUI.
- **Soporte de Idioma Dinámico**: Cambio de idioma "en caliente" (Hot-swapping) sin necesidad de reiniciar la aplicación.
- **Mejoras de GDI Shrink (Integradas de Derek Pascarella)**:
  - Soporte para reducir tamaño en juegos contenidos dentro de archivos comprimidos (`.7z`, `.rar`, `.zip`) mediante pre-extracción automática.
  - Soporte para aplicar GDI Shrink en imágenes GD-ROM en formato CUE/BIN.
- **Protecciones de UI**:
  - Bloqueo de edición de metadatos críticos para archivos comprimidos para evitar errores de integridad.
  - Nueva opción de "Verificación de archivos bloqueados" antes de guardar cambios.
  - Deshabilitación inteligente de funciones que requieren acceso directo al binario en archivos comprimidos (como renombrado por IP.BIN).

### Corregido
- Corregido el error de visualización de saltos de línea literal (`\n\n`) en las traducciones de AvaloniaUI.
- Sincronización de lógica entre el núcleo Core y las interfaces de usuario para mayor estabilidad.

## v2.0.0 - 2026-05-02

### Añadido
- Flujo de trabajo en GitHub Actions (`build-and-release.yml`) para compilar y generar instaladores automáticamente en cada nuevo *release* o *push*.
- Script `installer.iss` para la creación de instaladores usando Inno Setup.

### Corregido
- Solucionado un error crítico de arranque (`XamlParseException`) causado por definiciones duplicadas en los archivos de traducción (`en-US.xaml` y `es-ES.xaml`).
- Corregido el solapamiento visual en la cabecera de la aplicación (los botones de configuración y banderas se superponían).
- Restaurada la visibilidad dinámica de las columnas *Folder*, *Art* y *Type* de la tabla de juegos, las cuales dejaron de mostrarse por un conflicto en el código tras añadir las traducciones dinámicas.

### Modificado
- Consolidación de la modernización de la aplicación para que funcione correctamente bajo .NET 8.0.

### Planeado (Roadmap)
- Futuras mejoras y funcionalidades.
