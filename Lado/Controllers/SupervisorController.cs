using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Authorization;
using Lado.Data;
using Lado.Models;
using Lado.Models.Moderacion;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    [SupervisorAuthorize]
    public class SupervisorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IModeracionService _moderacionService;
        private readonly ILogEventoService _logService;
        private readonly ILogger<SupervisorController> _logger;

        public SupervisorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IModeracionService moderacionService,
            ILogEventoService logService,
            ILogger<SupervisorController> logger)
        {
            _context = context;
            _userManager = userManager;
            _moderacionService = moderacionService;
            _logService = logService;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════
        // DASHBOARD
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Dashboard principal del supervisor
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var supervisor = await _moderacionService.ObtenerSupervisorAsync(usuario.Id);
            var estadisticas = await _moderacionService.ObtenerEstadisticasColaAsync();
            var metricasHoy = await _moderacionService.ObtenerMetricasHoyAsync(usuario.Id);
            var itemsAsignados = await _moderacionService.ObtenerItemsAsignadosAsync(usuario.Id);

            // Obtener resumen semanal
            var inicioSemana = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
            var resumenSemanal = await _moderacionService.ObtenerResumenMetricasAsync(
                usuario.Id, inicioSemana, DateTime.UtcNow);

            // Obtener ranking
            var ranking = await _moderacionService.ObtenerRankingSupervisoresAsync(
                inicioSemana, DateTime.UtcNow, 10);
            var miRanking = ranking.FirstOrDefault(r => r.SupervisorId == usuario.Id);

            ViewBag.Usuario = usuario;
            ViewBag.Supervisor = supervisor;
            ViewBag.Estadisticas = estadisticas;
            ViewBag.MetricasHoy = metricasHoy;
            ViewBag.ItemsAsignados = itemsAsignados;
            ViewBag.ResumenSemanal = resumenSemanal;
            ViewBag.MiRanking = miRanking?.Ranking ?? 0;
            ViewBag.TotalSupervisores = ranking.Count;

            return View();
        }

        // ═══════════════════════════════════════════════════════════
        // COLA DE MODERACIÓN
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Ver cola de moderación pendiente
        /// </summary>
        [SupervisorAuthorize(PermisosPredefinidos.CONTENIDO_VER_COLA)]
        public async Task<IActionResult> Cola()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var colaPendiente = await _moderacionService.ObtenerColaPendienteAsync(100);
            var itemsAsignados = await _moderacionService.ObtenerItemsAsignadosAsync(usuario.Id);
            var estadisticas = await _moderacionService.ObtenerEstadisticasColaAsync();

            ViewBag.ColaPendiente = colaPendiente;
            ViewBag.ItemsAsignados = itemsAsignados;
            ViewBag.Estadisticas = estadisticas;

            return View();
        }

        /// <summary>
        /// Obtener siguiente item para revisar
        /// </summary>
        [HttpPost]
        [SupervisorAuthorize(PermisosPredefinidos.CONTENIDO_VER_COLA)]
        public async Task<IActionResult> ObtenerSiguiente()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Unauthorized();

            var item = await _moderacionService.ObtenerSiguienteItemAsync(usuario.Id);

            if (item == null)
            {
                return Json(new { success = false, message = "No hay items pendientes o has alcanzado tu límite" });
            }

            return Json(new
            {
                success = true,
                colaId = item.Id,
                redirectUrl = Url.Action("Revisar", new { id = item.Id })
            });
        }

        /// <summary>
        /// Vista de revisión de un contenido específico
        /// </summary>
        [SupervisorAuthorize(PermisosPredefinidos.CONTENIDO_VER_COLA)]
        public async Task<IActionResult> Revisar(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var item = await _moderacionService.ObtenerItemAsync(id);

            if (item == null)
            {
                TempData["Error"] = "Item no encontrado";
                return RedirectToAction(nameof(Cola));
            }

            // Verificar que está asignado a este supervisor o es admin
            if (item.SupervisorAsignadoId != usuario.Id && !User.IsInRole("Admin"))
            {
                // Intentar asignarlo
                var asignado = await _moderacionService.AsignarItemAsync(id, usuario.Id);
                if (!asignado)
                {
                    TempData["Error"] = "No se pudo asignar el item";
                    return RedirectToAction(nameof(Cola));
                }
                // Recargar item con la asignación
                item = await _moderacionService.ObtenerItemAsync(id);
            }

            // Obtener permisos del supervisor
            var permisos = await _moderacionService.ObtenerPermisosAsync(usuario.Id);

            ViewBag.Item = item;
            ViewBag.Permisos = permisos;
            ViewBag.PuedeAprobar = permisos.Contains(PermisosPredefinidos.CONTENIDO_APROBAR) || User.IsInRole("Admin");
            ViewBag.PuedeRechazar = permisos.Contains(PermisosPredefinidos.CONTENIDO_RECHAZAR) || User.IsInRole("Admin");
            ViewBag.PuedeCensurar = permisos.Contains(PermisosPredefinidos.CONTENIDO_CENSURAR) || User.IsInRole("Admin");
            ViewBag.PuedeEscalar = permisos.Contains(PermisosPredefinidos.CONTENIDO_ESCALAR) || User.IsInRole("Admin");
            ViewBag.RazonesRechazo = Enum.GetValues<RazonRechazo>();

            return View(item);
        }

        // ═══════════════════════════════════════════════════════════
        // ACCIONES DE MODERACIÓN
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Aprobar contenido
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SupervisorAuthorize(PermisosPredefinidos.CONTENIDO_APROBAR)]
        public async Task<IActionResult> Aprobar(int colaId, string? comentario, int tiempoSegundos)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Unauthorized();

            var resultado = await _moderacionService.AprobarAsync(colaId, usuario.Id, comentario, tiempoSegundos);

            if (resultado)
            {
                TempData["Success"] = "Contenido aprobado correctamente";
            }
            else
            {
                TempData["Error"] = "Error al aprobar el contenido";
            }

            // Verificar si hay más items
            var siguienteItem = await _moderacionService.ObtenerSiguienteItemAsync(usuario.Id);
            if (siguienteItem != null)
            {
                return RedirectToAction(nameof(Revisar), new { id = siguienteItem.Id });
            }

            return RedirectToAction(nameof(Cola));
        }

        /// <summary>
        /// Rechazar contenido
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SupervisorAuthorize(PermisosPredefinidos.CONTENIDO_RECHAZAR)]
        public async Task<IActionResult> Rechazar(int colaId, RazonRechazo razon, string? detalleRazon, int tiempoSegundos)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Unauthorized();

            var resultado = await _moderacionService.RechazarAsync(colaId, usuario.Id, razon, detalleRazon, tiempoSegundos);

            if (resultado)
            {
                TempData["Success"] = "Contenido rechazado";
            }
            else
            {
                TempData["Error"] = "Error al rechazar el contenido";
            }

            var siguienteItem = await _moderacionService.ObtenerSiguienteItemAsync(usuario.Id);
            if (siguienteItem != null)
            {
                return RedirectToAction(nameof(Revisar), new { id = siguienteItem.Id });
            }

            return RedirectToAction(nameof(Cola));
        }

        /// <summary>
        /// Censurar contenido
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SupervisorAuthorize(PermisosPredefinidos.CONTENIDO_CENSURAR)]
        public async Task<IActionResult> Censurar(int colaId, string razonCensura, int tiempoSegundos)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Unauthorized();

            var resultado = await _moderacionService.CensurarAsync(colaId, usuario.Id, razonCensura, tiempoSegundos);

            if (resultado)
            {
                TempData["Success"] = "Contenido censurado";
            }
            else
            {
                TempData["Error"] = "Error al censurar el contenido";
            }

            var siguienteItem = await _moderacionService.ObtenerSiguienteItemAsync(usuario.Id);
            if (siguienteItem != null)
            {
                return RedirectToAction(nameof(Revisar), new { id = siguienteItem.Id });
            }

            return RedirectToAction(nameof(Cola));
        }

        /// <summary>
        /// Escalar a administrador
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SupervisorAuthorize(PermisosPredefinidos.CONTENIDO_ESCALAR)]
        public async Task<IActionResult> Escalar(int colaId, string motivo, int tiempoSegundos)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Unauthorized();

            var resultado = await _moderacionService.EscalarAsync(colaId, usuario.Id, motivo, tiempoSegundos);

            if (resultado)
            {
                TempData["Success"] = "Contenido escalado a administrador";
            }
            else
            {
                TempData["Error"] = "Error al escalar el contenido";
            }

            var siguienteItem = await _moderacionService.ObtenerSiguienteItemAsync(usuario.Id);
            if (siguienteItem != null)
            {
                return RedirectToAction(nameof(Revisar), new { id = siguienteItem.Id });
            }

            return RedirectToAction(nameof(Cola));
        }

        /// <summary>
        /// Liberar item (devolver a la cola)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Liberar(int colaId)
        {
            var resultado = await _moderacionService.LiberarItemAsync(colaId);

            if (resultado)
            {
                TempData["Info"] = "Item devuelto a la cola";
            }

            return RedirectToAction(nameof(Cola));
        }

        // ═══════════════════════════════════════════════════════════
        // HISTORIAL Y ESTADÍSTICAS
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Ver historial de decisiones propias
        /// </summary>
        [SupervisorAuthorize(PermisosPredefinidos.CONTENIDO_VER_HISTORIAL)]
        public async Task<IActionResult> MisDecisiones(int dias = 7)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var historial = await _moderacionService.ObtenerHistorialDecisionesAsync(usuario.Id, dias, 200);

            ViewBag.Dias = dias;
            return View(historial);
        }

        /// <summary>
        /// Ver estadísticas personales
        /// </summary>
        [SupervisorAuthorize(PermisosPredefinidos.ESTADISTICAS_PROPIAS)]
        public async Task<IActionResult> Estadisticas()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            // Estadísticas del día
            var hoy = DateTime.UtcNow.Date;
            var metricasHoy = await _moderacionService.ObtenerMetricasHoyAsync(usuario.Id);

            // Estadísticas de la semana
            var inicioSemana = hoy.AddDays(-(int)hoy.DayOfWeek);
            var resumenSemanal = await _moderacionService.ObtenerResumenMetricasAsync(usuario.Id, inicioSemana, hoy);

            // Estadísticas del mes
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var resumenMensual = await _moderacionService.ObtenerResumenMetricasAsync(usuario.Id, inicioMes, hoy);

            // Historial de métricas diarias (últimos 30 días)
            var metricasDiarias = await _context.MetricasSupervisor
                .Where(m => m.SupervisorId == usuario.Id && m.Fecha >= hoy.AddDays(-30))
                .OrderBy(m => m.Fecha)
                .ToListAsync();

            ViewBag.MetricasHoy = metricasHoy;
            ViewBag.ResumenSemanal = resumenSemanal;
            ViewBag.ResumenMensual = resumenMensual;
            ViewBag.MetricasDiarias = metricasDiarias;

            return View();
        }

        /// <summary>
        /// Ver ranking del equipo
        /// </summary>
        [SupervisorAuthorize(PermisosPredefinidos.ESTADISTICAS_EQUIPO)]
        public async Task<IActionResult> Ranking()
        {
            var inicioSemana = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
            var ranking = await _moderacionService.ObtenerRankingSupervisoresAsync(inicioSemana, DateTime.UtcNow, 50);

            return View(ranking);
        }

        // ═══════════════════════════════════════════════════════════
        // DISPONIBILIDAD
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Cambiar estado de disponibilidad
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarDisponibilidad(bool disponible)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Unauthorized();

            await _moderacionService.ActualizarDisponibilidadAsync(usuario.Id, disponible);

            return Json(new { success = true, disponible });
        }

        // ═══════════════════════════════════════════════════════════
        // ACCESO DENEGADO
        // ═══════════════════════════════════════════════════════════

        public IActionResult AccesoDenegado()
        {
            return View();
        }

        // ═══════════════════════════════════════════════════════════
        // API ENDPOINTS (AJAX)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Obtener estadísticas de la cola (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerEstadisticasCola()
        {
            var estadisticas = await _moderacionService.ObtenerEstadisticasColaAsync();
            return Json(estadisticas);
        }

        /// <summary>
        /// Obtener items pendientes (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerColaPendiente(int limite = 20)
        {
            var cola = await _moderacionService.ObtenerColaPendienteAsync(limite);

            var items = cola.Select(c => new
            {
                c.Id,
                c.ContenidoId,
                Prioridad = c.Prioridad.ToString(),
                Estado = c.Estado.ToString(),
                c.FechaCreacion,
                c.EsDeReporte,
                CreadorSeudonimo = c.Contenido?.Usuario?.Seudonimo ?? c.Contenido?.Usuario?.UserName,
                TieneVideo = c.Contenido?.Archivos?.Any(a => a.TipoArchivo == TipoArchivo.Video) ?? false,
                NumeroArchivos = c.Contenido?.Archivos?.Count ?? 0,
                Thumbnail = c.Contenido?.Archivos?.FirstOrDefault()?.RutaArchivo
            });

            return Json(items);
        }
    }
}
