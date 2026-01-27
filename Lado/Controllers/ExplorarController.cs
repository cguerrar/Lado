using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    [Authorize]
    public class ExplorarController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ExplorarController> _logger;

        public ExplorarController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ExplorarController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // ========================================
        // INDEX - EXPLORAR USUARIOS
        // ========================================

        public async Task<IActionResult> Index(string buscar = "", string categoria = "todos")
        {
            var usuarioActual = await _userManager.GetUserAsync(User);
            if (usuarioActual == null)
            {
                _logger.LogWarning("Usuario no autenticado en Index");
                return RedirectToAction("Login", "Account");
            }

            _logger.LogInformation("Explorando usuarios - Buscar: {Buscar}, Categoría: {Categoria}",
                buscar, categoria);

            // Obtener usuarios activos (excluyendo al usuario actual)
            var query = _context.Users
                .Where(u => u.Id != usuarioActual.Id && u.EstaActivo);

            // Filtrar por búsqueda
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                query = query.Where(u =>
                    u.UserName.Contains(buscar) ||
                    u.NombreCompleto.Contains(buscar) ||
                    u.Seudonimo.Contains(buscar) ||
                    (u.Biografia != null && u.Biografia.Contains(buscar)));
            }

            // Filtrar por categoría
            if (!string.IsNullOrWhiteSpace(categoria) && categoria != "todos")
            {
                query = query.Where(u => u.Categoria == categoria);
            }

            var usuarios = await query
                .OrderByDescending(u => u.NumeroSeguidores)
                .ThenByDescending(u => u.CreadorVerificado)
                .Take(50)
                .ToListAsync();

            // Crear lista de perfiles (LadoA + LadoB como entidades separadas)
            var perfiles = new List<PerfilExplorar>();

            foreach (var usuario in usuarios)
            {
                // Agregar perfil LadoA (nombre real)
                perfiles.Add(new PerfilExplorar
                {
                    Id = usuario.Id,
                    NombreMostrado = usuario.NombreCompleto,
                    UsernameMostrado = usuario.UserName,
                    FotoPerfil = usuario.FotoPerfil,
                    FotoPortada = usuario.FotoPortada,
                    Biografia = usuario.Biografia,
                    NumeroSeguidores = usuario.NumeroSeguidores,
                    CreadorVerificado = usuario.CreadorVerificado,
                    EsLadoB = false
                });

                // Agregar perfil LadoB (seudónimo) si tiene uno
                if (!string.IsNullOrEmpty(usuario.Seudonimo))
                {
                    perfiles.Add(new PerfilExplorar
                    {
                        Id = usuario.Id,
                        NombreMostrado = usuario.Seudonimo,
                        UsernameMostrado = usuario.Seudonimo.ToLower().Replace(" ", ""),
                        FotoPerfil = usuario.FotoPerfilLadoB ?? usuario.FotoPerfil,
                        FotoPortada = usuario.FotoPortadaLadoB ?? usuario.FotoPortada,
                        Biografia = usuario.BiografiaLadoB ?? usuario.Biografia,
                        NumeroSeguidores = usuario.NumeroSeguidores,
                        CreadorVerificado = usuario.CreadorVerificado,
                        EsLadoB = true
                    });
                }
            }

            _logger.LogInformation("Perfiles encontrados: {Count} (LadoA + LadoB)", perfiles.Count);

            // Obtener suscripciones actuales del usuario (LadoA y LadoB)
            var suscripciones = await _context.Suscripciones
                .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva)
                .Select(s => new { s.CreadorId, s.TipoLado })
                .ToListAsync();

            ViewBag.SuscripcionesLadoA = suscripciones.Where(s => s.TipoLado == TipoLado.LadoA).Select(s => s.CreadorId).ToList();
            ViewBag.SuscripcionesLadoB = suscripciones.Where(s => s.TipoLado == TipoLado.LadoB).Select(s => s.CreadorId).ToList();
            ViewBag.BuscarTexto = buscar;
            ViewBag.Categoria = categoria;
            ViewBag.TotalUsuarios = perfiles.Count;

            return View(perfiles);
        }

        // ========================================
        // PERFIL - VER PERFIL DE USUARIO
        // ========================================

        public async Task<IActionResult> Perfil(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("ID de usuario vacío en Perfil");
                TempData["Error"] = "Usuario no especificado";
                return RedirectToAction("Index");
            }

            // ⭐ CAMBIO: Ya no verificamos TipoUsuario, todos son creadores
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.EstaActivo);

            if (usuario == null)
            {
                _logger.LogWarning("Usuario no encontrado: {Id}", id);
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction("Index");
            }

            var usuarioActual = await _userManager.GetUserAsync(User);
            if (usuarioActual == null)
            {
                _logger.LogWarning("Usuario actual no autenticado");
                return RedirectToAction("Login", "Account");
            }

            _logger.LogInformation("Viendo perfil de: {Username} ({Id})", usuario.UserName, usuario.Id);

            // Verificar si está suscrito
            var suscripcion = await _context.Suscripciones
                .FirstOrDefaultAsync(s =>
                    s.CreadorId == id &&
                    s.FanId == usuarioActual.Id &&
                    s.EstaActiva);

            var estaSuscrito = suscripcion != null;
            ViewBag.EstaSuscrito = estaSuscrito;

            // ⭐ NUEVO: Obtener contenido separado por LadoA/LadoB
            // LadoA: Siempre visible (público)
            var contenidoLadoA = await _context.Contenidos
                .Include(c => c.Likes)
                .Include(c => c.Comentarios)
                .Where(c => c.UsuarioId == id &&
                           c.EstaActivo &&
                           !c.EsBorrador &&
                           c.TipoLado == TipoLado.LadoA)
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(12)
                .ToListAsync();

            // LadoB: Solo si está suscrito
            var contenidoLadoB = estaSuscrito
                ? await _context.Contenidos
                    .Include(c => c.Likes)
                    .Include(c => c.Comentarios)
                    .Where(c => c.UsuarioId == id &&
                               c.EstaActivo &&
                               !c.EsBorrador &&
                               c.TipoLado == TipoLado.LadoB)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(12)
                    .ToListAsync()
                : new List<Contenido>();

            // Combinar ambos tipos de contenido
            var contenidos = contenidoLadoA.Union(contenidoLadoB)
                .OrderByDescending(c => c.FechaPublicacion)
                .ToList();

            // Actualizar contadores desde las relaciones cargadas
            foreach (var contenido in contenidos)
            {
                if (contenido.Likes != null && contenido.NumeroLikes != contenido.Likes.Count)
                {
                    contenido.NumeroLikes = contenido.Likes.Count;
                }
                if (contenido.Comentarios != null && contenido.NumeroComentarios != contenido.Comentarios.Count)
                {
                    contenido.NumeroComentarios = contenido.Comentarios.Count;
                }
            }

            ViewBag.Contenidos = contenidos;
            ViewBag.ContenidoLadoA = contenidoLadoA;
            ViewBag.ContenidoLadoB = contenidoLadoB;

            ViewBag.TotalContenidos = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == id && c.EstaActivo && !c.EsBorrador);

            ViewBag.TotalLadoA = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == id && c.EstaActivo && !c.EsBorrador && c.TipoLado == TipoLado.LadoA);

            ViewBag.TotalLadoB = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == id && c.EstaActivo && !c.EsBorrador && c.TipoLado == TipoLado.LadoB);

            _logger.LogInformation("Contenido cargado - LadoA: {LadoA}, LadoB: {LadoB}",
                contenidoLadoA.Count, contenidoLadoB.Count);

            return View(usuario);
        }

        // ========================================
        // SUSCRIBIRSE
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Suscribirse([FromBody] SuscripcionRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.CreadorId))
                {
                    _logger.LogWarning("Request inválido en Suscribirse");
                    return Json(new { success = false, message = "Datos inválidos" });
                }

                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    _logger.LogError("Usuario no autenticado en Suscribirse");
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // ⭐ CAMBIO: Ya no verificamos TipoUsuario del creador
                var creador = await _context.Users.FindAsync(request.CreadorId);
                if (creador == null || !creador.EstaActivo)
                {
                    _logger.LogWarning("Usuario/Creador no encontrado: {CreadorId}", request.CreadorId);
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                // No puedes suscribirte a ti mismo
                if (creador.Id == usuarioActual.Id)
                {
                    return Json(new { success = false, message = "No puedes suscribirte a ti mismo" });
                }

                _logger.LogInformation("Suscripción: {Fan} → {Creador} (${Precio})",
                    usuarioActual.UserName, creador.UserName, creador.PrecioSuscripcion);

                // Verificar saldo
                if (usuarioActual.Saldo < creador.PrecioSuscripcion)
                {
                    _logger.LogWarning("Saldo insuficiente: {Saldo} < {Precio}",
                        usuarioActual.Saldo, creador.PrecioSuscripcion);

                    return Json(new
                    {
                        success = false,
                        message = $"Saldo insuficiente. Necesitas ${creador.PrecioSuscripcion:N2} pero solo tienes ${usuarioActual.Saldo:N2}. Recarga tu billetera."
                    });
                }

                // Verificar suscripción existente
                var suscripcionExistente = await _context.Suscripciones
                    .FirstOrDefaultAsync(s =>
                        s.CreadorId == request.CreadorId &&
                        s.FanId == usuarioActual.Id);

                if (suscripcionExistente != null && suscripcionExistente.EstaActiva)
                {
                    _logger.LogWarning("Suscripción ya activa");
                    return Json(new { success = false, message = "Ya estás suscrito a este usuario" });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    if (suscripcionExistente != null)
                    {
                        // Reactivar suscripción
                        suscripcionExistente.EstaActiva = true;
                        suscripcionExistente.FechaInicio = DateTime.Now;
                        suscripcionExistente.ProximaRenovacion = DateTime.Now.AddMonths(1);
                        suscripcionExistente.RenovacionAutomatica = true;
                        suscripcionExistente.FechaCancelacion = null;
                        suscripcionExistente.PrecioMensual = creador.PrecioSuscripcion;
                        _logger.LogInformation("Suscripción reactivada");
                    }
                    else
                    {
                        // Nueva suscripción
                        var suscripcion = new Suscripcion
                        {
                            CreadorId = request.CreadorId,
                            FanId = usuarioActual.Id,
                            PrecioMensual = creador.PrecioSuscripcion,
                            FechaInicio = DateTime.Now,
                            ProximaRenovacion = DateTime.Now.AddMonths(1),
                            EstaActiva = true,
                            RenovacionAutomatica = true
                        };
                        _context.Suscripciones.Add(suscripcion);
                        _logger.LogInformation("Nueva suscripción creada");
                    }

                    // Calcular comisión (20%)
                    var comision = creador.PrecioSuscripcion * 0.20m;
                    var gananciaCreador = creador.PrecioSuscripcion - comision;

                    // Transacción Fan (Gasto)
                    var transaccionFan = new Transaccion
                    {
                        UsuarioId = usuarioActual.Id,
                        Monto = -creador.PrecioSuscripcion,
                        TipoTransaccion = TipoTransaccion.Suscripcion,
                        Descripcion = $"Suscripción a {creador.NombreCompleto} (@{creador.Seudonimo})",
                        EstadoPago = "Completado",
                        EstadoTransaccion = EstadoTransaccion.Completada,
                        FechaTransaccion = DateTime.Now
                    };
                    _context.Transacciones.Add(transaccionFan);

                    // Transacción Creador (Ingreso con comisión)
                    var transaccionCreador = new Transaccion
                    {
                        UsuarioId = creador.Id,
                        Monto = gananciaCreador,
                        Comision = comision,
                        MontoNeto = gananciaCreador,
                        TipoTransaccion = TipoTransaccion.IngresoSuscripcion,
                        Descripcion = $"Nueva suscripción de {usuarioActual.NombreCompleto}",
                        EstadoPago = "Completado",
                        EstadoTransaccion = EstadoTransaccion.Completada,
                        FechaTransaccion = DateTime.Now
                    };
                    _context.Transacciones.Add(transaccionCreador);

                    // Actualizar saldos (creador recibe monto menos comisión)
                    usuarioActual.Saldo -= creador.PrecioSuscripcion;
                    creador.Saldo += gananciaCreador;
                    creador.NumeroSeguidores++;
                    creador.TotalGanancias += gananciaCreador;

                    await _userManager.UpdateAsync(usuarioActual);
                    await _userManager.UpdateAsync(creador);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("✅ Suscripción completada exitosamente");

                    return Json(new
                    {
                        success = true,
                        message = $"¡Suscripción exitosa a {creador.NombreCompleto}! ${creador.PrecioSuscripcion:N2}/mes. Tu nuevo saldo: ${usuarioActual.Saldo:N2}"
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error en transacción de suscripción");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar suscripción");
                return Json(new
                {
                    success = false,
                    message = "Error al procesar la suscripción. Por favor intenta nuevamente."
                });
            }
        }

        // ========================================
        // CANCELAR SUSCRIPCIÓN
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar([FromBody] SuscripcionRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.CreadorId))
                {
                    _logger.LogWarning("Request inválido en Cancelar");
                    return Json(new { success = false, message = "Datos inválidos" });
                }

                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    _logger.LogError("Usuario no autenticado en Cancelar");
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var suscripcion = await _context.Suscripciones
                    .FirstOrDefaultAsync(s =>
                        s.CreadorId == request.CreadorId &&
                        s.FanId == usuarioActual.Id &&
                        s.EstaActiva);

                if (suscripcion == null)
                {
                    _logger.LogWarning("Suscripción no encontrada para cancelar");
                    return Json(new
                    {
                        success = false,
                        message = "No tienes una suscripción activa con este usuario"
                    });
                }

                _logger.LogInformation("Cancelando suscripción: {Fan} → {Creador}",
                    usuarioActual.UserName, request.CreadorId);

                // Cancelar suscripción
                suscripcion.EstaActiva = false;
                suscripcion.FechaCancelacion = DateTime.Now;
                suscripcion.RenovacionAutomatica = false;

                // Actualizar contador de seguidores
                var creador = await _context.Users.FindAsync(request.CreadorId);
                if (creador != null && creador.NumeroSeguidores > 0)
                {
                    creador.NumeroSeguidores--;
                    await _userManager.UpdateAsync(creador);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Suscripción cancelada exitosamente");

                return Json(new
                {
                    success = true,
                    message = "Suscripción cancelada exitosamente. Puedes reactivarla cuando quieras."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar suscripción");
                return Json(new
                {
                    success = false,
                    message = "Error al cancelar la suscripción. Por favor intenta nuevamente."
                });
            }
        }

        // ========================================
        // BUSCAR USUARIOS (AJAX)
        // ========================================

        [HttpGet]
        public async Task<IActionResult> BuscarUsuarios(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Json(new { success = false, message = "Mínimo 2 caracteres" });
                }

                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var usuarios = await _context.Users
                    .Where(u => u.Id != usuarioActual.Id && u.EstaActivo &&
                               (u.UserName.Contains(query) ||
                                u.NombreCompleto.Contains(query) ||
                                u.Seudonimo.Contains(query)))
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .Take(10)
                    .Select(u => new
                    {
                        id = u.Id,
                        username = u.UserName,
                        nombreCompleto = u.NombreCompleto,
                        seudonimo = u.Seudonimo,
                        fotoPerfil = u.FotoPerfil,
                        precio = u.PrecioSuscripcion,
                        seguidores = u.NumeroSeguidores,
                        verificado = u.CreadorVerificado
                    })
                    .ToListAsync();

                return Json(new { success = true, usuarios });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar usuarios");
                return Json(new { success = false, message = "Error en la búsqueda" });
            }
        }
    }

    // ========================================
    // VIEW MODELS
    // ========================================

    public class SuscripcionRequest
    {
        public string CreadorId { get; set; }
    }

    public class PerfilExplorar
    {
        public string Id { get; set; }
        public string NombreMostrado { get; set; }
        public string UsernameMostrado { get; set; }
        public string? FotoPerfil { get; set; }
        public string? FotoPortada { get; set; }
        public string? Biografia { get; set; }
        public int NumeroSeguidores { get; set; }
        public bool CreadorVerificado { get; set; }
        public bool EsLadoB { get; set; }
    }
}