# Instrucciones del Proyecto Lado

## Configuración de Claude

**IMPORTANTE**: Siempre usar pensamiento avanzado (ultrathink/extended thinking) para todas las tareas de este proyecto.

## Logging de Errores

**IMPORTANTE**: Todos los errores, warnings y eventos importantes deben registrarse en `/Admin/Logs` usando `ILogEventoService`.

### Cómo usar:

```csharp
// Inyectar en el constructor
private readonly ILogEventoService _logEventoService;

// Registrar un error
await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, usuarioId, usuarioNombre);

// Registrar un warning o evento
await _logEventoService.RegistrarEventoAsync(
    "Mensaje descriptivo",
    CategoriaEvento.Contenido,  // o Sistema, Auth, Usuario, Pago, Admin, Frontend
    TipoLogEvento.Warning,      // o Error, Info, Evento
    usuarioId,
    usuarioNombre,
    "Detalles adicionales aquí"
);
```

### Categorías disponibles:
- `Sistema` - Errores de app, BD, servicios externos
- `Auth` - Login, logout, registro, cambio password
- `Usuario` - Acciones de usuario
- `Pago` - Suscripciones, pagos, retiros, propinas
- `Contenido` - Publicaciones, comentarios, likes, archivos rechazados
- `Admin` - Acciones administrativas
- `Frontend` - Errores JS, Feed, Videos

### Tipos de log:
- `Error` - Errores críticos
- `Warning` - Advertencias (ej: archivo rechazado)
- `Info` - Información
- `Evento` - Eventos normales

## Conversión de Medios

- Videos se convierten a MP4 H.264 (CRF 20, 30fps, AAC 192kbps)
- Imágenes se convierten a JPEG (calidad 90, max 2048px)
- Formatos RAW soportados: DNG, CR2, NEF, ARW, ORF, RW2
- Videos ProRes RAW de iPhone están soportados

## Validación de Archivos

`FileValidationService` valida archivos usando magic bytes. Soporta:
- Videos: MP4, MOV, AVI, MKV, WebM, WMV, FLV, MPEG, MXF, 3GP, ProRes
- Imágenes: JPEG, PNG, GIF, WebP, BMP, HEIC, HEIF, AVIF, DNG, TIFF
