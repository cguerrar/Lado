using Lado.Data;
using Lado.Models;
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

        public ContenidoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<ContenidoController> logger)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string filtro = "todos")
        {
            var usuario = await _userManager.GetUserAsync(User);

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
                case "todos":
                default:
                    break;
            }

            var contenidos = await query
                .OrderByDescending(c => c.FechaPublicacion)
                .ToListAsync();

            ViewBag.FiltroActual = filtro ?? "todos";
            return View(contenidos);
        }

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

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user.TipoUsuario == 0)
            {
                TempData["Error"] = "Solo los creadores pueden publicar contenido";
                return RedirectToAction("Index", "Dashboard");
            }

            if (!user.CreadorVerificado)
            {
                TempData["Error"] = "Debes completar la verificación de identidad para publicar contenido.";
                return RedirectToAction("Request", "CreatorVerification");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            IFormFile archivo,
            string Descripcion,
            int TipoContenido,
            bool EsPremium,
            decimal PrecioDesbloqueo,
            bool EsBorrador = false)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);

                _logger.LogInformation($"=== CREAR CONTENIDO ===");
                _logger.LogInformation($"Usuario: {usuario?.UserName}");
                _logger.LogInformation($"Archivo recibido: {archivo?.FileName ?? "NULL"}");
                _logger.LogInformation($"Tamaño archivo: {archivo?.Length ?? 0} bytes");
                _logger.LogInformation($"Tipo: {TipoContenido}, EsBorrador: {EsBorrador}");
                _logger.LogInformation($"EsPremium: {EsPremium}, Precio: {PrecioDesbloqueo}");

                if (usuario.TipoUsuario == 0)
                {
                    TempData["Error"] = "Solo los creadores pueden publicar contenido";
                    return RedirectToAction("Index", "Dashboard");
                }

                if (!usuario.CreadorVerificado)
                {
                    TempData["Error"] = "No tienes permisos para publicar contenido. Completa tu verificación de identidad.";
                    return RedirectToAction("Request", "CreatorVerification");
                }

                if (!EsBorrador && string.IsNullOrWhiteSpace(Descripcion))
                {
                    TempData["Error"] = "La descripción es requerida para publicar";
                    return View();
                }

                if (!EsBorrador && TipoContenido != 3 && (archivo == null || archivo.Length == 0))
                {
                    TempData["Error"] = "Debes subir un archivo para este tipo de contenido";
                    return View();
                }

                // VALIDACIÓN MEJORADA: Precio múltiplo de 5
                if (EsPremium && (PrecioDesbloqueo <= 0 || PrecioDesbloqueo % 5 != 0))
                {
                    TempData["Error"] = "El precio debe ser un múltiplo de 5 (5, 10, 15, 20...)";
                    return View();
                }

                var contenido = new Contenido
                {
                    UsuarioId = usuario.Id,
                    TipoContenido = (Models.TipoContenido)TipoContenido,
                    Descripcion = Descripcion ?? "",
                    EsPremium = EsPremium,
                    PrecioDesbloqueo = EsPremium ? PrecioDesbloqueo : 0,
                    EsBorrador = EsBorrador,
                    FechaPublicacion = DateTime.Now,
                    EstaActivo = true,
                    NumeroLikes = 0,
                    NumeroComentarios = 0,
                    NumeroVistas = 0
                };

                if (archivo != null && archivo.Length > 0)
                {
                    _logger.LogInformation("Procesando archivo...");

                    if (archivo.Length > 100 * 1024 * 1024)
                    {
                        TempData["Error"] = "El archivo excede el tamaño máximo de 100 MB";
                        return View();
                    }

                    var extension = Path.GetExtension(archivo.FileName).ToLower();
                    var tiposPermitidos = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov", ".avi", ".webm" };

                    if (!tiposPermitidos.Contains(extension))
                    {
                        TempData["Error"] = "Tipo de archivo no permitido";
                        return View();
                    }

                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", usuario.Id);
                    _logger.LogInformation($"Carpeta de uploads: {uploadsFolder}");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                        _logger.LogInformation("Carpeta creada");
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    _logger.LogInformation($"Guardando en: {filePath}");

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await archivo.CopyToAsync(fileStream);
                    }

                    contenido.RutaArchivo = $"/uploads/{usuario.Id}/{uniqueFileName}";
                    _logger.LogInformation($"Archivo guardado: {contenido.RutaArchivo}");
                }

                _context.Contenidos.Add(contenido);
                var cambiosGuardados = await _context.SaveChangesAsync();

                _logger.LogInformation($"Cambios guardados: {cambiosGuardados}");
                _logger.LogInformation($"Contenido guardado - ID: {contenido.Id}, EsPremium: {contenido.EsPremium}, Precio: {contenido.PrecioDesbloqueo}");

                if (cambiosGuardados > 0)
                {
                    if (EsBorrador)
                    {
                        TempData["Success"] = "Borrador guardado exitosamente";
                    }
                    else
                    {
                        TempData["Success"] = EsPremium
                            ? $"Contenido premium publicado exitosamente (${PrecioDesbloqueo})"
                            : "Contenido publicado exitosamente";
                    }
                }
                else
                {
                    _logger.LogWarning("No se guardaron cambios en la base de datos");
                    TempData["Error"] = "Error al guardar el contenido";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear contenido");
                TempData["Error"] = $"Error al crear contenido: {ex.Message}";
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publicar(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (!usuario.CreadorVerificado)
            {
                TempData["Error"] = "No puedes publicar contenido sin completar tu verificación de identidad.";
                return RedirectToAction("Request", "CreatorVerification");
            }

            var contenido = await _context.Contenidos
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id);

            if (contenido == null)
                return NotFound();

            contenido.EsBorrador = false;
            contenido.FechaPublicacion = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Contenido publicado exitosamente";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            var contenido = await _context.Contenidos
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id && c.EstaActivo);

            if (contenido == null)
            {
                TempData["Error"] = "Contenido no encontrado";
                return RedirectToAction("Index");
            }

            return View(contenido);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int id,
            IFormFile archivo,
            string Descripcion,
            int TipoContenido,
            bool EsPremium,
            decimal PrecioDesbloqueo)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                var contenido = await _context.Contenidos
                    .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id && c.EstaActivo);

                if (contenido == null)
                {
                    TempData["Error"] = "Contenido no encontrado";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation($"=== EDITAR CONTENIDO ID: {id} ===");
                _logger.LogInformation($"EsPremium recibido: {EsPremium}, Precio: {PrecioDesbloqueo}");

                if (!contenido.EsBorrador && string.IsNullOrWhiteSpace(Descripcion))
                {
                    TempData["Error"] = "La descripción es requerida para contenido publicado";
                    return View(contenido);
                }

                // VALIDACIÓN MEJORADA: Precio múltiplo de 5
                if (EsPremium && (PrecioDesbloqueo <= 0 || PrecioDesbloqueo % 5 != 0))
                {
                    TempData["Error"] = "El precio debe ser un múltiplo de 5 (5, 10, 15, 20...)";
                    return View(contenido);
                }

                contenido.TipoContenido = (Models.TipoContenido)TipoContenido;
                contenido.Descripcion = Descripcion ?? "";
                contenido.EsPremium = EsPremium;
                contenido.PrecioDesbloqueo = EsPremium ? PrecioDesbloqueo : 0;
                contenido.FechaActualizacion = DateTime.Now;

                _logger.LogInformation($"Valores a guardar - EsPremium: {contenido.EsPremium}, Precio: {contenido.PrecioDesbloqueo}");

                if (archivo != null && archivo.Length > 0)
                {
                    _logger.LogInformation("Actualizando archivo...");

                    if (archivo.Length > 100 * 1024 * 1024)
                    {
                        TempData["Error"] = "El archivo excede el tamaño máximo de 100 MB";
                        return View(contenido);
                    }

                    var extension = Path.GetExtension(archivo.FileName).ToLower();
                    var tiposPermitidos = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov", ".avi", ".webm" };

                    if (!tiposPermitidos.Contains(extension))
                    {
                        TempData["Error"] = "Tipo de archivo no permitido";
                        return View(contenido);
                    }

                    if (!string.IsNullOrEmpty(contenido.RutaArchivo))
                    {
                        var archivoAnterior = Path.Combine(_environment.WebRootPath, contenido.RutaArchivo.TrimStart('/'));
                        if (System.IO.File.Exists(archivoAnterior))
                        {
                            System.IO.File.Delete(archivoAnterior);
                            _logger.LogInformation($"Archivo anterior eliminado: {archivoAnterior}");
                        }
                    }

                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", usuario.Id);
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await archivo.CopyToAsync(fileStream);
                    }

                    contenido.RutaArchivo = $"/uploads/{usuario.Id}/{uniqueFileName}";
                    _logger.LogInformation($"Nuevo archivo guardado: {contenido.RutaArchivo}");
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Contenido actualizado - EsPremium: {contenido.EsPremium}, Precio: {contenido.PrecioDesbloqueo}");

                TempData["Success"] = EsPremium
                    ? $"Contenido actualizado como premium (${PrecioDesbloqueo})"
                    : "Contenido actualizado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar contenido");
                TempData["Error"] = $"Error al editar contenido: {ex.Message}";
                return RedirectToAction("Editar", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            var contenido = await _context.Contenidos
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id);

            if (contenido == null)
                return NotFound();

            contenido.EstaActivo = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Contenido eliminado";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Like(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            var contenido = await _context.Contenidos.FindAsync(id);

            if (contenido == null)
                return NotFound();

            var likeExistente = await _context.Likes
                .FirstOrDefaultAsync(l => l.ContenidoId == id && l.UsuarioId == usuario.Id);

            if (likeExistente != null)
            {
                _context.Likes.Remove(likeExistente);
                contenido.NumeroLikes--;
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
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, likes = contenido.NumeroLikes });
        }
    }
}