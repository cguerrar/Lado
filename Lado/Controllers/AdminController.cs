using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            ViewBag.ReportesPendientes = _context.Reportes.Count(r => r.Estado == "Pendiente");
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsuarios = await _context.Users.CountAsync();
            ViewBag.TotalCreadores = await _context.Users.CountAsync(u => u.TipoUsuario == 1);
            ViewBag.TotalFans = await _context.Users.CountAsync(u => u.TipoUsuario == 2);
            ViewBag.TotalSuscripciones = await _context.Suscripciones.CountAsync(s => s.EstaActiva);
            ViewBag.TotalPublicaciones = await _context.Contenidos.CountAsync();
            ViewBag.PublicacionesHoy = await _context.Contenidos
                .CountAsync(c => c.FechaPublicacion.Date == DateTime.Today);
            ViewBag.UsuariosBloqueados = await _context.Users.CountAsync(u => !u.EstaActivo);

            return View();
        }

        // ========================================
        // USUARIOS
        // ========================================
        public async Task<IActionResult> Usuarios()
        {
            var usuarios = await _context.Users
                .OrderByDescending(u => u.FechaRegistro)
                .ToListAsync();

            var usuariosConRoles = new List<(ApplicationUser, string)>();

            foreach (var usuario in usuarios)
            {
                var roles = await _userManager.GetRolesAsync(usuario);
                var rol = roles.FirstOrDefault() ?? "Sin Rol";
                usuariosConRoles.Add((usuario, rol));
            }

            ViewBag.VerificacionesPendientes = await _context.CreatorVerificationRequests
                .Include(v => v.User)
                .Where(v => v.Estado == "Pendiente")
                .OrderByDescending(v => v.FechaSolicitud)
                .ToListAsync();

            return View(usuariosConRoles);
        }

        [HttpPost]
        public async Task<IActionResult> BloquearUsuario(string id)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario != null)
            {
                usuario.EstaActivo = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Usuario {usuario.NombreCompleto} bloqueado exitosamente.";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        [HttpPost]
        public async Task<IActionResult> DesbloquearUsuario(string id)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario != null)
            {
                usuario.EstaActivo = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Usuario {usuario.NombreCompleto} desbloqueado exitosamente.";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarUsuario(string id)
        {
            var usuario = await _context.Users.FindAsync(id);

            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction(nameof(Usuarios));
            }

            // Verificar que no sea un Admin
            var roles = await _userManager.GetRolesAsync(usuario);
            if (roles.Contains("Admin"))
            {
                TempData["Error"] = "No se puede eliminar un usuario administrador.";
                return RedirectToAction(nameof(Usuarios));
            }

            try
            {
                // 1. Eliminar contenidos del usuario
                var contenidos = await _context.Contenidos
                    .Where(c => c.UsuarioId == id)
                    .ToListAsync();
                if (contenidos.Any())
                {
                    _context.Contenidos.RemoveRange(contenidos);
                }

                // 2. Eliminar suscripciones donde es creador
                var suscripcionesCreador = await _context.Suscripciones
                    .Where(s => s.CreadorId == id)
                    .ToListAsync();
                if (suscripcionesCreador.Any())
                {
                    _context.Suscripciones.RemoveRange(suscripcionesCreador);
                }

                // 3. Eliminar suscripciones donde es fan
                var suscripcionesFan = await _context.Suscripciones
                    .Where(s => s.FanId == id)
                    .ToListAsync();
                if (suscripcionesFan.Any())
                {
                    _context.Suscripciones.RemoveRange(suscripcionesFan);
                }

                // 4. Eliminar transacciones
                var transacciones = await _context.Transacciones
                    .Where(t => t.UsuarioId == id)
                    .ToListAsync();
                if (transacciones.Any())
                {
                    _context.Transacciones.RemoveRange(transacciones);
                }

                // 5. Eliminar likes
                var likes = await _context.Likes
                    .Where(l => l.UsuarioId == id)
                    .ToListAsync();
                if (likes.Any())
                {
                    _context.Likes.RemoveRange(likes);
                }

                // 6. Eliminar comentarios
                var comentarios = await _context.Comentarios
                    .Where(c => c.UsuarioId == id)
                    .ToListAsync();
                if (comentarios.Any())
                {
                    _context.Comentarios.RemoveRange(comentarios);
                }

                // 7. Eliminar reportes hechos por el usuario
                var reportesHechos = await _context.Reportes
                    .Where(r => r.UsuarioReportadorId == id)
                    .ToListAsync();
                if (reportesHechos.Any())
                {
                    _context.Reportes.RemoveRange(reportesHechos);
                }

                // 8. Eliminar reportes contra el usuario (si existe la propiedad)
                try
                {
                    var reportesContra = await _context.Reportes
                        .Where(r => r.UsuarioReportadoId == id)
                        .ToListAsync();
                    if (reportesContra.Any())
                    {
                        _context.Reportes.RemoveRange(reportesContra);
                    }
                }
                catch
                {
                    // Si no existe la propiedad UsuarioReportadoId, continuar
                }

                // 9. Eliminar solicitud de verificación si existe
                try
                {
                    var verificacion = await _context.CreatorVerificationRequests
                        .FirstOrDefaultAsync(v => v.UserId == id);
                    if (verificacion != null)
                    {
                        _context.CreatorVerificationRequests.Remove(verificacion);
                    }
                }
                catch
                {
                    // Si no existe la tabla, continuar
                }

                // 10. Guardar cambios antes de eliminar el usuario
                await _context.SaveChangesAsync();

                // 11. Finalmente, eliminar el usuario con UserManager
                var result = await _userManager.DeleteAsync(usuario);

                if (result.Succeeded)
                {
                    TempData["Success"] = $"Usuario {usuario.NombreCompleto} eliminado permanentemente junto con todos sus datos.";
                }
                else
                {
                    TempData["Error"] = "Error al eliminar el usuario: " + string.Join(", ", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar el usuario: {ex.Message}";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        // ========================================
        // DETALLE USUARIO
        // ========================================
        public async Task<IActionResult> DetalleUsuario(string id)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
            {
                return NotFound();
            }

            ViewBag.Suscripciones = await _context.Suscripciones
                .Include(s => s.Creador)
                .Where(s => s.FanId == id && s.EstaActiva)
                .ToListAsync();

            ViewBag.Contenidos = await _context.Contenidos
                .Where(c => c.UsuarioId == id && c.EstaActivo)
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(20)
                .ToListAsync();

            ViewBag.Transacciones = await _context.Transacciones
                .Where(t => t.UsuarioId == id)
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(20)
                .ToListAsync();

            return View(usuario);
        }

        [HttpPost]
        public async Task<IActionResult> SuspenderUsuario(string userId)
        {
            var usuario = await _context.Users.FindAsync(userId);
            if (usuario != null)
            {
                usuario.EstaActivo = false;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> ActivarUsuario(string userId)
        {
            var usuario = await _context.Users.FindAsync(userId);
            if (usuario != null)
            {
                usuario.EstaActivo = true;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> VerificarUsuario(string userId)
        {
            var usuario = await _context.Users.FindAsync(userId);
            if (usuario != null)
            {
                usuario.EsVerificado = true;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        // ========================================
        // CONTENIDO
        // ========================================
        public async Task<IActionResult> Contenido()
        {
            var contenidos = await _context.Contenidos
                .Include(c => c.Usuario)
                .Include(c => c.Likes)
                .Include(c => c.Comentarios)
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(100)
                .ToListAsync();

            return View(contenidos);
        }

        [HttpPost]
        public async Task<IActionResult> CensurarContenido(int id, string razon)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                contenido.Censurado = true;
                contenido.RazonCensura = razon;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido censurado exitosamente.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        [HttpPost]
        public async Task<IActionResult> DescensurarContenido(int id)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                contenido.Censurado = false;
                contenido.RazonCensura = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido descensurado exitosamente.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        [HttpPost]
        public async Task<IActionResult> EliminarContenido(int id)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                _context.Contenidos.Remove(contenido);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido eliminado permanentemente.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        // ========================================
        // REPORTES
        // ========================================
        public async Task<IActionResult> Reportes()
        {
            var reportes = await _context.Reportes
                .Include(r => r.UsuarioReportador)
                .Include(r => r.ContenidoReportado)
                .ThenInclude(c => c.Usuario)
                .OrderByDescending(r => r.FechaReporte)
                .ToListAsync();

            return View(reportes);
        }

        [HttpPost]
        public async Task<IActionResult> ResolverReporte(int id, string accion)
        {
            var reporte = await _context.Reportes
                .Include(r => r.ContenidoReportado)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reporte != null)
            {
                reporte.Estado = "Resuelto";
                reporte.FechaResolucion = DateTime.Now;

                if (accion == "censurar" && reporte.ContenidoReportado != null)
                {
                    reporte.ContenidoReportado.Censurado = true;
                    reporte.ContenidoReportado.RazonCensura = $"Reporte: {reporte.Motivo}";
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Reporte resuelto exitosamente.";
            }

            return RedirectToAction(nameof(Reportes));
        }

        // ========================================
        // ESTADÍSTICAS
        // ========================================
        public async Task<IActionResult> Estadisticas()
        {
            ViewBag.TotalUsuarios = await _context.Users.CountAsync();
            ViewBag.TotalCreadores = await _context.Users.CountAsync(u => u.TipoUsuario == 1);
            ViewBag.TotalSuscripciones = await _context.Suscripciones.CountAsync(s => s.EstaActiva);
            ViewBag.TotalPublicaciones = await _context.Contenidos.CountAsync();

            var registrosPorMes = await _context.Users
                .GroupBy(u => new { u.FechaRegistro.Year, u.FechaRegistro.Month })
                .Select(g => new
                {
                    año = g.Key.Year,
                    mes = g.Key.Month,
                    total = g.Count()
                })
                .OrderBy(x => x.año).ThenBy(x => x.mes)
                .ToListAsync();

            var contenidosPorTipo = await _context.Contenidos
                .GroupBy(c => c.TipoContenido)
                .Select(g => new
                {
                    tipo = g.Key.ToString(),
                    total = g.Count()
                })
                .ToListAsync();

            var topCreadores = await _context.Contenidos
                .GroupBy(c => c.UsuarioId)
                .Select(g => new
                {
                    UsuarioId = g.Key,
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .Take(10)
                .ToListAsync();

            var topCreadoresConNombres = new List<object>();
            foreach (var item in topCreadores)
            {
                var usuario = await _context.Users.FindAsync(item.UsuarioId);
                topCreadoresConNombres.Add(new
                {
                    nombre = usuario?.UserName ?? "Desconocido",
                    total = item.Total
                });
            }

            ViewBag.RegistrosPorMes = registrosPorMes;
            ViewBag.ContenidosPorTipo = contenidosPorTipo;
            ViewBag.TopCreadores = topCreadoresConNombres;

            return View();
        }

        // ========================================
        // CONFIGURACIÓN
        // ========================================
        public IActionResult Configuracion()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarComision(decimal comision)
        {
            TempData["Success"] = $"Comisión actualizada a {comision}%";
            return RedirectToAction(nameof(Configuracion));
        }
    }
}