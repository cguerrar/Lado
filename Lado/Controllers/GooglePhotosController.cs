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
    public class GooglePhotosController : Controller
    {
        private readonly IGooglePhotosService _googlePhotosService;
        private readonly IWebHostEnvironment _environment;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<GooglePhotosController> _logger;
        private readonly IImageService _imageService;
        private readonly ILogEventoService _logEventoService;
        private readonly ICuotaGaleriaService _cuotaService;

        public GooglePhotosController(
            IGooglePhotosService googlePhotosService,
            IWebHostEnvironment environment,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<GooglePhotosController> logger,
            IImageService imageService,
            ILogEventoService logEventoService,
            ICuotaGaleriaService cuotaService)
        {
            _googlePhotosService = googlePhotosService;
            _environment = environment;
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _imageService = imageService;
            _logEventoService = logEventoService;
            _cuotaService = cuotaService;
        }

        // ========================================
        // INICIAR FLUJO OAUTH
        // ========================================

        /// <summary>
        /// Inicia el flujo OAuth - redirige a Google
        /// </summary>
        [HttpGet("Conectar")]
        public IActionResult Conectar()
        {
            try
            {
                // Generar state para prevenir CSRF
                var state = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("GooglePhotosState", state);

                // Construir redirect URI dinamicamente basado en el dominio actual
                var redirectUri = $"{Request.Scheme}://{Request.Host}/GooglePhotos/Callback";
                HttpContext.Session.SetString("GooglePhotosRedirectUri", redirectUri);

                var url = _googlePhotosService.GenerarUrlAutorizacion(state, redirectUri);

                _logger.LogInformation("[GooglePhotos] Usuario {UserId} iniciando conexion con Google Photos. RedirectUri: {RedirectUri}",
                    User.FindFirstValue(ClaimTypes.NameIdentifier), redirectUri);

                return Redirect(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GooglePhotos] Error al generar URL de autorizacion");
                TempData["Error"] = "Error al conectar con Google Photos";
                return RedirectToAction("Index", "Galeria");
            }
        }

        // ========================================
        // CALLBACK DE GOOGLE
        // ========================================

        /// <summary>
        /// Callback de Google despues de autorizar
        /// </summary>
        [HttpGet("Callback")]
        public async Task<IActionResult> Callback(string? code, string? state, string? error)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                // Verificar si hubo error
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("[GooglePhotos] Error de Google: {Error} para usuario {UserId}",
                        error, usuarioId);

                    TempData["Error"] = error == "access_denied"
                        ? "Acceso denegado. Debes autorizar el acceso a Google Photos."
                        : $"Error de Google: {error}";

                    return RedirectToAction("Index", "Galeria");
                }

                // Verificar que tenemos code
                if (string.IsNullOrEmpty(code))
                {
                    _logger.LogWarning("[GooglePhotos] Callback sin codigo para usuario {UserId}", usuarioId);
                    TempData["Error"] = "No se recibio codigo de autorizacion";
                    return RedirectToAction("Index", "Galeria");
                }

                // Verificar state para prevenir CSRF
                var savedState = HttpContext.Session.GetString("GooglePhotosState");
                if (string.IsNullOrEmpty(savedState) || savedState != state)
                {
                    _logger.LogWarning("[GooglePhotos] State invalido para usuario {UserId}. Esperado: {Expected}, Recibido: {Received}",
                        usuarioId, savedState, state);

                    TempData["Error"] = "Estado de sesion invalido. Por favor intenta de nuevo.";
                    return RedirectToAction("Index", "Galeria");
                }

                // Limpiar state de sesion
                HttpContext.Session.Remove("GooglePhotosState");

                // Obtener el redirect URI guardado
                var redirectUri = HttpContext.Session.GetString("GooglePhotosRedirectUri")
                    ?? $"{Request.Scheme}://{Request.Host}/GooglePhotos/Callback";

                // Intercambiar code por token
                var tokenResponse = await _googlePhotosService.IntercambiarCodigoAsync(code, redirectUri);

                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                {
                    _logger.LogWarning("[GooglePhotos] Error al obtener token para usuario {UserId}", usuarioId);
                    TempData["Error"] = "Error al obtener token de acceso";
                    return RedirectToAction("Index", "Galeria");
                }

                // Guardar tokens en sesion
                HttpContext.Session.SetString("GooglePhotosToken", tokenResponse.AccessToken);

                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    HttpContext.Session.SetString("GooglePhotosRefreshToken", tokenResponse.RefreshToken);
                }

                if (tokenResponse.ExpiresIn > 0)
                {
                    var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    HttpContext.Session.SetString("GooglePhotosTokenExpires", expiresAt.ToString("O"));
                }

                _logger.LogInformation("[GooglePhotos] Usuario {UserId} conectado exitosamente a Google Photos",
                    usuarioId);

                TempData["Success"] = "Conectado exitosamente a Google Photos";

                // Redirigir a la galeria con parametro para abrir modal
                return RedirectToAction("Index", "Galeria", new { abrirGooglePhotos = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GooglePhotos] Error en callback para usuario {UserId}", usuarioId);
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, usuarioId, User.Identity?.Name);

                TempData["Error"] = "Error al procesar la autorizacion de Google Photos";
                return RedirectToAction("Index", "Galeria");
            }
        }

        // ========================================
        // OBTENER ALBUMES (AJAX)
        // ========================================

        /// <summary>
        /// Obtener albumes del usuario de Google Photos
        /// </summary>
        [HttpGet("Albumes")]
        public async Task<IActionResult> Albumes(string? pageToken)
        {
            try
            {
                var accessToken = await ObtenerTokenValidoAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Json(new { success = false, message = "No conectado a Google Photos", requiresAuth = true });
                }

                var albumList = await _googlePhotosService.ObtenerAlbumesAsync(accessToken, pageToken);

                if (albumList == null)
                {
                    // Posible token expirado
                    HttpContext.Session.Remove("GooglePhotosToken");
                    return Json(new { success = false, message = "Error al obtener albumes. Sesion posiblemente expirada.", requiresAuth = true });
                }

                return Json(new
                {
                    success = true,
                    albums = albumList.Albums.Select(a => new
                    {
                        id = a.Id,
                        title = a.Title,
                        coverPhotoBaseUrl = a.CoverPhotoBaseUrl,
                        mediaItemsCount = a.MediaItemsCount
                    }),
                    nextPageToken = albumList.NextPageToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GooglePhotos] Error al obtener albumes");
                return Json(new { success = false, message = "Error al obtener albumes" });
            }
        }

        // ========================================
        // OBTENER FOTOS (AJAX)
        // ========================================

        /// <summary>
        /// Obtener fotos de un album o todas las fotos
        /// </summary>
        [HttpGet("Fotos")]
        public async Task<IActionResult> Fotos(string? albumId, string? pageToken)
        {
            try
            {
                var accessToken = await ObtenerTokenValidoAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Json(new { success = false, message = "No conectado a Google Photos", requiresAuth = true });
                }

                var mediaList = await _googlePhotosService.ObtenerMediaItemsAsync(accessToken, albumId, pageToken);

                if (mediaList == null)
                {
                    // Posible token expirado
                    HttpContext.Session.Remove("GooglePhotosToken");
                    return Json(new { success = false, message = "Error al obtener fotos. Sesion posiblemente expirada.", requiresAuth = true });
                }

                return Json(new
                {
                    success = true,
                    mediaItems = mediaList.MediaItems.Select(m => new
                    {
                        id = m.Id,
                        baseUrl = m.BaseUrl,
                        filename = m.Filename,
                        mimeType = m.MimeType,
                        esVideo = m.EsVideo,
                        esImagen = m.EsImagen,
                        width = m.MediaMetadata?.WidthInt ?? 0,
                        height = m.MediaMetadata?.HeightInt ?? 0,
                        creationTime = m.MediaMetadata?.CreationTime
                    }),
                    nextPageToken = mediaList.NextPageToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GooglePhotos] Error al obtener fotos");
                return Json(new { success = false, message = "Error al obtener fotos" });
            }
        }

        // ========================================
        // IMPORTAR FOTOS (AJAX)
        // ========================================

        /// <summary>
        /// Importar fotos seleccionadas desde Google Photos
        /// </summary>
        [HttpPost("Importar")]
        public async Task<IActionResult> Importar([FromBody] ImportarGooglePhotosDto dto)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "Usuario no autenticado" });

                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                    return Json(new { success = false, message = "Usuario no encontrado" });

                if (dto.MediaItems == null || dto.MediaItems.Count == 0)
                    return Json(new { success = false, message = "No se seleccionaron fotos" });

                var accessToken = await ObtenerTokenValidoAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return Json(new { success = false, message = "No conectado a Google Photos", requiresAuth = true });
                }

                // Verificar que el album destino pertenezca al usuario (si se especifico)
                if (dto.AlbumId.HasValue)
                {
                    var albumExiste = await _context.Albums
                        .AnyAsync(a => a.Id == dto.AlbumId.Value && a.UsuarioId == usuarioId);
                    if (!albumExiste)
                        return Json(new { success = false, message = "Album destino no encontrado" });
                }

                // Estimar tamano aproximado (5MB por foto promedio)
                var tamanoEstimado = dto.MediaItems.Count * 5 * 1024 * 1024L;
                if (!await _cuotaService.PuedeSubirAsync(usuarioId, tamanoEstimado))
                {
                    return Json(new { success = false, message = "No tienes suficiente espacio en tu galeria" });
                }

                var username = usuario.UserName ?? usuario.Id;
                var carpetaGaleria = Path.Combine(_environment.WebRootPath, "uploads", "galeria", username);

                if (!Directory.Exists(carpetaGaleria))
                    Directory.CreateDirectory(carpetaGaleria);

                var importados = new List<object>();
                var errores = new List<string>();
                var duplicados = 0;

                foreach (var item in dto.MediaItems)
                {
                    try
                    {
                        var esVideo = item.MimeType?.StartsWith("video/") == true;

                        _logger.LogInformation("[GooglePhotos] Descargando {Filename} desde Google Photos", item.Filename);

                        // Usar el servicio para descargar el archivo
                        var contentBytes = await _googlePhotosService.DescargarMediaAsync(accessToken, item.BaseUrl, esVideo);

                        if (contentBytes == null || contentBytes.Length == 0)
                        {
                            errores.Add($"{item.Filename}: Error al descargar o archivo vacio");
                            continue;
                        }

                        // Calcular hash para detectar duplicados
                        var hash = CalcularHash(contentBytes);

                        // Verificar si ya existe
                        var existente = await _context.MediasGaleria
                            .FirstOrDefaultAsync(m => m.UsuarioId == usuarioId && m.HashArchivo == hash);

                        if (existente != null)
                        {
                            duplicados++;
                            continue;
                        }

                        // Determinar tipo y extension
                        var extension = ObtenerExtension(item.MimeType, item.Filename);
                        var nombreBase = Guid.NewGuid().ToString();

                        // Guardar archivo
                        var rutaArchivo = Path.Combine(carpetaGaleria, $"{nombreBase}{extension}");
                        await System.IO.File.WriteAllBytesAsync(rutaArchivo, contentBytes);

                        // Generar thumbnail
                        string? rutaThumbnail = null;
                        if (esVideo)
                        {
                            rutaThumbnail = await GenerarThumbnailVideo(rutaArchivo, carpetaGaleria, nombreBase);
                        }
                        else
                        {
                            rutaThumbnail = await _imageService.GenerarThumbnailAsync(rutaArchivo, 300, 300, 80);
                        }

                        // Crear registro en BD
                        var fileInfo = new FileInfo(rutaArchivo);
                        var media = new MediaGaleria
                        {
                            UsuarioId = usuarioId,
                            AlbumId = dto.AlbumId,
                            RutaArchivo = GetRelativePath(rutaArchivo),
                            Thumbnail = !string.IsNullOrEmpty(rutaThumbnail) ? GetRelativePath(rutaThumbnail) : null,
                            NombreOriginal = item.Filename ?? $"google_photo_{nombreBase}{extension}",
                            TipoMedia = esVideo ? TipoMediaGaleria.Video : TipoMediaGaleria.Imagen,
                            TamanoBytes = fileInfo.Length,
                            FechaSubida = DateTime.Now,
                            HashArchivo = hash
                        };

                        _context.MediasGaleria.Add(media);
                        await _context.SaveChangesAsync();

                        importados.Add(new
                        {
                            id = media.Id,
                            rutaArchivo = media.RutaArchivo,
                            thumbnail = media.Thumbnail,
                            tipoMedia = media.TipoMedia,
                            nombreOriginal = media.NombreOriginal,
                            tamanoBytes = media.TamanoBytes
                        });

                        _logger.LogInformation("[GooglePhotos] Importado: {Filename} ({Size} bytes)",
                            item.Filename, fileInfo.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[GooglePhotos] Error al importar {Filename}", item.Filename);
                        errores.Add($"{item.Filename}: Error al procesar");
                    }
                }

                // Obtener info de cuota actualizada
                var infoCuota = await _cuotaService.ObtenerInfoCuotaAsync(usuarioId);

                _logger.LogInformation("[GooglePhotos] Importacion completada para usuario {UserId}: {Imported} importados, {Duplicates} duplicados, {Errors} errores",
                    usuarioId, importados.Count, duplicados, errores.Count);

                await _logEventoService.RegistrarEventoAsync(
                    $"Importacion de Google Photos: {importados.Count} archivos",
                    CategoriaEvento.Contenido,
                    TipoLogEvento.Info,
                    usuarioId,
                    usuario.UserName,
                    $"Importados: {importados.Count}, Duplicados: {duplicados}, Errores: {errores.Count}");

                return Json(new
                {
                    success = importados.Count > 0 || duplicados > 0,
                    archivos = importados,
                    errores,
                    message = GenerarMensajeResultado(importados.Count, duplicados, errores.Count),
                    estadisticas = new
                    {
                        importados = importados.Count,
                        duplicados,
                        errores = errores.Count
                    },
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
                _logger.LogError(ex, "[GooglePhotos] Error general en importacion para usuario {UserId}", usuarioId);
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, usuarioId, User.Identity?.Name);
                return Json(new { success = false, message = "Error al importar fotos" });
            }
        }

        // ========================================
        // DESCONECTAR (AJAX)
        // ========================================

        /// <summary>
        /// Desconectar de Google Photos (limpiar sesion)
        /// </summary>
        [HttpPost("Desconectar")]
        public IActionResult Desconectar()
        {
            try
            {
                HttpContext.Session.Remove("GooglePhotosToken");
                HttpContext.Session.Remove("GooglePhotosRefreshToken");
                HttpContext.Session.Remove("GooglePhotosTokenExpires");
                HttpContext.Session.Remove("GooglePhotosState");

                _logger.LogInformation("[GooglePhotos] Usuario {UserId} desconectado de Google Photos",
                    User.FindFirstValue(ClaimTypes.NameIdentifier));

                return Json(new { success = true, message = "Desconectado de Google Photos" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GooglePhotos] Error al desconectar");
                return Json(new { success = false, message = "Error al desconectar" });
            }
        }

        // ========================================
        // VERIFICAR CONEXION (AJAX)
        // ========================================

        /// <summary>
        /// Verificar si el usuario esta conectado a Google Photos
        /// </summary>
        [HttpGet("Estado")]
        public IActionResult Estado()
        {
            var token = HttpContext.Session.GetString("GooglePhotosToken");
            var conectado = !string.IsNullOrEmpty(token);

            // Verificar si el token ha expirado
            if (conectado)
            {
                var expiresStr = HttpContext.Session.GetString("GooglePhotosTokenExpires");
                if (!string.IsNullOrEmpty(expiresStr) &&
                    DateTime.TryParse(expiresStr, out var expiresAt) &&
                    expiresAt <= DateTime.UtcNow)
                {
                    conectado = false;
                }
            }

            return Json(new { conectado });
        }

        // ========================================
        // METODOS AUXILIARES
        // ========================================

        /// <summary>
        /// Obtiene un token valido, refrescandolo si es necesario
        /// </summary>
        private async Task<string?> ObtenerTokenValidoAsync()
        {
            var accessToken = HttpContext.Session.GetString("GooglePhotosToken");
            if (string.IsNullOrEmpty(accessToken))
                return null;

            // Verificar si el token ha expirado
            var expiresStr = HttpContext.Session.GetString("GooglePhotosTokenExpires");
            if (!string.IsNullOrEmpty(expiresStr) &&
                DateTime.TryParse(expiresStr, out var expiresAt))
            {
                // Refrescar si expira en menos de 5 minutos
                if (expiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    var refreshToken = HttpContext.Session.GetString("GooglePhotosRefreshToken");
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        var tokenResponse = await _googlePhotosService.RefrescarTokenAsync(refreshToken);
                        if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
                        {
                            HttpContext.Session.SetString("GooglePhotosToken", tokenResponse.AccessToken);

                            if (tokenResponse.ExpiresIn > 0)
                            {
                                var newExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                                HttpContext.Session.SetString("GooglePhotosTokenExpires", newExpiresAt.ToString("O"));
                            }

                            return tokenResponse.AccessToken;
                        }
                    }

                    // No se pudo refrescar
                    return null;
                }
            }

            return accessToken;
        }

        private string GetRelativePath(string fullPath)
        {
            var webRoot = _environment.WebRootPath;
            if (fullPath.StartsWith(webRoot))
            {
                return fullPath.Substring(webRoot.Length).Replace("\\", "/");
            }
            return fullPath.Replace("\\", "/");
        }

        private static string CalcularHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            // Solo usar los primeros 1MB para el hash (mas rapido para archivos grandes)
            var bytesToHash = Math.Min(1024 * 1024, data.Length);
            var hash = sha256.ComputeHash(data, 0, bytesToHash);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string ObtenerExtension(string? mimeType, string? filename)
        {
            // Primero intentar obtener del nombre del archivo
            if (!string.IsNullOrEmpty(filename))
            {
                var ext = Path.GetExtension(filename).ToLowerInvariant();
                if (!string.IsNullOrEmpty(ext))
                    return ext;
            }

            // Fallback a MIME type
            return mimeType?.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/heic" => ".heic",
                "image/heif" => ".heif",
                "video/mp4" => ".mp4",
                "video/quicktime" => ".mov",
                "video/x-msvideo" => ".avi",
                "video/webm" => ".webm",
                _ => ".jpg" // Default
            };
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
                _logger.LogWarning(ex, "[GooglePhotos] Error al generar thumbnail de video");
                return null;
            }
        }

        private static string GenerarMensajeResultado(int importados, int duplicados, int errores)
        {
            var partes = new List<string>();

            if (importados > 0)
                partes.Add($"{importados} archivo(s) importado(s)");

            if (duplicados > 0)
                partes.Add($"{duplicados} duplicado(s) omitido(s)");

            if (errores > 0)
                partes.Add($"{errores} error(es)");

            if (partes.Count == 0)
                return "No se importaron archivos";

            return string.Join(", ", partes);
        }
    }

    // ========================================
    // DTOs
    // ========================================

    public class ImportarGooglePhotosDto
    {
        public List<GooglePhotoItemDto> MediaItems { get; set; } = new();
        public int? AlbumId { get; set; } // Album destino en Lado (opcional)
    }

    public class GooglePhotoItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string? Filename { get; set; }
        public string? MimeType { get; set; }
    }
}
