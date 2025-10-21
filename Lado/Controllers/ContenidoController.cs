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

        // ========================================
        // CREAR CONTENIDO
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

            // ✅ Pasar información de verificación a la vista
            ViewBag.UsuarioVerificado = user.CreadorVerificado;

            _logger.LogInformation("GET Crear - Usuario: {Username}, Verificado: {Verificado}",
                user.UserName, user.CreadorVerificado);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            IFormFile archivo,
            string Descripcion,
            int TipoContenido,
            bool EsGratis = true,
            decimal PrecioDesbloqueo = 0,
            bool EsBorrador = false)
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
                _logger.LogInformation("🔴 PARÁMETRO RECIBIDO - EsGratis: {EsGratis}, Precio: {Precio}",
                    EsGratis, PrecioDesbloqueo);

                // ✅ Validar verificación SOLO si es contenido de pago (LadoB)
                if (!EsGratis && !usuario.CreadorVerificado)
                {
                    _logger.LogWarning("❌ Usuario {Username} intentó crear contenido LadoB sin verificación",
                        usuario.UserName);
                    TempData["Error"] = "Para monetizar contenido (LadoB) debes verificar tu identidad primero.";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View();
                }

                // Validaciones
                if (!EsBorrador && string.IsNullOrWhiteSpace(Descripcion))
                {
                    TempData["Error"] = "La descripción es requerida para publicar";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View();
                }

                if (!EsBorrador && TipoContenido != 3 && (archivo == null || archivo.Length == 0))
                {
                    TempData["Error"] = "Debes subir un archivo para este tipo de contenido";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View();
                }

                // Validación de precio múltiplo de 5
                if (!EsGratis && (PrecioDesbloqueo <= 0 || PrecioDesbloqueo % 5 != 0))
                {
                    TempData["Error"] = "El precio debe ser un múltiplo de 5 (5, 10, 15, 20...)";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View();
                }

                // ⭐ Determinar tipo de lado y nombre a mostrar
                var tipoLado = EsGratis ? TipoLado.LadoA : TipoLado.LadoB;
                var nombreMostrado = EsGratis ? usuario.NombreCompleto : usuario.Seudonimo;

                _logger.LogInformation("Tipo de Lado: {TipoLado} (mostrará como: {NombreMostrado})",
                    tipoLado, nombreMostrado);

                var contenido = new Contenido
                {
                    UsuarioId = usuario.Id,
                    TipoContenido = (Models.TipoContenido)TipoContenido,
                    Descripcion = Descripcion ?? "",
                    TipoLado = tipoLado,
                    EsGratis = EsGratis,
                    NombreMostrado = nombreMostrado,
                    EsPremium = !EsGratis,
                    PrecioDesbloqueo = EsGratis ? 0 : PrecioDesbloqueo,
                    EsBorrador = EsBorrador,
                    FechaPublicacion = DateTime.Now,
                    EstaActivo = true,
                    NumeroLikes = 0,
                    NumeroComentarios = 0,
                    NumeroVistas = 0
                };

                // Procesar archivo
                if (archivo != null && archivo.Length > 0)
                {
                    if (archivo.Length > 100 * 1024 * 1024)
                    {
                        TempData["Error"] = "El archivo excede el tamaño máximo de 100 MB";
                        ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                        return View();
                    }

                    var extension = Path.GetExtension(archivo.FileName).ToLower();
                    var tiposPermitidos = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov", ".avi", ".webm" };

                    if (!tiposPermitidos.Contains(extension))
                    {
                        TempData["Error"] = "Tipo de archivo no permitido";
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
                }

                _context.Contenidos.Add(contenido);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Contenido guardado - ID: {Id}, TipoLado: {TipoLado}, NombreMostrado: {Nombre}, Precio: {Precio}",
                    contenido.Id, contenido.TipoLado, contenido.NombreMostrado, contenido.PrecioDesbloqueo);

                if (EsBorrador)
                {
                    TempData["Success"] = "✅ Borrador guardado exitosamente";
                }
                else
                {
                    TempData["Success"] = EsGratis
                        ? $"✅ Contenido público (LadoA) publicado como {usuario.NombreCompleto}"
                        : $"✅ Contenido premium (LadoB) publicado como {usuario.Seudonimo} (${PrecioDesbloqueo})";
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

            // ✅ Verificar si es LadoB y requiere verificación
            if (contenido.TipoLado == TipoLado.LadoB && !usuario.CreadorVerificado)
            {
                TempData["Error"] = "Para publicar contenido premium (LadoB) debes verificar tu identidad.";
                return RedirectToAction("Request", "CreatorVerification");
            }

            contenido.EsBorrador = false;
            contenido.FechaPublicacion = DateTime.Now;
            await _context.SaveChangesAsync();

            var tipoContenido = contenido.TipoLado == TipoLado.LadoA ? "público (LadoA)" : "premium (LadoB)";
            TempData["Success"] = $"✅ Contenido {tipoContenido} publicado exitosamente";

            return RedirectToAction("Index");
        }

        // ========================================
        // EDITAR CONTENIDO
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
                .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuario.Id && c.EstaActivo);

            if (contenido == null)
            {
                TempData["Error"] = "Contenido no encontrado";
                return RedirectToAction("Index");
            }

            // ✅ CRÍTICO: Pasar estado de verificación a la vista
            ViewBag.UsuarioVerificado = usuario.CreadorVerificado;

            _logger.LogInformation("🔵 Editando contenido ID: {Id}, TipoContenido: {TipoContenido}, TipoLado: {TipoLado}, EsGratis: {EsGratis}",
                id, contenido.TipoContenido, contenido.TipoLado, contenido.EsGratis);

            return View(contenido);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int id,
            IFormFile archivo,
            string Descripcion,
            int TipoContenido,
            bool EsGratis,
            decimal? PrecioDesbloqueo)
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

                // ✅ Validar verificación SOLO si intenta publicar en LadoB (no gratis)
                if (!EsGratis && !usuario.CreadorVerificado)
                {
                    TempData["Error"] = "Debes verificar tu identidad para publicar contenido premium (LadoB)";
                    return RedirectToAction("Request", "CreatorVerification");
                }

                if (!contenido.EsBorrador && string.IsNullOrWhiteSpace(Descripcion))
                {
                    TempData["Error"] = "La descripción es requerida para contenido publicado";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View(contenido);
                }

                // ✅ Validar precio SOLO si es contenido de pago (LadoB)
                if (!EsGratis)
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

                // ✅ Actualizar tipo de lado y campos relacionados
                var tipoAnterior = contenido.TipoLado;
                contenido.TipoLado = EsGratis ? TipoLado.LadoA : TipoLado.LadoB;
                contenido.EsGratis = EsGratis;
                contenido.EsPremium = !EsGratis;
                contenido.PrecioDesbloqueo = EsGratis ? 0 : (PrecioDesbloqueo ?? 10);
                contenido.NombreMostrado = EsGratis ? usuario.NombreCompleto : usuario.Seudonimo;

                _logger.LogInformation("Tipo anterior: {TipoAnterior}, Nuevo tipo: {TipoNuevo}",
                    tipoAnterior, contenido.TipoLado);
                _logger.LogInformation("Precio asignado: ${Precio}, Nombre: {Nombre}",
                    contenido.PrecioDesbloqueo, contenido.NombreMostrado);

                // ✅ Subir nuevo archivo si se proporciona
                if (archivo != null && archivo.Length > 0)
                {
                    // Validar tipo de archivo según TipoContenido
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

                    // Eliminar archivo anterior si existe
                    if (!string.IsNullOrEmpty(contenido.RutaArchivo))
                    {
                        var rutaAnterior = Path.Combine(_environment.WebRootPath, contenido.RutaArchivo.TrimStart('/'));
                        if (System.IO.File.Exists(rutaAnterior))
                        {
                            System.IO.File.Delete(rutaAnterior);
                            _logger.LogInformation("Archivo anterior eliminado: {Ruta}", rutaAnterior);
                        }
                    }

                    // Subir nuevo archivo
                    var carpeta = contenido.TipoContenido == Models.TipoContenido.Foto ? "images" : "videos";
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", carpeta);
                    Directory.CreateDirectory(uploadsFolder);

                    var nombreArchivo = $"{Guid.NewGuid()}_{Path.GetFileName(archivo.FileName)}";
                    var rutaCompleta = Path.Combine(uploadsFolder, nombreArchivo);

                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await archivo.CopyToAsync(stream);
                    }

                    contenido.RutaArchivo = $"/uploads/{carpeta}/{nombreArchivo}";
                    _logger.LogInformation("Nuevo archivo guardado: {Ruta}", contenido.RutaArchivo);
                }

                contenido.FechaActualizacion = DateTime.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Contenido ID {Id} actualizado exitosamente", id);

                TempData["Success"] = !EsGratis
                    ? $"Contenido actualizado como premium en LadoB (${PrecioDesbloqueo})"
                    : "Contenido actualizado como gratuito en LadoA";

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
    }
}