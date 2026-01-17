using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Lado.Controllers
{
    [Authorize]
    public class ContenidoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ContenidoController> _logger;
        private readonly INotificationService _notificationService;
        private readonly IImageService _imageService;
        private readonly IMediaConversionService _mediaConversionService;
        private readonly IClaudeClassificationService _classificationService;
        private readonly IRateLimitService _rateLimitService;
        private readonly IExifService _exifService;
        private readonly ILogEventoService _logEventoService;
        private readonly IFileValidationService _fileValidationService;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly IRachasService _rachasService;
        private readonly IServiceProvider _serviceProvider;

        public ContenidoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<ContenidoController> logger,
            INotificationService notificationService,
            IImageService imageService,
            IMediaConversionService mediaConversionService,
            IClaudeClassificationService classificationService,
            IRateLimitService rateLimitService,
            IExifService exifService,
            ILogEventoService logEventoService,
            IFileValidationService fileValidationService,
            ILadoCoinsService ladoCoinsService,
            IRachasService rachasService,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _logger = logger;
            _notificationService = notificationService;
            _imageService = imageService;
            _mediaConversionService = mediaConversionService;
            _classificationService = classificationService;
            _rateLimitService = rateLimitService;
            _exifService = exifService;
            _logEventoService = logEventoService;
            _fileValidationService = fileValidationService;
            _ladoCoinsService = ladoCoinsService;
            _rachasService = rachasService;
            _serviceProvider = serviceProvider;
        }

        // ========================================
        // RENOVAR TOKEN CSRF (para páginas que llevan tiempo abiertas)
        // ========================================

        /// <summary>
        /// Obtiene un nuevo token CSRF para renovar formularios que llevan tiempo abiertos
        /// </summary>
        [HttpGet]
        public IActionResult RenovarToken([FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
        {
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            return Json(new {
                success = true,
                token = tokens.RequestToken,
                timestamp = DateTime.UtcNow
            });
        }

        // ========================================
        // DIAGNÓSTICO DE UPLOAD (temporal)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DiagnosticoUpload(List<IFormFile> archivos)
        {
            var usuario = await _userManager.GetUserAsync(User);
            var archivosInfo = new List<object>();

            if (archivos != null)
            {
                foreach (var a in archivos)
                {
                    // Leer los primeros 64 bytes para diagnóstico
                    byte[] header = new byte[64];
                    using (var stream = a.OpenReadStream())
                    {
                        await stream.ReadAsync(header, 0, 64);
                    }

                    // Intentar validar
                    var validacion = await _fileValidationService.ValidarVideoAsync(a);

                    archivosInfo.Add(new
                    {
                        nombre = a.FileName,
                        contentType = a.ContentType,
                        tamanoMB = Math.Round(a.Length / (1024.0 * 1024.0), 2),
                        extension = Path.GetExtension(a.FileName)?.ToLower(),
                        headerHex = BitConverter.ToString(header.Take(32).ToArray()),
                        headerAscii = new string(header.Take(32).Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray()),
                        validacion = new
                        {
                            esValido = validacion.EsValido,
                            tipoDetectado = validacion.TipoDetectado,
                            error = validacion.MensajeError
                        }
                    });
                }
            }

            var resultado = new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                usuario = usuario?.UserName ?? "no autenticado",
                archivosRecibidos = archivos?.Count ?? 0,
                archivos = archivosInfo,
                requestContentType = Request.ContentType,
                requestContentLength = Request.ContentLength
            };

            // Registrar en Admin/Logs
            await _logEventoService.RegistrarEventoAsync(
                $"Diagnóstico Upload: {archivos?.Count ?? 0} archivos",
                CategoriaEvento.Contenido,
                TipoLogEvento.Info,
                usuario?.Id,
                usuario?.UserName,
                System.Text.Json.JsonSerializer.Serialize(resultado, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
            );

            return Json(resultado);
        }

        // ========================================
        // INDEX - LISTADO DE CONTENIDO DEL USUARIO
        // ========================================

        public async Task<IActionResult> Index(string? filtro = null)
        {
            // Evitar cache del navegador para mostrar siempre el contenido más reciente
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            var usuario = await _userManager.GetUserAsync(User);

            if (usuario == null)
            {
                _logger.LogWarning("Usuario no encontrado en Index");
                return RedirectToAction("Login", "Account");
            }

            // Determinar modo actual basado en LadoPreferido
            var enModoLadoA = usuario.LadoPreferido == TipoLado.LadoA;
            ViewBag.ModoActual = enModoLadoA ? "LadoA" : "LadoB";

            // Si no se especifica filtro, usar el modo actual como default
            if (string.IsNullOrEmpty(filtro))
            {
                filtro = enModoLadoA ? "ladoa" : "ladob";
            }

            var query = _context.Contenidos.AsQueryable();
            query = query.Where(c => c.UsuarioId == usuario.Id && c.EstaActivo);

            switch (filtro.ToLower())
            {
                case "publicados":
                    query = query.Where(c => !c.EsBorrador);
                    break;
                case "borradores":
                    query = query.Where(c => c.EsBorrador);
                    break;
                case "programados":
                    query = query.Where(c => false);
                    break;
                case "ladoa":
                    query = query.Where(c => c.TipoLado == TipoLado.LadoA);
                    break;
                case "ladob":
                    query = query.Where(c => c.TipoLado == TipoLado.LadoB);
                    break;
                case "todos":
                    // Mostrar todo
                    break;
                default:
                    // Filtro no reconocido, usar modo actual
                    query = query.Where(c => c.TipoLado == usuario.LadoPreferido);
                    break;
            }

            var contenidos = await query
                .OrderByDescending(c => c.FechaPublicacion)
                .ToListAsync();

            ViewBag.FiltroActual = filtro;

            // Estadísticas por tipo
            ViewBag.TotalLadoA = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.EstaActivo && c.TipoLado == TipoLado.LadoA);
            ViewBag.TotalLadoB = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.EstaActivo && c.TipoLado == TipoLado.LadoB);

            return View(contenidos);
        }

        // ========================================
        // CREAR CONTENIDO - GET
        // ========================================

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("Usuario no encontrado en Crear");
                return RedirectToAction("Login", "Account");
            }

            ViewBag.UsuarioVerificado = user.CreadorVerificado;
            ViewBag.LadoPreferido = user.LadoPreferido;
            ViewBag.PermitirPreviewBlur = user.PermitirPreviewBlurLadoB && user.TieneLadoB();

            // Cargar límites de archivos desde configuración
            var configuraciones = await _context.ConfiguracionesPlataforma
                .Where(c => c.Clave.StartsWith("Limite_"))
                .ToDictionaryAsync(c => c.Clave, c => c.Valor);

            ViewBag.LimiteFotoMB = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LIMITE_TAMANO_FOTO_MB, "10"), out var fotoMb) ? fotoMb : 10;
            ViewBag.LimiteVideoMB = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LIMITE_TAMANO_VIDEO_MB, "100"), out var videoMb) ? videoMb : 100;
            ViewBag.LimiteCantidadArchivos = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LIMITE_CANTIDAD_ARCHIVOS, "10"), out var cantArchivos) ? cantArchivos : 10;

            _logger.LogInformation("GET Crear - Usuario: {Username}, Verificado: {Verificado}, LadoPreferido: {LadoPreferido}",
                user.UserName, user.CreadorVerificado, user.LadoPreferido);

            return View();
        }

        // ========================================
        // CREAR CONTENIDO - POST
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(500_000_000)] // 500MB para videos grandes
        [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
        public async Task<IActionResult> Crear(
            IFormFile archivo,
            string Descripcion,
            int TipoContenido,
            bool EsGratis,
            decimal? PrecioDesbloqueo = null,
            bool EsBorrador = false,
            bool EsPublicoGeneral = false,
            bool CrearPreviewBlur = false,
            int TipoCensuraPreview = 0,
            bool SoloSuscriptores = false,
            bool PublicarEnLadoB = false)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                if (usuario == null)
                {
                    _logger.LogWarning("Usuario no encontrado en Crear POST");
                    return RedirectToAction("Login", "Account");
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = Request.Headers["User-Agent"].ToString();
                var rateLimitKey = $"content_create_{usuario.Id}";
                var rateLimitKeyHourly = $"content_create_hourly_{usuario.Id}";
                var rateLimitKeyDaily = $"content_create_daily_{usuario.Id}";
                var rateLimitKeyIp = $"content_create_ip_{clientIp}";
                var rateLimitKeyIpHourly = $"content_create_ip_hourly_{clientIp}";

                // 🚨 Rate limit por IP (detectar ataques multi-cuenta) - registra en BD
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIp, RateLimits.ContentCreation_IP_MaxRequests, RateLimits.ContentCreation_IP_Window,
                    TipoAtaque.SpamContenido, "/Contenido/Crear", usuario.Id, userAgent))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP: IP {IP} excedió límite de 5 min - Usuario: {UserId} ({UserName})",
                        clientIp, usuario.Id, usuario.UserName);
                    TempData["Error"] = "Demasiadas solicitudes desde tu conexión. Espera unos minutos.";
                    return RedirectToAction("Index");
                }

                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIpHourly, RateLimits.ContentCreation_IP_Hourly_MaxRequests, RateLimits.ContentCreation_IP_Hourly_Window,
                    TipoAtaque.SpamContenido, "/Contenido/Crear", usuario.Id, userAgent))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP HORARIO: IP {IP} excedió límite de 1 hora - Usuario: {UserId}",
                        clientIp, usuario.Id);
                    TempData["Error"] = "Demasiadas solicitudes desde tu conexión. Intenta más tarde.";
                    return RedirectToAction("Index");
                }

                // Límite por usuario - 5 minutos: máximo 10 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, RateLimits.ContentCreation_MaxRequests, RateLimits.ContentCreation_Window,
                    TipoAtaque.SpamContenido, "/Contenido/Crear", usuario.Id, userAgent))
                {
                    _logger.LogWarning("🚫 RATE LIMIT: Usuario {UserId} ({UserName}) excedió límite de 5 min - IP: {IP}",
                        usuario.Id, usuario.UserName, clientIp);
                    TempData["Error"] = "Has creado demasiado contenido en poco tiempo. Espera unos minutos.";
                    return RedirectToAction("Index");
                }

                // Límite por hora: máximo 50 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyHourly, RateLimits.ContentCreation_Hourly_MaxRequests, RateLimits.ContentCreation_Hourly_Window,
                    TipoAtaque.SpamContenido, "/Contenido/Crear", usuario.Id, userAgent))
                {
                    _logger.LogWarning("🚫 RATE LIMIT HORARIO: Usuario {UserId} ({UserName}) excedió límite de 1 hora",
                        usuario.Id, usuario.UserName);
                    TempData["Error"] = "Has alcanzado el límite de contenido por hora. Intenta más tarde.";
                    return RedirectToAction("Index");
                }

                // Límite diario: máximo 100 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyDaily, RateLimits.ContentCreation_Daily_MaxRequests, RateLimits.ContentCreation_Daily_Window,
                    TipoAtaque.SpamContenido, "/Contenido/Crear", usuario.Id, userAgent))
                {
                    _logger.LogWarning("🚫 RATE LIMIT DIARIO: Usuario {UserId} ({UserName}) excedió límite de 24 horas",
                        usuario.Id, usuario.UserName);
                    TempData["Error"] = "Has alcanzado el límite diario de contenido. Intenta mañana.";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation("=== CREAR CONTENIDO ===");
                _logger.LogInformation("Usuario: {Username} (Real: {NombreCompleto}, Seudónimo: {Seudonimo})",
                    usuario.UserName, usuario.NombreCompleto, usuario.Seudonimo);
                _logger.LogInformation("Verificado: {Verificado}", usuario.CreadorVerificado);
                _logger.LogInformation("Parámetros - EsGratis: {EsGratis}, Precio: {Precio}, PublicarEnLadoB: {LadoB}",
                    EsGratis, PrecioDesbloqueo, PublicarEnLadoB);

                // ✅ GUARDAR LA INTENCIÓN ORIGINAL DEL USUARIO
                // NUEVO: Usar PublicarEnLadoB para determinar el lado, NO EsGratis
                var intentaPublicarEnLadoB = PublicarEnLadoB;

                // ✅ REGLA PRINCIPAL: Solo verificados pueden crear contenido en LadoB
                if (intentaPublicarEnLadoB && !usuario.CreadorVerificado)
                {
                    _logger.LogWarning("⚠️ Usuario {Username} intentó crear contenido LadoB sin verificación - Forzando LadoA",
                        usuario.UserName);

                    // Forzar a LadoA (gratis) para usuarios no verificados
                    intentaPublicarEnLadoB = false;
                    EsGratis = true;
                    PrecioDesbloqueo = 0;

                    TempData["Warning"] = "Para crear contenido en LadoB debes verificar tu identidad. Tu contenido se ha publicado en LadoA.";
                }

                // Validaciones básicas - archivo requerido para fotos/videos
                if (!EsBorrador && TipoContenido != (int)Models.TipoContenido.Post && (archivo == null || archivo.Length == 0))
                {
                    TempData["Error"] = "Debes subir un archivo para este tipo de contenido";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View();
                }

                // Validación de precio múltiplo de 5 (solo si NO es gratis Y está verificado)
                // Si SoloSuscriptores=true y PrecioDesbloqueo=0, significa que solo suscriptores pueden ver (sin compra individual)
                if (!EsGratis && usuario.CreadorVerificado)
                {
                    // Solo validar precio si hay compra individual disponible
                    var tieneCompraIndividual = !SoloSuscriptores || (PrecioDesbloqueo.HasValue && PrecioDesbloqueo > 0);

                    if (tieneCompraIndividual && PrecioDesbloqueo.HasValue && PrecioDesbloqueo > 0)
                    {
                        if (PrecioDesbloqueo % 5 != 0)
                        {
                            TempData["Error"] = "El precio debe ser un múltiplo de 5 (5, 10, 15, 20...)";
                            ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                            return View();
                        }
                    }

                    // Si no tiene precio y no es solo suscriptores, establecer precio default
                    if (!SoloSuscriptores && (!PrecioDesbloqueo.HasValue || PrecioDesbloqueo <= 0))
                    {
                        PrecioDesbloqueo = 10m;
                    }
                }

                // ⭐ DETERMINAR TIPO DE LADO USANDO LA INTENCIÓN ORIGINAL
                // Si intentó publicar en LadoB (aunque se forzó a gratis), va a LadoB
                var tipoLado = intentaPublicarEnLadoB ? TipoLado.LadoB : TipoLado.LadoA;
                var nombreMostrado = tipoLado == TipoLado.LadoA ? usuario.NombreCompleto : usuario.Seudonimo;

                _logger.LogInformation("🔍 DEBUG - IntentaLadoB: {IntentaLadoB}, EsGratis: {EsGratis}, TipoLado: {TipoLado}",
                    intentaPublicarEnLadoB, EsGratis, tipoLado);

                var contenido = new Contenido
                {
                    UsuarioId = usuario.Id,
                    TipoContenido = (Models.TipoContenido)TipoContenido,
                    Descripcion = Descripcion ?? "",
                    TipoLado = tipoLado,
                    EsGratis = EsGratis,
                    NombreMostrado = nombreMostrado,
                    EsPremium = !EsGratis,
                    PrecioDesbloqueo = EsGratis ? 0m : (PrecioDesbloqueo ?? 0m),
                    SoloSuscriptores = tipoLado == TipoLado.LadoB && SoloSuscriptores,
                    EsBorrador = EsBorrador,
                    FechaPublicacion = DateTime.Now,
                    EstaActivo = true,
                    NumeroLikes = 0,
                    NumeroComentarios = 0,
                    NumeroVistas = 0,
                    // Solo contenido LadoA puede ser publico general
                    EsPublicoGeneral = tipoLado == TipoLado.LadoA && EsPublicoGeneral
                };

                // ✅ Procesar archivo
                if (archivo != null && archivo.Length > 0)
                {
                    if (archivo.Length > 100 * 1024 * 1024)
                    {
                        TempData["Error"] = "El archivo excede el tamaño máximo de 100 MB";
                        ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                        return View();
                    }

                    // ✅ SEGURIDAD: Validar archivo con magic bytes (no solo extensión)
                    var tipoEsperado = contenido.TipoContenido == Models.TipoContenido.Foto
                        ? Services.TipoArchivoValidacion.Imagen
                        : Services.TipoArchivoValidacion.Video;

                    FileValidationResult validacionArchivo;
                    if (tipoEsperado == Services.TipoArchivoValidacion.Imagen)
                    {
                        validacionArchivo = await _fileValidationService.ValidarImagenAsync(archivo);
                    }
                    else
                    {
                        validacionArchivo = await _fileValidationService.ValidarVideoAsync(archivo);
                    }

                    if (!validacionArchivo.EsValido)
                    {
                        _logger.LogWarning("⚠️ Archivo rechazado en Crear: {FileName}, Error: {Error}",
                            archivo.FileName, validacionArchivo.MensajeError);

                        // Registrar en Admin/Logs para diagnóstico
                        await _logEventoService.RegistrarEventoAsync(
                            $"Archivo rechazado: {archivo.FileName}",
                            CategoriaEvento.Contenido,
                            TipoLogEvento.Warning,
                            usuario.Id,
                            usuario.UserName,
                            $"Error: {validacionArchivo.MensajeError}\nContentType: {archivo.ContentType}\nExtensión: {validacionArchivo.Extension}\nTamaño: {archivo.Length / 1024.0:F1} KB\nTipo detectado: {validacionArchivo.TipoDetectado ?? "N/A"}"
                        );

                        TempData["Error"] = validacionArchivo.MensajeError ?? "El archivo no es válido";
                        ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                        return View();
                    }

                    var extension = validacionArchivo.Extension ?? Path.GetExtension(archivo.FileName).ToLower();

                    var carpetaUsuario = usuario.UserName?.Replace("@", "_").Replace(".", "_") ?? usuario.Id;
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", carpetaUsuario);

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // ====================================================================
                    // 🔄 PROCESAMIENTO UNIFICADO - Usar ProcesarArchivoAsync
                    // Todos los archivos pasan por el mismo pipeline de conversión.
                    // NO hay fallback - si falla, se rechaza el archivo.
                    // ====================================================================
                    _logger.LogInformation("[Crear] 🔄 Procesando {FileName} ({Size}MB) con pipeline unificado...",
                        archivo.FileName, (archivo.Length / 1024.0 / 1024.0).ToString("F1"));

                    Services.MediaProcessingResult resultado;
                    using (var stream = archivo.OpenReadStream())
                    {
                        resultado = await _mediaConversionService.ProcesarArchivoAsync(stream, extension, uploadsFolder);
                    }

                    if (!resultado.Exitoso)
                    {
                        _logger.LogError("[Crear] ❌ Error procesando {FileName}: {Error}",
                            archivo.FileName, resultado.Error);

                        // Registrar en Admin/Logs
                        await _logEventoService.RegistrarEventoAsync(
                            $"❌ Error procesando archivo: {archivo.FileName}",
                            CategoriaEvento.Contenido,
                            TipoLogEvento.Error,
                            usuario.Id,
                            usuario.UserName,
                            $"Error: {resultado.Error}\nDetalle: {resultado.ErrorDetallado}\nExtensión: {extension}\nTamaño: {archivo.Length / 1024.0:F1} KB"
                        );

                        // NO hay fallback - rechazar el archivo
                        TempData["Error"] = resultado.Error ?? "Error al procesar el archivo. Intenta con otro formato.";
                        ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                        return View();
                    }

                    var filePath = resultado.RutaArchivo!;
                    var nombreConvertido = resultado.NombreArchivo!;
                    contenido.RutaArchivo = $"/uploads/{carpetaUsuario}/{nombreConvertido}";

                    _logger.LogInformation("[Crear] ✅ Archivo procesado: {Original} ({OriginalSize}MB) -> {Convertido} ({FinalSize}MB)",
                        archivo.FileName, (resultado.TamanoOriginal / 1024.0 / 1024.0).ToString("F1"),
                        nombreConvertido, (resultado.TamanoFinal / 1024.0 / 1024.0).ToString("F1"));

                    // Generar thumbnail para imágenes (usar TipoMedia del resultado, no la extensión original)
                    if (resultado.TipoMedia == Services.TipoMediaProcesado.Imagen)
                    {
                        var thumbnail = await _imageService.GenerarThumbnailAsync(filePath, 400, 400, 75);
                        if (!string.IsNullOrEmpty(thumbnail))
                        {
                            contenido.Thumbnail = thumbnail;
                            _logger.LogInformation("Thumbnail generado: {Thumbnail}", thumbnail);
                        }

                        // Extraer ubicación EXIF si el usuario lo tiene habilitado
                        if (usuario.DetectarUbicacionAutomaticamente)
                        {
                            var coordenadas = _exifService.ExtraerCoordenadas(filePath);
                            if (coordenadas.HasValue)
                            {
                                contenido.Latitud = coordenadas.Value.Lat;
                                contenido.Longitud = coordenadas.Value.Lon;

                                // Obtener nombre de ubicación
                                var nombreUbicacion = await _exifService.ObtenerNombreUbicacion(
                                    coordenadas.Value.Lat, coordenadas.Value.Lon);
                                contenido.NombreUbicacion = nombreUbicacion;

                                _logger.LogInformation("Ubicación detectada: {Ubicacion} ({Lat}, {Lon})",
                                    nombreUbicacion, coordenadas.Value.Lat, coordenadas.Value.Lon);
                            }
                        }
                    }
                }

                // ========================================
                // CLASIFICAR + DETECTAR OBJETOS (UNA SOLA LLAMADA A CLAUDE)
                // ========================================
                List<Services.ObjetoDetectado> objetosParaGuardar = new();
                try
                {
                    byte[]? imagenBytes = null;
                    string? mimeType = null;

                    // Leer el archivo guardado
                    if (!string.IsNullOrEmpty(contenido.RutaArchivo))
                    {
                        var extension = Path.GetExtension(contenido.RutaArchivo).ToLower();
                        var tiposImagen = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                        if (tiposImagen.Contains(extension))
                        {
                            var rutaCompleta = Path.Combine(_environment.WebRootPath, contenido.RutaArchivo.TrimStart('/'));
                            if (System.IO.File.Exists(rutaCompleta))
                            {
                                var fileInfo = new FileInfo(rutaCompleta);
                                if (fileInfo.Length < 5 * 1024 * 1024)
                                {
                                    imagenBytes = await System.IO.File.ReadAllBytesAsync(rutaCompleta);
                                    mimeType = extension switch
                                    {
                                        ".jpg" or ".jpeg" => "image/jpeg",
                                        ".png" => "image/png",
                                        ".gif" => "image/gif",
                                        ".webp" => "image/webp",
                                        _ => "image/jpeg"
                                    };
                                }
                            }
                        }
                    }

                    // UNA SOLA llamada a Claude para clasificacion Y deteccion de objetos
                    var resultado = await _classificationService.ClasificarYDetectarObjetosAsync(
                        imagenBytes, Descripcion, mimeType);

                    if (resultado.Clasificacion.Exito && resultado.Clasificacion.CategoriaId.HasValue)
                    {
                        contenido.CategoriaInteresId = resultado.Clasificacion.CategoriaId.Value;
                        _logger.LogInformation("Contenido clasificado en categoria {CategoriaId}", resultado.Clasificacion.CategoriaId.Value);
                    }

                    // Guardar objetos para despues (necesitamos el ID del contenido)
                    objetosParaGuardar = resultado.ObjetosDetectados;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al clasificar contenido, continuando sin categoria");
                    _ = _logEventoService.RegistrarErrorAsync(ex, Models.CategoriaEvento.Contenido,
                        usuario?.Id, usuario?.UserName);
                }

                _context.Contenidos.Add(contenido);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Contenido guardado - ID: {Id}, TipoLado: {TipoLado}, CategoriaId: {CategoriaId}",
                    contenido.Id, contenido.TipoLado, contenido.CategoriaInteresId);

                // ⭐ LADOCOINS: Procesar bonos de contenido (síncrono para mejor manejo de errores)
                if (!EsBorrador)
                {
                    try
                    {
                        // Bono de primer contenido (solo una vez)
                        if (!usuario.BonoPrimerContenidoEntregado)
                        {
                            var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                                usuario.Id,
                                Models.TipoTransaccionLadoCoin.BonoPrimerContenido,
                                "Bono por publicar tu primer contenido en LADO"
                            );

                            if (bonoEntregado)
                            {
                                usuario.BonoPrimerContenidoEntregado = true;
                                await _userManager.UpdateAsync(usuario);
                                _logger.LogInformation("⭐ Bono de primer contenido entregado a: {UserId}", usuario.Id);
                            }
                        }

                        // Bono diario por subir contenido (una vez al día)
                        var bonoContenido = await _rachasService.RegistrarContenidoAsync(usuario.Id);
                        _logger.LogInformation("⭐ Registro de contenido diario: {UserId}, BonoEntregado: {Bono}", usuario.Id, bonoContenido);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al procesar LadoCoins para contenido: {ContentId}", contenido.Id);
                        // No fallar la creación de contenido por error en LadoCoins
                        await _logEventoService.RegistrarErrorAsync(ex, Models.CategoriaEvento.Pago, usuario.Id, usuario.UserName);
                    }
                }
                else
                {
                    _logger.LogInformation("⭐ Contenido es borrador, no se procesan LadoCoins: {ContentId}", contenido.Id);
                }

                // Guardar objetos detectados (ahora tenemos el ID del contenido)
                if (objetosParaGuardar.Any())
                {
                    foreach (var obj in objetosParaGuardar)
                    {
                        _context.ObjetosContenido.Add(new Models.ObjetoContenido
                        {
                            ContenidoId = contenido.Id,
                            NombreObjeto = obj.Nombre,
                            Confianza = obj.Confianza,
                            FechaDeteccion = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Guardados {Count} objetos para contenido {Id}",
                        objetosParaGuardar.Count, contenido.Id);
                }

                // ========================================
                // CREAR PREVIEW BLUR PARA LADOA (si se solicitó)
                // ========================================
                if (CrearPreviewBlur && tipoLado == TipoLado.LadoB && !EsBorrador)
                {
                    try
                    {
                        var previewBlur = new Contenido
                        {
                            UsuarioId = usuario.Id,
                            TipoContenido = contenido.TipoContenido,
                            Descripcion = "✨ Contenido exclusivo disponible en mi LadoB",
                            RutaArchivo = contenido.RutaArchivo,
                            Thumbnail = contenido.Thumbnail,
                            TipoLado = TipoLado.LadoA,
                            EsGratis = true,
                            EsPublicoGeneral = true,
                            NombreMostrado = usuario.NombreCompleto,
                            EsPreviewBlurDeLadoB = true,
                            ContenidoOriginalLadoBId = contenido.Id,
                            TipoCensuraPreview = (Models.TipoCensuraPreview)TipoCensuraPreview,
                            CategoriaInteresId = contenido.CategoriaInteresId,
                            FechaPublicacion = DateTime.Now,
                            EstaActivo = true,
                            Latitud = contenido.Latitud,
                            Longitud = contenido.Longitud,
                            NombreUbicacion = contenido.NombreUbicacion
                        };

                        _context.Contenidos.Add(previewBlur);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Preview blur creado - ID: {Id} para LadoB contenido ID: {OriginalId}",
                            previewBlur.Id, contenido.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al crear preview blur, el contenido original se guardó correctamente");
                        _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, usuario?.Id, usuario?.UserName);
                    }
                }

                // ✅ Mensajes de éxito personalizados
                if (EsBorrador)
                {
                    TempData["Success"] = "✅ Borrador guardado exitosamente";
                }
                else
                {
                    // Notificar a seguidores sobre el nuevo contenido
                    _ = _notificationService.NotificarNuevoContenidoAsync(
                        usuario.Id,
                        contenido.Id,
                        contenido.Descripcion ?? "Nuevo contenido",
                        tipoLado);

                    if (tipoLado == TipoLado.LadoA)
                    {
                        TempData["Success"] = $"✅ Contenido público (LadoA) publicado como {usuario.NombreCompleto}";
                    }
                    else if (SoloSuscriptores && (PrecioDesbloqueo == null || PrecioDesbloqueo == 0))
                    {
                        // Solo suscriptores (incluido en plan)
                        TempData["Success"] = $"✅ Contenido para suscriptores publicado como {usuario.Seudonimo}";
                    }
                    else if (SoloSuscriptores && PrecioDesbloqueo > 0)
                    {
                        // Suscriptores gratis + compra individual
                        TempData["Success"] = $"✅ Contenido para suscriptores y compra individual (${PrecioDesbloqueo}) publicado como {usuario.Seudonimo}";
                    }
                    else if (!SoloSuscriptores && PrecioDesbloqueo > 0)
                    {
                        // Solo compra individual
                        TempData["Success"] = $"✅ Contenido premium (${PrecioDesbloqueo}) publicado como {usuario.Seudonimo}";
                    }
                    else
                    {
                        // Gratis para todos en LadoB
                        TempData["Success"] = $"✅ Contenido gratis en LadoB publicado como {usuario.Seudonimo}";
                    }
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear contenido. Tipo: {Tipo}", TipoContenido);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                TempData["Error"] = $"Error al crear contenido: {ex.Message}";
                ViewBag.UsuarioVerificado = false;
                return View();
            }
        }

        // ========================================
        // CREAR RAPIDO (DESDE EDITOR DE IMAGENES)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(500_000_000)] // 500MB para videos grandes
        [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
        public async Task<IActionResult> CrearRapido(IFormFile archivo, string descripcion, bool esPublico = true)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { success = false, error = "Usuario no autenticado" });
                }

                if (archivo == null || archivo.Length == 0)
                {
                    return Json(new { success = false, error = "No se recibió el archivo" });
                }

                // Rate limiting básico
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = Request.Headers["User-Agent"].ToString();
                var rateLimitKey = $"content_create_{usuario.Id}";

                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 10, TimeSpan.FromMinutes(5),
                    TipoAtaque.SpamContenido, "/Contenido/CrearRapido", usuario.Id, userAgent))
                {
                    return Json(new { success = false, error = "Demasiadas publicaciones. Espera unos minutos." });
                }

                // Validar archivo
                var extension = Path.GetExtension(archivo.FileName).ToLower();
                var tiposPermitidos = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                if (!tiposPermitidos.Contains(extension))
                {
                    return Json(new { success = false, error = "Formato no permitido. Usa JPG, PNG, GIF o WEBP." });
                }

                if (archivo.Length > 20 * 1024 * 1024) // 20MB max
                {
                    return Json(new { success = false, error = "El archivo es muy grande (max 20MB)" });
                }

                // Validar magic bytes (prevenir archivos maliciosos disfrazados)
                var validacionArchivo = await _fileValidationService.ValidarImagenAsync(archivo);
                if (!validacionArchivo.EsValido)
                {
                    _logger.LogWarning("Archivo rechazado por validación de magic bytes: {FileName}", archivo.FileName);
                    return Json(new { success = false, error = "El archivo no es válido o está corrupto" });
                }

                // Guardar archivo
                var carpetaUsuario = usuario.UserName?.Replace("@", "_").Replace(".", "_") ?? usuario.Id;
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", carpetaUsuario);

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // ====================================================================
                // 🔄 PROCESAMIENTO UNIFICADO - Usar ProcesarArchivoAsync
                // NO hay fallback - si falla, se rechaza el archivo.
                // ====================================================================
                _logger.LogInformation("[CrearRapido] 🔄 Procesando {FileName} ({Size}KB) con pipeline unificado...",
                    archivo.FileName, archivo.Length / 1024);

                Services.MediaProcessingResult resultado;
                using (var stream = archivo.OpenReadStream())
                {
                    resultado = await _mediaConversionService.ProcesarArchivoAsync(stream, extension, uploadsFolder);
                }

                if (!resultado.Exitoso)
                {
                    _logger.LogError("[CrearRapido] ❌ Error procesando {FileName}: {Error}",
                        archivo.FileName, resultado.Error);

                    // Registrar en Admin/Logs
                    await _logEventoService.RegistrarEventoAsync(
                        $"❌ CrearRapido: Error procesando imagen: {archivo.FileName}",
                        CategoriaEvento.Contenido,
                        TipoLogEvento.Error,
                        usuario.Id,
                        usuario.UserName,
                        $"Error: {resultado.Error}\nDetalle: {resultado.ErrorDetallado}\nExtensión: {extension}\nTamaño: {archivo.Length / 1024.0:F1} KB"
                    );

                    // NO hay fallback - rechazar el archivo
                    return Json(new { success = false, error = resultado.Error ?? "Error al procesar la imagen" });
                }

                var filePath = resultado.RutaArchivo!;
                var rutaArchivo = $"/uploads/{carpetaUsuario}/{resultado.NombreArchivo}";

                _logger.LogInformation("[CrearRapido] ✅ Imagen procesada: {Original} ({OriginalSize}KB) -> {Convertido} ({FinalSize}KB)",
                    archivo.FileName, resultado.TamanoOriginal / 1024,
                    resultado.NombreArchivo, resultado.TamanoFinal / 1024);

                // Crear contenido
                var contenido = new Contenido
                {
                    UsuarioId = usuario.Id,
                    TipoContenido = Models.TipoContenido.Foto,
                    Descripcion = descripcion ?? "",
                    RutaArchivo = rutaArchivo,
                    TipoLado = TipoLado.LadoA,
                    EsGratis = true,
                    EsPublicoGeneral = esPublico,
                    NombreMostrado = usuario.NombreCompleto,
                    EsPremium = false,
                    PrecioDesbloqueo = 0,
                    EsBorrador = false,
                    FechaPublicacion = DateTime.Now,
                    EstaActivo = true,
                    NumeroLikes = 0,
                    NumeroComentarios = 0,
                    NumeroVistas = 0
                };

                // Generar thumbnail
                var thumbnail = await _imageService.GenerarThumbnailAsync(filePath, 400, 400, 75);
                if (!string.IsNullOrEmpty(thumbnail))
                {
                    contenido.Thumbnail = thumbnail;
                }

                // ========================================
                // CLASIFICAR + DETECTAR OBJETOS (UNA SOLA LLAMADA A CLAUDE)
                // ========================================
                List<Services.ObjetoDetectado> objetosEditor = new();
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length < 5 * 1024 * 1024)
                    {
                        var imagenBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                        // Usar la extensión del archivo final (puede ser .jpg después de conversión)
                        var extensionFinal = Path.GetExtension(filePath).ToLower();
                        var mimeType = extensionFinal switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            ".gif" => "image/gif",
                            ".webp" => "image/webp",
                            _ => "image/jpeg"
                        };

                        var clasificacionResult = await _classificationService.ClasificarYDetectarObjetosAsync(
                            imagenBytes, descripcion, mimeType);

                        if (clasificacionResult.Clasificacion.Exito && clasificacionResult.Clasificacion.CategoriaId.HasValue)
                        {
                            contenido.CategoriaInteresId = clasificacionResult.Clasificacion.CategoriaId.Value;
                        }

                        objetosEditor = clasificacionResult.ObjetosDetectados;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al clasificar contenido del editor, continuando sin categoria");
                    _ = _logEventoService.RegistrarErrorAsync(ex, Models.CategoriaEvento.Contenido,
                        usuario?.Id, usuario?.UserName);
                }

                _context.Contenidos.Add(contenido);
                await _context.SaveChangesAsync();

                // Guardar objetos detectados
                if (objetosEditor.Any())
                {
                    foreach (var obj in objetosEditor)
                    {
                        _context.ObjetosContenido.Add(new Models.ObjetoContenido
                        {
                            ContenidoId = contenido.Id,
                            NombreObjeto = obj.Nombre,
                            Confianza = obj.Confianza,
                            FechaDeteccion = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Contenido creado desde editor - ID: {Id}, Usuario: {Usuario}, Categoria: {Cat}",
                    contenido.Id, usuario.UserName, contenido.CategoriaInteresId);

                // Notificar a seguidores
                _ = _notificationService.NotificarNuevoContenidoAsync(
                    usuario.Id,
                    contenido.Id,
                    contenido.Descripcion ?? "Nueva imagen promocional",
                    TipoLado.LadoA);

                return Json(new { success = true, contenidoId = contenido.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CrearRapido. Archivo: {FileName}, Tamaño: {Length}", archivo?.FileName, archivo?.Length);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                return Json(new { success = false, error = "Error al publicar: " + ex.Message });
            }
        }

        // ========================================
        // CREAR CARRUSEL (MÚLTIPLES ARCHIVOS)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(500_000_000)] // 500MB total para carrusel con videos grandes
        [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
        public async Task<IActionResult> CrearCarrusel(
            List<IFormFile> archivos,
            string Descripcion,
            bool EsGratis,
            decimal? PrecioDesbloqueo = null,
            bool EsBorrador = false,
            bool EsPublicoGeneral = false,
            bool SoloSuscriptores = false,
            bool CrearPreviewBlur = false,
            int TipoCensuraPreview = 0,
            bool PublicarEnLadoB = false)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Log de diagnóstico para archivos recibidos
                _logger.LogInformation("[CrearCarrusel] Request recibida - Usuario: {User}, Archivos: {Count}",
                    usuario.UserName, archivos?.Count ?? 0);

                if (archivos != null && archivos.Count > 0)
                {
                    foreach (var arch in archivos)
                    {
                        _logger.LogInformation("[CrearCarrusel] Archivo: {Name}, ContentType: {CT}, Size: {Size}MB",
                            arch.FileName, arch.ContentType, arch.Length / (1024.0 * 1024.0));
                    }
                }
                else
                {
                    _logger.LogWarning("[CrearCarrusel] No se recibieron archivos!");
                    await _logEventoService.RegistrarEventoAsync(
                        "CrearCarrusel sin archivos",
                        CategoriaEvento.Contenido,
                        TipoLogEvento.Warning,
                        usuario.Id,
                        usuario.UserName,
                        $"Request llegó al servidor pero sin archivos. UserAgent: {Request.Headers["User-Agent"]}"
                    );
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"content_create_{usuario.Id}";
                var rateLimitKeyHourly = $"content_create_hourly_{usuario.Id}";
                var rateLimitKeyDaily = $"content_create_daily_{usuario.Id}";
                var rateLimitKeyIp = $"content_create_ip_{clientIp}";
                var rateLimitKeyIpHourly = $"content_create_ip_hourly_{clientIp}";

                // 🚨 Rate limit por IP (detectar ataques multi-cuenta)
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIp, RateLimits.ContentCreation_IP_MaxRequests, RateLimits.ContentCreation_IP_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearCarrusel", usuario.Id))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP CARRUSEL: IP {IP} excedió límite de 5 min - Usuario: {UserId} ({UserName})",
                        clientIp, usuario.Id, usuario.UserName);
                    return Json(new { success = false, message = "Demasiadas solicitudes desde tu conexión. Espera unos minutos." });
                }

                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIpHourly, RateLimits.ContentCreation_IP_Hourly_MaxRequests, RateLimits.ContentCreation_IP_Hourly_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearCarrusel", usuario.Id))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP HORARIO CARRUSEL: IP {IP} excedió límite de 1 hora - Usuario: {UserId}",
                        clientIp, usuario.Id);
                    return Json(new { success = false, message = "Demasiadas solicitudes desde tu conexión. Intenta más tarde." });
                }

                // Límite por usuario - 5 minutos: máximo 10 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, RateLimits.ContentCreation_MaxRequests, RateLimits.ContentCreation_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearCarrusel", usuario.Id))
                {
                    _logger.LogWarning("🚫 RATE LIMIT CARRUSEL: Usuario {UserId} ({UserName}) excedió límite de 5 min - IP: {IP}",
                        usuario.Id, usuario.UserName, clientIp);
                    return Json(new { success = false, message = "Has creado demasiado contenido en poco tiempo. Espera unos minutos." });
                }

                // Límite por hora: máximo 50 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyHourly, RateLimits.ContentCreation_Hourly_MaxRequests, RateLimits.ContentCreation_Hourly_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearCarrusel", usuario.Id))
                {
                    _logger.LogWarning("🚫 RATE LIMIT HORARIO CARRUSEL: Usuario {UserId} ({UserName}) excedió límite de 1 hora",
                        usuario.Id, usuario.UserName);
                    return Json(new { success = false, message = "Has alcanzado el límite de contenido por hora. Intenta más tarde." });
                }

                // Límite diario: máximo 100 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyDaily, RateLimits.ContentCreation_Daily_MaxRequests, RateLimits.ContentCreation_Daily_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearCarrusel", usuario.Id))
                {
                    _logger.LogWarning("🚫 RATE LIMIT DIARIO CARRUSEL: Usuario {UserId} ({UserName}) excedió límite de 24 horas",
                        usuario.Id, usuario.UserName);
                    return Json(new { success = false, message = "Has alcanzado el límite diario de contenido. Intenta mañana." });
                }

                // Validar que hay archivos
                if (archivos == null || !archivos.Any())
                {
                    return Json(new { success = false, message = "Debes seleccionar al menos un archivo" });
                }

                // Límite de 10 archivos por carrusel
                if (archivos.Count > 10)
                {
                    return Json(new { success = false, message = "Máximo 10 archivos por publicación" });
                }

                _logger.LogInformation("=== CREAR CARRUSEL ===");
                _logger.LogInformation("Usuario: {Username}, Archivos: {Count}", usuario.UserName, archivos.Count);
                _logger.LogInformation("🔍 PARAMS RECIBIDOS - EsGratis: {EsGratis}, PrecioDesbloqueo: {Precio}, SoloSuscriptores: {SoloSusc}, PublicarEnLadoB: {LadoB}",
                    EsGratis, PrecioDesbloqueo, SoloSuscriptores, PublicarEnLadoB);

                // Validar verificación - solo verificados pueden crear contenido en LadoB
                // NUEVO: Usar PublicarEnLadoB para determinar el lado, NO EsGratis
                var intentaPublicarEnLadoB = PublicarEnLadoB;
                if (intentaPublicarEnLadoB && !usuario.CreadorVerificado)
                {
                    _logger.LogWarning("⚠️ Usuario {Username} intentó crear carrusel LadoB sin verificación - Forzando LadoA",
                        usuario.UserName);
                    intentaPublicarEnLadoB = false;
                    EsGratis = true;
                    PrecioDesbloqueo = 0;
                }

                // Validar precio (solo si NO es gratis Y está verificado)
                // Si SoloSuscriptores=true y PrecioDesbloqueo=0, significa que solo suscriptores pueden ver (sin compra individual)
                if (!EsGratis && usuario.CreadorVerificado)
                {
                    // Solo validar precio si hay compra individual disponible
                    var tieneCompraIndividual = !SoloSuscriptores || (PrecioDesbloqueo.HasValue && PrecioDesbloqueo > 0);

                    if (tieneCompraIndividual && PrecioDesbloqueo.HasValue && PrecioDesbloqueo > 0)
                    {
                        if (PrecioDesbloqueo % 5 != 0)
                        {
                            return Json(new { success = false, message = "El precio debe ser un múltiplo de 5" });
                        }
                    }

                    // Si no tiene precio y no es solo suscriptores, establecer precio default
                    if (!SoloSuscriptores && (!PrecioDesbloqueo.HasValue || PrecioDesbloqueo <= 0))
                    {
                        PrecioDesbloqueo = 10m;
                    }
                }

                var tipoLado = intentaPublicarEnLadoB ? TipoLado.LadoB : TipoLado.LadoA;
                var nombreMostrado = tipoLado == TipoLado.LadoA ? usuario.NombreCompleto : usuario.Seudonimo;

                // Determinar tipo de contenido (si hay video, es video; si no, foto)
                var extensionesVideo = new[] { ".mp4", ".mov", ".avi", ".webm", ".m4v", ".3gp", ".3gpp", ".mkv", ".wmv", ".flv", ".mpeg", ".mpg", ".mxf" };
                var tieneVideo = archivos.Any(a =>
                {
                    var ext = Path.GetExtension(a.FileName).ToLower();
                    return extensionesVideo.Contains(ext);
                });

                var contenido = new Contenido
                {
                    UsuarioId = usuario.Id,
                    TipoContenido = tieneVideo ? Models.TipoContenido.Video : Models.TipoContenido.Foto,
                    Descripcion = Descripcion ?? "",
                    TipoLado = tipoLado,
                    EsGratis = EsGratis,
                    NombreMostrado = nombreMostrado,
                    EsPremium = !EsGratis,
                    PrecioDesbloqueo = EsGratis ? 0m : (PrecioDesbloqueo ?? 0m),
                    SoloSuscriptores = tipoLado == TipoLado.LadoB && SoloSuscriptores,
                    EsBorrador = EsBorrador,
                    FechaPublicacion = DateTime.Now,
                    EstaActivo = true,
                    NumeroLikes = 0,
                    NumeroComentarios = 0,
                    NumeroVistas = 0,
                    EsPublicoGeneral = tipoLado == TipoLado.LadoA && EsPublicoGeneral
                };

                // Guardar contenido primero para obtener el ID
                _context.Contenidos.Add(contenido);
                await _context.SaveChangesAsync();

                // Procesar cada archivo
                var carpetaUsuario = usuario.UserName?.Replace("@", "_").Replace(".", "_") ?? usuario.Id;
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", carpetaUsuario);

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Incluye formatos RAW que serán convertidos a JPEG
                var tiposPermitidosImg = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif", ".bmp", ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2", ".tiff", ".tif" };
                var tiposPermitidosVideo = new[] { ".mp4", ".mov", ".avi", ".webm", ".m4v", ".3gp", ".3gpp", ".mkv", ".wmv", ".flv", ".mpeg", ".mpg", ".mxf" };
                var archivosGuardados = new List<ArchivoContenido>();

                for (int i = 0; i < archivos.Count; i++)
                {
                    var archivo = archivos[i];

                    if (archivo.Length > 100 * 1024 * 1024) // 100 MB
                    {
                        continue; // Saltar archivos muy grandes
                    }

                    var extension = Path.GetExtension(archivo.FileName).ToLower();
                    var esVideo = tiposPermitidosVideo.Contains(extension);
                    var esFoto = tiposPermitidosImg.Contains(extension);

                    if (!esVideo && !esFoto)
                    {
                        continue; // Saltar tipos no permitidos
                    }

                    // Validar magic bytes (prevenir archivos maliciosos disfrazados)
                    _logger.LogInformation("[Carrusel] Validando archivo: {FileName}, Extension: {Extension}, ContentType: {ContentType}, Size: {Size}MB",
                        archivo.FileName, extension, archivo.ContentType, archivo.Length / (1024.0 * 1024.0));

                    var validacion = esFoto
                        ? await _fileValidationService.ValidarImagenAsync(archivo)
                        : await _fileValidationService.ValidarVideoAsync(archivo);

                    _logger.LogInformation("[Carrusel] Procesando archivo: Extension={Extension}, EsFoto={EsFoto}, EsVideo={EsVideo}",
                        extension, esFoto, esVideo);

                    // Validar magic bytes para TODOS los archivos (seguridad)
                    if (!validacion.EsValido)
                    {
                        _logger.LogWarning("[Carrusel] Archivo rechazado: {FileName}, Razón: {Razon}, TipoDetectado: {TipoDetectado}",
                            archivo.FileName, validacion.MensajeError, validacion.TipoDetectado ?? "ninguno");

                        // Registrar en Admin/Logs para diagnóstico
                        await _logEventoService.RegistrarEventoAsync(
                            $"Archivo rechazado (Carrusel): {archivo.FileName}",
                            CategoriaEvento.Contenido,
                            TipoLogEvento.Warning,
                            usuario.Id,
                            usuario.UserName,
                            $"Error: {validacion.MensajeError}\nContentType: {archivo.ContentType}\nExtensión: {extension}\nTamaño: {archivo.Length / 1024.0:F1} KB\nTipo detectado: {validacion.TipoDetectado ?? "N/A"}\nEsVideo: {esVideo}, EsFoto: {esFoto}"
                        );

                        continue; // Saltar archivos inválidos
                    }

                    // ====================================================================
                    // 🔄 PROCESAMIENTO UNIFICADO - Usar ProcesarArchivoAsync
                    // Todos los archivos pasan por el mismo pipeline de conversión.
                    // NO hay fallback - si falla, se omite el archivo.
                    // ====================================================================
                    _logger.LogInformation("[Carrusel] 🔄 Procesando {FileName} ({Size}MB) con pipeline unificado...",
                        archivo.FileName, (archivo.Length / 1024.0 / 1024.0).ToString("F1"));

                    Services.MediaProcessingResult resultado;
                    using (var stream = archivo.OpenReadStream())
                    {
                        resultado = await _mediaConversionService.ProcesarArchivoAsync(stream, extension, uploadsFolder);
                    }

                    if (!resultado.Exitoso)
                    {
                        _logger.LogWarning("[Carrusel] ❌ Error procesando {FileName}: {Error}",
                            archivo.FileName, resultado.Error);

                        // Registrar en Admin/Logs
                        await _logEventoService.RegistrarEventoAsync(
                            $"❌ Error procesando archivo: {archivo.FileName}",
                            CategoriaEvento.Contenido,
                            TipoLogEvento.Error,
                            usuario.Id,
                            usuario.UserName,
                            $"Error: {resultado.Error}\nDetalle: {resultado.ErrorDetallado}\nExtensión: {extension}\nTamaño: {archivo.Length / 1024.0:F1} KB"
                        );

                        // NO hay fallback - simplemente omitir el archivo
                        continue;
                    }

                    var filePath = resultado.RutaArchivo!;
                    var uniqueFileName = resultado.NombreArchivo!;
                    var tamanoFinal = resultado.TamanoFinal;

                    _logger.LogInformation("[Carrusel] ✅ Archivo procesado: {Original} ({OriginalSize}MB) -> {Convertido} ({FinalSize}MB)",
                        archivo.FileName, (resultado.TamanoOriginal / 1024.0 / 1024.0).ToString("F1"),
                        uniqueFileName, (tamanoFinal / 1024.0 / 1024.0).ToString("F1"));

                    var archivoContenido = new ArchivoContenido
                    {
                        ContenidoId = contenido.Id,
                        RutaArchivo = $"/uploads/{carpetaUsuario}/{uniqueFileName}",
                        Orden = i,
                        TipoArchivo = esVideo ? TipoArchivo.Video : TipoArchivo.Foto,
                        TamanoBytes = tamanoFinal,
                        FechaCreacion = DateTime.Now
                    };

                    // Generar thumbnail para imágenes (usar extensión final, no original)
                    var extensionFinal = Path.GetExtension(filePath).ToLower();
                    if (esFoto && _imageService.EsImagenValida(extensionFinal))
                    {
                        var thumbnail = await _imageService.GenerarThumbnailAsync(filePath, 400, 400, 75);
                        if (!string.IsNullOrEmpty(thumbnail))
                        {
                            archivoContenido.Thumbnail = thumbnail;
                        }
                    }

                    archivosGuardados.Add(archivoContenido);
                    _logger.LogInformation("Archivo {Index} guardado: {Ruta}", i, archivoContenido.RutaArchivo);
                }

                if (!archivosGuardados.Any())
                {
                    // Si no se guardó ningún archivo válido, eliminar el contenido
                    _context.Contenidos.Remove(contenido);
                    await _context.SaveChangesAsync();

                    await _logEventoService.RegistrarEventoAsync(
                        "Carrusel: Ningún archivo procesado",
                        CategoriaEvento.Contenido,
                        TipoLogEvento.Error,
                        usuario.Id,
                        usuario.UserName,
                        $"Se recibieron {archivos.Count} archivos pero ninguno se pudo procesar.\nArchivos: {string.Join(", ", archivos.Select(a => $"{a.FileName} ({a.Length / 1024.0 / 1024.0:F1}MB)"))}"
                    );

                    return Json(new { success = false, message = "No se pudo procesar ningún archivo válido" });
                }

                // Guardar archivos en BD
                _context.ArchivosContenido.AddRange(archivosGuardados);

                // Establecer el primer archivo como RutaArchivo principal (compatibilidad)
                contenido.RutaArchivo = archivosGuardados.First().RutaArchivo;

                // Establecer thumbnail del contenido principal (usar el thumbnail generado si existe)
                var primeraImagen = archivosGuardados.FirstOrDefault(a => a.TipoArchivo == TipoArchivo.Foto);
                if (primeraImagen != null)
                {
                    // Usar thumbnail generado si existe, sino usar imagen original
                    contenido.Thumbnail = primeraImagen.Thumbnail ?? primeraImagen.RutaArchivo;

                    // Extraer ubicación EXIF de la primera imagen si el usuario lo tiene habilitado
                    if (usuario.DetectarUbicacionAutomaticamente)
                    {
                        var rutaCompleta = Path.Combine(_environment.WebRootPath, primeraImagen.RutaArchivo.TrimStart('/'));
                        if (System.IO.File.Exists(rutaCompleta))
                        {
                            var coordenadas = _exifService.ExtraerCoordenadas(rutaCompleta);
                            if (coordenadas.HasValue)
                            {
                                contenido.Latitud = coordenadas.Value.Lat;
                                contenido.Longitud = coordenadas.Value.Lon;

                                var nombreUbicacion = await _exifService.ObtenerNombreUbicacion(
                                    coordenadas.Value.Lat, coordenadas.Value.Lon);
                                contenido.NombreUbicacion = nombreUbicacion;

                                _logger.LogInformation("Carrusel: Ubicación detectada: {Ubicacion} ({Lat}, {Lon})",
                                    nombreUbicacion, coordenadas.Value.Lat, coordenadas.Value.Lon);
                            }
                        }
                    }
                }
                // Si solo hay videos, dejar Thumbnail null para que la vista use el tag <video>

                // ========================================
                // CLASIFICAR + DETECTAR OBJETOS (UNA SOLA LLAMADA A CLAUDE)
                // ========================================
                List<Services.ObjetoDetectado> objetosCarrusel = new();
                try
                {
                    byte[]? imagenBytes = null;
                    string? mimeType = null;

                    // Usar la primera imagen para clasificacion
                    if (primeraImagen != null && !string.IsNullOrEmpty(primeraImagen.RutaArchivo))
                    {
                        var extension = Path.GetExtension(primeraImagen.RutaArchivo).ToLower();
                        var tiposImagen = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                        if (tiposImagen.Contains(extension))
                        {
                            var rutaCompleta = Path.Combine(_environment.WebRootPath, primeraImagen.RutaArchivo.TrimStart('/'));
                            if (System.IO.File.Exists(rutaCompleta))
                            {
                                var fileInfo = new FileInfo(rutaCompleta);
                                if (fileInfo.Length < 5 * 1024 * 1024)
                                {
                                    imagenBytes = await System.IO.File.ReadAllBytesAsync(rutaCompleta);
                                    mimeType = extension switch
                                    {
                                        ".jpg" or ".jpeg" => "image/jpeg",
                                        ".png" => "image/png",
                                        ".gif" => "image/gif",
                                        ".webp" => "image/webp",
                                        _ => "image/jpeg"
                                    };
                                }
                            }
                        }
                    }

                    // UNA SOLA llamada a Claude
                    var resultado = await _classificationService.ClasificarYDetectarObjetosAsync(
                        imagenBytes, Descripcion, mimeType);

                    if (resultado.Clasificacion.Exito && resultado.Clasificacion.CategoriaId.HasValue)
                    {
                        contenido.CategoriaInteresId = resultado.Clasificacion.CategoriaId.Value;
                        _logger.LogInformation("Carrusel clasificado en categoria {CategoriaId}", resultado.Clasificacion.CategoriaId.Value);
                    }

                    objetosCarrusel = resultado.ObjetosDetectados;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al clasificar carrusel, continuando sin categoria");
                    _ = _logEventoService.RegistrarErrorAsync(ex, Models.CategoriaEvento.Contenido,
                        usuario?.Id, usuario?.UserName);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Carrusel creado - ID: {Id}, Archivos: {Count}, CategoriaId: {CategoriaId}",
                    contenido.Id, archivosGuardados.Count, contenido.CategoriaInteresId);

                // Guardar objetos detectados
                if (objetosCarrusel.Any())
                {
                    foreach (var obj in objetosCarrusel)
                    {
                        _context.ObjetosContenido.Add(new Models.ObjetoContenido
                        {
                            ContenidoId = contenido.Id,
                            NombreObjeto = obj.Nombre,
                            Confianza = obj.Confianza,
                            FechaDeteccion = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Guardados {Count} objetos para carrusel {Id}",
                        objetosCarrusel.Count, contenido.Id);
                }

                // Notificar a seguidores
                if (!EsBorrador)
                {
                    _ = _notificationService.NotificarNuevoContenidoAsync(
                        usuario.Id,
                        contenido.Id,
                        contenido.Descripcion ?? "Nuevo contenido",
                        tipoLado);

                    // ========================================
                    // LADO COINS - Registrar contenido diario
                    // ========================================
                    try
                    {
                        // Bono primer contenido (una sola vez)
                        if (!usuario.BonoPrimerContenidoEntregado)
                        {
                            var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                                usuario.Id,
                                Models.TipoTransaccionLadoCoin.BonoPrimerContenido,
                                "Bono por publicar tu primer contenido en LADO"
                            );
                            if (bonoEntregado)
                            {
                                usuario.BonoPrimerContenidoEntregado = true;
                                await _userManager.UpdateAsync(usuario);
                                _logger.LogInformation("⭐ Bono de primer contenido entregado a: {UserId}", usuario.Id);
                            }
                        }

                        // Bono diario por subir contenido (una vez al día)
                        var bonoContenido = await _rachasService.RegistrarContenidoAsync(usuario.Id);
                        _logger.LogInformation("⭐ Registro de contenido diario (Carrusel): {UserId}, BonoEntregado: {Bono}", usuario.Id, bonoContenido);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al procesar LadoCoins para carrusel: {ContentId}", contenido.Id);
                        await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, usuario.Id, usuario.UserName);
                    }
                }

                // ========================================
                // CREAR PREVIEW BLUR PARA LADOA (si se solicitó)
                // ========================================
                if (CrearPreviewBlur && tipoLado == TipoLado.LadoB && !EsBorrador)
                {
                    try
                    {
                        var previewBlur = new Contenido
                        {
                            UsuarioId = usuario.Id,
                            TipoContenido = contenido.TipoContenido,
                            Descripcion = "✨ Contenido exclusivo disponible en mi LadoB",
                            RutaArchivo = contenido.RutaArchivo,
                            Thumbnail = contenido.Thumbnail,
                            TipoLado = TipoLado.LadoA,
                            EsGratis = true,
                            EsPublicoGeneral = true,
                            NombreMostrado = usuario.NombreCompleto,
                            EsPreviewBlurDeLadoB = true,
                            ContenidoOriginalLadoBId = contenido.Id,
                            TipoCensuraPreview = (Models.TipoCensuraPreview)TipoCensuraPreview,
                            CategoriaInteresId = contenido.CategoriaInteresId,
                            FechaPublicacion = DateTime.Now,
                            EstaActivo = true,
                            Latitud = contenido.Latitud,
                            Longitud = contenido.Longitud,
                            NombreUbicacion = contenido.NombreUbicacion
                        };

                        _context.Contenidos.Add(previewBlur);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Preview blur creado para carrusel - ID: {Id} para LadoB contenido ID: {OriginalId}",
                            previewBlur.Id, contenido.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al crear preview blur para carrusel, el contenido original se guardó correctamente");
                        _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, usuario?.Id, usuario?.UserName);
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Carrusel publicado con {archivosGuardados.Count} archivos",
                    contenidoId = contenido.Id,
                    archivos = archivosGuardados.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear carrusel");

                // Registrar en Admin/Logs
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.Identity?.Name;
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, userName);

                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========================================
        // CREAR DESDE REELS (AJAX)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearDesdeReels(
            IFormFile archivo,
            string descripcion,
            string lado,
            string tipo,
            bool esGratis = false,
            bool permitirComentarios = true,
            bool esPublicoGeneral = false,
            // Parámetros de música
            int? audioTrackId = null,
            string? audioTrackTitle = null,
            decimal? audioStartTime = null,
            decimal? audioVolume = null,
            decimal? originalVolume = null)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"content_create_{usuario.Id}";
                var rateLimitKeyHourly = $"content_create_hourly_{usuario.Id}";
                var rateLimitKeyDaily = $"content_create_daily_{usuario.Id}";
                var rateLimitKeyIp = $"content_create_ip_{clientIp}";
                var rateLimitKeyIpHourly = $"content_create_ip_hourly_{clientIp}";

                // 🚨 Rate limit por IP (detectar ataques multi-cuenta)
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIp, RateLimits.ContentCreation_IP_MaxRequests, RateLimits.ContentCreation_IP_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearReel", usuario.Id))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP REELS: IP {IP} excedió límite de 5 min - Usuario: {UserId} ({UserName})",
                        clientIp, usuario.Id, usuario.UserName);
                    return Json(new { success = false, message = "Demasiadas solicitudes desde tu conexión. Espera unos minutos." });
                }

                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIpHourly, RateLimits.ContentCreation_IP_Hourly_MaxRequests, RateLimits.ContentCreation_IP_Hourly_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearReel", usuario.Id))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP HORARIO REELS: IP {IP} excedió límite de 1 hora - Usuario: {UserId}",
                        clientIp, usuario.Id);
                    return Json(new { success = false, message = "Demasiadas solicitudes desde tu conexión. Intenta más tarde." });
                }

                // Límite por usuario - 5 minutos: máximo 10 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, RateLimits.ContentCreation_MaxRequests, RateLimits.ContentCreation_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearReel", usuario.Id))
                {
                    _logger.LogWarning("🚫 RATE LIMIT REELS: Usuario {UserId} ({UserName}) excedió límite de 5 min - IP: {IP}",
                        usuario.Id, usuario.UserName, clientIp);
                    return Json(new { success = false, message = "Has creado demasiado contenido en poco tiempo. Espera unos minutos." });
                }

                // Límite por hora: máximo 50 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyHourly, RateLimits.ContentCreation_Hourly_MaxRequests, RateLimits.ContentCreation_Hourly_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearReel", usuario.Id))
                {
                    _logger.LogWarning("🚫 RATE LIMIT HORARIO REELS: Usuario {UserId} ({UserName}) excedió límite de 1 hora",
                        usuario.Id, usuario.UserName);
                    return Json(new { success = false, message = "Has alcanzado el límite de contenido por hora. Intenta más tarde." });
                }

                // Límite diario: máximo 100 contenidos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyDaily, RateLimits.ContentCreation_Daily_MaxRequests, RateLimits.ContentCreation_Daily_Window,
                    TipoAtaque.SpamContenido, "/Contenido/CrearReel", usuario.Id))
                {
                    _logger.LogWarning("🚫 RATE LIMIT DIARIO REELS: Usuario {UserId} ({UserName}) excedió límite de 24 horas",
                        usuario.Id, usuario.UserName);
                    return Json(new { success = false, message = "Has alcanzado el límite diario de contenido. Intenta mañana." });
                }

                _logger.LogInformation("=== CREAR DESDE REELS ===");
                _logger.LogInformation("Usuario: {Username}, Lado: {Lado}, Tipo: {Tipo}, EsGratis: {EsGratis}",
                    usuario.UserName, lado, tipo, esGratis);
                _logger.LogInformation("🎵 Audio: TrackId={TrackId}, Title={Title}, StartTime={Start}, Volume={Vol}",
                    audioTrackId, audioTrackTitle, audioStartTime, audioVolume);

                // Validar archivo
                if (archivo == null || archivo.Length == 0)
                {
                    return Json(new { success = false, message = "No se recibió ningún archivo" });
                }

                // Validar tamaño
                if (archivo.Length > 100 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "El archivo excede el tamaño máximo de 100 MB" });
                }

                // Determinar tipo de contenido
                var extension = Path.GetExtension(archivo.FileName).ToLower();
                var tiposImagen = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var tiposVideo = new[] { ".mp4", ".mov", ".avi", ".webm" };

                TipoContenido tipoContenido;
                if (tiposImagen.Contains(extension) || tipo?.ToLower() == "imagen")
                {
                    tipoContenido = TipoContenido.Foto;
                }
                else if (tiposVideo.Contains(extension) || tipo?.ToLower() == "video")
                {
                    tipoContenido = TipoContenido.Video;
                }
                else
                {
                    return Json(new { success = false, message = "Tipo de archivo no soportado" });
                }

                // Validar magic bytes (prevenir archivos maliciosos disfrazados)
                var validacion = tipoContenido == TipoContenido.Foto
                    ? await _fileValidationService.ValidarImagenAsync(archivo)
                    : await _fileValidationService.ValidarVideoAsync(archivo);

                if (!validacion.EsValido)
                {
                    _logger.LogWarning("Archivo rechazado en CrearDesdeReels por magic bytes: {FileName}", archivo.FileName);
                    return Json(new { success = false, message = "El archivo no es válido o está corrupto" });
                }

                // Determinar lado
                var tipoLado = lado?.ToUpper() == "B" ? TipoLado.LadoB : TipoLado.LadoA;
                var nombreMostrado = tipoLado == TipoLado.LadoA ? usuario.NombreCompleto : usuario.Seudonimo;

                // Verificación para monetización
                var precioDesbloqueo = 0m;
                if (!esGratis && tipoLado == TipoLado.LadoB)
                {
                    if (!usuario.CreadorVerificado)
                    {
                        esGratis = true;
                        _logger.LogWarning("Usuario sin verificar intentó monetizar - forzando gratis");
                    }
                    else
                    {
                        precioDesbloqueo = 10m; // Precio por defecto
                    }
                }

                // Guardar archivo
                var carpetaUsuario = usuario.UserName?.Replace("@", "_").Replace(".", "_") ?? usuario.Id;
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", carpetaUsuario);

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await archivo.CopyToAsync(fileStream);
                }

                var rutaArchivo = $"/uploads/{carpetaUsuario}/{uniqueFileName}";

                // Generar thumbnail para imágenes
                string? thumbnailPath = null;
                double? latitud = null;
                double? longitud = null;
                string? nombreUbicacion = null;

                if (tipoContenido == TipoContenido.Foto && _imageService.EsImagenValida(extension))
                {
                    thumbnailPath = await _imageService.GenerarThumbnailAsync(filePath, 400, 400, 75);
                    _logger.LogInformation("Thumbnail generado: {Thumbnail}", thumbnailPath);

                    // Extraer ubicación EXIF si el usuario lo tiene habilitado
                    if (usuario.DetectarUbicacionAutomaticamente)
                    {
                        var coordenadas = _exifService.ExtraerCoordenadas(filePath);
                        if (coordenadas.HasValue)
                        {
                            latitud = coordenadas.Value.Lat;
                            longitud = coordenadas.Value.Lon;
                            nombreUbicacion = await _exifService.ObtenerNombreUbicacion(
                                coordenadas.Value.Lat, coordenadas.Value.Lon);

                            _logger.LogInformation("Reel: Ubicación detectada: {Ubicacion} ({Lat}, {Lon})",
                                nombreUbicacion, latitud, longitud);
                        }
                    }
                }

                // Crear contenido
                var contenido = new Contenido
                {
                    UsuarioId = usuario.Id,
                    TipoContenido = tipoContenido,
                    Descripcion = descripcion ?? "",
                    TipoLado = tipoLado,
                    EsGratis = esGratis || tipoLado == TipoLado.LadoA,
                    NombreMostrado = nombreMostrado,
                    EsPremium = !esGratis && tipoLado == TipoLado.LadoB && usuario.CreadorVerificado,
                    PrecioDesbloqueo = precioDesbloqueo,
                    EsBorrador = false,
                    FechaPublicacion = DateTime.Now,
                    EstaActivo = true,
                    NumeroLikes = 0,
                    NumeroComentarios = 0,
                    NumeroVistas = 0,
                    RutaArchivo = rutaArchivo,
                    Thumbnail = thumbnailPath,
                    EsPublicoGeneral = esPublicoGeneral,
                    // Ubicación EXIF
                    Latitud = latitud,
                    Longitud = longitud,
                    NombreUbicacion = nombreUbicacion,
                    // Música asociada
                    PistaMusicalId = audioTrackId,
                    MusicaVolumen = audioVolume,
                    AudioOriginalVolumen = originalVolume,
                    AudioTrimInicio = audioStartTime.HasValue ? (int)audioStartTime.Value : null,
                    AudioDuracion = null, // Se calculará si es necesario
                    // Marcar como Reel (creado desde el creador de Reels)
                    EsReel = true
                };

                // Incrementar contador de uso de la pista musical
                if (audioTrackId.HasValue)
                {
                    var pista = await _context.PistasMusica.FindAsync(audioTrackId.Value);
                    if (pista != null)
                    {
                        pista.ContadorUsos++;
                        _logger.LogInformation("Musica agregada: {Titulo} - {Artista}", pista.Titulo, pista.Artista);
                    }
                }

                // ========================================
                // CLASIFICAR + DETECTAR OBJETOS (UNA SOLA LLAMADA A CLAUDE)
                // ========================================
                List<Services.ObjetoDetectado> objetosReel = new();
                try
                {
                    byte[]? imagenBytes = null;
                    string? mimeType = null;

                    if (tipoContenido == TipoContenido.Foto && System.IO.File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length < 5 * 1024 * 1024)
                        {
                            imagenBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                            mimeType = extension switch
                            {
                                ".jpg" or ".jpeg" => "image/jpeg",
                                ".png" => "image/png",
                                ".gif" => "image/gif",
                                ".webp" => "image/webp",
                                _ => "image/jpeg"
                            };
                        }
                    }

                    // UNA SOLA llamada a Claude
                    var resultado = await _classificationService.ClasificarYDetectarObjetosAsync(
                        imagenBytes, descripcion, mimeType);

                    if (resultado.Clasificacion.Exito && resultado.Clasificacion.CategoriaId.HasValue)
                    {
                        contenido.CategoriaInteresId = resultado.Clasificacion.CategoriaId.Value;
                        _logger.LogInformation("Reel clasificado en categoria {CategoriaId}", resultado.Clasificacion.CategoriaId.Value);
                    }

                    objetosReel = resultado.ObjetosDetectados;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al clasificar reel, continuando sin categoria");
                    _ = _logEventoService.RegistrarErrorAsync(ex, Models.CategoriaEvento.Contenido,
                        usuario?.Id, usuario?.UserName);
                }

                _context.Contenidos.Add(contenido);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Contenido creado desde Reels - ID: {Id}, CategoriaId: {CategoriaId}",
                    contenido.Id, contenido.CategoriaInteresId);

                // Guardar objetos detectados
                if (objetosReel.Any())
                {
                    foreach (var obj in objetosReel)
                    {
                        _context.ObjetosContenido.Add(new Models.ObjetoContenido
                        {
                            ContenidoId = contenido.Id,
                            NombreObjeto = obj.Nombre,
                            Confianza = obj.Confianza,
                            FechaDeteccion = DateTime.Now
                        });
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Guardados {Count} objetos para reel {Id}",
                        objetosReel.Count, contenido.Id);
                }

                // Notificar a seguidores sobre el nuevo contenido
                _ = _notificationService.NotificarNuevoContenidoAsync(
                    usuario.Id,
                    contenido.Id,
                    contenido.Descripcion ?? "Nuevo contenido",
                    contenido.TipoLado);

                // ========================================
                // LADO COINS - Registrar contenido diario
                // ========================================
                try
                {
                    // Bono primer contenido (una sola vez)
                    if (!usuario.BonoPrimerContenidoEntregado)
                    {
                        var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                            usuario.Id,
                            Models.TipoTransaccionLadoCoin.BonoPrimerContenido,
                            "Bono por publicar tu primer contenido en LADO"
                        );
                        if (bonoEntregado)
                        {
                            usuario.BonoPrimerContenidoEntregado = true;
                            await _userManager.UpdateAsync(usuario);
                            _logger.LogInformation("⭐ Bono de primer contenido entregado a: {UserId}", usuario.Id);
                        }
                    }

                    // Bono diario por subir contenido (una vez al día)
                    var bonoContenido = await _rachasService.RegistrarContenidoAsync(usuario.Id);
                    _logger.LogInformation("⭐ Registro de contenido diario (Reel): {UserId}, BonoEntregado: {Bono}", usuario.Id, bonoContenido);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar LadoCoins para reel: {ContentId}", contenido.Id);
                    await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, usuario.Id, usuario.UserName);
                }

                return Json(new
                {
                    success = true,
                    message = "Contenido publicado exitosamente",
                    contenidoId = contenido.Id,
                    redirectUrl = "/Feed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear contenido desde Reels. Archivo: {FileName}, Tamaño: {Length}", archivo?.FileName, archivo?.Length);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========================================
        // PUBLICAR BORRADOR
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publicar(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (usuario == null)
            {
                _logger.LogWarning("Usuario no encontrado en Publicar");
                return RedirectToAction("Login", "Account");
            }

            var contenido = await _context.Contenidos
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id);

            if (contenido == null)
            {
                _logger.LogWarning("Contenido no encontrado: {Id}", id);
                return NotFound();
            }

            // ✅ Verificar si es contenido de pago y requiere verificación
            if (!contenido.EsGratis && !usuario.CreadorVerificado)
            {
                TempData["Warning"] = "Para publicar contenido premium debes verificar tu identidad. Se publicará como gratis.";

                // Forzar a gratis pero mantener en LadoB
                contenido.EsGratis = true;
                contenido.EsPremium = false;
                contenido.PrecioDesbloqueo = 0;
            }

            contenido.EsBorrador = false;
            contenido.FechaPublicacion = DateTime.Now;
            await _context.SaveChangesAsync();

            // Notificar a seguidores sobre el nuevo contenido
            _ = _notificationService.NotificarNuevoContenidoAsync(
                usuario.Id,
                contenido.Id,
                contenido.Descripcion ?? "Nuevo contenido",
                contenido.TipoLado);

            // ========================================
            // LADO COINS - Registrar contenido diario (al publicar borrador)
            // ========================================
            try
            {
                // Bono primer contenido (una sola vez)
                if (!usuario.BonoPrimerContenidoEntregado)
                {
                    var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                        usuario.Id,
                        Models.TipoTransaccionLadoCoin.BonoPrimerContenido,
                        "Bono por publicar tu primer contenido en LADO"
                    );
                    if (bonoEntregado)
                    {
                        usuario.BonoPrimerContenidoEntregado = true;
                        await _userManager.UpdateAsync(usuario);
                        _logger.LogInformation("⭐ Bono de primer contenido entregado a: {UserId}", usuario.Id);
                    }
                }

                // Bono diario por subir contenido (una vez al día)
                var bonoContenido = await _rachasService.RegistrarContenidoAsync(usuario.Id);
                _logger.LogInformation("⭐ Registro de contenido diario (Publicar borrador): {UserId}, BonoEntregado: {Bono}", usuario.Id, bonoContenido);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar LadoCoins para publicar borrador: {ContentId}", contenido.Id);
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, usuario.Id, usuario.UserName);
            }

            var tipoContenido = contenido.TipoLado == TipoLado.LadoA ? "público (LadoA)" :
                                contenido.EsGratis ? "gratis en LadoB" : "premium (LadoB)";
            TempData["Success"] = $"✅ Contenido {tipoContenido} publicado exitosamente";

            return RedirectToAction("Index");
        }

        // ========================================
        // EDITAR CONTENIDO - GET
        // ========================================

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (usuario == null)
            {
                _logger.LogWarning("Usuario no encontrado en Editar");
                return RedirectToAction("Login", "Account");
            }

            var contenido = await _context.Contenidos
                .Include(c => c.PistaMusical)
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id && c.EstaActivo);

            if (contenido == null)
            {
                TempData["Error"] = "Contenido no encontrado";
                return RedirectToAction("Index");
            }

            ViewBag.UsuarioVerificado = usuario.CreadorVerificado;

            _logger.LogInformation("Editando contenido ID: {Id}, TipoContenido: {TipoContenido}, TipoLado: {TipoLado}, EsGratis: {EsGratis}",
                id, contenido.TipoContenido, contenido.TipoLado, contenido.EsGratis);

            // Debug: Log de la pista musical
            if (contenido.PistaMusical != null)
            {
                _logger.LogInformation("PistaMusical ID: {Id}, Titulo: {Titulo}, RutaArchivo: '{RutaArchivo}'",
                    contenido.PistaMusical.Id, contenido.PistaMusical.Titulo, contenido.PistaMusical.RutaArchivo);
            }
            else
            {
                _logger.LogInformation("Contenido sin PistaMusical");
            }

            return View(contenido);
        }

        // ========================================
        // EDITAR CONTENIDO - POST
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int id,
            IFormFile archivo,
            string Descripcion,
            int TipoContenido,
            bool EsGratis,
            decimal? PrecioDesbloqueo,
            bool EsPublicoGeneral = false,
            bool EsPrivado = false,
            int? PistaMusicalId = null,
            int? AudioTrimInicio = null,
            decimal? MusicaVolumen = null)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                if (usuario == null)
                {
                    _logger.LogWarning("Usuario no encontrado en Editar POST");
                    return RedirectToAction("Login", "Account");
                }

                var contenido = await _context.Contenidos
                    .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id && c.EstaActivo);

                if (contenido == null)
                {
                    TempData["Error"] = "Contenido no encontrado";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation("=== EDITAR CONTENIDO ID: {Id} ===", id);
                _logger.LogInformation("EsGratis recibido: {EsGratis}, Precio: {Precio}", EsGratis, PrecioDesbloqueo);

                // ✅ GUARDAR INTENCIÓN ORIGINAL
                var intentaPublicarEnLadoB = !EsGratis;

                // ✅ REGLA PRINCIPAL: Solo verificados pueden crear/editar contenido en LadoB
                if (intentaPublicarEnLadoB && !usuario.CreadorVerificado)
                {
                    _logger.LogWarning("⚠️ Usuario {Username} intentó editar a LadoB sin verificación - Forzando LadoA",
                        usuario.UserName);

                    intentaPublicarEnLadoB = false;
                    EsGratis = true;
                    PrecioDesbloqueo = 0;

                    TempData["Warning"] = "Para crear contenido en LadoB debes verificar tu identidad. El contenido permanece en LadoA.";
                }

                // ✅ Validar precio SOLO si es contenido de pago Y está verificado
                if (!EsGratis && usuario.CreadorVerificado)
                {
                    if (!PrecioDesbloqueo.HasValue || PrecioDesbloqueo <= 0 || PrecioDesbloqueo % 5 != 0)
                    {
                        TempData["Error"] = "El precio debe ser un múltiplo de 5 (5, 10, 15, 20...)";
                        ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                        return View(contenido);
                    }
                }

                // ✅ Actualizar campos básicos
                contenido.TipoContenido = (Models.TipoContenido)TipoContenido;
                contenido.Descripcion = Descripcion ?? "";

                // ✅ Actualizar tipo de lado usando la INTENCIÓN ORIGINAL
                var tipoAnterior = contenido.TipoLado;
                contenido.TipoLado = intentaPublicarEnLadoB ? TipoLado.LadoB : TipoLado.LadoA;
                contenido.EsGratis = EsGratis;
                contenido.EsPremium = !EsGratis;
                contenido.PrecioDesbloqueo = EsGratis ? 0m : (PrecioDesbloqueo ?? 10m);
                contenido.NombreMostrado = contenido.TipoLado == TipoLado.LadoA ?
                                          usuario.NombreCompleto : usuario.Seudonimo;

                // EsPublicoGeneral solo aplica para LadoA y no privado
                contenido.EsPublicoGeneral = (contenido.TipoLado == TipoLado.LadoA && !EsPrivado) ? EsPublicoGeneral : false;

                // ✅ Actualizar campo privado
                contenido.EsPrivado = EsPrivado;

                // ✅ Actualizar música asociada (solo para fotos)
                if (contenido.TipoContenido == Models.TipoContenido.Foto || contenido.TipoContenido == Models.TipoContenido.Imagen)
                {
                    // Si se envió PistaMusicalId = 0, significa quitar la música
                    if (PistaMusicalId.HasValue && PistaMusicalId.Value == 0)
                    {
                        contenido.PistaMusicalId = null;
                        contenido.AudioTrimInicio = null;
                        contenido.MusicaVolumen = null;
                        _logger.LogInformation("Música eliminada del contenido ID {Id}", id);
                    }
                    else if (PistaMusicalId.HasValue && PistaMusicalId.Value > 0)
                    {
                        // Verificar que la pista existe
                        var pistaExiste = await _context.PistasMusica.AnyAsync(p => p.Id == PistaMusicalId.Value && p.Activo);
                        if (pistaExiste)
                        {
                            contenido.PistaMusicalId = PistaMusicalId.Value;
                            contenido.AudioTrimInicio = AudioTrimInicio ?? 0;
                            contenido.MusicaVolumen = MusicaVolumen ?? 0.7m;
                            _logger.LogInformation("Música ID {MusicaId} asociada al contenido ID {Id}, AudioTrimInicio={Trim}, Volumen={Vol}",
                                PistaMusicalId.Value, id, AudioTrimInicio, MusicaVolumen);
                        }
                    }
                    // Si ya tiene música pero solo se actualizan los controles
                    else if (contenido.PistaMusicalId.HasValue && contenido.PistaMusicalId > 0)
                    {
                        if (AudioTrimInicio.HasValue)
                        {
                            contenido.AudioTrimInicio = AudioTrimInicio.Value;
                            _logger.LogInformation("AudioTrimInicio actualizado a {Trim} para contenido {Id}", AudioTrimInicio.Value, id);
                        }
                        if (MusicaVolumen.HasValue)
                        {
                            contenido.MusicaVolumen = MusicaVolumen.Value;
                        }
                    }
                }

                _logger.LogInformation("Tipo anterior: {TipoAnterior}, Nuevo tipo: {TipoNuevo}",
                    tipoAnterior, contenido.TipoLado);
                _logger.LogInformation("Precio asignado: ${Precio}, Nombre: {Nombre}",
                    contenido.PrecioDesbloqueo, contenido.NombreMostrado);

                // ✅ Subir nuevo archivo si se proporciona (SOLO para borradores)
                if (archivo != null && archivo.Length > 0)
                {
                    // ⚠️ SEGURIDAD: No permitir cambiar archivo en contenido ya publicado
                    if (!contenido.EsBorrador)
                    {
                        _logger.LogWarning("⚠️ Intento de cambiar archivo en contenido publicado ID {Id} por usuario {Username}",
                            id, usuario.UserName);
                        // Ignorar el archivo silenciosamente - la UI no debería permitir esto
                        archivo = null;
                    }
                }

                if (archivo != null && archivo.Length > 0)
                {
                    // ✅ SEGURIDAD: Validar archivo con magic bytes (no solo Content-Type)
                    FileValidationResult validacionArchivo;
                    if (contenido.TipoContenido == Models.TipoContenido.Foto)
                    {
                        validacionArchivo = await _fileValidationService.ValidarImagenAsync(archivo);
                    }
                    else if (contenido.TipoContenido == Models.TipoContenido.Video)
                    {
                        validacionArchivo = await _fileValidationService.ValidarVideoAsync(archivo);
                    }
                    else
                    {
                        validacionArchivo = await _fileValidationService.ValidarMediaAsync(archivo);
                    }

                    if (!validacionArchivo.EsValido)
                    {
                        _logger.LogWarning("Archivo rechazado en Editar: {FileName}, Razón: {Razon}",
                            archivo.FileName, validacionArchivo.MensajeError);
                        TempData["Error"] = validacionArchivo.MensajeError ?? "El tipo de archivo no es válido";
                        ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                        return View(contenido);
                    }

                    if (!string.IsNullOrEmpty(contenido.RutaArchivo))
                    {
                        var rutaAnterior = Path.Combine(_environment.WebRootPath, contenido.RutaArchivo.TrimStart('/'));
                        if (System.IO.File.Exists(rutaAnterior))
                        {
                            try
                            {
                                System.IO.File.Delete(rutaAnterior);
                                _logger.LogInformation("Archivo anterior eliminado: {Ruta}", rutaAnterior);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "No se pudo eliminar archivo anterior: {Ruta}", rutaAnterior);
                                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, usuario?.Id, usuario?.UserName);
                            }
                        }
                    }

                    var carpetaUsuario = usuario.UserName?.Replace("@", "_").Replace(".", "_") ?? usuario.Id;
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", carpetaUsuario);

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var extension = Path.GetExtension(archivo.FileName);
                    var nombreArchivo = $"{Guid.NewGuid()}{extension}";
                    var rutaCompleta = Path.Combine(uploadsFolder, nombreArchivo);

                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await archivo.CopyToAsync(stream);
                    }

                    contenido.RutaArchivo = $"/uploads/{carpetaUsuario}/{nombreArchivo}";
                    _logger.LogInformation("Nuevo archivo guardado: {Ruta}", contenido.RutaArchivo);
                }

                contenido.FechaActualizacion = DateTime.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Contenido ID {Id} actualizado exitosamente", id);

                if (contenido.TipoLado == TipoLado.LadoA)
                {
                    TempData["Success"] = "✅ Contenido actualizado como gratuito en LadoA";
                }
                else if (contenido.EsGratis)
                {
                    TempData["Success"] = "✅ Contenido actualizado como gratis en LadoB";
                }
                else
                {
                    TempData["Success"] = $"✅ Contenido actualizado como premium en LadoB (${contenido.PrecioDesbloqueo})";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar contenido {Id}", id);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                TempData["Error"] = $"Error al editar contenido: {ex.Message}";
                return RedirectToAction("Editar", new { id });
            }
        }

        // ========================================
        // USAR COMO FOTO DE PERFIL
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UsarComoFotoPerfil(int id, string lado = "A")
        {
            try
            {
                _logger.LogInformation("UsarComoFotoPerfil iniciado - Id: {Id}, Lado: {Lado}", id, lado);

                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                    return Json(new { success = false, message = "No autenticado" });

                var contenido = await _context.Contenidos
                    .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id);

                if (contenido == null)
                    return Json(new { success = false, message = "Contenido no encontrado" });

                _logger.LogInformation("Contenido encontrado - TipoContenido: {Tipo}, RutaArchivo: {Ruta}",
                    contenido.TipoContenido, contenido.RutaArchivo);

                // Verificar que sea una foto
                if (contenido.TipoContenido != Models.TipoContenido.Foto &&
                    contenido.TipoContenido != Models.TipoContenido.Imagen)
                    return Json(new { success = false, message = "Solo se pueden usar fotos como imagen de perfil" });

                if (string.IsNullOrEmpty(contenido.RutaArchivo))
                    return Json(new { success = false, message = "El contenido no tiene archivo" });

                // Copiar archivo a carpeta de perfiles - normalizar la ruta
                var rutaNormalizada = contenido.RutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var rutaOrigen = Path.Combine(_environment.WebRootPath, rutaNormalizada);
                _logger.LogInformation("Ruta origen: {Ruta}", rutaOrigen);

                if (!System.IO.File.Exists(rutaOrigen))
                    return Json(new { success = false, message = $"Archivo no encontrado: {rutaOrigen}" });

                var extension = Path.GetExtension(contenido.RutaArchivo);
                var nuevoNombre = $"{Guid.NewGuid()}{extension}";
                var carpetaDestino = lado == "B" ? "perfiles-ladob" : "perfiles";
                var rutaDestino = Path.Combine(_environment.WebRootPath, "uploads", carpetaDestino);

                if (!Directory.Exists(rutaDestino))
                    Directory.CreateDirectory(rutaDestino);

                var archivoDestino = Path.Combine(rutaDestino, nuevoNombre);
                System.IO.File.Copy(rutaOrigen, archivoDestino, true);

                var rutaRelativa = $"/uploads/{carpetaDestino}/{nuevoNombre}";

                // Actualizar usuario
                if (lado == "B")
                    usuario.FotoPerfilLadoB = rutaRelativa;
                else
                    usuario.FotoPerfil = rutaRelativa;

                await _userManager.UpdateAsync(usuario);

                _logger.LogInformation("Usuario {UserId} uso contenido {ContenidoId} como foto de perfil (Lado {Lado})",
                    usuario.Id, id, lado);

                return Json(new { success = true, message = $"Foto de perfil actualizada", nuevaRuta = rutaRelativa });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al usar contenido {Id} como foto de perfil", id);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========================================
        // ELIMINAR CONTENIDO
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                if (usuario == null)
                {
                    _logger.LogWarning("Usuario no autenticado intentando eliminar contenido {Id}", id);
                    TempData["Error"] = "Debes iniciar sesión para eliminar contenido";
                    return RedirectToAction("Login", "Account");
                }

                var contenido = await _context.Contenidos
                    .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id);

                if (contenido == null)
                {
                    _logger.LogWarning("Contenido {Id} no encontrado o no pertenece al usuario {Username}", id, usuario.UserName);
                    TempData["Error"] = "El contenido no existe o no tienes permiso para eliminarlo";
                    return RedirectToAction("Index");
                }

                if (!contenido.EstaActivo)
                {
                    _logger.LogWarning("Intento de eliminar contenido ya eliminado {Id}", id);
                    TempData["Warning"] = "Este contenido ya fue eliminado";
                    return RedirectToAction("Index");
                }

                contenido.EstaActivo = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Contenido eliminado (lógico) - ID: {Id}, Usuario: {Username}",
                    id, usuario.UserName);

                TempData["Success"] = "Contenido eliminado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar contenido {Id}", id);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                TempData["Error"] = "Error al eliminar el contenido. Por favor intenta de nuevo.";
                return RedirectToAction("Index");
            }
        }

        // ========================================
        // LIKES
        // ========================================

        [HttpPost]
        public async Task<IActionResult> Like(int id)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var contenido = await _context.Contenidos.FindAsync(id);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                var likeExistente = await _context.Likes
                    .FirstOrDefaultAsync(l => l.ContenidoId == id && l.UsuarioId == usuario.Id);

                bool liked;

                if (likeExistente != null)
                {
                    _context.Likes.Remove(likeExistente);
                    contenido.NumeroLikes = Math.Max(0, contenido.NumeroLikes - 1);
                    liked = false;
                    _logger.LogInformation("Like removido - Contenido: {Id}, Usuario: {Username}", id, usuario.UserName);
                }
                else
                {
                    var like = new Like
                    {
                        ContenidoId = id,
                        UsuarioId = usuario.Id,
                        FechaLike = DateTime.Now
                    };
                    _context.Likes.Add(like);
                    contenido.NumeroLikes++;
                    liked = true;
                    _logger.LogInformation("Like agregado - Contenido: {Id}, Usuario: {Username}", id, usuario.UserName);

                    // Registrar like para LadoCoins
                    try
                    {
                        await _rachasService.RegistrarLikeAsync(usuario.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al registrar like para LadoCoins");
                        _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, usuario?.Id, usuario?.UserName);
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    likes = contenido.NumeroLikes,
                    liked = liked
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar like para contenido {Id}", id);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                return Json(new { success = false, message = "Error al procesar el like" });
            }
        }

        // ========================================
        // COMENTARIOS
        // ========================================

        [HttpPost]
        public async Task<IActionResult> Comentar(int id, string texto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(texto))
                {
                    return Json(new { success = false, message = "El comentario no puede estar vacío" });
                }

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var contenido = await _context.Contenidos.FindAsync(id);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                var comentario = new Comentario
                {
                    ContenidoId = id,
                    UsuarioId = usuarioId,
                    Texto = texto,
                    FechaCreacion = DateTime.Now
                };

                _context.Comentarios.Add(comentario);
                contenido.NumeroComentarios++;

                await _context.SaveChangesAsync();

                // Registrar comentario para LadoCoins
                try
                {
                    await _rachasService.RegistrarComentarioAsync(usuarioId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al registrar comentario para LadoCoins");
                    _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, usuarioId, null);
                }

                var usuario = await _userManager.FindByIdAsync(usuarioId);

                return Json(new
                {
                    success = true,
                    comentario = new
                    {
                        id = comentario.Id,
                        texto = comentario.Texto,
                        usuario = new
                        {
                            nombre = usuario.NombreCompleto,
                            username = usuario.UserName,
                            fotoPerfil = usuario.FotoPerfil
                        },
                        fechaCreacion = comentario.FechaCreacion
                    },
                    totalComentarios = contenido.NumeroComentarios
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al publicar comentario en contenido {Id}", id);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                return Json(new { success = false, message = "Error al publicar el comentario" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerComentarios(int id)
        {
            try
            {
                var comentarios = await _context.Comentarios
                    .Include(c => c.Usuario)
                    .Where(c => c.ContenidoId == id)
                    .OrderByDescending(c => c.FechaCreacion)
                    .Select(c => new
                    {
                        id = c.Id,
                        texto = c.Texto,
                        usuario = new
                        {
                            nombre = c.Usuario.NombreCompleto,
                            username = c.Usuario.UserName,
                            fotoPerfil = c.Usuario.FotoPerfil
                        },
                        fechaCreacion = c.FechaCreacion
                    })
                    .ToListAsync();

                return Json(new { success = true, comentarios });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comentarios");
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _ = _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, userId, null);
                return Json(new { success = false, message = "Error al cargar comentarios" });
            }
        }
    }
}