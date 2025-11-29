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

            _logger.LogInformation("GET Crear - Usuario: {Username}, Verificado: {Verificado}",
                user.UserName, user.CreadorVerificado);

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

                // Validaciones básicas
                if (!EsBorrador && string.IsNullOrWhiteSpace(Descripcion))
                {
                    TempData["Error"] = "La descripción es requerida para publicar";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View();
                }

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
                    NumeroVistas = 0
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

                _logger.LogInformation("✅ Contenido guardado - ID: {Id}, TipoLado: {TipoLado}, NombreMostrado: {Nombre}, Precio: {Precio}",
                    contenido.Id, contenido.TipoLado, contenido.NombreMostrado, contenido.PrecioDesbloqueo);

                // ✅ Mensajes de éxito personalizados
                if (EsBorrador)
                {
                    TempData["Success"] = "✅ Borrador guardado exitosamente";
                }
                else
                {
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

                // Validaciones
                if (!contenido.EsBorrador && string.IsNullOrWhiteSpace(Descripcion))
                {
                    TempData["Error"] = "La descripción es requerida para contenido publicado";
                    ViewBag.UsuarioVerificado = usuario.CreadorVerificado;
                    return View(contenido);
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