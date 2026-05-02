# Changelog

## v1.5.3 - 2026-05-02

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
