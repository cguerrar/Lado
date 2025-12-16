using ClosedXML.Excel;
using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    [Authorize(Roles = "Admin")]
    public partial class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IServerMetricsService _serverMetrics;
        private readonly IConfiguration _configuration;
        private readonly IVisitasService _visitasService;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IServerMetricsService serverMetrics,
            IConfiguration configuration,
            IVisitasService visitasService)
        {
            _context = context;
            _userManager = userManager;
            _serverMetrics = serverMetrics;
            _configuration = configuration;
            _visitasService = visitasService;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            ViewBag.ReportesPendientes = _context.Reportes.Count(r => r.Estado == "Pendiente");
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsuarios = await _context.Users.CountAsync();
            ViewBag.TotalSuscripciones = await _context.Suscripciones.CountAsync(s => s.EstaActiva);
            ViewBag.TotalPublicaciones = await _context.Contenidos.CountAsync();
            ViewBag.PublicacionesHoy = await _context.Contenidos
                .CountAsync(c => c.FechaPublicacion.Date == DateTime.Today);
            ViewBag.UsuariosBloqueados = await _context.Users.CountAsync(u => !u.EstaActivo);

            // Estadisticas de visitas
            ViewBag.TotalVisitas = await _visitasService.ObtenerTotalVisitasAsync();
            ViewBag.VisitasHoy = await _visitasService.ObtenerVisitasHoyAsync();
            ViewBag.VisitantesUnicosHoy = await _visitasService.ObtenerVisitantesUnicosHoyAsync();
            ViewBag.VisitasUltimos7Dias = await _visitasService.ObtenerVisitasUltimos7DiasAsync();

            return View();
        }

        // ========================================
        // USUARIOS
        // ========================================
        public async Task<IActionResult> Usuarios()
        {
            // OPTIMIZADO: Evitar N+1 usando una sola consulta con join
            var usuariosConRoles = await _context.Users
                .OrderByDescending(u => u.FechaRegistro)
                .Select(u => new
                {
                    Usuario = u,
                    Rol = _context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                        .FirstOrDefault() ?? "Sin Rol"
                })
                .ToListAsync();

            var resultado = usuariosConRoles
                .Select(x => (x.Usuario, x.Rol))
                .ToList();

            ViewBag.VerificacionesPendientes = await _context.CreatorVerificationRequests
                .Include(v => v.User)
                .Where(v => v.Estado == "Pendiente")
                .OrderByDescending(v => v.FechaSolicitud)
                .ToListAsync();

            return View(resultado);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
        public async Task<IActionResult> CambiarContrasena(string id, string nuevaContrasena)
        {
            if (string.IsNullOrWhiteSpace(nuevaContrasena) || nuevaContrasena.Length < 8)
            {
                TempData["Error"] = "La contraseña debe tener al menos 8 caracteres.";
                return RedirectToAction(nameof(Usuarios));
            }

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction(nameof(Usuarios));
            }

            // Verificar que no sea un Admin (solo otro admin puede cambiar la contraseña de un admin)
            var roles = await _userManager.GetRolesAsync(usuario);
            var currentUser = await _userManager.GetUserAsync(User);

            if (roles.Contains("Admin") && currentUser?.Id != usuario.Id)
            {
                // Solo el propio admin puede cambiar su contraseña
                TempData["Error"] = "No puedes cambiar la contraseña de otro administrador.";
                return RedirectToAction(nameof(Usuarios));
            }

            // Eliminar la contraseña actual y establecer la nueva
            var removeResult = await _userManager.RemovePasswordAsync(usuario);
            if (!removeResult.Succeeded)
            {
                TempData["Error"] = "Error al procesar la solicitud.";
                return RedirectToAction(nameof(Usuarios));
            }

            var addResult = await _userManager.AddPasswordAsync(usuario, nuevaContrasena);
            if (addResult.Succeeded)
            {
                TempData["Success"] = $"Contraseña de {usuario.NombreCompleto} actualizada exitosamente.";
            }
            else
            {
                // Mostrar errores de validación
                var errors = string.Join(" ", addResult.Errors.Select(e => e.Description));
                TempData["Error"] = $"Error al cambiar contraseña: {errors}";
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

                // 9. Eliminar solicitud de verificaci�n si existe
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspenderUsuario(string userId)
        {
            // Prevenir auto-suspensión
            var adminActual = await _userManager.GetUserAsync(User);
            if (adminActual != null && userId == adminActual.Id)
            {
                return Json(new { success = false, message = "No puedes suspenderte a ti mismo" });
            }

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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
        // ESTADISTICAS COMPLETAS
        // ========================================
        public async Task<IActionResult> Estadisticas()
        {
            var hoy = DateTime.Today;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioMesAnterior = inicioMes.AddMonths(-1);
            var hace7Dias = hoy.AddDays(-7);
            var hace30Dias = hoy.AddDays(-30);

            // ========================================
            // KPIs PRINCIPALES
            // ========================================

            // Usuarios
            var totalUsuarios = await _context.Users.CountAsync();
            var usuariosMesActual = await _context.Users.CountAsync(u => u.FechaRegistro >= inicioMes);
            var usuariosMesAnterior = await _context.Users.CountAsync(u => u.FechaRegistro >= inicioMesAnterior && u.FechaRegistro < inicioMes);
            var crecimientoUsuarios = usuariosMesAnterior > 0 ? ((double)(usuariosMesActual - usuariosMesAnterior) / usuariosMesAnterior * 100) : 100;

            ViewBag.TotalUsuarios = totalUsuarios;
            ViewBag.UsuariosNuevosMes = usuariosMesActual;
            ViewBag.CrecimientoUsuarios = Math.Round(crecimientoUsuarios, 1);

            // Usuarios activos (que han publicado o interactuado en los ultimos 7 dias)
            var usuariosActivosIds = await _context.Contenidos
                .Where(c => c.FechaPublicacion >= hace7Dias)
                .Select(c => c.UsuarioId)
                .Union(_context.Likes.Where(l => l.FechaLike >= hace7Dias).Select(l => l.UsuarioId))
                .Union(_context.Comentarios.Where(c => c.FechaCreacion >= hace7Dias).Select(c => c.UsuarioId))
                .Distinct()
                .CountAsync();
            ViewBag.UsuariosActivos7Dias = usuariosActivosIds;

            // Creadores
            var totalCreadores = await _context.Users.CountAsync(u => u.EsCreador);
            var creadoresVerificados = await _context.Users.CountAsync(u => u.EsCreador && u.CreadorVerificado);
            ViewBag.TotalCreadores = totalCreadores;
            ViewBag.CreadoresVerificados = creadoresVerificados;

            // Suscripciones
            var suscripcionesActivas = await _context.Suscripciones.CountAsync(s => s.EstaActiva);
            var suscripcionesMesActual = await _context.Suscripciones.CountAsync(s => s.FechaInicio >= inicioMes);
            var suscripcionesCanceladas = await _context.Suscripciones.CountAsync(s => s.FechaCancelacion != null && s.FechaCancelacion >= inicioMes);
            ViewBag.SuscripcionesActivas = suscripcionesActivas;
            ViewBag.SuscripcionesNuevasMes = suscripcionesMesActual;
            ViewBag.SuscripcionesCanceladas = suscripcionesCanceladas;

            // Contenido
            var totalContenido = await _context.Contenidos.CountAsync();
            var contenidoHoy = await _context.Contenidos.CountAsync(c => c.FechaPublicacion.Date == hoy);
            var contenidoMes = await _context.Contenidos.CountAsync(c => c.FechaPublicacion >= inicioMes);
            var contenidoCensurado = await _context.Contenidos.CountAsync(c => c.Censurado);
            ViewBag.TotalContenido = totalContenido;
            ViewBag.ContenidoHoy = contenidoHoy;
            ViewBag.ContenidoMes = contenidoMes;
            ViewBag.ContenidoCensurado = contenidoCensurado;

            // ========================================
            // METRICAS FINANCIERAS
            // ========================================
            var ingresosTotales = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            var ingresosMes = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada && t.FechaTransaccion >= inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            var ingresosMesAnterior = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada &&
                       t.FechaTransaccion >= inicioMesAnterior && t.FechaTransaccion < inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            var crecimientoIngresos = ingresosMesAnterior > 0 ? ((double)(ingresosMes - ingresosMesAnterior) / (double)ingresosMesAnterior * 100) : 100;

            var comisionesTotales = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada && t.Comision != null)
                .SumAsync(t => (decimal?)t.Comision) ?? 0;

            ViewBag.IngresosTotales = ingresosTotales;
            ViewBag.IngresosMes = ingresosMes;
            ViewBag.CrecimientoIngresos = Math.Round(crecimientoIngresos, 1);
            ViewBag.ComisionesPlataforma = comisionesTotales;

            // ========================================
            // METRICAS DE ENGAGEMENT
            // ========================================
            var totalLikes = await _context.Likes.CountAsync();
            var totalComentarios = await _context.Comentarios.CountAsync();
            var promedioLikesPorPost = totalContenido > 0 ? (double)totalLikes / totalContenido : 0;
            var promedioComentariosPorPost = totalContenido > 0 ? (double)totalComentarios / totalContenido : 0;

            ViewBag.TotalLikes = totalLikes;
            ViewBag.TotalComentarios = totalComentarios;
            ViewBag.PromedioLikes = Math.Round(promedioLikesPorPost, 1);
            ViewBag.PromedioComentarios = Math.Round(promedioComentariosPorPost, 1);

            // ========================================
            // DATOS PARA GRAFICOS
            // ========================================

            // Registros por mes (ultimos 12 meses)
            var hace12Meses = hoy.AddMonths(-12);
            var registrosPorMes = await _context.Users
                .Where(u => u.FechaRegistro >= hace12Meses)
                .GroupBy(u => new { u.FechaRegistro.Year, u.FechaRegistro.Month })
                .Select(g => new
                {
                    Anio = g.Key.Year,
                    Mes = g.Key.Month,
                    Total = g.Count()
                })
                .OrderBy(x => x.Anio).ThenBy(x => x.Mes)
                .ToListAsync();
            ViewBag.RegistrosPorMes = registrosPorMes;

            // Contenido por tipo
            var contenidoPorTipo = await _context.Contenidos
                .GroupBy(c => c.TipoContenido)
                .Select(g => new
                {
                    Tipo = g.Key.ToString(),
                    Total = g.Count()
                })
                .ToListAsync();
            ViewBag.ContenidoPorTipo = contenidoPorTipo;

            // Ingresos por dia (ultimos 30 dias)
            var ingresosPorDia = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada && t.FechaTransaccion >= hace30Dias)
                .GroupBy(t => t.FechaTransaccion.Date)
                .Select(g => new
                {
                    Fecha = g.Key,
                    Total = g.Sum(t => t.Monto)
                })
                .OrderBy(x => x.Fecha)
                .ToListAsync();
            ViewBag.IngresosPorDia = ingresosPorDia;

            // Usuarios por pais
            var usuariosPorPais = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.Pais))
                .GroupBy(u => u.Pais)
                .Select(g => new
                {
                    Pais = g.Key,
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .Take(10)
                .ToListAsync();
            ViewBag.UsuariosPorPais = usuariosPorPais;

            // ========================================
            // TOP RANKINGS
            // ========================================

            // Top 10 creadores con mas suscriptores
            var topCreadoresSuscriptores = await _context.Suscripciones
                .Where(s => s.EstaActiva)
                .GroupBy(s => s.CreadorId)
                .Select(g => new
                {
                    CreadorId = g.Key,
                    TotalSuscriptores = g.Count()
                })
                .OrderByDescending(x => x.TotalSuscriptores)
                .Take(10)
                .ToListAsync();

            var topSuscriptoresConNombres = new List<object>();
            foreach (var item in topCreadoresSuscriptores)
            {
                var usuario = await _context.Users.FindAsync(item.CreadorId);
                topSuscriptoresConNombres.Add(new
                {
                    Nombre = usuario?.UserName ?? "Desconocido",
                    FotoPerfil = usuario?.FotoPerfil,
                    Total = item.TotalSuscriptores
                });
            }
            ViewBag.TopCreadoresSuscriptores = topSuscriptoresConNombres;

            // Top 10 creadores con mas contenido
            var topCreadoresContenido = await _context.Contenidos
                .GroupBy(c => c.UsuarioId)
                .Select(g => new
                {
                    CreadorId = g.Key,
                    TotalContenido = g.Count()
                })
                .OrderByDescending(x => x.TotalContenido)
                .Take(10)
                .ToListAsync();

            var topContenidoConNombres = new List<object>();
            foreach (var item in topCreadoresContenido)
            {
                var usuario = await _context.Users.FindAsync(item.CreadorId);
                topContenidoConNombres.Add(new
                {
                    Nombre = usuario?.UserName ?? "Desconocido",
                    FotoPerfil = usuario?.FotoPerfil,
                    Total = item.TotalContenido
                });
            }
            ViewBag.TopCreadoresContenido = topContenidoConNombres;

            // Top 10 contenidos mas populares (likes + comentarios)
            var topContenidos = await _context.Contenidos
                .Include(c => c.Usuario)
                .Include(c => c.Likes)
                .Include(c => c.Comentarios)
                .OrderByDescending(c => c.Likes.Count + c.Comentarios.Count)
                .Take(10)
                .Select(c => new
                {
                    c.Id,
                    c.Descripcion,
                    c.TipoContenido,
                    c.Thumbnail,
                    c.RutaArchivo,
                    CreadorNombre = c.Usuario != null ? c.Usuario.UserName : "Desconocido",
                    TotalLikes = c.Likes.Count,
                    TotalComentarios = c.Comentarios.Count
                })
                .ToListAsync();
            ViewBag.TopContenidos = topContenidos;

            // ========================================
            // ALERTAS Y TENDENCIAS
            // ========================================

            // Creadores sin publicar hace 7 dias (que tenian actividad antes)
            var creadoresInactivos = await _context.Users
                .Where(u => u.EsCreador)
                .Where(u => !_context.Contenidos.Any(c => c.UsuarioId == u.Id && c.FechaPublicacion >= hace7Dias))
                .Where(u => _context.Contenidos.Any(c => c.UsuarioId == u.Id))
                .Take(10)
                .Select(u => new { u.Id, u.UserName, u.FotoPerfil })
                .ToListAsync();
            ViewBag.CreadoresInactivos = creadoresInactivos;

            // Reportes pendientes
            var reportesPendientes = await _context.Reportes.CountAsync(r => r.Estado == "Pendiente");
            ViewBag.ReportesPendientesTotal = reportesPendientes;

            // Verificaciones pendientes
            var verificacionesPendientes = await _context.CreatorVerificationRequests
                .CountAsync(v => v.Estado == "Pendiente");
            ViewBag.VerificacionesPendientes = verificacionesPendientes;

            return View();
        }

        // ========================================
        // CONFIGURACI�N
        // ========================================
        public IActionResult Configuracion()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarComision(decimal comision)
        {
            TempData["Success"] = $"Comision actualizada a {comision}%";
            return RedirectToAction(nameof(Configuracion));
        }

        // ========================================
        // COMISIONES POR USUARIO
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarComisionUsuario(
            string userId,
            decimal comisionRetiro,
            decimal montoMinimoRetiro,
            bool usarRetencionPais,
            decimal? retencionImpuestos)
        {
            var usuario = await _userManager.FindByIdAsync(userId);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction(nameof(Usuarios));
            }

            // Validar rangos
            if (comisionRetiro < 0 || comisionRetiro > 100)
            {
                TempData["Error"] = "La comisión debe estar entre 0% y 100%";
                return RedirectToAction(nameof(Usuarios));
            }

            if (montoMinimoRetiro < 0)
            {
                TempData["Error"] = "El monto mínimo no puede ser negativo";
                return RedirectToAction(nameof(Usuarios));
            }

            if (retencionImpuestos.HasValue && (retencionImpuestos < 0 || retencionImpuestos > 100))
            {
                TempData["Error"] = "La retención debe estar entre 0% y 100%";
                return RedirectToAction(nameof(Usuarios));
            }

            usuario.ComisionRetiro = comisionRetiro;
            usuario.MontoMinimoRetiro = montoMinimoRetiro;
            usuario.UsarRetencionPais = usarRetencionPais;
            usuario.RetencionImpuestos = usarRetencionPais ? null : retencionImpuestos;

            var result = await _userManager.UpdateAsync(usuario);
            if (result.Succeeded)
            {
                var retencionMsg = usarRetencionPais
                    ? "Retención: según país"
                    : $"Retención: {retencionImpuestos}%";
                TempData["Success"] = $"Configuración de {usuario.NombreCompleto} actualizada: Comisión {comisionRetiro}% - Mínimo ${montoMinimoRetiro} - {retencionMsg}";
            }
            else
            {
                TempData["Error"] = "Error al actualizar la configuración";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        // ========================================
        // TASAS DE CAMBIO
        // ========================================

        public async Task<IActionResult> TasasCambio()
        {
            var tasas = await _context.TasasCambio.OrderBy(t => t.CodigoMoneda).ToListAsync();

            // Si no hay tasas, crear las por defecto
            if (!tasas.Any())
            {
                foreach (var moneda in MonedasSoportadas.Monedas)
                {
                    var tasa = new TasaCambio
                    {
                        CodigoMoneda = moneda.Key,
                        NombreMoneda = moneda.Value.Nombre,
                        Simbolo = moneda.Value.Simbolo,
                        TasaVsUSD = moneda.Value.TasaDefault,
                        Activa = true,
                        UltimaActualizacion = DateTime.Now
                    };
                    _context.TasasCambio.Add(tasa);
                }
                await _context.SaveChangesAsync();
                tasas = await _context.TasasCambio.OrderBy(t => t.CodigoMoneda).ToListAsync();
            }

            return View(tasas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarTasaCambio(int id, decimal tasaVsUSD, bool activa)
        {
            var tasa = await _context.TasasCambio.FindAsync(id);
            if (tasa == null)
            {
                TempData["Error"] = "Tasa no encontrada";
                return RedirectToAction(nameof(TasasCambio));
            }

            if (tasaVsUSD <= 0)
            {
                TempData["Error"] = "La tasa debe ser mayor a 0";
                return RedirectToAction(nameof(TasasCambio));
            }

            tasa.TasaVsUSD = tasaVsUSD;
            tasa.Activa = activa;
            tasa.UltimaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Tasa de {tasa.CodigoMoneda} actualizada: 1 USD = {tasaVsUSD} {tasa.CodigoMoneda}";

            return RedirectToAction(nameof(TasasCambio));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarTasaCambio(string codigoMoneda, string nombreMoneda, string simbolo, decimal tasaVsUSD)
        {
            if (string.IsNullOrEmpty(codigoMoneda) || string.IsNullOrEmpty(nombreMoneda))
            {
                TempData["Error"] = "Código y nombre de moneda son requeridos";
                return RedirectToAction(nameof(TasasCambio));
            }

            // Verificar si ya existe
            var existente = await _context.TasasCambio.FirstOrDefaultAsync(t => t.CodigoMoneda == codigoMoneda.ToUpper());
            if (existente != null)
            {
                TempData["Error"] = $"La moneda {codigoMoneda} ya existe";
                return RedirectToAction(nameof(TasasCambio));
            }

            var tasa = new TasaCambio
            {
                CodigoMoneda = codigoMoneda.ToUpper(),
                NombreMoneda = nombreMoneda,
                Simbolo = string.IsNullOrEmpty(simbolo) ? "$" : simbolo,
                TasaVsUSD = tasaVsUSD > 0 ? tasaVsUSD : 1,
                Activa = true,
                UltimaActualizacion = DateTime.Now
            };

            _context.TasasCambio.Add(tasa);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Moneda {codigoMoneda} agregada correctamente";
            return RedirectToAction(nameof(TasasCambio));
        }

        // ========================================
        // RETENCIONES POR PAÍS
        // ========================================

        public async Task<IActionResult> RetencionesPaises()
        {
            var retenciones = await _context.RetencionesPaises.OrderBy(r => r.NombrePais).ToListAsync();

            // Si no hay retenciones, crear las predeterminadas
            if (!retenciones.Any())
            {
                foreach (var pais in RetencionesPredeterminadas.Paises)
                {
                    var retencion = new RetencionPais
                    {
                        CodigoPais = pais.Key,
                        NombrePais = pais.Value.Nombre,
                        PorcentajeRetencion = pais.Value.Retencion,
                        Descripcion = pais.Value.Descripcion,
                        Activo = true,
                        UltimaActualizacion = DateTime.Now
                    };
                    _context.RetencionesPaises.Add(retencion);
                }
                await _context.SaveChangesAsync();
                retenciones = await _context.RetencionesPaises.OrderBy(r => r.NombrePais).ToListAsync();
            }

            return View(retenciones);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarRetencionPais(int id, decimal porcentajeRetencion, bool activo, string? descripcion)
        {
            var retencion = await _context.RetencionesPaises.FindAsync(id);
            if (retencion == null)
            {
                TempData["Error"] = "Retención no encontrada";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            if (porcentajeRetencion < 0 || porcentajeRetencion > 100)
            {
                TempData["Error"] = "El porcentaje debe estar entre 0% y 100%";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            retencion.PorcentajeRetencion = porcentajeRetencion;
            retencion.Activo = activo;
            retencion.Descripcion = descripcion;
            retencion.UltimaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Retención de {retencion.NombrePais} actualizada: {porcentajeRetencion}%";

            return RedirectToAction(nameof(RetencionesPaises));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarRetencionPais(string codigoPais, string nombrePais, decimal porcentajeRetencion, string? descripcion)
        {
            if (string.IsNullOrEmpty(codigoPais) || string.IsNullOrEmpty(nombrePais))
            {
                TempData["Error"] = "Código y nombre del país son requeridos";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            // Verificar si ya existe
            var existente = await _context.RetencionesPaises.FirstOrDefaultAsync(r => r.CodigoPais == codigoPais.ToUpper());
            if (existente != null)
            {
                TempData["Error"] = $"El país {codigoPais} ya existe";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            var retencion = new RetencionPais
            {
                CodigoPais = codigoPais.ToUpper(),
                NombrePais = nombrePais,
                PorcentajeRetencion = porcentajeRetencion >= 0 && porcentajeRetencion <= 100 ? porcentajeRetencion : 0,
                Descripcion = descripcion,
                Activo = true,
                UltimaActualizacion = DateTime.Now
            };

            _context.RetencionesPaises.Add(retencion);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"País {nombrePais} agregado correctamente con retención de {porcentajeRetencion}%";
            return RedirectToAction(nameof(RetencionesPaises));
        }

        // Método helper para obtener la retención de un usuario
        public async Task<decimal> ObtenerRetencionUsuario(ApplicationUser usuario)
        {
            // Si no usa retención del país, devolver la retención personalizada
            if (!usuario.UsarRetencionPais && usuario.RetencionImpuestos.HasValue)
            {
                return usuario.RetencionImpuestos.Value;
            }

            // Buscar la retención del país del usuario
            if (!string.IsNullOrEmpty(usuario.Pais))
            {
                var retencionPais = await _context.RetencionesPaises
                    .FirstOrDefaultAsync(r => r.CodigoPais == usuario.Pais && r.Activo);

                if (retencionPais != null)
                {
                    return retencionPais.PorcentajeRetencion;
                }
            }

            // Si no hay país configurado o no existe la retención, devolver 0
            return 0;
        }

        // ========================================
        // AGENCIAS
        // ========================================
        public async Task<IActionResult> Agencias(string estado = "")
        {
            var query = _context.Agencias
                .Include(a => a.Usuario)
                .Include(a => a.Anuncios)
                .AsQueryable();

            if (!string.IsNullOrEmpty(estado))
            {
                if (Enum.TryParse<EstadoAgencia>(estado, out var estadoEnum))
                {
                    query = query.Where(a => a.Estado == estadoEnum);
                }
            }

            var agencias = await query
                .OrderByDescending(a => a.FechaRegistro)
                .ToListAsync();

            ViewBag.EstadoFiltro = estado;
            ViewBag.AgenciasPendientes = await _context.Agencias.CountAsync(a => a.Estado == EstadoAgencia.Pendiente);
            ViewBag.AgenciasActivas = await _context.Agencias.CountAsync(a => a.Estado == EstadoAgencia.Activa);
            ViewBag.AgenciasSuspendidas = await _context.Agencias.CountAsync(a => a.Estado == EstadoAgencia.Suspendida);

            return View(agencias);
        }

        public async Task<IActionResult> DetalleAgencia(int id)
        {
            var agencia = await _context.Agencias
                .Include(a => a.Usuario)
                .Include(a => a.Anuncios)
                .Include(a => a.Transacciones)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (agencia == null)
            {
                return NotFound();
            }

            ViewBag.AnunciosActivos = agencia.Anuncios.Count(a => a.Estado == EstadoAnuncio.Activo);
            ViewBag.AnunciosPendientes = agencia.Anuncios.Count(a => a.Estado == EstadoAnuncio.EnRevision);
            ViewBag.TotalImpresiones = agencia.Anuncios.Sum(a => a.Impresiones);
            ViewBag.TotalClics = agencia.Anuncios.Sum(a => a.Clics);

            return View(agencia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarAgencia(int id)
        {
            var agencia = await _context.Agencias.FindAsync(id);
            if (agencia == null)
            {
                TempData["Error"] = "Agencia no encontrada.";
                return RedirectToAction(nameof(Agencias));
            }

            agencia.Estado = EstadoAgencia.Activa;
            agencia.EstaVerificada = true;
            agencia.FechaAprobacion = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"La agencia '{agencia.NombreEmpresa}' ha sido aprobada exitosamente.";
            return RedirectToAction(nameof(Agencias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarAgencia(int id, string razon)
        {
            var agencia = await _context.Agencias.FindAsync(id);
            if (agencia == null)
            {
                TempData["Error"] = "Agencia no encontrada.";
                return RedirectToAction(nameof(Agencias));
            }

            agencia.Estado = EstadoAgencia.Rechazada;
            agencia.MotivoRechazo = razon;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"La agencia '{agencia.NombreEmpresa}' ha sido rechazada.";
            return RedirectToAction(nameof(Agencias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspenderAgencia(int id, string razon)
        {
            var agencia = await _context.Agencias.FindAsync(id);
            if (agencia == null)
            {
                TempData["Error"] = "Agencia no encontrada.";
                return RedirectToAction(nameof(Agencias));
            }

            agencia.Estado = EstadoAgencia.Suspendida;
            agencia.MotivoSuspension = razon;
            agencia.FechaSuspension = DateTime.Now;

            // Pausar todos los anuncios activos
            var anunciosActivos = await _context.Anuncios
                .Where(a => a.AgenciaId == id && a.Estado == EstadoAnuncio.Activo)
                .ToListAsync();

            foreach (var anuncio in anunciosActivos)
            {
                anuncio.Estado = EstadoAnuncio.Pausado;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"La agencia '{agencia.NombreEmpresa}' ha sido suspendida.";
            return RedirectToAction(nameof(Agencias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivarAgencia(int id)
        {
            var agencia = await _context.Agencias.FindAsync(id);
            if (agencia == null)
            {
                TempData["Error"] = "Agencia no encontrada.";
                return RedirectToAction(nameof(Agencias));
            }

            agencia.Estado = EstadoAgencia.Activa;
            agencia.MotivoSuspension = null;
            agencia.FechaSuspension = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"La agencia '{agencia.NombreEmpresa}' ha sido reactivada.";
            return RedirectToAction(nameof(Agencias));
        }

        // ========================================
        // GESTION DE ANUNCIOS (desde Admin)
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarAnuncio(int id)
        {
            var anuncio = await _context.Anuncios.FindAsync(id);
            if (anuncio == null)
            {
                TempData["Error"] = "Anuncio no encontrado.";
                return RedirectToAction(nameof(Agencias));
            }

            anuncio.Estado = EstadoAnuncio.Activo;
            anuncio.FechaInicio = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"El anuncio '{anuncio.Titulo}' ha sido aprobado y esta activo.";
            return RedirectToAction(nameof(DetalleAgencia), new { id = anuncio.AgenciaId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarAnuncio(int id, string razon)
        {
            var anuncio = await _context.Anuncios.FindAsync(id);
            if (anuncio == null)
            {
                TempData["Error"] = "Anuncio no encontrado.";
                return RedirectToAction(nameof(Agencias));
            }

            anuncio.Estado = EstadoAnuncio.Rechazado;
            anuncio.MotivoRechazo = razon;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"El anuncio '{anuncio.Titulo}' ha sido rechazado.";
            return RedirectToAction(nameof(DetalleAgencia), new { id = anuncio.AgenciaId });
        }

        // ========================================
        // METRICAS DEL SERVIDOR
        // ========================================
        [HttpGet]
        public async Task<IActionResult> GetServerMetrics()
        {
            try
            {
                var metrics = await _serverMetrics.GetMetricsAsync();
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        cpu = new
                        {
                            usage = Math.Round(metrics.CpuUsagePercent, 1),
                            cores = metrics.ProcessorCount
                        },
                        memory = new
                        {
                            used = metrics.MemoryUsedMB,
                            total = metrics.MemoryTotalMB,
                            usagePercent = metrics.MemoryUsagePercent
                        },
                        disks = metrics.Disks.Select(d => new
                        {
                            name = d.DriveName,
                            label = d.DriveLabel,
                            totalGB = d.TotalGB,
                            usedGB = d.UsedGB,
                            freeGB = d.FreeGB,
                            usagePercent = d.UsagePercent,
                            format = d.DriveFormat
                        }),
                        server = new
                        {
                            name = metrics.ServerName,
                            os = metrics.OsDescription,
                            uptime = new
                            {
                                days = metrics.Uptime.Days,
                                hours = metrics.Uptime.Hours,
                                minutes = metrics.Uptime.Minutes
                            }
                        },
                        timestamp = metrics.Timestamp.ToString("HH:mm:ss")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        public IActionResult Servidor()
        {
            return View();
        }

        // ========================================
        // BIBLIOTECA MUSICAL
        // ========================================
        public async Task<IActionResult> Musica()
        {
            var pistas = await _context.PistasMusica
                .OrderByDescending(p => p.FechaCreacion)
                .ToListAsync();

            ViewBag.TotalPistas = pistas.Count;
            ViewBag.PistasActivas = pistas.Count(p => p.Activo);
            ViewBag.TotalGeneros = pistas.Select(p => p.Genero).Distinct().Count();
            ViewBag.TotalUsos = pistas.Sum(p => p.ContadorUsos);

            return View(pistas);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPista(int id)
        {
            var pista = await _context.PistasMusica.FindAsync(id);
            if (pista == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = pista.Id,
                titulo = pista.Titulo,
                artista = pista.Artista,
                album = pista.Album,
                genero = pista.Genero,
                duracion = pista.Duracion,
                bpm = pista.Bpm,
                energia = pista.Energia,
                estadoAnimo = pista.EstadoAnimo,
                rutaArchivo = pista.RutaArchivo,
                rutaPortada = pista.RutaPortada,
                activo = pista.Activo
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarPista(
            int Id,
            string Titulo,
            string Artista,
            string? Album,
            string Genero,
            int Duracion,
            int? Bpm,
            string? Energia,
            string? EstadoAnimo,
            IFormFile? ArchivoAudio,
            IFormFile? ImagenPortada)
        {
            try
            {
                PistaMusical pista;
                bool esNueva = Id == 0;

                if (esNueva)
                {
                    pista = new PistaMusical();
                }
                else
                {
                    pista = await _context.PistasMusica.FindAsync(Id);
                    if (pista == null)
                    {
                        TempData["Error"] = "Pista no encontrada.";
                        return RedirectToAction(nameof(Musica));
                    }
                }

                // Actualizar propiedades
                pista.Titulo = Titulo;
                pista.Artista = Artista;
                pista.Album = Album;
                pista.Genero = Genero;
                pista.Duracion = Duracion;
                pista.Bpm = Bpm;
                pista.Energia = Energia;
                pista.EstadoAnimo = EstadoAnimo;

                // Procesar archivo de audio
                if (ArchivoAudio != null && ArchivoAudio.Length > 0)
                {
                    var audioFileName = $"{Guid.NewGuid()}{Path.GetExtension(ArchivoAudio.FileName)}";
                    var audioPath = Path.Combine("wwwroot", "audio", "biblioteca", audioFileName);

                    // Crear directorio si no existe
                    var audioDir = Path.GetDirectoryName(audioPath);
                    if (!string.IsNullOrEmpty(audioDir) && !Directory.Exists(audioDir))
                    {
                        Directory.CreateDirectory(audioDir);
                    }

                    using (var stream = new FileStream(audioPath, FileMode.Create))
                    {
                        await ArchivoAudio.CopyToAsync(stream);
                    }

                    // Eliminar archivo anterior si existe
                    if (!string.IsNullOrEmpty(pista.RutaArchivo))
                    {
                        var oldPath = Path.Combine("wwwroot", pista.RutaArchivo.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    pista.RutaArchivo = $"/audio/biblioteca/{audioFileName}";
                }
                else if (esNueva)
                {
                    TempData["Error"] = "El archivo de audio es requerido para una nueva pista.";
                    return RedirectToAction(nameof(Musica));
                }

                // Procesar imagen de portada
                if (ImagenPortada != null && ImagenPortada.Length > 0)
                {
                    var coverFileName = $"{Guid.NewGuid()}{Path.GetExtension(ImagenPortada.FileName)}";
                    var coverPath = Path.Combine("wwwroot", "audio", "biblioteca", "covers", coverFileName);

                    // Crear directorio si no existe
                    var coverDir = Path.GetDirectoryName(coverPath);
                    if (!string.IsNullOrEmpty(coverDir) && !Directory.Exists(coverDir))
                    {
                        Directory.CreateDirectory(coverDir);
                    }

                    using (var stream = new FileStream(coverPath, FileMode.Create))
                    {
                        await ImagenPortada.CopyToAsync(stream);
                    }

                    // Eliminar portada anterior si existe
                    if (!string.IsNullOrEmpty(pista.RutaPortada))
                    {
                        var oldPath = Path.Combine("wwwroot", pista.RutaPortada.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    pista.RutaPortada = $"/audio/biblioteca/covers/{coverFileName}";
                }

                if (esNueva)
                {
                    pista.FechaCreacion = DateTime.UtcNow;
                    _context.PistasMusica.Add(pista);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = esNueva ? "Pista agregada exitosamente." : "Pista actualizada exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al guardar la pista: {ex.Message}";
            }

            return RedirectToAction(nameof(Musica));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPista(int id)
        {
            var pista = await _context.PistasMusica.FindAsync(id);
            if (pista == null)
            {
                TempData["Error"] = "Pista no encontrada.";
                return RedirectToAction(nameof(Musica));
            }

            try
            {
                // Eliminar archivo de audio
                if (!string.IsNullOrEmpty(pista.RutaArchivo))
                {
                    var audioPath = Path.Combine("wwwroot", pista.RutaArchivo.TrimStart('/'));
                    if (System.IO.File.Exists(audioPath))
                    {
                        System.IO.File.Delete(audioPath);
                    }
                }

                // Eliminar imagen de portada
                if (!string.IsNullOrEmpty(pista.RutaPortada))
                {
                    var coverPath = Path.Combine("wwwroot", pista.RutaPortada.TrimStart('/'));
                    if (System.IO.File.Exists(coverPath))
                    {
                        System.IO.File.Delete(coverPath);
                    }
                }

                _context.PistasMusica.Remove(pista);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Pista '{pista.Titulo}' eliminada exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar la pista: {ex.Message}";
            }

            return RedirectToAction(nameof(Musica));
        }

        [HttpPost]
        public async Task<IActionResult> TogglePistaEstado([FromBody] TogglePistaRequest request)
        {
            var pista = await _context.PistasMusica.FindAsync(request.Id);
            if (pista == null)
            {
                return Json(new { success = false, message = "Pista no encontrada" });
            }

            pista.Activo = request.Activo;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class TogglePistaRequest
        {
            public int Id { get; set; }
            public bool Activo { get; set; }
        }

        // ========================================
        // IMPORTADOR DE MÚSICA
        // ========================================
        public IActionResult ImportarMusica()
        {
            // Leer Client ID de Jamendo desde configuración
            ViewBag.JamendoClientId = _configuration["Jamendo:ClientId"] ?? "";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EscanearCarpetaMusica([FromBody] EscanearCarpetaRequest request)
        {
            try
            {
                var carpetaRuta = Path.Combine(Directory.GetCurrentDirectory(), request.Carpeta);

                if (!Directory.Exists(carpetaRuta))
                {
                    // Crear la carpeta si no existe
                    Directory.CreateDirectory(carpetaRuta);
                    return Json(new { success = false, message = $"La carpeta fue creada. Coloca archivos MP3 en: {carpetaRuta}" });
                }

                var archivosMP3 = Directory.GetFiles(carpetaRuta, "*.mp3", SearchOption.TopDirectoryOnly);

                if (archivosMP3.Length == 0)
                {
                    return Json(new { success = false, message = "No se encontraron archivos MP3 en la carpeta." });
                }

                var tracks = new List<object>();

                foreach (var archivo in archivosMP3)
                {
                    try
                    {
                        using var tagFile = TagLib.File.Create(archivo);
                        var tag = tagFile.Tag;

                        string? portadaBase64 = null;
                        if (tag.Pictures?.Length > 0)
                        {
                            var picture = tag.Pictures[0];
                            portadaBase64 = Convert.ToBase64String(picture.Data.Data);
                        }

                        // Detectar género desde tag o inferir del nombre
                        var genero = !string.IsNullOrEmpty(tag.FirstGenre)
                            ? MapearGenero(tag.FirstGenre)
                            : "Pop";

                        tracks.Add(new
                        {
                            archivo = Path.GetFileName(archivo),
                            rutaCompleta = archivo,
                            rutaTemporal = $"/audio/importar/{Path.GetFileName(archivo)}",
                            titulo = !string.IsNullOrEmpty(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(archivo),
                            artista = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer : "Artista Desconocido",
                            album = tag.Album,
                            genero = genero,
                            duracion = (int)tagFile.Properties.Duration.TotalSeconds,
                            bpm = tag.BeatsPerMinute > 0 ? (int?)tag.BeatsPerMinute : null,
                            anio = tag.Year > 0 ? (int?)tag.Year : null,
                            portadaBase64 = portadaBase64
                        });
                    }
                    catch (Exception)
                    {
                        // Si no se puede leer el archivo, agregar con datos básicos
                        tracks.Add(new
                        {
                            archivo = Path.GetFileName(archivo),
                            rutaCompleta = archivo,
                            rutaTemporal = $"/audio/importar/{Path.GetFileName(archivo)}",
                            titulo = Path.GetFileNameWithoutExtension(archivo),
                            artista = "Artista Desconocido",
                            album = (string?)null,
                            genero = "Pop",
                            duracion = 0,
                            bpm = (int?)null,
                            anio = (int?)null,
                            portadaBase64 = (string?)null
                        });
                    }
                }

                return Json(new { success = true, tracks = tracks });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al escanear: {ex.Message}" });
            }
        }

        private string MapearGenero(string generoOriginal)
        {
            var generoLower = generoOriginal.ToLower();

            // Mapear géneros comunes a los géneros del sistema
            if (generoLower.Contains("pop")) return "Pop";
            if (generoLower.Contains("rock")) return "Rock";
            if (generoLower.Contains("hip") || generoLower.Contains("hop") || generoLower.Contains("rap")) return "Hip Hop";
            if (generoLower.Contains("electro") || generoLower.Contains("edm") || generoLower.Contains("house") || generoLower.Contains("techno")) return "Electronica";
            if (generoLower.Contains("jazz")) return "Jazz";
            if (generoLower.Contains("classic")) return "Clasica";
            if (generoLower.Contains("reggae")) return "Reggaeton";
            if (generoLower.Contains("latin") || generoLower.Contains("salsa") || generoLower.Contains("bachata")) return "Latino";
            if (generoLower.Contains("indie")) return "Indie";
            if (generoLower.Contains("ambient") || generoLower.Contains("chill")) return "Ambiental";
            if (generoLower.Contains("acoustic") || generoLower.Contains("folk")) return "Acustico";
            if (generoLower.Contains("lofi") || generoLower.Contains("lo-fi")) return "Lo-Fi";
            if (generoLower.Contains("cinema") || generoLower.Contains("soundtrack") || generoLower.Contains("epic")) return "Cinematico";
            if (generoLower.Contains("r&b") || generoLower.Contains("soul")) return "R&B";
            if (generoLower.Contains("country")) return "Country";
            if (generoLower.Contains("metal")) return "Metal";
            if (generoLower.Contains("funk")) return "Funk";

            return "Pop"; // Default
        }

        [HttpPost]
        public async Task<IActionResult> ImportarPistaEscaneada([FromBody] ImportarPistaRequest request)
        {
            try
            {
                // Mover archivo de importar a biblioteca
                var nombreArchivo = $"{Guid.NewGuid()}.mp3";
                var rutaDestino = Path.Combine("wwwroot", "audio", "biblioteca", nombreArchivo);
                var rutaOrigen = request.RutaCompleta;

                // Crear directorio si no existe
                var dirDestino = Path.GetDirectoryName(rutaDestino);
                if (!string.IsNullOrEmpty(dirDestino) && !Directory.Exists(dirDestino))
                {
                    Directory.CreateDirectory(dirDestino);
                }

                // Copiar archivo (no mover, por si quiere reimportar)
                System.IO.File.Copy(rutaOrigen, rutaDestino, true);

                // Guardar portada si existe
                string? rutaPortada = null;
                if (!string.IsNullOrEmpty(request.PortadaBase64))
                {
                    var nombrePortada = $"{Guid.NewGuid()}.jpg";
                    var rutaPortadaDestino = Path.Combine("wwwroot", "audio", "biblioteca", "covers", nombrePortada);

                    var dirCovers = Path.GetDirectoryName(rutaPortadaDestino);
                    if (!string.IsNullOrEmpty(dirCovers) && !Directory.Exists(dirCovers))
                    {
                        Directory.CreateDirectory(dirCovers);
                    }

                    var bytesPortada = Convert.FromBase64String(request.PortadaBase64);
                    await System.IO.File.WriteAllBytesAsync(rutaPortadaDestino, bytesPortada);
                    rutaPortada = $"/audio/biblioteca/covers/{nombrePortada}";
                }

                // Crear registro en base de datos
                var pista = new PistaMusical
                {
                    Titulo = request.Titulo ?? "Sin Titulo",
                    Artista = request.Artista ?? "Artista Desconocido",
                    Album = request.Album,
                    Genero = request.Genero ?? "Pop",
                    Duracion = request.Duracion,
                    Bpm = request.Bpm,
                    RutaArchivo = $"/audio/biblioteca/{nombreArchivo}",
                    RutaPortada = rutaPortada,
                    EsLibreDeRegalias = true,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.PistasMusica.Add(pista);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = pista.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // JAMENDO API
        // ========================================
        [HttpPost]
        public IActionResult GuardarJamendoClientId([FromBody] JamendoClientIdRequest request)
        {
            // En producción guardarías esto en la base de datos o en un archivo de configuración seguro
            // Por ahora solo confirmamos que se recibió
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> BuscarJamendoMusic([FromBody] JamendoSearchRequest request)
        {
            try
            {
                using var httpClient = new HttpClient();

                var queryParams = new List<string>
                {
                    $"client_id={request.ClientId}",
                    "format=json",
                    "limit=50",
                    "include=musicinfo",
                    "audioformat=mp32"
                };

                if (!string.IsNullOrEmpty(request.Query))
                {
                    queryParams.Add($"namesearch={Uri.EscapeDataString(request.Query)}");
                }

                if (!string.IsNullOrEmpty(request.Tags))
                {
                    queryParams.Add($"tags={Uri.EscapeDataString(request.Tags)}");
                }

                var url = $"https://api.jamendo.com/v3.0/tracks/?{string.Join("&", queryParams)}";

                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Error de API: {response.StatusCode}" });
                }

                var content = await response.Content.ReadAsStringAsync();
                var jamendoResponse = System.Text.Json.JsonSerializer.Deserialize<JamendoMusicResponse>(content,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (jamendoResponse?.Results == null || jamendoResponse.Results.Count == 0)
                {
                    return Json(new { success = false, message = "No se encontraron resultados." });
                }

                var tracks = jamendoResponse.Results.Select(r => new
                {
                    id = r.Id,
                    title = r.Name ?? "Sin titulo",
                    artistName = r.Artist_name ?? "Desconocido",
                    albumName = r.Album_name,
                    albumImage = r.Album_image,
                    duration = r.Duration,
                    audioUrl = r.Audio,
                    audioDownload = r.Audiodownload,
                    license = r.License_ccurl,
                    releaseDate = r.Releasedate
                }).ToList();

                return Json(new { success = true, tracks = tracks });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DescargarJamendoTrack([FromBody] JamendoTrackDownload request)
        {
            try
            {
                using var httpClient = new HttpClient();

                // Descargar el archivo de audio
                var audioBytes = await httpClient.GetByteArrayAsync(request.AudioDownload);

                // Generar nombre único
                var nombreArchivo = $"{Guid.NewGuid()}.mp3";
                var rutaArchivo = Path.Combine("wwwroot", "audio", "biblioteca", nombreArchivo);

                // Crear directorio si no existe
                var dirDestino = Path.GetDirectoryName(rutaArchivo);
                if (!string.IsNullOrEmpty(dirDestino) && !Directory.Exists(dirDestino))
                {
                    Directory.CreateDirectory(dirDestino);
                }

                // Guardar archivo de audio
                await System.IO.File.WriteAllBytesAsync(rutaArchivo, audioBytes);

                // Descargar y guardar portada si existe
                string? rutaPortada = null;
                if (!string.IsNullOrEmpty(request.AlbumImage))
                {
                    try
                    {
                        var coverBytes = await httpClient.GetByteArrayAsync(request.AlbumImage);
                        var nombrePortada = $"{Guid.NewGuid()}.jpg";
                        var rutaPortadaArchivo = Path.Combine("wwwroot", "audio", "biblioteca", "covers", nombrePortada);

                        var dirCovers = Path.GetDirectoryName(rutaPortadaArchivo);
                        if (!string.IsNullOrEmpty(dirCovers) && !Directory.Exists(dirCovers))
                        {
                            Directory.CreateDirectory(dirCovers);
                        }

                        await System.IO.File.WriteAllBytesAsync(rutaPortadaArchivo, coverBytes);
                        rutaPortada = $"/audio/biblioteca/covers/{nombrePortada}";
                    }
                    catch
                    {
                        // Si falla la descarga de portada, continuar sin ella
                    }
                }

                // Crear registro en base de datos
                var pista = new PistaMusical
                {
                    Titulo = request.Title ?? "Sin Titulo",
                    Artista = request.ArtistName ?? "Jamendo",
                    Album = request.AlbumName,
                    Genero = "Pop", // Jamendo no siempre proporciona género
                    Duracion = request.Duration,
                    RutaArchivo = $"/audio/biblioteca/{nombreArchivo}",
                    RutaPortada = rutaPortada,
                    EsLibreDeRegalias = true,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.PistasMusica.Add(pista);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = pista.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al descargar: {ex.Message}" });
            }
        }

        // ========================================
        // ACTUALIZAR PISTAS DESDE EXCEL
        // ========================================

        /// <summary>
        /// Actualiza las pistas existentes con datos del Excel (artist_name, popularity, genre)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ActualizarPistasDesdeExcel(IFormFile archivoExcel)
        {
            if (archivoExcel == null || archivoExcel.Length == 0)
            {
                return Json(new { success = false, message = "No se recibió el archivo Excel" });
            }

            try
            {
                var actualizadas = 0;
                var noEncontradas = new List<string>();
                var errores = new List<string>();

                using var stream = archivoExcel.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Obtener todas las pistas de la base de datos
                var pistasDb = await _context.PistasMusica.ToListAsync();

                // Leer filas del Excel (empezando desde la fila 2, la 1 es el header)
                var lastRowUsed = worksheet.LastRowUsed()?.RowNumber() ?? 1;

                for (int row = 2; row <= lastRowUsed; row++)
                {
                    try
                    {
                        var trackName = worksheet.Cell(row, 2).GetString()?.Trim(); // track_name
                        var artistName = worksheet.Cell(row, 4).GetString()?.Trim(); // artist_name
                        var popularityStr = worksheet.Cell(row, 8).GetString()?.Trim(); // popularity
                        var genre = worksheet.Cell(row, 23).GetString()?.Trim(); // genre

                        if (string.IsNullOrEmpty(trackName))
                            continue;

                        // Buscar pista por título (case-insensitive y parcial)
                        var pista = pistasDb.FirstOrDefault(p =>
                            p.Titulo.Equals(trackName, StringComparison.OrdinalIgnoreCase) ||
                            p.Titulo.Contains(trackName, StringComparison.OrdinalIgnoreCase) ||
                            trackName.Contains(p.Titulo, StringComparison.OrdinalIgnoreCase));

                        if (pista == null)
                        {
                            // No agregar todos a la lista, solo los primeros 20
                            if (noEncontradas.Count < 20)
                                noEncontradas.Add(trackName);
                            continue;
                        }

                        // Actualizar campos
                        var cambios = false;

                        if (!string.IsNullOrEmpty(artistName) && pista.Artista != artistName)
                        {
                            pista.Artista = artistName;
                            cambios = true;
                        }

                        if (int.TryParse(popularityStr, out int popularity) && pista.ContadorUsos != popularity)
                        {
                            pista.ContadorUsos = popularity;
                            cambios = true;
                        }

                        if (!string.IsNullOrEmpty(genre))
                        {
                            var generoMapeado = MapearGeneroExcel(genre);
                            if (pista.Genero != generoMapeado)
                            {
                                pista.Genero = generoMapeado;
                                cambios = true;
                            }
                        }

                        if (cambios)
                            actualizadas++;
                    }
                    catch (Exception ex)
                    {
                        if (errores.Count < 10)
                            errores.Add($"Fila {row}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Se actualizaron {actualizadas} pistas",
                    actualizadas,
                    noEncontradas = noEncontradas.Take(20).ToList(),
                    totalNoEncontradas = noEncontradas.Count,
                    errores
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al procesar Excel: {ex.Message}" });
            }
        }

        /// <summary>
        /// Mapea géneros del Excel a los géneros del sistema
        /// </summary>
        private string MapearGeneroExcel(string generoExcel)
        {
            if (string.IsNullOrEmpty(generoExcel))
                return "Pop";

            var generoLower = generoExcel.ToLower();

            return generoLower switch
            {
                var g when g.Contains("tiktok") || g.Contains("dance") || g.Contains("edm") => "Electrónica",
                var g when g.Contains("hip hop") || g.Contains("hiphop") || g.Contains("rap") => "Hip Hop",
                var g when g.Contains("r&b") || g.Contains("rnb") || g.Contains("soul") => "R&B",
                var g when g.Contains("rock") => "Rock",
                var g when g.Contains("latin") || g.Contains("latino") => "Latino",
                var g when g.Contains("reggaeton") || g.Contains("reggaetón") => "Reggaetón",
                var g when g.Contains("indie") || g.Contains("alternative") => "Indie",
                var g when g.Contains("lofi") || g.Contains("lo-fi") || g.Contains("chill") => "Lo-Fi",
                var g when g.Contains("ambient") || g.Contains("ambiental") => "Ambiental",
                var g when g.Contains("acoustic") || g.Contains("acústico") => "Acústico",
                var g when g.Contains("cinematic") || g.Contains("film") || g.Contains("movie") => "Cinemático",
                var g when g.Contains("electronic") || g.Contains("house") || g.Contains("techno") => "Electrónica",
                var g when g.Contains("pop") => "Pop",
                _ => "Pop" // Default
            };
        }

        // Request/Response classes para el importador
        public class EscanearCarpetaRequest
        {
            public string Carpeta { get; set; } = "";
        }

        public class ImportarPistaRequest
        {
            public string? Archivo { get; set; }
            public string RutaCompleta { get; set; } = "";
            public string? Titulo { get; set; }
            public string? Artista { get; set; }
            public string? Album { get; set; }
            public string? Genero { get; set; }
            public int Duracion { get; set; }
            public int? Bpm { get; set; }
            public string? PortadaBase64 { get; set; }
        }

        public class JamendoClientIdRequest
        {
            public string ClientId { get; set; } = "";
        }

        public class JamendoSearchRequest
        {
            public string ClientId { get; set; } = "";
            public string? Query { get; set; }
            public string? Tags { get; set; }
        }

        public class JamendoTrackDownload
        {
            public string Id { get; set; } = "";
            public string? Title { get; set; }
            public string? ArtistName { get; set; }
            public string? AlbumName { get; set; }
            public string? AlbumImage { get; set; }
            public int Duration { get; set; }
            public string AudioDownload { get; set; } = "";
        }

        public class JamendoMusicResponse
        {
            public JamendoHeaders? Headers { get; set; }
            public List<JamendoTrack> Results { get; set; } = new();
        }

        public class JamendoHeaders
        {
            public string? Status { get; set; }
            public int Code { get; set; }
            public string? Error_message { get; set; }
            public int Results_count { get; set; }
        }

        public class JamendoTrack
        {
            public string Id { get; set; } = "";
            public string? Name { get; set; }
            public int Duration { get; set; }
            public string? Artist_id { get; set; }
            public string? Artist_name { get; set; }
            public string? Album_name { get; set; }
            public string? Album_id { get; set; }
            public string? Album_image { get; set; }
            public string? License_ccurl { get; set; }
            public string? Audio { get; set; }
            public string? Audiodownload { get; set; }
            public string? Releasedate { get; set; }
        }

        // ========================================
        // GESTION DE FEEDBACKS
        // ========================================
        public async Task<IActionResult> Feedbacks(EstadoFeedback? estado = null)
        {
            var query = _context.Feedbacks
                .Include(f => f.Usuario)
                .AsQueryable();

            if (estado.HasValue)
            {
                query = query.Where(f => f.Estado == estado.Value);
            }

            var feedbacks = await query
                .OrderByDescending(f => f.FechaEnvio)
                .ToListAsync();

            ViewBag.FeedbacksPendientes = await _context.Feedbacks.CountAsync(f => f.Estado == EstadoFeedback.Pendiente);
            ViewBag.FeedbacksEnRevision = await _context.Feedbacks.CountAsync(f => f.Estado == EstadoFeedback.EnRevision);
            ViewBag.FeedbacksRespondidos = await _context.Feedbacks.CountAsync(f => f.Estado == EstadoFeedback.Respondido);
            ViewBag.TotalFeedbacks = await _context.Feedbacks.CountAsync();
            ViewBag.EstadoFiltro = estado;

            return View(feedbacks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoFeedback(int id, EstadoFeedback estado)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                TempData["Error"] = "Feedback no encontrado.";
                return RedirectToAction(nameof(Feedbacks));
            }

            feedback.Estado = estado;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Estado del feedback actualizado.";
            return RedirectToAction(nameof(Feedbacks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResponderFeedback(int id, string respuesta)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                TempData["Error"] = "Feedback no encontrado.";
                return RedirectToAction(nameof(Feedbacks));
            }

            if (string.IsNullOrWhiteSpace(respuesta))
            {
                TempData["Error"] = "La respuesta no puede estar vacia.";
                return RedirectToAction(nameof(Feedbacks));
            }

            var admin = await _userManager.GetUserAsync(User);

            feedback.RespuestaAdmin = respuesta;
            feedback.FechaRespuesta = DateTime.Now;
            feedback.AdminId = admin?.Id;
            feedback.Estado = EstadoFeedback.Respondido;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Respuesta enviada exitosamente.";
            return RedirectToAction(nameof(Feedbacks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFeedback(int id)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                TempData["Error"] = "Feedback no encontrado.";
                return RedirectToAction(nameof(Feedbacks));
            }

            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Feedback eliminado.";
            return RedirectToAction(nameof(Feedbacks));
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerFeedback(int id)
        {
            var feedback = await _context.Feedbacks
                .Include(f => f.Usuario)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = feedback.Id,
                nombreUsuario = feedback.NombreUsuario,
                email = feedback.Email,
                tipo = feedback.Tipo.ToString(),
                asunto = feedback.Asunto,
                mensaje = feedback.Mensaje,
                fechaEnvio = feedback.FechaEnvio.ToString("dd/MM/yyyy HH:mm"),
                estado = feedback.Estado.ToString(),
                respuestaAdmin = feedback.RespuestaAdmin,
                fechaRespuesta = feedback.FechaRespuesta?.ToString("dd/MM/yyyy HH:mm"),
                usuarioId = feedback.UsuarioId,
                usuarioFoto = feedback.Usuario?.FotoPerfil
            });
        }

        // ========================================
        // PUBLICIDAD DE LADO
        // ========================================

        /// <summary>
        /// Panel de gestión de publicidad propia de Lado
        /// </summary>
        public async Task<IActionResult> Publicidad()
        {
            var anunciosLado = await _context.Anuncios
                .Where(a => a.EsAnuncioLado)
                .OrderByDescending(a => a.FechaCreacion)
                .ToListAsync();

            // Estadísticas
            ViewBag.TotalAnuncios = anunciosLado.Count;
            ViewBag.AnunciosActivos = anunciosLado.Count(a => a.Estado == EstadoAnuncio.Activo);
            ViewBag.TotalImpresiones = anunciosLado.Sum(a => a.Impresiones);
            ViewBag.TotalClics = anunciosLado.Sum(a => a.Clics);

            // Estadísticas de anuncios de agencias para comparación
            var anunciosAgencias = await _context.Anuncios
                .Where(a => !a.EsAnuncioLado)
                .ToListAsync();
            ViewBag.AnunciosAgencias = anunciosAgencias.Count;
            ViewBag.ImpresionesAgencias = anunciosAgencias.Sum(a => a.Impresiones);

            return View(anunciosLado);
        }

        /// <summary>
        /// Obtiene los datos de un anuncio de Lado para edición
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerAnuncioLado(int id)
        {
            var anuncio = await _context.Anuncios.FindAsync(id);
            if (anuncio == null || !anuncio.EsAnuncioLado)
            {
                return NotFound();
            }

            return Json(new
            {
                id = anuncio.Id,
                titulo = anuncio.Titulo,
                descripcion = anuncio.Descripcion,
                urlDestino = anuncio.UrlDestino,
                urlCreativo = anuncio.UrlCreativo,
                tipoCreativo = anuncio.TipoCreativo.ToString(),
                textoBoton = anuncio.TextoBoton.ToString(),
                textoBotonPersonalizado = anuncio.TextoBotonPersonalizado,
                prioridad = anuncio.Prioridad,
                estado = anuncio.Estado.ToString(),
                fechaInicio = anuncio.FechaInicio?.ToString("yyyy-MM-dd"),
                fechaFin = anuncio.FechaFin?.ToString("yyyy-MM-dd"),
                impresiones = anuncio.Impresiones,
                clics = anuncio.Clics
            });
        }

        /// <summary>
        /// Crea o actualiza un anuncio de Lado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarAnuncioLado(
            int Id,
            string Titulo,
            string? Descripcion,
            string UrlDestino,
            int Prioridad,
            string TextoBoton,
            string? TextoBotonPersonalizado,
            string? FechaInicio,
            string? FechaFin,
            string Estado,
            IFormFile? ImagenCreativo)
        {
            try
            {
                Anuncio anuncio;
                bool esNuevo = Id == 0;

                if (esNuevo)
                {
                    anuncio = new Anuncio
                    {
                        EsAnuncioLado = true,
                        FechaCreacion = DateTime.Now
                    };
                }
                else
                {
                    anuncio = await _context.Anuncios.FindAsync(Id);
                    if (anuncio == null || !anuncio.EsAnuncioLado)
                    {
                        TempData["Error"] = "Anuncio no encontrado.";
                        return RedirectToAction(nameof(Publicidad));
                    }
                }

                // Actualizar propiedades
                anuncio.Titulo = Titulo;
                anuncio.Descripcion = Descripcion;
                anuncio.UrlDestino = UrlDestino;
                anuncio.Prioridad = Prioridad;
                anuncio.UltimaActualizacion = DateTime.Now;

                // Texto del botón
                if (Enum.TryParse<TextoBotonAnuncio>(TextoBoton, out var textoBotonEnum))
                {
                    anuncio.TextoBoton = textoBotonEnum;
                }
                anuncio.TextoBotonPersonalizado = TextoBotonPersonalizado;

                // Fechas
                if (!string.IsNullOrEmpty(FechaInicio) && DateTime.TryParse(FechaInicio, out var fechaInicioDate))
                {
                    anuncio.FechaInicio = fechaInicioDate;
                }
                if (!string.IsNullOrEmpty(FechaFin) && DateTime.TryParse(FechaFin, out var fechaFinDate))
                {
                    anuncio.FechaFin = fechaFinDate;
                }

                // Estado
                if (Enum.TryParse<EstadoAnuncio>(Estado, out var estadoEnum))
                {
                    anuncio.Estado = estadoEnum;
                }

                // Procesar imagen del creativo
                if (ImagenCreativo != null && ImagenCreativo.Length > 0)
                {
                    var nombreArchivo = $"anuncio_lado_{Guid.NewGuid()}{Path.GetExtension(ImagenCreativo.FileName)}";
                    var rutaArchivo = Path.Combine("wwwroot", "uploads", "anuncios", nombreArchivo);

                    // Crear directorio si no existe
                    var directorio = Path.GetDirectoryName(rutaArchivo);
                    if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                    {
                        Directory.CreateDirectory(directorio);
                    }

                    // Eliminar archivo anterior si existe
                    if (!string.IsNullOrEmpty(anuncio.UrlCreativo))
                    {
                        var rutaAnterior = Path.Combine("wwwroot", anuncio.UrlCreativo.TrimStart('/'));
                        if (System.IO.File.Exists(rutaAnterior))
                        {
                            System.IO.File.Delete(rutaAnterior);
                        }
                    }

                    using (var stream = new FileStream(rutaArchivo, FileMode.Create))
                    {
                        await ImagenCreativo.CopyToAsync(stream);
                    }

                    anuncio.UrlCreativo = $"/uploads/anuncios/{nombreArchivo}";

                    // Determinar tipo de creativo
                    var extension = Path.GetExtension(ImagenCreativo.FileName).ToLower();
                    anuncio.TipoCreativo = extension switch
                    {
                        ".mp4" or ".webm" or ".mov" => TipoCreativo.Video,
                        _ => TipoCreativo.Imagen
                    };
                }
                else if (esNuevo)
                {
                    TempData["Error"] = "La imagen del creativo es requerida para un nuevo anuncio.";
                    return RedirectToAction(nameof(Publicidad));
                }

                if (esNuevo)
                {
                    _context.Anuncios.Add(anuncio);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = esNuevo ? "Anuncio creado exitosamente." : "Anuncio actualizado exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al guardar el anuncio: {ex.Message}";
            }

            return RedirectToAction(nameof(Publicidad));
        }

        /// <summary>
        /// Cambia el estado de un anuncio de Lado (activar/pausar)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleEstadoAnuncioLado([FromBody] ToggleAnuncioRequest request)
        {
            var anuncio = await _context.Anuncios.FindAsync(request.Id);
            if (anuncio == null || !anuncio.EsAnuncioLado)
            {
                return Json(new { success = false, message = "Anuncio no encontrado" });
            }

            anuncio.Estado = request.Activo ? EstadoAnuncio.Activo : EstadoAnuncio.Pausado;
            anuncio.UltimaActualizacion = DateTime.Now;

            if (request.Activo && anuncio.FechaInicio == null)
            {
                anuncio.FechaInicio = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class ToggleAnuncioRequest
        {
            public int Id { get; set; }
            public bool Activo { get; set; }
        }

        /// <summary>
        /// Elimina un anuncio de Lado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarAnuncioLado(int id)
        {
            var anuncio = await _context.Anuncios.FindAsync(id);
            if (anuncio == null || !anuncio.EsAnuncioLado)
            {
                TempData["Error"] = "Anuncio no encontrado.";
                return RedirectToAction(nameof(Publicidad));
            }

            try
            {
                // Eliminar archivo del creativo
                if (!string.IsNullOrEmpty(anuncio.UrlCreativo))
                {
                    var rutaArchivo = Path.Combine("wwwroot", anuncio.UrlCreativo.TrimStart('/'));
                    if (System.IO.File.Exists(rutaArchivo))
                    {
                        System.IO.File.Delete(rutaArchivo);
                    }
                }

                // Eliminar registros de impresiones y clics asociados
                var impresiones = await _context.ImpresionesAnuncios
                    .Where(i => i.AnuncioId == id)
                    .ToListAsync();
                if (impresiones.Any())
                {
                    _context.ImpresionesAnuncios.RemoveRange(impresiones);
                }

                var clics = await _context.ClicsAnuncios
                    .Where(c => c.AnuncioId == id)
                    .ToListAsync();
                if (clics.Any())
                {
                    _context.ClicsAnuncios.RemoveRange(clics);
                }

                _context.Anuncios.Remove(anuncio);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Anuncio '{anuncio.Titulo}' eliminado exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar el anuncio: {ex.Message}";
            }

            return RedirectToAction(nameof(Publicidad));
        }

        // ========================================
        // RETIROS
        // ========================================

        /// <summary>
        /// Panel de gestión de solicitudes de retiro
        /// </summary>
        public async Task<IActionResult> Retiros(string filtro = "pendientes")
        {
            var query = _context.Transacciones
                .Include(t => t.Usuario)
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro);

            // Aplicar filtro
            switch (filtro.ToLower())
            {
                case "pendientes":
                    query = query.Where(t => t.EstadoPago == "Pendiente");
                    break;
                case "aprobados":
                    query = query.Where(t => t.EstadoPago == "Completado" || t.EstadoPago == "Aprobado");
                    break;
                case "rechazados":
                    query = query.Where(t => t.EstadoPago == "Rechazado");
                    break;
                // "todos" no aplica filtro adicional
            }

            var retiros = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .ToListAsync();

            // Estadísticas
            ViewBag.TotalPendientes = await _context.Transacciones
                .CountAsync(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoPago == "Pendiente");

            ViewBag.MontoPendiente = await _context.Transacciones
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoPago == "Pendiente")
                .SumAsync(t => (decimal?)t.MontoNeto) ?? 0;

            ViewBag.TotalAprobados = await _context.Transacciones
                .CountAsync(t => t.TipoTransaccion == TipoTransaccion.Retiro &&
                    (t.EstadoPago == "Completado" || t.EstadoPago == "Aprobado"));

            ViewBag.MontoAprobado = await _context.Transacciones
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro &&
                    (t.EstadoPago == "Completado" || t.EstadoPago == "Aprobado"))
                .SumAsync(t => (decimal?)t.MontoNeto) ?? 0;

            ViewBag.TotalRechazados = await _context.Transacciones
                .CountAsync(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoPago == "Rechazado");

            ViewBag.FiltroActual = filtro;

            return View(retiros);
        }

        /// <summary>
        /// Aprobar una solicitud de retiro
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarRetiro(int id, string? notas)
        {
            var retiro = await _context.Transacciones
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id && t.TipoTransaccion == TipoTransaccion.Retiro);

            if (retiro == null)
            {
                TempData["Error"] = "Retiro no encontrado.";
                return RedirectToAction(nameof(Retiros));
            }

            if (retiro.EstadoPago != "Pendiente")
            {
                TempData["Error"] = "Este retiro ya fue procesado.";
                return RedirectToAction(nameof(Retiros));
            }

            retiro.EstadoPago = "Completado";
            retiro.EstadoTransaccion = EstadoTransaccion.Completada;
            retiro.Notas = string.IsNullOrEmpty(notas) ? "Aprobado por administrador" : notas;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Retiro #{id} aprobado. Monto: ${retiro.MontoNeto:N2} para {retiro.Usuario?.NombreCompleto ?? "Usuario"}";
            return RedirectToAction(nameof(Retiros));
        }

        /// <summary>
        /// Rechazar una solicitud de retiro y devolver el saldo al usuario
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarRetiro(int id, string razon)
        {
            var retiro = await _context.Transacciones
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id && t.TipoTransaccion == TipoTransaccion.Retiro);

            if (retiro == null)
            {
                TempData["Error"] = "Retiro no encontrado.";
                return RedirectToAction(nameof(Retiros));
            }

            if (retiro.EstadoPago != "Pendiente")
            {
                TempData["Error"] = "Este retiro ya fue procesado.";
                return RedirectToAction(nameof(Retiros));
            }

            if (string.IsNullOrWhiteSpace(razon))
            {
                TempData["Error"] = "Debes especificar una razón para rechazar el retiro.";
                return RedirectToAction(nameof(Retiros));
            }

            // Devolver el monto al saldo del usuario
            if (retiro.Usuario != null)
            {
                retiro.Usuario.Saldo += retiro.Monto;
            }

            retiro.EstadoPago = "Rechazado";
            retiro.EstadoTransaccion = EstadoTransaccion.Cancelada;
            retiro.Notas = $"Rechazado: {razon}";

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Retiro #{id} rechazado. Se devolvió ${retiro.Monto:N2} al saldo de {retiro.Usuario?.NombreCompleto ?? "Usuario"}";
            return RedirectToAction(nameof(Retiros));
        }

        /// <summary>
        /// Obtener detalles de un retiro para el modal
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerRetiro(int id)
        {
            var retiro = await _context.Transacciones
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id && t.TipoTransaccion == TipoTransaccion.Retiro);

            if (retiro == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = retiro.Id,
                usuario = retiro.Usuario?.NombreCompleto ?? "Usuario",
                username = retiro.Usuario?.UserName,
                email = retiro.Usuario?.Email,
                foto = retiro.Usuario?.FotoPerfil,
                monto = retiro.Monto,
                comision = retiro.Comision,
                retencion = retiro.RetencionImpuestos,
                montoNeto = retiro.MontoNeto,
                metodoPago = retiro.MetodoPago,
                cuentaRetiro = retiro.Usuario?.CuentaRetiro,
                tipoCuenta = retiro.Usuario?.TipoCuentaRetiro,
                descripcion = retiro.Descripcion,
                fecha = retiro.FechaTransaccion.ToString("dd/MM/yyyy HH:mm"),
                estado = retiro.EstadoPago,
                notas = retiro.Notas
            });
        }

        // ========================================
        // TRANSACCIONES
        // ========================================

        /// <summary>
        /// Panel de todas las transacciones de la plataforma
        /// </summary>
        public async Task<IActionResult> Transacciones(
            string? buscar,
            string? tipo,
            string? estado,
            DateTime? desde,
            DateTime? hasta,
            int pagina = 1)
        {
            var query = _context.Transacciones
                .Include(t => t.Usuario)
                .AsQueryable();

            // Filtro por búsqueda (usuario)
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                query = query.Where(t => t.Usuario != null &&
                    (t.Usuario.NombreCompleto.Contains(buscar) ||
                     t.Usuario.UserName.Contains(buscar) ||
                     t.Usuario.Email.Contains(buscar)));
            }

            // Filtro por tipo
            if (!string.IsNullOrWhiteSpace(tipo) && Enum.TryParse<TipoTransaccion>(tipo, out var tipoEnum))
            {
                query = query.Where(t => t.TipoTransaccion == tipoEnum);
            }

            // Filtro por estado
            if (!string.IsNullOrWhiteSpace(estado))
            {
                query = query.Where(t => t.EstadoPago == estado);
            }

            // Filtro por fechas
            if (desde.HasValue)
            {
                query = query.Where(t => t.FechaTransaccion >= desde.Value);
            }
            if (hasta.HasValue)
            {
                query = query.Where(t => t.FechaTransaccion <= hasta.Value.AddDays(1));
            }

            // Estadísticas generales
            ViewBag.TotalTransacciones = await _context.Transacciones.CountAsync();
            ViewBag.IngresosTotales = await _context.Transacciones
                .Where(t => t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoTransaccion == EstadoTransaccion.Completada)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;
            ViewBag.RetirosTotales = await _context.Transacciones
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro &&
                            (t.EstadoPago == "Completado" || t.EstadoPago == "Aprobado"))
                .SumAsync(t => (decimal?)t.MontoNeto) ?? 0;
            ViewBag.ComisionesTotales = await _context.Transacciones
                .Where(t => t.Comision != null && t.EstadoTransaccion == EstadoTransaccion.Completada)
                .SumAsync(t => (decimal?)t.Comision) ?? 0;

            // Paginación
            var totalItems = await query.CountAsync();
            var itemsPorPagina = 50;
            var totalPaginas = (int)Math.Ceiling(totalItems / (double)itemsPorPagina);

            var transacciones = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .Skip((pagina - 1) * itemsPorPagina)
                .Take(itemsPorPagina)
                .ToListAsync();

            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalItems = totalItems;

            // Mantener filtros en la vista
            ViewBag.Buscar = buscar;
            ViewBag.Tipo = tipo;
            ViewBag.Estado = estado;
            ViewBag.Desde = desde?.ToString("yyyy-MM-dd");
            ViewBag.Hasta = hasta?.ToString("yyyy-MM-dd");

            // Lista de tipos de transacción para el filtro
            ViewBag.TiposTransaccion = Enum.GetValues<TipoTransaccion>()
                .Select(t => new { Valor = t.ToString(), Nombre = t.ToString() })
                .ToList();

            return View(transacciones);
        }

        /// <summary>
        /// Exportar transacciones a CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarTransacciones(
            string? tipo,
            string? estado,
            DateTime? desde,
            DateTime? hasta)
        {
            var query = _context.Transacciones
                .Include(t => t.Usuario)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(tipo) && Enum.TryParse<TipoTransaccion>(tipo, out var tipoEnum))
            {
                query = query.Where(t => t.TipoTransaccion == tipoEnum);
            }
            if (!string.IsNullOrWhiteSpace(estado))
            {
                query = query.Where(t => t.EstadoPago == estado);
            }
            if (desde.HasValue)
            {
                query = query.Where(t => t.FechaTransaccion >= desde.Value);
            }
            if (hasta.HasValue)
            {
                query = query.Where(t => t.FechaTransaccion <= hasta.Value.AddDays(1));
            }

            var transacciones = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Usuario,Email,Tipo,Monto,Comision,Retencion,MontoNeto,Estado,MetodoPago,Fecha,Descripcion");

            foreach (var t in transacciones)
            {
                csv.AppendLine($"{t.Id},{t.Usuario?.NombreCompleto ?? "N/A"},{t.Usuario?.Email ?? "N/A"},{t.TipoTransaccion},{t.Monto:F2},{t.Comision:F2},{t.RetencionImpuestos:F2},{t.MontoNeto:F2},{t.EstadoPago},{t.MetodoPago},{t.FechaTransaccion:yyyy-MM-dd HH:mm},\"{t.Descripcion?.Replace("\"", "\"\"")}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"transacciones_{DateTime.Now:yyyyMMdd}.csv");
        }

        // ========================================
        // ALGORITMOS DE FEED
        // ========================================

        public async Task<IActionResult> Algoritmos()
        {
            var algoritmos = await _context.AlgoritmosFeed
                .OrderBy(a => a.Orden)
                .ToListAsync();

            // Estadísticas de uso por algoritmo (últimos 7 días)
            var hace7Dias = DateTime.Now.AddDays(-7);
            var usosPorAlgoritmo = await _context.PreferenciasAlgoritmoUsuario
                .Where(p => p.FechaSeleccion >= hace7Dias)
                .GroupBy(p => p.AlgoritmoFeedId)
                .Select(g => new { AlgoritmoId = g.Key, Usuarios = g.Count() })
                .ToDictionaryAsync(x => x.AlgoritmoId, x => x.Usuarios);

            ViewBag.UsosPorAlgoritmo = usosPorAlgoritmo;

            // Total de usuarios que han seleccionado un algoritmo
            ViewBag.TotalUsuariosConPreferencia = await _context.PreferenciasAlgoritmoUsuario.CountAsync();

            return View(algoritmos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAlgoritmo(int id)
        {
            var algoritmo = await _context.AlgoritmosFeed.FindAsync(id);
            if (algoritmo != null)
            {
                // No permitir desactivar el algoritmo por defecto
                if (algoritmo.EsPorDefecto && algoritmo.Activo)
                {
                    TempData["Error"] = "No se puede desactivar el algoritmo por defecto. Primero establece otro como predeterminado.";
                    return RedirectToAction("Algoritmos");
                }

                algoritmo.Activo = !algoritmo.Activo;
                algoritmo.FechaModificacion = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Algoritmo '{algoritmo.Nombre}' {(algoritmo.Activo ? "activado" : "desactivado")}.";
            }
            return RedirectToAction("Algoritmos");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAlgoritmoPorDefecto(int id)
        {
            var algoritmo = await _context.AlgoritmosFeed.FindAsync(id);
            if (algoritmo != null && algoritmo.Activo)
            {
                // Quitar el flag de todos los demás
                var todos = await _context.AlgoritmosFeed.ToListAsync();
                foreach (var alg in todos)
                {
                    alg.EsPorDefecto = (alg.Id == id);
                    alg.FechaModificacion = DateTime.Now;
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = $"'{algoritmo.Nombre}' es ahora el algoritmo por defecto.";
            }
            else
            {
                TempData["Error"] = "El algoritmo debe estar activo para ser el predeterminado.";
            }
            return RedirectToAction("Algoritmos");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarOrdenAlgoritmo(int id, int nuevoOrden)
        {
            var algoritmo = await _context.AlgoritmosFeed.FindAsync(id);
            if (algoritmo != null)
            {
                algoritmo.Orden = nuevoOrden;
                algoritmo.FechaModificacion = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarDescripcionAlgoritmo(int id, string descripcion)
        {
            var algoritmo = await _context.AlgoritmosFeed.FindAsync(id);
            if (algoritmo != null)
            {
                algoritmo.Descripcion = descripcion;
                algoritmo.FechaModificacion = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Descripción actualizada.";
            }
            return RedirectToAction("Algoritmos");
        }

        [HttpGet]
        public async Task<IActionResult> EstadisticasAlgoritmos()
        {
            var algoritmos = await _context.AlgoritmosFeed
                .Where(a => a.Activo)
                .OrderBy(a => a.Orden)
                .ToListAsync();

            var resultado = new List<object>();

            foreach (var alg in algoritmos)
            {
                var usuariosActivos = await _context.PreferenciasAlgoritmoUsuario
                    .CountAsync(p => p.AlgoritmoFeedId == alg.Id);

                resultado.Add(new
                {
                    alg.Id,
                    alg.Nombre,
                    alg.Codigo,
                    alg.TotalUsos,
                    UsuariosActivos = usuariosActivos
                });
            }

            return Json(resultado);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RepararAlgoritmos()
        {
            // Verificar si existe el algoritmo cronologico
            var cronologico = await _context.AlgoritmosFeed
                .FirstOrDefaultAsync(a => a.Codigo == "cronologico");

            if (cronologico == null)
            {
                // Crear los 4 algoritmos si no existen
                var algoritmos = new List<Lado.Models.AlgoritmoFeed>
                {
                    new Lado.Models.AlgoritmoFeed
                    {
                        Codigo = "cronologico",
                        Nombre = "Cronologico",
                        Descripcion = "Muestra los posts ordenados por fecha de publicacion, los mas recientes primero",
                        Icono = "clock",
                        Activo = true,
                        EsPorDefecto = true,
                        Orden = 1,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    },
                    new Lado.Models.AlgoritmoFeed
                    {
                        Codigo = "trending",
                        Nombre = "Trending",
                        Descripcion = "Prioriza contenido con alto engagement reciente (likes, comentarios, vistas)",
                        Icono = "trending-up",
                        Activo = true,
                        EsPorDefecto = false,
                        Orden = 2,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    },
                    new Lado.Models.AlgoritmoFeed
                    {
                        Codigo = "seguidos",
                        Nombre = "Seguidos Primero",
                        Descripcion = "70% contenido de creadores que sigues, 30% descubrimiento de nuevos",
                        Icono = "users",
                        Activo = true,
                        EsPorDefecto = false,
                        Orden = 3,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    },
                    new Lado.Models.AlgoritmoFeed
                    {
                        Codigo = "para_ti",
                        Nombre = "Para Ti",
                        Descripcion = "Personalizado basado en tu historial de interacciones y preferencias",
                        Icono = "heart",
                        Activo = true,
                        EsPorDefecto = false,
                        Orden = 4,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    }
                };

                _context.AlgoritmosFeed.AddRange(algoritmos);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Algoritmos creados correctamente.";
            }
            else
            {
                // Asegurar que cronologico sea el por defecto
                var todos = await _context.AlgoritmosFeed.ToListAsync();
                foreach (var alg in todos)
                {
                    alg.EsPorDefecto = (alg.Codigo == "cronologico");
                    alg.Activo = true;
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "Algoritmos reparados. 'Cronologico' es ahora el por defecto.";
            }

            return RedirectToAction("Algoritmos");
        }

        [HttpGet]
        public async Task<IActionResult> DiagnosticoAlgoritmos()
        {
            var algoritmos = await _context.AlgoritmosFeed.ToListAsync();
            var preferencias = await _context.PreferenciasAlgoritmoUsuario.CountAsync();

            return Json(new
            {
                TotalAlgoritmos = algoritmos.Count,
                Algoritmos = algoritmos.Select(a => new
                {
                    a.Id,
                    a.Codigo,
                    a.Nombre,
                    a.Activo,
                    a.EsPorDefecto,
                    a.Orden,
                    a.TotalUsos
                }),
                TotalPreferenciasUsuarios = preferencias
            });
        }
    }
}