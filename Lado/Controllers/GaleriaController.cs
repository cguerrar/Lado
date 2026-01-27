using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Lado.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class GaleriaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GaleriaController> _logger;
        private readonly IImageService _imageService;
        private readonly IMediaConversionService _mediaConversionService;
        private readonly IFileValidationService _fileValidationService;
        private readonly ILogEventoService _logEventoService;
        private readonly ICuotaGaleriaService _cuotaService;

        private const int MAX_FILE_SIZE_IMAGE_MB = 50;
        private const int MAX_FILE_SIZE_VIDEO_MB = 500;
        private const int MAX_FILES_PER_UPLOAD = 20;

        public GaleriaController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<GaleriaController> logger,
            IImageService imageService,
            IMediaConversionService mediaConversionService,
            IFileValidationService fileValidationService,
            ILogEventoService logEventoService,
            ICuotaGaleriaService cuotaService)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _logger = logger;
            _imageService = imageService;
            _mediaConversionService = mediaConversionService;
            _fileValidationService = fileValidationService;
            _logEventoService = logEventoService;
            _cuotaService = cuotaService;
        }

        // ========================================
        // VISTA PRINCIPAL DE GALERÍA
        // ========================================

        [HttpGet("")]
        public async Task<IActionResult> Index(int? albumId = null, string? filtro = null)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return RedirectToAction("Login", "Account");

                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                    return RedirectToAction("Login", "Account");

                // Obtener álbumes del usuario
                var albums = await _context.Albums
                    .Where(a => a.UsuarioId == usuarioId)
                    .OrderBy(a => a.Orden)
                    .ThenBy(a => a.FechaCreacion)
                    .Select(a => new
                    {
                        a.Id,
                        a.Nombre,
                        a.ImagenPortada,
                        CantidadArchivos = a.Archivos.Count
                    })
                    .ToListAsync();

                // Query base para archivos
                var archivosQuery = _context.MediasGaleria
                    .Where(m => m.UsuarioId == usuarioId);

                // Filtrar por álbum si se especifica
                if (albumId.HasValue)
                {
                    archivosQuery = archivosQuery.Where(m => m.AlbumId == albumId.Value);
                }

                // Aplicar filtros
                if (!string.IsNullOrEmpty(filtro))
                {
                    switch (filtro.ToLower())
                    {
                        case "favoritos":
                            archivosQuery = archivosQuery.Where(m => m.EsFavorito);
                            break;
                        case "imagenes":
                            archivosQuery = archivosQuery.Where(m => m.TipoMedia == TipoMediaGaleria.Imagen);
                            break;
                        case "videos":
                            archivosQuery = archivosQuery.Where(m => m.TipoMedia == TipoMediaGaleria.Video);
                            break;
                        case "sin-album":
                            archivosQuery = archivosQuery.Where(m => m.AlbumId == null);
                            break;
                    }
                }

                var archivos = await archivosQuery
                    .OrderByDescending(m => m.FechaSubida)
                    .Take(100) // Paginación inicial
                    .Select(m => new
                    {
                        m.Id,
                        m.RutaArchivo,
                        m.Thumbnail,
                        m.TipoMedia,
                        m.NombreOriginal,
                        m.TamanoBytes,
                        m.DuracionSegundos,
                        m.EsFavorito,
                        m.FechaSubida,
                        m.AlbumId,
                        m.ContenidoAsociadoId,
                        m.MensajeAsociadoId
                    })
                    .ToListAsync();

                // Estadísticas
                var totalArchivos = await _context.MediasGaleria.CountAsync(m => m.UsuarioId == usuarioId);
                var totalEspacio = await _context.MediasGaleria
                    .Where(m => m.UsuarioId == usuarioId)
                    .SumAsync(m => m.TamanoBytes);

                // Obtener info de cuota
                var infoCuota = await _cuotaService.ObtenerInfoCuotaAsync(usuarioId);

                ViewBag.Albums = albums;
                ViewBag.Archivos = archivos;
                ViewBag.AlbumActual = albumId;
                ViewBag.FiltroActual = filtro;
                ViewBag.TotalArchivos = totalArchivos;
                ViewBag.TotalEspacio = totalEspacio;
                ViewBag.Usuario = usuario;
                ViewBag.InfoCuota = infoCuota;

                // Obtener álbum actual si existe
                if (albumId.HasValue)
                {
                    var albumActual = await _context.Albums
                        .FirstOrDefaultAsync(a => a.Id == albumId.Value && a.UsuarioId == usuarioId);
                    ViewBag.AlbumActualInfo = albumActual;
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al cargar galería");
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
                TempData["Error"] = "Error al cargar la galería";
                return RedirectToAction("Index", "Feed");
            }
        }

        // ========================================
        // SUBIR ARCHIVOS
        // ========================================

        [HttpPost("Subir")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(524288000)] // 500MB
        public async Task<IActionResult> Subir(List<IFormFile> archivos, int? albumId = null)
        {
            _logger.LogInformation("[Galeria] Subir llamado - archivos: {Count}, albumId: {AlbumId}",
                archivos?.Count ?? 0, albumId);

            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    _logger.LogWarning("[Galeria] Usuario no autenticado");
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                {
                    _logger.LogWarning("[Galeria] Usuario no encontrado: {UserId}", usuarioId);
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                if (archivos == null || archivos.Count == 0)
                {
                    _logger.LogWarning("[Galeria] No se recibieron archivos");
                    return Json(new { success = false, message = "No se recibieron archivos" });
                }

                if (archivos.Count > MAX_FILES_PER_UPLOAD)
                    return Json(new { success = false, message = $"Máximo {MAX_FILES_PER_UPLOAD} archivos por subida" });

                // Calcular tamaño total de archivos a subir y verificar cuota
                var tamanoTotal = archivos.Sum(a => a.Length);
                if (!await _cuotaService.PuedeSubirAsync(usuarioId, tamanoTotal))
                {
                    return Json(new { success = false, message = "No tienes suficiente espacio en tu galería. Elimina algunos archivos o solicita más espacio." });
                }

                // Verificar que el álbum pertenezca al usuario
                if (albumId.HasValue)
                {
                    var albumExiste = await _context.Albums
                        .AnyAsync(a => a.Id == albumId.Value && a.UsuarioId == usuarioId);
                    if (!albumExiste)
                        return Json(new { success = false, message = "Álbum no encontrado" });
                }

                var username = usuario.UserName ?? usuario.Id;
                var carpetaGaleria = Path.Combine(_environment.WebRootPath, "uploads", "galeria", username);

                if (!Directory.Exists(carpetaGaleria))
                    Directory.CreateDirectory(carpetaGaleria);

                var archivosSubidos = new List<object>();
                var errores = new List<string>();

                foreach (var archivo in archivos)
                {
                    try
                    {
                        // Validar archivo
                        var validacion = await _fileValidationService.ValidarMediaAsync(archivo);
                        if (!validacion.EsValido)
                        {
                            errores.Add($"{archivo.FileName}: {validacion.MensajeError}");
                            continue;
                        }

                        // Validar tamaño
                        var esVideo = validacion.Tipo == TipoArchivoValidacion.Video;
                        var maxSize = esVideo ? MAX_FILE_SIZE_VIDEO_MB * 1024 * 1024L : MAX_FILE_SIZE_IMAGE_MB * 1024 * 1024L;
                        if (archivo.Length > maxSize)
                        {
                            errores.Add($"{archivo.FileName}: Archivo demasiado grande");
                            continue;
                        }

                        var nombreBase = Guid.NewGuid().ToString();
                        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                        string rutaArchivo;
                        string? rutaThumbnail = null;

                        // Procesar según tipo
                        if (esVideo)
                        {
                            // Convertir video a MP4 si es necesario
                            if (_mediaConversionService.VideoRequiereConversion(extension))
                            {
                                using var stream = archivo.OpenReadStream();
                                var rutaConvertida = await _mediaConversionService.ConvertirVideoAsync(
                                    stream, carpetaGaleria, extension, nombreBase, 20, 1920);

                                if (string.IsNullOrEmpty(rutaConvertida))
                                {
                                    errores.Add($"{archivo.FileName}: Error al convertir video");
                                    continue;
                                }
                                rutaArchivo = rutaConvertida;
                            }
                            else
                            {
                                rutaArchivo = Path.Combine(carpetaGaleria, $"{nombreBase}{extension}");
                                using var stream = new FileStream(rutaArchivo, FileMode.Create);
                                await archivo.CopyToAsync(stream);
                            }

                            // Generar thumbnail para video
                            rutaThumbnail = await GenerarThumbnailVideo(rutaArchivo, carpetaGaleria, nombreBase);
                        }
                        else
                        {
                            // Convertir imagen a JPEG si es necesario
                            if (_mediaConversionService.ImagenRequiereConversion(extension))
                            {
                                using var stream = archivo.OpenReadStream();
                                var rutaConvertida = await _mediaConversionService.ConvertirImagenAsync(
                                    stream, carpetaGaleria, extension, nombreBase, 2048, 90);

                                if (string.IsNullOrEmpty(rutaConvertida))
                                {
                                    errores.Add($"{archivo.FileName}: Error al convertir imagen");
                                    continue;
                                }
                                rutaArchivo = rutaConvertida;
                            }
                            else
                            {
                                rutaArchivo = Path.Combine(carpetaGaleria, $"{nombreBase}{extension}");
                                using var stream = new FileStream(rutaArchivo, FileMode.Create);
                                await archivo.CopyToAsync(stream);
                            }

                            // Generar thumbnail para imagen
                            rutaThumbnail = await _imageService.GenerarThumbnailAsync(rutaArchivo, 300, 300, 80);
                        }

                        // Calcular hash para detectar duplicados
                        var hash = await CalcularHashArchivo(rutaArchivo);

                        // Verificar duplicado
                        var duplicado = await _context.MediasGaleria
                            .FirstOrDefaultAsync(m => m.UsuarioId == usuarioId && m.HashArchivo == hash);

                        if (duplicado != null)
                        {
                            // Eliminar archivo recién subido (es duplicado)
                            if (System.IO.File.Exists(rutaArchivo))
                                System.IO.File.Delete(rutaArchivo);
                            if (!string.IsNullOrEmpty(rutaThumbnail) && System.IO.File.Exists(rutaThumbnail))
                                System.IO.File.Delete(rutaThumbnail);

                            errores.Add($"{archivo.FileName}: Ya existe en tu galería");
                            continue;
                        }

                        // Obtener información del archivo
                        var fileInfo = new FileInfo(rutaArchivo);

                        // Crear registro en BD
                        var media = new MediaGaleria
                        {
                            UsuarioId = usuarioId,
                            AlbumId = albumId,
                            RutaArchivo = GetRelativePath(rutaArchivo),
                            Thumbnail = !string.IsNullOrEmpty(rutaThumbnail) ? GetRelativePath(rutaThumbnail) : null,
                            NombreOriginal = archivo.FileName,
                            TipoMedia = esVideo ? TipoMediaGaleria.Video : TipoMediaGaleria.Imagen,
                            TamanoBytes = fileInfo.Length,
                            FechaSubida = DateTime.Now,
                            HashArchivo = hash
                        };

                        _context.MediasGaleria.Add(media);
                        await _context.SaveChangesAsync();

                        archivosSubidos.Add(new
                        {
                            id = media.Id,
                            rutaArchivo = media.RutaArchivo,
                            thumbnail = media.Thumbnail,
                            tipoMedia = media.TipoMedia,
                            nombreOriginal = media.NombreOriginal,
                            tamanoBytes = media.TamanoBytes
                        });

                        _logger.LogInformation("[Galeria] Archivo subido: {FileName} por usuario {UserId}",
                            archivo.FileName, usuarioId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Galeria] Error al procesar archivo {FileName}", archivo.FileName);
                        errores.Add($"{archivo.FileName}: Error al procesar");
                    }
                }

                // Calcular estadísticas actualizadas
                var estadisticas = await CalcularEstadisticasAsync(usuario.Id);

                // Obtener info de cuota actualizada
                var infoCuota = await _cuotaService.ObtenerInfoCuotaAsync(usuarioId);

                return Json(new
                {
                    success = archivosSubidos.Count > 0,
                    archivos = archivosSubidos,
                    errores = errores,
                    message = archivosSubidos.Count > 0
                        ? $"{archivosSubidos.Count} archivo(s) subido(s)"
                        : "No se pudo subir ningún archivo",
                    estadisticas,
                    infoCuota = new
                    {
                        espacioUsado = infoCuota.EspacioUsadoFormateado,
                        cuotaMaxima = infoCuota.CuotaMaximaFormateada,
                        espacioDisponible = infoCuota.EspacioDisponibleFormateado,
                        porcentajeUso = infoCuota.PorcentajeUso,
                        nivelAlerta = infoCuota.NivelAlerta,
                        mensajeAlerta = infoCuota.MensajeAlerta
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error general al subir archivos");
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
                return Json(new { success = false, message = "Error al subir archivos" });
            }
        }

        // ========================================
        // OBTENER INFO DE MEDIA (para integración con /Contenido/Crear)
        // ========================================

        [HttpPost("ObtenerMediaInfo")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ObtenerMediaInfo([FromBody] ObtenerMediaInfoDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                if (dto.Ids == null || dto.Ids.Count == 0)
                    return Json(new { success = false, message = "No se especificaron archivos" });

                var archivos = await _context.MediasGaleria
                    .Where(m => dto.Ids.Contains(m.Id) && m.UsuarioId == usuarioId)
                    .Select(m => new
                    {
                        id = m.Id,
                        rutaArchivo = m.RutaArchivo,
                        thumbnail = m.Thumbnail ?? m.RutaArchivo,
                        tipoMedia = m.TipoMedia == TipoMediaGaleria.Video ? "video" : "imagen",
                        nombreOriginal = m.NombreOriginal
                    })
                    .ToListAsync();

                return Json(new { success = true, archivos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GaleriaController] Error obteniendo info de media");
                return Json(new { success = false, message = "Error al obtener información" });
            }
        }

        // ========================================
        // ELIMINAR ARCHIVOS
        // ========================================

        [HttpPost("Eliminar")]
        public async Task<IActionResult> Eliminar([FromBody] EliminarArchivosDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                if (dto.Ids == null || dto.Ids.Count == 0)
                    return Json(new { success = false, message = "No se especificaron archivos" });

                var archivos = await _context.MediasGaleria
                    .Where(m => dto.Ids.Contains(m.Id) && m.UsuarioId == usuarioId)
                    .ToListAsync();

                if (archivos.Count == 0)
                    return Json(new { success = false, message = "Archivos no encontrados" });

                foreach (var archivo in archivos)
                {
                    // Eliminar archivos físicos
                    var rutaCompleta = Path.Combine(_environment.WebRootPath, archivo.RutaArchivo.TrimStart('/'));
                    if (System.IO.File.Exists(rutaCompleta))
                        System.IO.File.Delete(rutaCompleta);

                    if (!string.IsNullOrEmpty(archivo.Thumbnail))
                    {
                        var rutaThumb = Path.Combine(_environment.WebRootPath, archivo.Thumbnail.TrimStart('/'));
                        if (System.IO.File.Exists(rutaThumb))
                            System.IO.File.Delete(rutaThumb);
                    }

                    _context.MediasGaleria.Remove(archivo);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("[Galeria] {Count} archivo(s) eliminado(s) por usuario {UserId}",
                    archivos.Count, usuarioId);

                // Calcular estadísticas actualizadas
                var estadisticas = await CalcularEstadisticasAsync(usuarioId);

                // Obtener info de cuota actualizada
                var infoCuota = await _cuotaService.ObtenerInfoCuotaAsync(usuarioId);

                return Json(new
                {
                    success = true,
                    message = $"{archivos.Count} archivo(s) eliminado(s)",
                    estadisticas,
                    infoCuota = new
                    {
                        espacioUsado = infoCuota.EspacioUsadoFormateado,
                        cuotaMaxima = infoCuota.CuotaMaximaFormateada,
                        espacioDisponible = infoCuota.EspacioDisponibleFormateado,
                        porcentajeUso = infoCuota.PorcentajeUso,
                        nivelAlerta = infoCuota.NivelAlerta,
                        mensajeAlerta = infoCuota.MensajeAlerta
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al eliminar archivos");
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
                return Json(new { success = false, message = "Error al eliminar archivos" });
            }
        }

        // ========================================
        // GESTIÓN DE ÁLBUMES
        // ========================================

        [HttpPost("CrearAlbum")]
        public async Task<IActionResult> CrearAlbum([FromBody] CrearAlbumDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                if (string.IsNullOrWhiteSpace(dto.Nombre))
                    return Json(new { success = false, message = "El nombre es requerido" });

                // Obtener el orden máximo actual
                var maxOrden = await _context.Albums
                    .Where(a => a.UsuarioId == usuarioId)
                    .MaxAsync(a => (int?)a.Orden) ?? 0;

                var album = new Album
                {
                    UsuarioId = usuarioId,
                    Nombre = dto.Nombre.Trim(),
                    Descripcion = dto.Descripcion?.Trim(),
                    FechaCreacion = DateTime.Now,
                    Orden = maxOrden + 1
                };

                _context.Albums.Add(album);
                await _context.SaveChangesAsync();

                _logger.LogInformation("[Galeria] Álbum '{Nombre}' creado por usuario {UserId}",
                    album.Nombre, usuarioId);

                return Json(new
                {
                    success = true,
                    album = new
                    {
                        album.Id,
                        album.Nombre,
                        album.Descripcion,
                        cantidadArchivos = 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al crear álbum");
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
                return Json(new { success = false, message = "Error al crear álbum" });
            }
        }

        [HttpPost("EditarAlbum")]
        public async Task<IActionResult> EditarAlbum([FromBody] EditarAlbumDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                var album = await _context.Albums
                    .FirstOrDefaultAsync(a => a.Id == dto.Id && a.UsuarioId == usuarioId);

                if (album == null)
                    return Json(new { success = false, message = "Álbum no encontrado" });

                if (!string.IsNullOrWhiteSpace(dto.Nombre))
                    album.Nombre = dto.Nombre.Trim();

                album.Descripcion = dto.Descripcion?.Trim();
                album.FechaActualizacion = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("[Galeria] Álbum {AlbumId} editado por usuario {UserId}",
                    dto.Id, usuarioId);

                return Json(new { success = true, message = "Álbum actualizado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al editar álbum");
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
                return Json(new { success = false, message = "Error al editar álbum" });
            }
        }

        [HttpPost("EliminarAlbum")]
        public async Task<IActionResult> EliminarAlbum([FromBody] EliminarAlbumDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                var album = await _context.Albums
                    .Include(a => a.Archivos)
                    .FirstOrDefaultAsync(a => a.Id == dto.Id && a.UsuarioId == usuarioId);

                if (album == null)
                    return Json(new { success = false, message = "Álbum no encontrado" });

                // Mover archivos a "sin álbum" en lugar de eliminarlos
                foreach (var archivo in album.Archivos)
                {
                    archivo.AlbumId = null;
                }

                _context.Albums.Remove(album);
                await _context.SaveChangesAsync();

                _logger.LogInformation("[Galeria] Álbum {AlbumId} eliminado por usuario {UserId}",
                    dto.Id, usuarioId);

                return Json(new { success = true, message = "Álbum eliminado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al eliminar álbum");
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
                return Json(new { success = false, message = "Error al eliminar álbum" });
            }
        }

        [HttpPost("MoverAAlbum")]
        public async Task<IActionResult> MoverAAlbum([FromBody] MoverArchivosDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                if (dto.MediaIds == null || dto.MediaIds.Count == 0)
                    return Json(new { success = false, message = "No se especificaron archivos" });

                // Verificar que el álbum destino pertenezca al usuario (si no es null)
                if (dto.AlbumId.HasValue)
                {
                    var albumExiste = await _context.Albums
                        .AnyAsync(a => a.Id == dto.AlbumId.Value && a.UsuarioId == usuarioId);
                    if (!albumExiste)
                        return Json(new { success = false, message = "Álbum no encontrado" });
                }

                var archivos = await _context.MediasGaleria
                    .Where(m => dto.MediaIds.Contains(m.Id) && m.UsuarioId == usuarioId)
                    .ToListAsync();

                foreach (var archivo in archivos)
                {
                    archivo.AlbumId = dto.AlbumId;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("[Galeria] {Count} archivo(s) movido(s) a álbum {AlbumId} por usuario {UserId}",
                    archivos.Count, dto.AlbumId, usuarioId);

                return Json(new { success = true, message = $"{archivos.Count} archivo(s) movido(s)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al mover archivos");
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
                return Json(new { success = false, message = "Error al mover archivos" });
            }
        }

        // ========================================
        // FAVORITOS
        // ========================================

        [HttpPost("ToggleFavorito")]
        public async Task<IActionResult> ToggleFavorito([FromBody] ToggleFavoritoDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                var media = await _context.MediasGaleria
                    .FirstOrDefaultAsync(m => m.Id == dto.MediaId && m.UsuarioId == usuarioId);

                if (media == null)
                    return Json(new { success = false, message = "Archivo no encontrado" });

                media.EsFavorito = !media.EsFavorito;
                await _context.SaveChangesAsync();

                return Json(new { success = true, esFavorito = media.EsFavorito });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al cambiar favorito");
                return Json(new { success = false, message = "Error al cambiar favorito" });
            }
        }

        // ========================================
        // API PARA SELECTOR (MODAL)
        // ========================================

        [HttpGet("ObtenerArchivos")]
        public async Task<IActionResult> ObtenerArchivos(int? albumId = null, string? tipo = null,
            bool? soloFavoritos = null, int pagina = 1, int cantidad = 50)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                var query = _context.MediasGaleria
                    .Where(m => m.UsuarioId == usuarioId);

                if (albumId.HasValue)
                    query = query.Where(m => m.AlbumId == albumId.Value);

                if (!string.IsNullOrEmpty(tipo))
                {
                    if (tipo.ToLower() == "imagen")
                        query = query.Where(m => m.TipoMedia == TipoMediaGaleria.Imagen);
                    else if (tipo.ToLower() == "video")
                        query = query.Where(m => m.TipoMedia == TipoMediaGaleria.Video);
                }

                if (soloFavoritos == true)
                    query = query.Where(m => m.EsFavorito);

                var total = await query.CountAsync();

                var archivos = await query
                    .OrderByDescending(m => m.FechaSubida)
                    .Skip((pagina - 1) * cantidad)
                    .Take(cantidad)
                    .Select(m => new
                    {
                        m.Id,
                        m.RutaArchivo,
                        m.Thumbnail,
                        tipoMedia = m.TipoMedia.ToString().ToLower(),
                        m.NombreOriginal,
                        m.TamanoBytes,
                        m.DuracionSegundos,
                        m.EsFavorito,
                        m.FechaSubida,
                        m.AlbumId
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    archivos,
                    total,
                    pagina,
                    totalPaginas = (int)Math.Ceiling((double)total / cantidad)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al obtener archivos");
                return Json(new { success = false, message = "Error al obtener archivos" });
            }
        }

        [HttpGet("ObtenerAlbums")]
        public async Task<IActionResult> ObtenerAlbums()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                var albums = await _context.Albums
                    .Where(a => a.UsuarioId == usuarioId)
                    .OrderBy(a => a.Orden)
                    .Select(a => new
                    {
                        a.Id,
                        a.Nombre,
                        a.ImagenPortada,
                        cantidadArchivos = a.Archivos.Count
                    })
                    .ToListAsync();

                return Json(new { success = true, albums });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al obtener álbumes");
                return Json(new { success = false, message = "Error al obtener álbumes" });
            }
        }

        [HttpGet("ObtenerMedia/{id}")]
        public async Task<IActionResult> ObtenerMedia(int id)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                var media = await _context.MediasGaleria
                    .Where(m => m.Id == id && m.UsuarioId == usuarioId)
                    .Select(m => new
                    {
                        m.Id,
                        m.RutaArchivo,
                        m.Thumbnail,
                        tipoMedia = m.TipoMedia.ToString().ToLower(),
                        m.NombreOriginal,
                        m.TamanoBytes,
                        m.DuracionSegundos,
                        m.Ancho,
                        m.Alto,
                        m.Descripcion,
                        m.Tags,
                        m.EsFavorito,
                        m.FechaSubida,
                        m.AlbumId,
                        albumNombre = m.Album != null ? m.Album.Nombre : null
                    })
                    .FirstOrDefaultAsync();

                if (media == null)
                    return Json(new { success = false, message = "Archivo no encontrado" });

                return Json(new { success = true, media });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al obtener media {MediaId}", id);
                return Json(new { success = false, message = "Error al obtener archivo" });
            }
        }

        // ========================================
        // ACTUALIZAR DESCRIPCIÓN/TAGS
        // ========================================

        [HttpPost("ActualizarInfo")]
        public async Task<IActionResult> ActualizarInfo([FromBody] ActualizarInfoDto dto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                var media = await _context.MediasGaleria
                    .FirstOrDefaultAsync(m => m.Id == dto.MediaId && m.UsuarioId == usuarioId);

                if (media == null)
                    return Json(new { success = false, message = "Archivo no encontrado" });

                media.Descripcion = dto.Descripcion?.Trim();
                media.Tags = dto.Tags?.Trim();

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Información actualizada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Galeria] Error al actualizar info");
                return Json(new { success = false, message = "Error al actualizar información" });
            }
        }

        // ========================================
        // MÉTODOS AUXILIARES
        // ========================================

        private string GetRelativePath(string fullPath)
        {
            var webRoot = _environment.WebRootPath;
            if (fullPath.StartsWith(webRoot))
            {
                return fullPath.Substring(webRoot.Length).Replace("\\", "/");
            }
            return fullPath.Replace("\\", "/");
        }

        private async Task<string?> GenerarThumbnailVideo(string rutaVideo, string carpetaDestino, string nombreBase)
        {
            try
            {
                var rutaThumbnail = Path.Combine(carpetaDestino, $"{nombreBase}_thumb.jpg");

                // Usar FFmpeg para extraer un frame
                var ffmpegPath = Path.Combine(_environment.ContentRootPath, "tools", "ffmpeg.exe");
                if (!System.IO.File.Exists(ffmpegPath))
                {
                    // Intentar con ffmpeg en PATH
                    ffmpegPath = "ffmpeg";
                }

                var args = $"-i \"{rutaVideo}\" -ss 00:00:01 -vframes 1 -vf \"scale=300:-1\" -y \"{rutaThumbnail}\"";

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (System.IO.File.Exists(rutaThumbnail))
                    return rutaThumbnail;

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Galeria] Error al generar thumbnail de video");
                return null;
            }
        }

        private async Task<string> CalcularHashArchivo(string rutaArchivo)
        {
            using var sha256 = SHA256.Create();
            using var stream = System.IO.File.OpenRead(rutaArchivo);

            // Solo leer los primeros 1MB para el hash (más rápido para archivos grandes)
            var buffer = new byte[Math.Min(1024 * 1024, stream.Length)];
            await stream.ReadAsync(buffer, 0, buffer.Length);

            var hash = sha256.ComputeHash(buffer);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private async Task<object> CalcularEstadisticasAsync(string usuarioId)
        {
            var medias = await _context.MediasGaleria
                .Where(m => m.UsuarioId == usuarioId)
                .ToListAsync();

            var albums = await _context.Albums
                .Where(a => a.UsuarioId == usuarioId)
                .Select(a => new { a.Id, Count = a.Archivos.Count })
                .ToListAsync();

            var totalBytes = medias.Sum(m => m.TamanoBytes);

            return new
            {
                totalArchivos = medias.Count,
                totalEspacio = FormatearTamano(totalBytes),
                albumCounts = albums.ToDictionary(a => a.Id, a => a.Count)
            };
        }

        private static string FormatearTamano(long bytes)
        {
            if (bytes >= 1073741824) return $"{bytes / 1073741824.0:F1} GB";
            if (bytes >= 1048576) return $"{bytes / 1048576.0:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }

    // ========================================
    // DTOs
    // ========================================

    public class EliminarArchivosDto
    {
        public List<int> Ids { get; set; } = new();
    }

    public class ObtenerMediaInfoDto
    {
        public List<int> Ids { get; set; } = new();
    }

    public class CrearAlbumDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    public class EditarAlbumDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
    }

    public class EliminarAlbumDto
    {
        public int Id { get; set; }
    }

    public class MoverArchivosDto
    {
        public List<int> MediaIds { get; set; } = new();
        public int? AlbumId { get; set; }
    }

    public class ToggleFavoritoDto
    {
        public int MediaId { get; set; }
    }

    public class ActualizarInfoDto
    {
        public int MediaId { get; set; }
        public string? Descripcion { get; set; }
        public string? Tags { get; set; }
    }
}
