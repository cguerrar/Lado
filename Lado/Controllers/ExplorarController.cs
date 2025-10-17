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

        public ExplorarController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Explorar
        public async Task<IActionResult> Index(string buscar = "", string categoria = "todos")
        {
            var usuarioActual = await _userManager.GetUserAsync(User);

            // Obtener creadores activos
            var query = _context.Users
                .Where(u => u.TipoUsuario == (int)TipoUsuario.Creador && u.Id != usuarioActual.Id);

            // Filtrar por búsqueda
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                query = query.Where(u =>
                    u.UserName.Contains(buscar) ||
                    u.NombreCompleto.Contains(buscar) ||
                    (u.Biografia != null && u.Biografia.Contains(buscar)));
            }

            // Filtrar por categoría
            if (!string.IsNullOrWhiteSpace(categoria) && categoria != "todos")
            {
                query = query.Where(u => u.Categoria == categoria);
            }

            var creadores = await query
                .OrderByDescending(u => u.NumeroSeguidores)
                .Take(50)
                .ToListAsync();

            // Obtener suscripciones actuales del usuario
            var suscripcionesIds = await _context.Suscripciones
                .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva)
                .Select(s => s.CreadorId)
                .ToListAsync();

            ViewBag.SuscripcionesIds = suscripcionesIds;
            ViewBag.BuscarTexto = buscar;
            ViewBag.Categoria = categoria;

            return View(creadores);
        }

        // GET: /Explorar/Perfil/id
        public async Task<IActionResult> Perfil(string id)
        {
            var creador = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.TipoUsuario == (int)TipoUsuario.Creador);

            if (creador == null)
            {
                TempData["Error"] = "Creador no encontrado";
                return RedirectToAction("Index");
            }

            var usuarioActual = await _userManager.GetUserAsync(User);

            // Verificar si está suscrito
            var suscripcion = await _context.Suscripciones
                .FirstOrDefaultAsync(s =>
                    s.CreadorId == id &&
                    s.FanId == usuarioActual.Id &&
                    s.EstaActiva);

            ViewBag.EstaSuscrito = suscripcion != null;

            // ✅ CARGAR contenido CON relaciones
            var contenidos = await _context.Contenidos
                .Include(c => c.Likes)
                .Include(c => c.Comentarios)
                .Where(c => c.UsuarioId == id &&
                           c.EstaActivo &&
                           !c.EsBorrador &&
                           (!c.EsPremium || suscripcion != null))
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(12)
                .ToListAsync();

            // ✅ ACTUALIZAR contadores desde las relaciones cargadas
            foreach (var contenido in contenidos)
            {
                // Si los contadores no coinciden con las colecciones, actualizarlos
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
            ViewBag.TotalContenidos = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == id && c.EstaActivo && !c.EsBorrador);

            return View(creador);
        }

        // POST: /Explorar/Suscribirse
        [HttpPost]
        public async Task<IActionResult> Suscribirse([FromBody] SuscripcionRequest request)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                var creador = await _context.Users.FindAsync(request.CreadorId);

                if (creador == null || creador.TipoUsuario != (int)TipoUsuario.Creador)
                {
                    return Json(new { success = false, message = "Creador no encontrado" });
                }

                // ✅ VERIFICAR SALDO DEL FAN
                if (usuarioActual.Saldo < creador.PrecioSuscripcion)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Saldo insuficiente. Necesitas ${creador.PrecioSuscripcion:N2} pero solo tienes ${usuarioActual.Saldo:N2}. Carga saldo desde tu billetera."
                    });
                }

                // Verificar si ya está suscrito
                var suscripcionExistente = await _context.Suscripciones
                    .FirstOrDefaultAsync(s =>
                        s.CreadorId == request.CreadorId &&
                        s.FanId == usuarioActual.Id);

                if (suscripcionExistente != null)
                {
                    if (suscripcionExistente.EstaActiva)
                    {
                        return Json(new { success = false, message = "Ya estás suscrito a este creador" });
                    }

                    // Reactivar suscripción cancelada
                    suscripcionExistente.EstaActiva = true;
                    suscripcionExistente.FechaInicio = DateTime.Now;
                    suscripcionExistente.ProximaRenovacion = DateTime.Now.AddMonths(1);
                    suscripcionExistente.RenovacionAutomatica = true;
                    suscripcionExistente.FechaCancelacion = null;
                    suscripcionExistente.PrecioMensual = creador.PrecioSuscripcion;
                }
                else
                {
                    // Crear nueva suscripción
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
                }

                // ✅ CREAR TRANSACCIÓN PARA EL FAN (GASTO)
                var transaccionFan = new Transaccion
                {
                    UsuarioId = usuarioActual.Id,
                    Monto = creador.PrecioSuscripcion,
                    TipoTransaccion = TipoTransaccion.Suscripcion,
                    Descripcion = $"Suscripción a {creador.NombreCompleto}",
                    EstadoPago = "Completado",
                    FechaTransaccion = DateTime.Now
                };
                _context.Transacciones.Add(transaccionFan);

                // ✅ CREAR TRANSACCIÓN PARA EL CREADOR (INGRESO)
                var transaccionCreador = new Transaccion
                {
                    UsuarioId = creador.Id,
                    Monto = creador.PrecioSuscripcion,
                    TipoTransaccion = TipoTransaccion.Suscripcion,
                    Descripcion = $"Nueva suscripción de {usuarioActual.NombreCompleto}",
                    EstadoPago = "Completado",
                    FechaTransaccion = DateTime.Now
                };
                _context.Transacciones.Add(transaccionCreador);

                // ✅ ACTUALIZAR SALDOS
                usuarioActual.Saldo -= creador.PrecioSuscripcion;
                creador.Saldo += creador.PrecioSuscripcion;

                // Actualizar contador de seguidores
                creador.NumeroSeguidores++;

                // Actualizar ganancias totales del creador
                creador.TotalGanancias += creador.PrecioSuscripcion;

                await _userManager.UpdateAsync(usuarioActual);
                await _userManager.UpdateAsync(creador);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"¡Suscripción exitosa! ${creador.PrecioSuscripcion:N2}/mes. Tu nuevo saldo: ${usuarioActual.Saldo:N2}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al procesar la suscripción: " + ex.Message });
            }
        }

        // POST: /Explorar/Cancelar
        [HttpPost]
        public async Task<IActionResult> Cancelar([FromBody] SuscripcionRequest request)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);

                var suscripcion = await _context.Suscripciones
                    .FirstOrDefaultAsync(s =>
                        s.CreadorId == request.CreadorId &&
                        s.FanId == usuarioActual.Id &&
                        s.EstaActiva);

                if (suscripcion == null)
                {
                    return Json(new { success = false, message = "No tienes una suscripción activa con este creador" });
                }

                // Cancelar suscripción
                suscripcion.EstaActiva = false;
                suscripcion.FechaCancelacion = DateTime.Now;
                suscripcion.RenovacionAutomatica = false;

                // Actualizar contador de seguidores
                var creador = await _context.Users.FindAsync(request.CreadorId);
                if (creador != null && creador.NumeroSeguidores > 0)
                {
                    creador.NumeroSeguidores--;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Suscripción cancelada exitosamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al cancelar: " + ex.Message });
            }
        }
    }

    public class SuscripcionRequest
    {
        public string CreadorId { get; set; }
    }
}