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
        private readonly IClaudeClassificationService _classificationService;

        public ContenidoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<ContenidoController> logger,
            INotificationService notificationService,
            IImageService imageService,
            IClaudeClassificationService classificationService)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _logger = logger;
            _notificationService = notificationService;
            _imageService = imageService;
            _classificationService = classificationService;
        }

        // ========================================
        // INDEX - LISTADO DE CONTENIDO DEL USUARIO
        // ========================================

        public async Task<IActionResult> Index(string filtro = "todos")
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (usuario == null)
            {
                _logger.LogWarning("Usuario no encontrado en Index");
                return RedirectToAction("Login", "Account");
            }

            var query = _context.Contenidos.AsQueryable();
            query = query.Where(c => c.UsuarioId == usuario.Id && c.EstaActivo);

            switch (filtro?.ToLower() ?? "todos")
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
                default:
                    break;
            }

            var contenidos = await query
                .OrderByDescending(c => c.FechaPublicacion)
                .ToListAsync();

            ViewBag.FiltroActual = filtro ?? "todos";

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

            _logger.LogInformation("GET Crear - Usuario: {Username}, Verificado: {Verificado}, LadoPreferido: {LadoPreferido}",
                user.UserName, user.CreadorVerificado, user.LadoPreferido);

            return View();
        }

        // ========================================
        // CREAR CONTENIDO - POST
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            IFormFile archivo,
            string Descripcion,
            int TipoContenido,
            bool EsGratis,
            decimal? PrecioDesbloqueo = null,
            bool EsBorrador = false,
            bool EsPublicoGeneral = false)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                if (usuario == null)
                {
                    _logger.LogWarning("Usuario no encontrado en Crear POST");
                    return RedirectToAction("Login", "Account");
                }

                _logger.LogInformation("=== CREAR CONTENIDO ===");
                _logger.LogInformation("Usuario: {Username} (Real: {NombreCompleto}, Seudónimo: {Seudonimo})",
                    usuario.UserName, usuario.NombreCompleto, usuario.Seudonimo);
                _logger.LogInformation("Verificado: {Verificado}", usuario.CreadorVerificado);
                _logger.LogInformation("Parámetros - EsGratis: {EsGratis}, Precio: {Precio}",
                    EsGratis, PrecioDesbloqueo);

                // ✅ GUARDAR LA INTENCIÓN ORIGINAL DEL USUARIO
                var intentaPublicarEnLadoB = !EsGratis;

                // ✅ REGLA PRINCIPAL: Solo verificados pueden monetizar
                if (!EsGratis && !usuario.CreadorVerificado)
                {
                    _logger.LogWarning("⚠️ Usuario {Username} intentó monetizar sin verificación - Forzando contenido gratis",
                        usuario.UserName);

                    // Forzar gratis pero mantener que intentó publicar en LadoB
                    EsGratis = true;
                    PrecioDesbloqueo = 0;

                    TempData["Warning"] = "Para monetizar contenido debes verificar tu identidad. Tu contenido se ha publicado gratis en LadoB.";
                }

                // Validaciones básicas - archivo requerido para fotos/videos
                if (!EsBorrador && TipoContenido != (int)Models.TipoContenido.Post && (archivo == null || archivo.Length == 0))
                {
                    TempData["Error"] = "Debes subir un archivo para este tipo de contenido";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View();
                }

                // Validación de precio múltiplo de 5 (solo si NO es gratis Y está verificado)
                if (!EsGratis && usuario.CreadorVerificado)
                {
                    if (!PrecioDesbloqueo.HasValue || PrecioDesbloqueo <= 0)
                    {
                        PrecioDesbloqueo = 10m;
                    }

                    if (PrecioDesbloqueo % 5 != 0)
                    {
                        TempData["Error"] = "El precio debe ser un múltiplo de 5 (5, 10, 15, 20...)";
                        ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                        return View();
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

                    var extension = Path.GetExtension(archivo.FileName).ToLower();
                    var tiposPermitidosImg = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif", ".bmp" };
                    var tiposPermitidosVideo = new[] { ".mp4", ".mov", ".avi", ".webm", ".m4v", ".3gp" };
                    var tiposPermitidos = tiposPermitidosImg.Concat(tiposPermitidosVideo).ToArray();

                    if (!tiposPermitidos.Contains(extension))
                    {
                        TempData["Error"] = "Tipo de archivo no permitido. Formatos válidos: JPG, PNG, GIF, WEBP, HEIC, MP4, MOV, AVI";
                        ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                        return View();
                    }

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

                    contenido.RutaArchivo = $"/uploads/{carpetaUsuario}/{uniqueFileName}";
                    _logger.LogInformation("Archivo guardado: {RutaArchivo}", contenido.RutaArchivo);

                    // Generar thumbnail para imágenes
                    if (_imageService.EsImagenValida(extension))
                    {
                        var thumbnail = await _imageService.GenerarThumbnailAsync(filePath, 400, 400, 75);
                        if (!string.IsNullOrEmpty(thumbnail))
                        {
                            contenido.Thumbnail = thumbnail;
                            _logger.LogInformation("Thumbnail generado: {Thumbnail}", thumbnail);
                        }
                    }
                }

                // Clasificar contenido automaticamente con Claude AI
                try
                {
                    byte[]? imagenBytes = null;
                    string? mimeType = null;

                    // Leer el archivo guardado para clasificacion
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
                                if (fileInfo.Length < 5 * 1024 * 1024) // Max 5MB para clasificacion
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

                    var resultado = await _classificationService.ClasificarContenidoDetalladoAsync(
                        imagenBytes, Descripcion, mimeType);

                    if (resultado.Exito && resultado.CategoriaId.HasValue)
                    {
                        contenido.CategoriaInteresId = resultado.CategoriaId.Value;
                        if (resultado.CategoriaCreada)
                        {
                            _logger.LogInformation("Nueva categoria creada por IA: {Nombre} (ID: {Id})",
                                resultado.CategoriaNombre, resultado.CategoriaId.Value);
                        }
                        else
                        {
                            _logger.LogInformation("Contenido clasificado en categoria {CategoriaId}", resultado.CategoriaId.Value);
                        }
                    }
                    else if (!string.IsNullOrEmpty(resultado.Error))
                    {
                        _logger.LogWarning("No se pudo clasificar: {Error} - {Detalle}", resultado.Error, resultado.DetalleError);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al clasificar contenido, continuando sin categoria");
                }

                _context.Contenidos.Add(contenido);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Contenido guardado - ID: {Id}, TipoLado: {TipoLado}, CategoriaId: {CategoriaId}",
                    contenido.Id, contenido.TipoLado, contenido.CategoriaInteresId);

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
                    else if (EsGratis)
                    {
                        TempData["Success"] = $"✅ Contenido gratis en LadoB publicado como {usuario.Seudonimo}";
                    }
                    else
                    {
                        TempData["Success"] = $"✅ Contenido premium (LadoB) publicado como {usuario.Seudonimo} (${PrecioDesbloqueo})";
                    }
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear contenido");
                TempData["Error"] = $"Error al crear contenido: {ex.Message}";
                ViewBag.UsuarioVerificado = false;
                return View();
            }
        }

        // ========================================
        // CREAR CARRUSEL (MÚLTIPLES ARCHIVOS)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearCarrusel(
            List<IFormFile> archivos,
            string Descripcion,
            bool EsGratis,
            decimal? PrecioDesbloqueo = null,
            bool EsBorrador = false,
            bool EsPublicoGeneral = false)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
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

                // Validar verificación para monetización
                var intentaPublicarEnLadoB = !EsGratis;
                if (!EsGratis && !usuario.CreadorVerificado)
                {
                    EsGratis = true;
                    PrecioDesbloqueo = 0;
                }

                // Validar precio
                if (!EsGratis && usuario.CreadorVerificado)
                {
                    if (!PrecioDesbloqueo.HasValue || PrecioDesbloqueo <= 0)
                        PrecioDesbloqueo = 10m;

                    if (PrecioDesbloqueo % 5 != 0)
                    {
                        return Json(new { success = false, message = "El precio debe ser un múltiplo de 5" });
                    }
                }

                var tipoLado = intentaPublicarEnLadoB ? TipoLado.LadoB : TipoLado.LadoA;
                var nombreMostrado = tipoLado == TipoLado.LadoA ? usuario.NombreCompleto : usuario.Seudonimo;

                // Determinar tipo de contenido (si hay video, es video; si no, foto)
                var extensionesVideo = new[] { ".mp4", ".mov", ".avi", ".webm", ".m4v", ".3gp" };
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

                var tiposPermitidosImg = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif", ".bmp" };
                var tiposPermitidosVideo = new[] { ".mp4", ".mov", ".avi", ".webm", ".m4v", ".3gp" };
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

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await archivo.CopyToAsync(fileStream);
                    }

                    var archivoContenido = new ArchivoContenido
                    {
                        ContenidoId = contenido.Id,
                        RutaArchivo = $"/uploads/{carpetaUsuario}/{uniqueFileName}",
                        Orden = i,
                        TipoArchivo = esVideo ? TipoArchivo.Video : TipoArchivo.Foto,
                        TamanoBytes = archivo.Length,
                        FechaCreacion = DateTime.Now
                    };

                    // Generar thumbnail para imágenes
                    if (esFoto && _imageService.EsImagenValida(extension))
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
                }
                // Si solo hay videos, dejar Thumbnail null para que la vista use el tag <video>

                // Clasificar contenido automaticamente con Claude AI
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

                    var resultado = await _classificationService.ClasificarContenidoDetalladoAsync(
                        imagenBytes, Descripcion, mimeType);

                    if (resultado.Exito && resultado.CategoriaId.HasValue)
                    {
                        contenido.CategoriaInteresId = resultado.CategoriaId.Value;
                        if (resultado.CategoriaCreada)
                        {
                            _logger.LogInformation("Carrusel: Nueva categoria creada por IA: {Nombre} (ID: {Id})",
                                resultado.CategoriaNombre, resultado.CategoriaId.Value);
                        }
                        else
                        {
                            _logger.LogInformation("Carrusel clasificado en categoria {CategoriaId}", resultado.CategoriaId.Value);
                        }
                    }
                    else if (!string.IsNullOrEmpty(resultado.Error))
                    {
                        _logger.LogWarning("Carrusel no clasificado: {Error} - {Detalle}", resultado.Error, resultado.DetalleError);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al clasificar carrusel, continuando sin categoria");
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Carrusel creado - ID: {Id}, Archivos: {Count}, CategoriaId: {CategoriaId}",
                    contenido.Id, archivosGuardados.Count, contenido.CategoriaInteresId);

                // Notificar a seguidores
                if (!EsBorrador)
                {
                    _ = _notificationService.NotificarNuevoContenidoAsync(
                        usuario.Id,
                        contenido.Id,
                        contenido.Descripcion ?? "Nuevo contenido",
                        tipoLado);
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
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========================================
        // CREAR DESDE REELS (AJAX)
        // ========================================

        [HttpPost]
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
                if (tipoContenido == TipoContenido.Foto && _imageService.EsImagenValida(extension))
                {
                    thumbnailPath = await _imageService.GenerarThumbnailAsync(filePath, 400, 400, 75);
                    _logger.LogInformation("Thumbnail generado: {Thumbnail}", thumbnailPath);
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
                    // Música asociada
                    PistaMusicalId = audioTrackId,
                    MusicaVolumen = audioVolume,
                    AudioOriginalVolumen = originalVolume,
                    AudioTrimInicio = audioStartTime.HasValue ? (int)audioStartTime.Value : null,
                    AudioDuracion = null // Se calculará si es necesario
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

                // Clasificar contenido automaticamente con Claude AI
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

                    var resultado = await _classificationService.ClasificarContenidoDetalladoAsync(
                        imagenBytes, descripcion, mimeType);

                    if (resultado.Exito && resultado.CategoriaId.HasValue)
                    {
                        contenido.CategoriaInteresId = resultado.CategoriaId.Value;
                        if (resultado.CategoriaCreada)
                        {
                            _logger.LogInformation("Reel: Nueva categoria creada por IA: {Nombre} (ID: {Id})",
                                resultado.CategoriaNombre, resultado.CategoriaId.Value);
                        }
                        else
                        {
                            _logger.LogInformation("Reel clasificado en categoria {CategoriaId}", resultado.CategoriaId.Value);
                        }
                    }
                    else if (!string.IsNullOrEmpty(resultado.Error))
                    {
                        _logger.LogWarning("Reel no clasificado: {Error} - {Detalle}", resultado.Error, resultado.DetalleError);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al clasificar reel, continuando sin categoria");
                }

                _context.Contenidos.Add(contenido);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Contenido creado desde Reels - ID: {Id}, CategoriaId: {CategoriaId}",
                    contenido.Id, contenido.CategoriaInteresId);

                // Notificar a seguidores sobre el nuevo contenido
                _ = _notificationService.NotificarNuevoContenidoAsync(
                    usuario.Id,
                    contenido.Id,
                    contenido.Descripcion ?? "Nuevo contenido",
                    contenido.TipoLado);

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
                _logger.LogError(ex, "Error al crear contenido desde Reels");
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

                // ✅ REGLA: Solo verificados pueden monetizar
                if (!EsGratis && !usuario.CreadorVerificado)
                {
                    _logger.LogWarning("⚠️ Usuario {Username} intentó monetizar sin verificación en edición",
                        usuario.UserName);

                    EsGratis = true;
                    PrecioDesbloqueo = 0;

                    TempData["Warning"] = "Para monetizar contenido debes verificar tu identidad. El contenido se mantendrá gratis.";
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
                            _logger.LogInformation("Música ID {MusicaId} asociada al contenido ID {Id}", PistaMusicalId.Value, id);
                        }
                    }
                }

                _logger.LogInformation("Tipo anterior: {TipoAnterior}, Nuevo tipo: {TipoNuevo}",
                    tipoAnterior, contenido.TipoLado);
                _logger.LogInformation("Precio asignado: ${Precio}, Nombre: {Nombre}",
                    contenido.PrecioDesbloqueo, contenido.NombreMostrado);

                // ✅ Subir nuevo archivo si se proporciona
                if (archivo != null && archivo.Length > 0)
                {
                    var extensionPermitida = false;
                    if (contenido.TipoContenido == Models.TipoContenido.Foto)
                    {
                        extensionPermitida = archivo.ContentType.StartsWith("image/");
                    }
                    else if (contenido.TipoContenido == Models.TipoContenido.Video)
                    {
                        extensionPermitida = archivo.ContentType.StartsWith("video/");
                    }

                    if (!extensionPermitida)
                    {
                        TempData["Error"] = "El tipo de archivo no coincide con el tipo de contenido seleccionado";
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
                _logger.LogError(ex, "Error al editar contenido");
                TempData["Error"] = $"Error al editar contenido: {ex.Message}";
                return RedirectToAction("Editar", new { id });
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
                var contenido = await _context.Contenidos
                    .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id);

                if (contenido == null)
                {
                    _logger.LogWarning("Contenido no encontrado para eliminar: {Id}", id);
                    return NotFound();
                }

                contenido.EstaActivo = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Contenido eliminado (lógico) - ID: {Id}, Usuario: {Username}",
                    id, usuario.UserName);

                TempData["Success"] = "✅ Contenido eliminado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar contenido {Id}", id);
                TempData["Error"] = "Error al eliminar el contenido";
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
                _logger.LogError(ex, "Error al publicar comentario");
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
                return Json(new { success = false, message = "Error al cargar comentarios" });
            }
        }
    }
}