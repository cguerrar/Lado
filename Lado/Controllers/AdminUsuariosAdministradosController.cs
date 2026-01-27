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
    [Authorize(Roles = "Admin")]
    [Route("Admin/UsuariosAdministrados")]
    public class AdminUsuariosAdministradosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AdminUsuariosAdministradosController> _logger;
        private readonly IMediaConversionService _mediaConversionService;

        public AdminUsuariosAdministradosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<AdminUsuariosAdministradosController> logger,
            IMediaConversionService mediaConversionService)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _logger = logger;
            _mediaConversionService = mediaConversionService;
        }

        // ========================================
        // LISTA DE USUARIOS ADMINISTRADOS
        // ========================================

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var usuarios = await _context.Users
                .Where(u => u.EsUsuarioAdministrado)
                .OrderByDescending(u => u.FechaRegistro)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.NombreCompleto,
                    u.FotoPerfil,
                    u.EsCreador,
                    u.EstaActivo,
                    u.FechaRegistro,
                    MediaPendiente = _context.MediaBiblioteca.Count(m => m.UsuarioId == u.Id && m.Estado == EstadoMediaBiblioteca.Pendiente),
                    MediaProgramada = _context.MediaBiblioteca.Count(m => m.UsuarioId == u.Id && m.Estado == EstadoMediaBiblioteca.Programado),
                    MediaPublicada = _context.MediaBiblioteca.Count(m => m.UsuarioId == u.Id && m.Estado == EstadoMediaBiblioteca.Publicado),
                    ConfiguracionActiva = _context.ConfiguracionesPublicacionAutomatica
                        .Where(c => c.UsuarioId == u.Id)
                        .Select(c => c.Activo)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return View(usuarios);
        }

        // ========================================
        // CREAR NUEVO USUARIO ADMINISTRADO
        // ========================================

        [HttpGet("Crear")]
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost("Crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(string userName, string nombreCompleto, string? email, string? biografia, bool esCreador = true)
        {
            try
            {
                // Validaciones
                if (string.IsNullOrWhiteSpace(userName))
                {
                    TempData["Error"] = "El nombre de usuario es requerido";
                    return View();
                }

                // Verificar si ya existe
                var existente = await _userManager.FindByNameAsync(userName);
                if (existente != null)
                {
                    TempData["Error"] = "Ya existe un usuario con ese nombre";
                    return View();
                }

                // Crear usuario
                var usuario = new ApplicationUser
                {
                    UserName = userName.Trim().ToLower().Replace(" ", ""),
                    NombreCompleto = nombreCompleto?.Trim() ?? userName,
                    Email = email ?? $"{userName.ToLower()}@lado.virtual",
                    EmailConfirmed = true,
                    EsUsuarioAdministrado = true,
                    EsCreador = esCreador,
                    TipoUsuario = esCreador ? 1 : 0,
                    EstaActivo = true,
                    FechaRegistro = DateTime.Now,
                    Biografia = biografia,
                    CreadorVerificado = true
                };

                // Generar contraseña aleatoria (no se usará pero es requerida)
                var password = Guid.NewGuid().ToString() + "Aa1!";
                var result = await _userManager.CreateAsync(usuario, password);

                if (!result.Succeeded)
                {
                    TempData["Error"] = "Error al crear usuario: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    return View();
                }

                // Crear configuración de publicación automática
                var config = new ConfiguracionPublicacionAutomatica
                {
                    UsuarioId = usuario.Id,
                    Activo = false,
                    PublicacionesMinPorDia = 1,
                    PublicacionesMaxPorDia = 3,
                    HoraInicio = new TimeSpan(9, 0, 0),
                    HoraFin = new TimeSpan(22, 0, 0),
                    VariacionMinutos = 30,
                    FechaCreacion = DateTime.Now,
                    FechaModificacion = DateTime.Now
                };
                _context.ConfiguracionesPublicacionAutomatica.Add(config);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Usuario administrado creado: {UserName} (ID: {UserId})", usuario.UserName, usuario.Id);

                TempData["Success"] = $"Usuario '{usuario.UserName}' creado exitosamente";
                return RedirectToAction(nameof(Gestionar), new { id = usuario.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear usuario administrado");
                TempData["Error"] = "Error interno: " + ex.Message;
                return View();
            }
        }

        // ========================================
        // GESTIONAR USUARIO ADMINISTRADO
        // ========================================

        [HttpGet("Gestionar/{id}")]
        public async Task<IActionResult> Gestionar(string id, string? tab = null)
        {
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction(nameof(Index));
            }

            // Obtener configuración de publicación
            var config = await _context.ConfiguracionesPublicacionAutomatica
                .FirstOrDefaultAsync(c => c.UsuarioId == id);

            // Estadísticas de la biblioteca
            var estadisticasBiblioteca = await _context.MediaBiblioteca
                .Where(m => m.UsuarioId == id)
                .GroupBy(m => m.Estado)
                .Select(g => new { Estado = g.Key, Count = g.Count() })
                .ToListAsync();

            // Contenido publicado
            var contenidoCount = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == id && c.EstaActivo);

            // Seguidores
            var seguidoresCount = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == id && s.EstaActiva);

            ViewBag.Config = config;
            ViewBag.Pendientes = estadisticasBiblioteca.FirstOrDefault(e => e.Estado == EstadoMediaBiblioteca.Pendiente)?.Count ?? 0;
            ViewBag.Programados = estadisticasBiblioteca.FirstOrDefault(e => e.Estado == EstadoMediaBiblioteca.Programado)?.Count ?? 0;
            ViewBag.Publicados = estadisticasBiblioteca.FirstOrDefault(e => e.Estado == EstadoMediaBiblioteca.Publicado)?.Count ?? 0;
            ViewBag.ContenidoCount = contenidoCount;
            ViewBag.SeguidoresCount = seguidoresCount;
            ViewBag.Tab = tab ?? "perfil";

            return View(usuario);
        }

        // ========================================
        // ACTUALIZAR PERFIL
        // ========================================

        [HttpPost("ActualizarPerfil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPerfil(string id, string nombreCompleto, string? biografia,
            string? seudonimo, decimal? precioSuscripcion, bool esCreador, bool estaActivo,
            // Nuevos campos
            string? biografiaLadoB, decimal? precioSuscripcionLadoB, string? categoria,
            bool esVerificado, bool creadorVerificado, string? pais, string? ciudad)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no encontrado" });
            }

            // Campos básicos
            usuario.NombreCompleto = nombreCompleto?.Trim();
            usuario.Biografia = biografia;
            usuario.Seudonimo = seudonimo;
            usuario.PrecioSuscripcion = precioSuscripcion ?? 0;
            usuario.EsCreador = esCreador;
            usuario.TipoUsuario = esCreador ? 1 : 0;
            usuario.EstaActivo = estaActivo;

            // Campos Lado B
            usuario.BiografiaLadoB = biografiaLadoB;
            usuario.PrecioSuscripcionLadoB = precioSuscripcionLadoB;

            // Categoría y verificación
            usuario.Categoria = categoria;
            usuario.EsVerificado = esVerificado;
            usuario.CreadorVerificado = creadorVerificado;
            if (creadorVerificado && usuario.FechaVerificacion == null)
            {
                usuario.FechaVerificacion = DateTime.Now;
            }

            // Ubicación
            usuario.Pais = pais;
            usuario.Ciudad = ciudad;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Perfil actualizado" });
        }

        // ========================================
        // SUBIR FOTO DE PERFIL
        // ========================================

        [HttpPost("SubirFotoPerfil")]
        public async Task<IActionResult> SubirFotoPerfil(string id, IFormFile foto)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no encontrado" });
            }

            if (foto == null || foto.Length == 0)
            {
                return Json(new { success = false, message = "No se recibió ningún archivo" });
            }

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "perfiles");
                Directory.CreateDirectory(uploadsFolder);

                var extension = Path.GetExtension(foto.FileName).ToLower();
                var fileName = $"{id}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await foto.CopyToAsync(stream);
                }

                usuario.FotoPerfil = $"/uploads/perfiles/{fileName}";
                await _context.SaveChangesAsync();

                return Json(new { success = true, url = usuario.FotoPerfil });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir foto de perfil");
                return Json(new { success = false, message = "Error al subir la imagen" });
            }
        }

        [HttpPost("SubirFotoPortada")]
        public async Task<IActionResult> SubirFotoPortada(string id, IFormFile foto)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
                return Json(new { success = false, message = "Usuario no encontrado" });

            if (foto == null || foto.Length == 0)
                return Json(new { success = false, message = "No se recibió ningún archivo" });

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "portadas");
                Directory.CreateDirectory(uploadsFolder);

                var extension = Path.GetExtension(foto.FileName).ToLower();
                var fileName = $"{id}_portada_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await foto.CopyToAsync(stream);
                }

                usuario.FotoPortada = $"/uploads/portadas/{fileName}";
                await _context.SaveChangesAsync();

                return Json(new { success = true, url = usuario.FotoPortada });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir foto de portada");
                return Json(new { success = false, message = "Error al subir la imagen" });
            }
        }

        [HttpPost("SubirFotoPerfilLadoB")]
        public async Task<IActionResult> SubirFotoPerfilLadoB(string id, IFormFile foto)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
                return Json(new { success = false, message = "Usuario no encontrado" });

            if (foto == null || foto.Length == 0)
                return Json(new { success = false, message = "No se recibió ningún archivo" });

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "perfiles");
                Directory.CreateDirectory(uploadsFolder);

                var extension = Path.GetExtension(foto.FileName).ToLower();
                var fileName = $"{id}_ladob_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await foto.CopyToAsync(stream);
                }

                usuario.FotoPerfilLadoB = $"/uploads/perfiles/{fileName}";
                await _context.SaveChangesAsync();

                return Json(new { success = true, url = usuario.FotoPerfilLadoB });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir foto de perfil Lado B");
                return Json(new { success = false, message = "Error al subir la imagen" });
            }
        }

        // ========================================
        // ESTADÍSTICAS DEL USUARIO
        // ========================================

        [HttpGet("Estadisticas/{id}")]
        public async Task<IActionResult> ObtenerEstadisticas(string id)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
                return Json(new { success = false, message = "Usuario no encontrado" });

            var contenidos = await _context.Contenidos
                .Where(c => c.UsuarioId == id && c.EstaActivo)
                .ToListAsync();

            var contenidoIds = contenidos.Select(c => c.Id).ToList();

            var totalLikes = await _context.Reacciones
                .Where(r => contenidoIds.Contains(r.ContenidoId))
                .CountAsync();

            var totalComentarios = await _context.Comentarios
                .Where(c => contenidoIds.Contains(c.ContenidoId))
                .CountAsync();

            var storiesCount = await _context.Stories
                .Where(s => s.CreadorId == id && s.EstaActivo)
                .CountAsync();

            var storiesVistas = await _context.Stories
                .Where(s => s.CreadorId == id)
                .SumAsync(s => s.NumeroVistas);

            // Los seguidores se toman del campo del usuario (no hay tabla relacional)
            var seguidores = usuario.NumeroSeguidores;
            var siguiendo = 0; // No disponible sin tabla relacional

            var suscriptores = await _context.Suscripciones
                .Where(s => s.CreadorId == id && s.EstaActiva)
                .CountAsync();

            return Json(new
            {
                success = true,
                stats = new
                {
                    seguidores,
                    siguiendo,
                    suscriptores,
                    contenidos = contenidos.Count,
                    stories = storiesCount,
                    totalLikes,
                    totalComentarios,
                    visitasPerfil = usuario.VisitasPerfil,
                    storiesVistas,
                    mensajesRecibidos = usuario.MensajesRecibidosTotal,
                    mensajesRespondidos = usuario.MensajesRespondidosTotal,
                    tiempoPromedioRespuesta = usuario.TiempoPromedioRespuesta,
                    totalGanancias = usuario.TotalGanancias,
                    saldo = usuario.Saldo
                }
            });
        }

        // ========================================
        // STORIES PUBLICADAS
        // ========================================

        [HttpGet("Stories/{id}")]
        public async Task<IActionResult> ObtenerStories(string id, int page = 1, int pageSize = 20)
        {
            var query = _context.Stories
                .Where(s => s.CreadorId == id)
                .OrderByDescending(s => s.FechaPublicacion);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    id = s.Id,
                    rutaArchivo = s.RutaArchivo,
                    tipoContenido = s.TipoContenido,
                    fechaPublicacion = s.FechaPublicacion,
                    fechaExpiracion = s.FechaExpiracion,
                    estaActivo = s.EstaActivo,
                    numeroVistas = s.NumeroVistas,
                    numeroLikes = s.NumeroLikes,
                    tipoLado = s.TipoLado,
                    texto = s.Texto,
                    expirada = s.FechaExpiracion < DateTime.Now
                })
                .ToListAsync();

            return Json(new { success = true, items, total, page, pageSize });
        }

        [HttpPost("EliminarStory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarStory(int id)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story == null)
                return Json(new { success = false, message = "Story no encontrada" });

            story.EstaActivo = false;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // ========================================
        // IMPORTAR DESDE URL
        // ========================================

        [HttpPost("ImportarDesdeUrl")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarDesdeUrl(string id, string url, TipoPublicacionMedia tipoPublicacion = TipoPublicacionMedia.Contenido)
        {
            if (string.IsNullOrWhiteSpace(url))
                return Json(new { success = false, message = "URL requerida" });

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return Json(new { success = false, message = "No se pudo descargar el archivo" });

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                var isVideo = contentType.StartsWith("video/");
                var isImage = contentType.StartsWith("image/");

                if (!isVideo && !isImage)
                    return Json(new { success = false, message = "El archivo debe ser una imagen o video" });

                var extension = isVideo ? ".mp4" : ".jpg";
                if (url.Contains("."))
                {
                    var urlExt = Path.GetExtension(new Uri(url).AbsolutePath).ToLower();
                    if (!string.IsNullOrEmpty(urlExt))
                        extension = urlExt;
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "biblioteca", id);
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await System.IO.File.WriteAllBytesAsync(filePath, bytes);

                var media = new MediaBiblioteca
                {
                    UsuarioId = id,
                    RutaArchivo = $"/uploads/biblioteca/{id}/{fileName}",
                    NombreOriginal = Path.GetFileName(new Uri(url).AbsolutePath),
                    TipoMedia = isVideo ? TipoMediaBiblioteca.Video : TipoMediaBiblioteca.Imagen,
                    TipoPublicacion = tipoPublicacion,
                    TamanoBytes = bytes.Length,
                    Estado = EstadoMediaBiblioteca.Pendiente,
                    FechaSubida = DateTime.Now
                };

                _context.MediaBiblioteca.Add(media);
                await _context.SaveChangesAsync();

                return Json(new { success = true, mediaId = media.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al importar desde URL: {Url}", url);
                return Json(new { success = false, message = "Error al importar: " + ex.Message });
            }
        }

        // ========================================
        // DUPLICAR MEDIA
        // ========================================

        [HttpPost("DuplicarMedia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuplicarMedia(int mediaId)
        {
            var original = await _context.MediaBiblioteca.FindAsync(mediaId);
            if (original == null)
                return Json(new { success = false, message = "Media no encontrado" });

            try
            {
                // Copiar archivo físico
                var originalPath = Path.Combine(_environment.WebRootPath, original.RutaArchivo.TrimStart('/'));
                if (!System.IO.File.Exists(originalPath))
                    return Json(new { success = false, message = "Archivo original no encontrado" });

                var extension = Path.GetExtension(originalPath);
                var newFileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
                var newPath = Path.Combine(Path.GetDirectoryName(originalPath)!, newFileName);

                System.IO.File.Copy(originalPath, newPath);

                // Crear registro
                var copia = new MediaBiblioteca
                {
                    UsuarioId = original.UsuarioId,
                    RutaArchivo = original.RutaArchivo.Replace(Path.GetFileName(original.RutaArchivo), newFileName),
                    NombreOriginal = original.NombreOriginal,
                    TipoMedia = original.TipoMedia,
                    TipoPublicacion = original.TipoPublicacion,
                    TamanoBytes = original.TamanoBytes,
                    Descripcion = original.Descripcion,
                    Hashtags = original.Hashtags,
                    Estado = EstadoMediaBiblioteca.Pendiente,
                    TipoLado = original.TipoLado,
                    SoloSuscriptores = original.SoloSuscriptores,
                    PrecioLadoCoins = original.PrecioLadoCoins,
                    FechaSubida = DateTime.Now
                };

                _context.MediaBiblioteca.Add(copia);
                await _context.SaveChangesAsync();

                return Json(new { success = true, mediaId = copia.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al duplicar media {MediaId}", mediaId);
                return Json(new { success = false, message = "Error al duplicar" });
            }
        }

        // ========================================
        // ACCIONES EN LOTE
        // ========================================

        [HttpPost("CambiarTipoPublicacionLote")]
        public async Task<IActionResult> CambiarTipoPublicacionLote([FromBody] CambiarTipoLoteRequest request)
        {
            if (request?.Ids == null || !request.Ids.Any())
                return Json(new { success = false, message = "No se seleccionaron elementos" });

            var medios = await _context.MediaBiblioteca
                .Where(m => request.Ids.Contains(m.Id) && m.Estado == EstadoMediaBiblioteca.Pendiente)
                .ToListAsync();

            foreach (var media in medios)
            {
                media.TipoPublicacion = request.TipoPublicacion;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, actualizados = medios.Count });
        }

        public class CambiarTipoLoteRequest
        {
            public List<int> Ids { get; set; } = new();
            public TipoPublicacionMedia TipoPublicacion { get; set; }
        }

        // ========================================
        // QUITAR MARCA DE ADMINISTRADO
        // ========================================

        [HttpPost("QuitarMarcaAdministrado")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarMarcaAdministrado(string id)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
                return Json(new { success = false, message = "Usuario no encontrado" });

            usuario.EsUsuarioAdministrado = false;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Usuario ya no es administrado" });
        }

        // ========================================
        // SEGUIR/DEJAR DE SEGUIR (Simplificado - solo actualiza contador)
        // Nota: No hay tabla relacional de seguidores, solo contadores
        // ========================================

        [HttpPost("IncrementarSeguidores")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IncrementarSeguidores(string id, int cantidad = 1)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
                return Json(new { success = false, message = "Usuario no encontrado" });

            usuario.NumeroSeguidores = Math.Max(0, usuario.NumeroSeguidores + cantidad);
            await _context.SaveChangesAsync();

            return Json(new { success = true, nuevoTotal = usuario.NumeroSeguidores });
        }

        [HttpPost("EstablecerSeguidores")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EstablecerSeguidores(string id, int cantidad)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
                return Json(new { success = false, message = "Usuario no encontrado" });

            usuario.NumeroSeguidores = Math.Max(0, cantidad);
            await _context.SaveChangesAsync();

            return Json(new { success = true, nuevoTotal = usuario.NumeroSeguidores });
        }

        [HttpGet("BuscarUsuarioParaSeguir")]
        public async Task<IActionResult> BuscarUsuarioParaSeguir(string q, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Json(new { success = true, usuarios = new List<object>() });

            var usuarios = await _context.Users
                .Where(u => u.Id != excludeId &&
                           (u.UserName!.Contains(q) || u.NombreCompleto.Contains(q)))
                .Take(10)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.NombreCompleto,
                    u.FotoPerfil,
                    u.EsCreador,
                    u.EsVerificado
                })
                .ToListAsync();

            return Json(new { success = true, usuarios });
        }

        // ========================================
        // HISTORIAL DE ERRORES
        // ========================================

        [HttpGet("HistorialErrores/{id}")]
        public async Task<IActionResult> ObtenerHistorialErrores(string id)
        {
            var errores = await _context.MediaBiblioteca
                .Where(m => m.UsuarioId == id && m.Estado == EstadoMediaBiblioteca.Error)
                .OrderByDescending(m => m.FechaSubida)
                .Select(m => new
                {
                    m.Id,
                    m.NombreOriginal,
                    m.RutaArchivo,
                    m.TipoMedia,
                    m.MensajeError,
                    m.IntentosPublicacion,
                    m.FechaSubida,
                    m.FechaProgramada
                })
                .ToListAsync();

            return Json(new { success = true, errores });
        }

        [HttpPost("ReintentarPublicacion")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReintentarPublicacion(int mediaId)
        {
            var media = await _context.MediaBiblioteca.FindAsync(mediaId);
            if (media == null)
                return Json(new { success = false, message = "Media no encontrado" });

            media.Estado = EstadoMediaBiblioteca.Pendiente;
            media.IntentosPublicacion = 0;
            media.MensajeError = null;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // ========================================
        // BIBLIOTECA DE MEDIOS
        // ========================================

        [HttpGet("Biblioteca/{id}")]
        public async Task<IActionResult> ObtenerBiblioteca(string id, EstadoMediaBiblioteca? estado = null, TipoMediaBiblioteca? tipoMedia = null, int page = 1, int pageSize = 50)
        {
            var query = _context.MediaBiblioteca
                .Where(m => m.UsuarioId == id);

            if (estado.HasValue)
            {
                query = query.Where(m => m.Estado == estado.Value);
            }

            if (tipoMedia.HasValue)
            {
                query = query.Where(m => m.TipoMedia == tipoMedia.Value);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(m => m.Orden)
                .ThenByDescending(m => m.FechaSubida)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    id = m.Id,
                    rutaArchivo = m.RutaArchivo,
                    nombreOriginal = m.NombreOriginal,
                    tipoMedia = (int)m.TipoMedia,
                    tipoPublicacion = (int)m.TipoPublicacion,
                    descripcion = m.Descripcion,
                    hashtags = m.Hashtags,
                    estado = (int)m.Estado,
                    fechaSubida = m.FechaSubida,
                    fechaProgramada = m.FechaProgramada,
                    fechaPublicado = m.FechaPublicado,
                    tipoLado = (int)m.TipoLado,
                    soloSuscriptores = m.SoloSuscriptores,
                    orden = m.Orden,
                    tamanoBytes = m.TamanoBytes,
                    duracionSegundos = m.DuracionSegundos
                })
                .ToListAsync();

            return Json(new { success = true, items, total, page, pageSize });
        }

        [HttpPost("SubirMedia")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 524288000)] // 500MB
        public async Task<IActionResult> SubirMedia(string id, List<IFormFile> archivos, string? descripcionDefault = null, TipoLado tipoLado = TipoLado.LadoA, TipoPublicacionMedia tipoPublicacion = TipoPublicacionMedia.Contenido)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no encontrado" });
            }

            if (archivos == null || !archivos.Any())
            {
                return Json(new { success = false, message = "No se recibieron archivos" });
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "biblioteca", id);
            Directory.CreateDirectory(uploadsFolder);

            var maxOrden = await _context.MediaBiblioteca
                .Where(m => m.UsuarioId == id)
                .MaxAsync(m => (int?)m.Orden) ?? 0;

            var subidos = new List<object>();
            var errores = new List<string>();

            foreach (var archivo in archivos)
            {
                try
                {
                    var extension = Path.GetExtension(archivo.FileName).ToLower();
                    var esVideo = _mediaConversionService.EsVideoSoportado(extension);
                    var esImagen = _mediaConversionService.EsImagenSoportada(extension);

                    if (!esVideo && !esImagen)
                    {
                        errores.Add($"{archivo.FileName}: Formato no soportado");
                        continue;
                    }

                    var nombreBase = Guid.NewGuid().ToString();
                    string rutaFinal;
                    long tamanoFinal;
                    TipoMediaBiblioteca tipoMedia;

                    // Procesar archivo usando el servicio de conversión
                    using var stream = archivo.OpenReadStream();
                    var resultado = await _mediaConversionService.ProcesarArchivoAsync(stream, extension, uploadsFolder, nombreBase);

                    if (!resultado.Exitoso || string.IsNullOrEmpty(resultado.RutaArchivo))
                    {
                        _logger.LogWarning("[SubirMedia] Error procesando {Archivo}: {Error}", archivo.FileName, resultado.Error);
                        errores.Add($"{archivo.FileName}: {resultado.Error ?? "Error desconocido en conversión"}");
                        continue;
                    }

                    rutaFinal = resultado.RutaArchivo;
                    tamanoFinal = resultado.TamanoFinal > 0 ? resultado.TamanoFinal : new FileInfo(rutaFinal).Length;
                    tipoMedia = resultado.TipoMedia == TipoMediaProcesado.Video ? TipoMediaBiblioteca.Video : TipoMediaBiblioteca.Imagen;

                    // Convertir ruta física a ruta relativa web
                    var rutaRelativa = "/" + rutaFinal.Substring(_environment.WebRootPath.Length).Replace('\\', '/').TrimStart('/');

                    _logger.LogInformation("[SubirMedia] Archivo procesado: {Original} -> {Final} ({TamanoOriginal}KB -> {TamanoFinal}KB)",
                        archivo.FileName, Path.GetFileName(rutaFinal), archivo.Length / 1024, tamanoFinal / 1024);

                    maxOrden++;
                    var media = new MediaBiblioteca
                    {
                        UsuarioId = id,
                        RutaArchivo = rutaRelativa,
                        NombreOriginal = archivo.FileName,
                        TipoMedia = tipoMedia,
                        TipoPublicacion = tipoPublicacion,
                        TamanoBytes = tamanoFinal,
                        Descripcion = descripcionDefault,
                        Estado = EstadoMediaBiblioteca.Pendiente,
                        FechaSubida = DateTime.Now,
                        TipoLado = tipoLado,
                        Orden = maxOrden
                    };

                    _context.MediaBiblioteca.Add(media);
                    await _context.SaveChangesAsync();

                    subidos.Add(new
                    {
                        media.Id,
                        media.RutaArchivo,
                        media.NombreOriginal,
                        media.TipoMedia,
                        media.Orden
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al subir archivo {FileName}", archivo.FileName);
                    errores.Add($"{archivo.FileName}: {ex.Message}");
                }
            }

            return Json(new
            {
                success = true,
                subidos = subidos.Count,
                errores = errores.Count,
                items = subidos,
                mensajesError = errores
            });
        }

        [HttpPost("ActualizarMedia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarMedia(int id, string? descripcion, string? hashtags,
            TipoLado tipoLado, bool soloSuscriptores, int? precioLadoCoins, int orden,
            TipoPublicacionMedia tipoPublicacion = TipoPublicacionMedia.Contenido)
        {
            var media = await _context.MediaBiblioteca.FindAsync(id);
            if (media == null)
            {
                return Json(new { success = false, message = "Medio no encontrado" });
            }

            media.Descripcion = descripcion;
            media.Hashtags = hashtags;
            media.TipoLado = tipoLado;
            media.SoloSuscriptores = soloSuscriptores;
            media.PrecioLadoCoins = precioLadoCoins;
            media.Orden = orden;
            media.TipoPublicacion = tipoPublicacion;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost("EliminarMedia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarMedia(int id)
        {
            var media = await _context.MediaBiblioteca.FindAsync(id);
            if (media == null)
            {
                return Json(new { success = false, message = "Medio no encontrado" });
            }

            // Eliminar archivo físico
            var filePath = Path.Combine(_environment.WebRootPath, media.RutaArchivo.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.MediaBiblioteca.Remove(media);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost("EliminarMediaMultiple")]
        public async Task<IActionResult> EliminarMediaMultiple([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No se recibieron IDs" });

            // Procesar en lotes de 100 para evitar problemas de memoria
            var eliminados = 0;
            var errores = 0;

            foreach (var lote in ids.Chunk(100))
            {
                var medios = await _context.MediaBiblioteca
                    .Where(m => lote.Contains(m.Id))
                    .ToListAsync();

                foreach (var media in medios)
                {
                    try
                    {
                        var filePath = Path.Combine(_environment.WebRootPath, media.RutaArchivo.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                            System.IO.File.Delete(filePath);
                    }
                    catch { errores++; }
                }

                _context.MediaBiblioteca.RemoveRange(medios);
                await _context.SaveChangesAsync();
                eliminados += medios.Count;
            }

            return Json(new { success = true, eliminados, errores });
        }

        /// <summary>
        /// Eliminar TODA la biblioteca de un usuario (archivos y registros BD)
        /// </summary>
        [HttpPost("EliminarTodaBiblioteca")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarTodaBiblioteca(string id)
        {
            if (string.IsNullOrEmpty(id))
                return Json(new { success = false, message = "ID de usuario requerido" });

            var eliminados = 0;
            var errores = 0;

            // Obtener todos los medios del usuario
            var totalMedias = await _context.MediaBiblioteca
                .Where(m => m.UsuarioId == id)
                .CountAsync();

            // Procesar en lotes de 100 para evitar timeout
            var procesados = 0;
            while (procesados < totalMedias)
            {
                var medios = await _context.MediaBiblioteca
                    .Where(m => m.UsuarioId == id)
                    .Take(100)
                    .ToListAsync();

                if (!medios.Any()) break;

                foreach (var media in medios)
                {
                    try
                    {
                        var filePath = Path.Combine(_environment.WebRootPath, media.RutaArchivo.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                            System.IO.File.Delete(filePath);
                        eliminados++;
                    }
                    catch { errores++; }
                }

                _context.MediaBiblioteca.RemoveRange(medios);
                await _context.SaveChangesAsync();
                procesados += medios.Count;
            }

            // Eliminar carpeta si está vacía
            var carpetaBiblioteca = Path.Combine(_environment.WebRootPath, "uploads", "biblioteca", id);
            try
            {
                if (Directory.Exists(carpetaBiblioteca) && !Directory.EnumerateFileSystemEntries(carpetaBiblioteca).Any())
                    Directory.Delete(carpetaBiblioteca);
            }
            catch { }

            _logger.LogInformation("[EliminarTodaBiblioteca] Usuario {Id}: {Eliminados} archivos eliminados, {Errores} errores", id, eliminados, errores);

            return Json(new { success = true, eliminados, errores, message = $"Se eliminaron {eliminados} archivos" });
        }

        // ========================================
        // PROGRAMACIÓN DE PUBLICACIONES
        // ========================================

        [HttpPost("GuardarConfiguracion")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarConfiguracion(string usuarioId, bool activo,
            int publicacionesMinPorDia, int publicacionesMaxPorDia,
            TimeSpan horaInicio, TimeSpan horaFin,
            bool publicarFinesDeSemana, int variacionMinutos,
            TipoLado tipoLadoDefault, bool soloSuscriptoresDefault,
            int storiesMinPorDia = 0, int storiesMaxPorDia = 2)
        {
            var config = await _context.ConfiguracionesPublicacionAutomatica
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

            if (config == null)
            {
                config = new ConfiguracionPublicacionAutomatica
                {
                    UsuarioId = usuarioId,
                    FechaCreacion = DateTime.Now
                };
                _context.ConfiguracionesPublicacionAutomatica.Add(config);
            }

            config.Activo = activo;
            config.PublicacionesMinPorDia = publicacionesMinPorDia;
            config.PublicacionesMaxPorDia = publicacionesMaxPorDia;
            config.StoriesMinPorDia = storiesMinPorDia;
            config.StoriesMaxPorDia = storiesMaxPorDia;
            config.HoraInicio = horaInicio;
            config.HoraFin = horaFin;
            config.PublicarFinesDeSemana = publicarFinesDeSemana;
            config.VariacionMinutos = variacionMinutos;
            config.TipoLadoDefault = tipoLadoDefault;
            config.SoloSuscriptoresDefault = soloSuscriptoresDefault;
            config.FechaModificacion = DateTime.Now;

            await _context.SaveChangesAsync();

            // Si se activó, programar el contenido pendiente
            if (activo)
            {
                await ProgramarContenidoPendiente(usuarioId, config);
            }

            return Json(new { success = true });
        }

        [HttpPost("ProgramarContenido")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProgramarContenido(string usuarioId)
        {
            var config = await _context.ConfiguracionesPublicacionAutomatica
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

            if (config == null)
            {
                return Json(new { success = false, message = "No hay configuración de publicación" });
            }

            var programados = await ProgramarContenidoPendiente(usuarioId, config);

            return Json(new { success = true, programados, message = $"Se programaron {programados} publicaciones" });
        }

        private async Task<int> ProgramarContenidoPendiente(string usuarioId, ConfiguracionPublicacionAutomatica config)
        {
            var pendientes = await _context.MediaBiblioteca
                .Where(m => m.UsuarioId == usuarioId && m.Estado == EstadoMediaBiblioteca.Pendiente)
                .OrderBy(m => m.Orden)
                .ToListAsync();

            if (!pendientes.Any()) return 0;

            // Separar Feed (Contenido) y Stories
            var feedPendientes = pendientes.Where(m => m.TipoPublicacion == TipoPublicacionMedia.Contenido).ToList();
            var storiesPendientes = pendientes.Where(m => m.TipoPublicacion == TipoPublicacionMedia.Story).ToList();

            var random = new Random();
            var totalProgramados = 0;

            // Programar Feed posts
            if (feedPendientes.Any() && config.PublicacionesMaxPorDia > 0)
            {
                totalProgramados += ProgramarLista(feedPendientes, config, random,
                    config.PublicacionesMinPorDia, config.PublicacionesMaxPorDia);
            }

            // Programar Stories (con cuota independiente)
            if (storiesPendientes.Any() && config.StoriesMaxPorDia > 0)
            {
                totalProgramados += ProgramarLista(storiesPendientes, config, random,
                    config.StoriesMinPorDia, config.StoriesMaxPorDia);
            }

            await _context.SaveChangesAsync();

            return totalProgramados;
        }

        /// <summary>
        /// Programa una lista de medios con los límites especificados
        /// </summary>
        private int ProgramarLista(List<MediaBiblioteca> medios, ConfiguracionPublicacionAutomatica config,
            Random random, int minPorDia, int maxPorDia)
        {
            if (!medios.Any() || maxPorDia <= 0) return 0;

            var fechaActual = DateTime.Now;

            // Si es fuera de horario, empezar mañana
            if (fechaActual.TimeOfDay < config.HoraInicio || fechaActual.TimeOfDay > config.HoraFin)
            {
                fechaActual = fechaActual.Date.AddDays(1).Add(config.HoraInicio);
            }

            var publicacionesPorDia = random.Next(minPorDia, maxPorDia + 1);
            var publicacionesHoy = 0;
            var diaActual = fechaActual.Date;

            foreach (var media in medios)
            {
                // Saltar fines de semana si no está permitido
                while (!config.PublicarFinesDeSemana &&
                       (diaActual.DayOfWeek == DayOfWeek.Saturday || diaActual.DayOfWeek == DayOfWeek.Sunday))
                {
                    diaActual = diaActual.AddDays(1);
                    publicacionesHoy = 0;
                    publicacionesPorDia = random.Next(minPorDia, maxPorDia + 1);
                }

                // Calcular hora aleatoria dentro del rango
                var rangoMinutos = (int)(config.HoraFin - config.HoraInicio).TotalMinutes;
                var minutosAleatorios = random.Next(0, rangoMinutos);
                var horaPublicacion = config.HoraInicio.Add(TimeSpan.FromMinutes(minutosAleatorios));

                // Añadir variación
                var variacion = random.Next(-config.VariacionMinutos, config.VariacionMinutos + 1);
                horaPublicacion = horaPublicacion.Add(TimeSpan.FromMinutes(variacion));

                // Asegurar que esté dentro del rango
                if (horaPublicacion < config.HoraInicio) horaPublicacion = config.HoraInicio;
                if (horaPublicacion > config.HoraFin) horaPublicacion = config.HoraFin;

                media.FechaProgramada = diaActual.Add(horaPublicacion);
                media.Estado = EstadoMediaBiblioteca.Programado;

                publicacionesHoy++;

                // Si alcanzamos el límite diario, pasar al siguiente día
                if (publicacionesHoy >= publicacionesPorDia)
                {
                    diaActual = diaActual.AddDays(1);
                    publicacionesHoy = 0;
                    publicacionesPorDia = random.Next(minPorDia, maxPorDia + 1);
                }
            }

            return medios.Count;
        }

        [HttpPost("DesprogramarTodo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesprogramarTodo(string usuarioId)
        {
            var programados = await _context.MediaBiblioteca
                .Where(m => m.UsuarioId == usuarioId && m.Estado == EstadoMediaBiblioteca.Programado)
                .ToListAsync();

            foreach (var media in programados)
            {
                media.Estado = EstadoMediaBiblioteca.Pendiente;
                media.FechaProgramada = null;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, desprogramados = programados.Count });
        }

        /// <summary>
        /// Programar múltiples elementos con calendario elaborado
        /// </summary>
        [HttpPost("ProgramarMultiple")]
        public async Task<IActionResult> ProgramarMultiple([FromBody] ProgramarMultipleRequest request)
        {
            if (request.Ids == null || !request.Ids.Any())
                return Json(new { success = false, message = "No se seleccionaron elementos" });

            // Si se pidió orden aleatorio, mezclar los IDs primero
            var idsOrdenados = request.Ids.ToList();
            if (request.OrdenAleatorio)
            {
                var random = new Random();
                idsOrdenados = idsOrdenados.OrderBy(_ => random.Next()).ToList();
            }

            var mediosQuery = await _context.MediaBiblioteca
                .Where(m => request.Ids.Contains(m.Id))
                .ToListAsync();

            // Ordenar según los IDs (ya sea aleatorio o por orden original)
            var medios = request.OrdenAleatorio
                ? idsOrdenados.Select(id => mediosQuery.First(m => m.Id == id)).ToList()
                : mediosQuery.OrderBy(m => m.Orden).ToList();

            if (!medios.Any())
                return Json(new { success = false, message = "No se encontraron los elementos" });

            var fechaInicio = request.FechaInicio;
            var horaInicio = TimeSpan.FromHours(request.HoraInicio);
            var horaFin = TimeSpan.FromHours(request.HoraFin);
            var minutosDisponibles = (int)(horaFin - horaInicio).TotalMinutes;

            var programados = 0;

            if (request.DistribuirEnDias)
            {
                // Distribuir en varios días
                var pubsPorDia = Math.Max(1, request.PublicacionesPorDia);
                var intervaloMinutos = Math.Max(15, request.IntervaloMinutos);
                var diaActual = fechaInicio.Date;
                var pubsHoy = 0;
                var minutosUsadosHoy = 0;

                foreach (var media in medios)
                {
                    // Nueva hora para esta publicación
                    var horaPublicacion = horaInicio.Add(TimeSpan.FromMinutes(minutosUsadosHoy));

                    // Si excede la hora fin, pasar al siguiente día
                    if (horaPublicacion > horaFin || pubsHoy >= pubsPorDia)
                    {
                        diaActual = diaActual.AddDays(1);
                        pubsHoy = 0;
                        minutosUsadosHoy = 0;
                        horaPublicacion = horaInicio;
                    }

                    media.FechaProgramada = diaActual.Add(horaPublicacion);
                    media.Estado = EstadoMediaBiblioteca.Programado;

                    pubsHoy++;
                    minutosUsadosHoy += intervaloMinutos;
                    programados++;
                }
            }
            else
            {
                // Todo el mismo día
                var intervalo = medios.Count > 1 ? minutosDisponibles / (medios.Count - 1) : 0;
                var diaActual = fechaInicio.Date;

                for (int i = 0; i < medios.Count; i++)
                {
                    var minutosOffset = i * intervalo;
                    var horaPublicacion = horaInicio.Add(TimeSpan.FromMinutes(minutosOffset));

                    // Si excede la hora fin, ajustar
                    if (horaPublicacion > horaFin) horaPublicacion = horaFin;

                    medios[i].FechaProgramada = diaActual.Add(horaPublicacion);
                    medios[i].Estado = EstadoMediaBiblioteca.Programado;
                    programados++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("[ProgramarMultiple] {Programados} medios programados desde {Fecha}", programados, fechaInicio);

            return Json(new { success = true, programados });
        }

        public class ProgramarMultipleRequest
        {
            public List<int> Ids { get; set; } = new();
            public DateTime FechaInicio { get; set; }
            public int HoraInicio { get; set; } = 9;
            public int HoraFin { get; set; } = 22;
            public bool DistribuirEnDias { get; set; }
            public int PublicacionesPorDia { get; set; } = 3;
            public int IntervaloMinutos { get; set; } = 60;
            public bool OrdenAleatorio { get; set; }
        }

        // ========================================
        // PUBLICAR MANUALMENTE
        // ========================================

        [HttpPost("PublicarAhora")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublicarAhora(int mediaId)
        {
            var media = await _context.MediaBiblioteca
                .Include(m => m.Usuario)
                .FirstOrDefaultAsync(m => m.Id == mediaId);

            if (media == null)
            {
                return Json(new { success = false, message = "Medio no encontrado" });
            }

            try
            {
                media.Estado = EstadoMediaBiblioteca.Publicado;
                media.FechaPublicado = DateTime.Now;

                if (media.TipoPublicacion == TipoPublicacionMedia.Story)
                {
                    var story = await CrearStoryDesdeMedia(media);
                    media.StoryPublicadoId = story.Id;
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, storyId = story.Id, tipo = "story" });
                }
                else
                {
                    var contenido = await CrearContenidoDesdeMedia(media);
                    media.ContenidoPublicadoId = contenido.Id;
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, contenidoId = contenido.Id, tipo = "contenido" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al publicar media {MediaId}", mediaId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("PublicarMultiple")]
        public async Task<IActionResult> PublicarMultiple([FromBody] List<int> ids)
        {
            var publicados = 0;
            var errores = new List<string>();

            foreach (var id in ids)
            {
                var media = await _context.MediaBiblioteca
                    .Include(m => m.Usuario)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (media == null) continue;

                try
                {
                    media.Estado = EstadoMediaBiblioteca.Publicado;
                    media.FechaPublicado = DateTime.Now;

                    if (media.TipoPublicacion == TipoPublicacionMedia.Story)
                    {
                        var story = await CrearStoryDesdeMedia(media);
                        media.StoryPublicadoId = story.Id;
                    }
                    else
                    {
                        var contenido = await CrearContenidoDesdeMedia(media);
                        media.ContenidoPublicadoId = contenido.Id;
                    }

                    publicados++;
                }
                catch (Exception ex)
                {
                    errores.Add($"ID {id}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, publicados, errores });
        }

        private async Task<Contenido> CrearContenidoDesdeMedia(MediaBiblioteca media)
        {
            var contenido = new Contenido
            {
                UsuarioId = media.UsuarioId,
                Descripcion = media.Descripcion ?? "",
                TipoContenido = media.TipoMedia == TipoMediaBiblioteca.Video ? TipoContenido.Video : TipoContenido.Imagen,
                FechaPublicacion = DateTime.Now,
                EstaActivo = true,
                TipoLado = media.TipoLado,
                SoloSuscriptores = media.SoloSuscriptores,
                PrecioDesbloqueo = media.PrecioLadoCoins ?? 0
            };

            _context.Contenidos.Add(contenido);
            await _context.SaveChangesAsync();

            // Crear archivo de contenido
            var archivo = new ArchivoContenido
            {
                ContenidoId = contenido.Id,
                RutaArchivo = media.RutaArchivo,
                TipoArchivo = media.TipoMedia == TipoMediaBiblioteca.Video ? TipoArchivo.Video : TipoArchivo.Foto,
                Orden = 0,
                FechaCreacion = DateTime.Now
            };

            _context.ArchivosContenido.Add(archivo);
            await _context.SaveChangesAsync();

            return contenido;
        }

        private async Task<Story> CrearStoryDesdeMedia(MediaBiblioteca media)
        {
            var ahora = DateTime.Now;

            var story = new Story
            {
                CreadorId = media.UsuarioId,
                RutaArchivo = media.RutaArchivo,
                TipoContenido = media.TipoMedia == TipoMediaBiblioteca.Video ? TipoContenido.Video : TipoContenido.Imagen,
                FechaPublicacion = ahora,
                FechaExpiracion = ahora.AddHours(24), // Expira en 24 horas
                EstaActivo = true,
                TipoLado = media.TipoLado,
                Texto = media.Descripcion
            };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync();

            return story;
        }

        // ========================================
        // CONTENIDO PUBLICADO
        // ========================================

        [HttpGet("Contenido/{id}")]
        public async Task<IActionResult> ObtenerContenido(string id, int page = 1, int pageSize = 20)
        {
            var query = _context.Contenidos
                .Include(c => c.Archivos)
                .Where(c => c.UsuarioId == id && c.EstaActivo)
                .OrderByDescending(c => c.FechaPublicacion);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    id = c.Id,
                    descripcion = c.Descripcion,
                    tipoContenido = (int)c.TipoContenido,
                    fechaPublicacion = c.FechaPublicacion,
                    estaActivo = c.EstaActivo,
                    tipoLado = c.TipoLado,
                    numeroLikes = c.NumeroLikes,
                    numeroComentarios = c.NumeroComentarios,
                    esVideo = c.TipoContenido == TipoContenido.Video,
                    // Para videos: usar RutaArchivo directamente; para imágenes: thumbnail o primer archivo
                    rutaMedia = c.TipoContenido == TipoContenido.Video
                        ? (c.Archivos.OrderBy(a => a.Orden).Select(a => a.RutaArchivo).FirstOrDefault() ?? c.RutaArchivo)
                        : (c.Thumbnail ?? c.Archivos.OrderBy(a => a.Orden).Select(a => a.RutaArchivo).FirstOrDefault() ?? c.RutaArchivo),
                    thumbnail = c.Thumbnail ?? c.Archivos.OrderBy(a => a.Orden).Select(a => a.RutaArchivo).FirstOrDefault() ?? c.RutaArchivo
                })
                .ToListAsync();

            return Json(new { success = true, items, total, page, pageSize });
        }

        [HttpPost("EliminarContenido")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarContenido(int id)
        {
            var contenido = await _context.Contenidos
                .Include(c => c.Archivos)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contenido == null)
            {
                return Json(new { success = false, message = "Contenido no encontrado" });
            }

            // Marcar como inactivo en lugar de eliminar
            contenido.EstaActivo = false;

            // Actualizar media biblioteca si existe
            var media = await _context.MediaBiblioteca
                .FirstOrDefaultAsync(m => m.ContenidoPublicadoId == id);
            if (media != null)
            {
                media.Estado = EstadoMediaBiblioteca.Cancelado;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // ========================================
        // MENSAJES
        // ========================================

        [HttpGet("Conversaciones/{id}")]
        public async Task<IActionResult> ObtenerConversaciones(string id)
        {
            var conversaciones = await _context.MensajesPrivados
                .Where(m => m.RemitenteId == id || m.DestinatarioId == id)
                .GroupBy(m => m.RemitenteId == id ? m.DestinatarioId : m.RemitenteId)
                .Select(g => new
                {
                    UsuarioId = g.Key,
                    UltimoMensaje = g.OrderByDescending(m => m.FechaEnvio).First(),
                    NoLeidos = g.Count(m => m.DestinatarioId == id && !m.Leido)
                })
                .ToListAsync();

            var usuarioIds = conversaciones.Select(c => c.UsuarioId).ToList();
            var usuarios = await _context.Users
                .Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var resultado = conversaciones.Select(c => new
            {
                c.UsuarioId,
                Usuario = usuarios.TryGetValue(c.UsuarioId!, out var u) ? new
                {
                    u.UserName,
                    u.NombreCompleto,
                    u.FotoPerfil
                } : null,
                UltimoMensaje = new
                {
                    c.UltimoMensaje.Contenido,
                    c.UltimoMensaje.FechaEnvio,
                    EsEnviado = c.UltimoMensaje.RemitenteId == id
                },
                c.NoLeidos
            }).OrderByDescending(c => c.UltimoMensaje.FechaEnvio);

            return Json(new { success = true, conversaciones = resultado });
        }

        [HttpGet("Mensajes/{usuarioId}/{otroUsuarioId}")]
        public async Task<IActionResult> ObtenerMensajes(string usuarioId, string otroUsuarioId, int page = 1, int pageSize = 50)
        {
            var mensajes = await _context.MensajesPrivados
                .Where(m => (m.RemitenteId == usuarioId && m.DestinatarioId == otroUsuarioId) ||
                           (m.RemitenteId == otroUsuarioId && m.DestinatarioId == usuarioId))
                .OrderByDescending(m => m.FechaEnvio)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.Contenido,
                    m.FechaEnvio,
                    m.Leido,
                    EsEnviado = m.RemitenteId == usuarioId
                })
                .ToListAsync();

            return Json(new { success = true, mensajes = mensajes.OrderBy(m => m.FechaEnvio) });
        }

        [HttpPost("EnviarMensaje")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensaje(string remitenteId, string destinatarioId, string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido))
            {
                return Json(new { success = false, message = "El mensaje no puede estar vacío" });
            }

            var mensaje = new MensajePrivado
            {
                RemitenteId = remitenteId,
                DestinatarioId = destinatarioId,
                Contenido = contenido.Trim(),
                FechaEnvio = DateTime.Now,
                Leido = false
            };

            _context.MensajesPrivados.Add(mensaje);
            await _context.SaveChangesAsync();

            return Json(new { success = true, mensajeId = mensaje.Id });
        }

        // ========================================
        // MARCAR COMO ADMINISTRADO
        // ========================================

        [HttpPost("MarcarComoAdministrado")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarComoAdministrado(string id, bool administrado)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no encontrado" });
            }

            usuario.EsUsuarioAdministrado = administrado;
            await _context.SaveChangesAsync();

            // Crear configuración si no existe
            if (administrado)
            {
                var config = await _context.ConfiguracionesPublicacionAutomatica
                    .FirstOrDefaultAsync(c => c.UsuarioId == id);

                if (config == null)
                {
                    config = new ConfiguracionPublicacionAutomatica
                    {
                        UsuarioId = id,
                        Activo = false,
                        FechaCreacion = DateTime.Now,
                        FechaModificacion = DateTime.Now
                    };
                    _context.ConfiguracionesPublicacionAutomatica.Add(config);
                    await _context.SaveChangesAsync();
                }
            }

            return Json(new { success = true });
        }

        // ========================================
        // BUSCAR USUARIO EXISTENTE
        // ========================================

        [HttpGet("BuscarUsuario")]
        public async Task<IActionResult> BuscarUsuario(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Json(new { success = true, usuarios = new List<object>() });
            }

            var usuarios = await _context.Users
                .Where(u => !u.EsUsuarioAdministrado &&
                           (u.UserName!.Contains(q) || u.NombreCompleto!.Contains(q)))
                .Take(10)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.NombreCompleto,
                    u.FotoPerfil,
                    u.EsCreador
                })
                .ToListAsync();

            return Json(new { success = true, usuarios });
        }

        // ========================================
        // REORDENAR BIBLIOTECA
        // ========================================

        [HttpPost("ReordenarBiblioteca")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReordenarBiblioteca([FromBody] List<ReordenItem> items)
        {
            foreach (var item in items)
            {
                var media = await _context.MediaBiblioteca.FindAsync(item.Id);
                if (media != null)
                {
                    media.Orden = item.Orden;
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public class ReordenItem
        {
            public int Id { get; set; }
            public int Orden { get; set; }
        }
    }
}
