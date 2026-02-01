using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    public class BienestarCreadorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IBienestarCreadorService _bienestarService;
        private readonly ILogEventoService _logEventoService;

        public BienestarCreadorController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IBienestarCreadorService bienestarService,
            ILogEventoService logEventoService)
        {
            _userManager = userManager;
            _context = context;
            _bienestarService = bienestarService;
            _logEventoService = logEventoService;
        }

        /// <summary>
        /// Dashboard principal de bienestar del creador
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Verificar que es creador
            if (!usuario.EsCreador)
            {
                TempData["Error"] = "Esta seccion es solo para creadores.";
                return RedirectToAction("Index", "Dashboard");
            }

            // Obtener todas las metricas
            var configVacaciones = await _bienestarService.ObtenerConfiguracionVacacionesAsync(usuario.Id);
            var alertasBurnout = await _bienestarService.AnalizarPatronesBurnoutAsync(usuario.Id);
            var celebraciones = await _bienestarService.GenerarCelebracionesAsync(usuario.Id);
            var proyeccion = await _bienestarService.CalcularProyeccionIngresosAsync(usuario.Id);
            var metricas = await _bienestarService.ObtenerMetricasBienestarAsync(usuario.Id);
            var contenidoProgramado = await _bienestarService.ObtenerContenidoProgramadoAsync(usuario.Id);

            ViewBag.ConfigVacaciones = configVacaciones;
            ViewBag.AlertasBurnout = alertasBurnout;
            ViewBag.Celebraciones = celebraciones;
            ViewBag.Proyeccion = proyeccion;
            ViewBag.Metricas = metricas;
            ViewBag.ContenidoProgramado = contenidoProgramado;
            ViewBag.Usuario = usuario;

            return View();
        }

        /// <summary>
        /// GET: Formulario para configurar vacaciones
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ConfigurarVacaciones()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var config = await _bienestarService.ObtenerConfiguracionVacacionesAsync(usuario.Id);

            ViewBag.Config = config;
            ViewBag.Usuario = usuario;

            return View();
        }

        /// <summary>
        /// POST: Guardar configuracion de vacaciones
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigurarVacaciones(
            DateTime fechaInicio,
            DateTime fechaFin,
            string? mensajeAutorespuesta,
            string? mensajePerfilPublico,
            bool autoResponder = true,
            bool protegerSuscriptores = true)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            // Validaciones
            if (fechaFin <= fechaInicio)
            {
                return Json(new { success = false, message = "La fecha de fin debe ser posterior a la de inicio" });
            }

            if (fechaInicio < DateTime.Today)
            {
                return Json(new { success = false, message = "La fecha de inicio no puede ser en el pasado" });
            }

            var resultado = await _bienestarService.ActivarModoVacacionesAsync(
                usuario.Id,
                fechaInicio,
                fechaFin,
                mensajeAutorespuesta,
                mensajePerfilPublico);

            if (resultado)
            {
                TempData["Success"] = "Modo vacaciones activado correctamente.";
                return Json(new { success = true, message = "Modo vacaciones activado" });
            }

            return Json(new { success = false, message = "Error al activar modo vacaciones" });
        }

        /// <summary>
        /// POST: Desactivar modo vacaciones
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesactivarVacaciones()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var resultado = await _bienestarService.DesactivarModoVacacionesAsync(usuario.Id);

            if (resultado)
            {
                TempData["Success"] = "Modo vacaciones desactivado.";
                return Json(new { success = true, message = "Modo vacaciones desactivado" });
            }

            return Json(new { success = false, message = "Error al desactivar modo vacaciones" });
        }

        /// <summary>
        /// API: Obtener proyeccion de ingresos (JSON)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProyeccionIngresos()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var proyeccion = await _bienestarService.CalcularProyeccionIngresosAsync(usuario.Id);

            return Json(new
            {
                success = true,
                data = proyeccion
            });
        }

        /// <summary>
        /// API: Obtener alertas de bienestar (JSON)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AlertasBienestar()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var alertas = await _bienestarService.AnalizarPatronesBurnoutAsync(usuario.Id);

            return Json(new
            {
                success = true,
                alertas = alertas
            });
        }

        /// <summary>
        /// API: Obtener celebraciones (JSON)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Celebraciones()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var celebraciones = await _bienestarService.GenerarCelebracionesAsync(usuario.Id);

            return Json(new
            {
                success = true,
                celebraciones = celebraciones
            });
        }

        /// <summary>
        /// POST: Marcar logro como visto
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarLogroVisto(int logroId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            await _bienestarService.MarcarLogroVistoAsync(logroId);

            return Json(new { success = true });
        }

        /// <summary>
        /// API: Obtener metricas de bienestar (JSON)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Metricas()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var metricas = await _bienestarService.ObtenerMetricasBienestarAsync(usuario.Id);

            return Json(new
            {
                success = true,
                metricas = metricas
            });
        }

        /// <summary>
        /// POST: Programar contenido para publicar
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProgramarContenido(int contenidoBorradorId, DateTime fechaProgramada)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            if (fechaProgramada <= DateTime.Now)
            {
                return Json(new { success = false, message = "La fecha debe ser futura" });
            }

            var resultado = await _bienestarService.ProgramarContenidoAsync(
                usuario.Id,
                contenidoBorradorId,
                fechaProgramada);

            if (resultado)
            {
                return Json(new { success = true, message = "Contenido programado correctamente" });
            }

            return Json(new { success = false, message = "Error al programar contenido" });
        }

        /// <summary>
        /// POST: Cancelar contenido programado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarProgramacion(int programacionId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var resultado = await _bienestarService.CancelarContenidoProgramadoAsync(
                programacionId,
                usuario.Id);

            if (resultado)
            {
                return Json(new { success = true, message = "Programacion cancelada" });
            }

            return Json(new { success = false, message = "Error al cancelar programacion" });
        }

        /// <summary>
        /// API: Obtener contenido programado (JSON)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ContenidoProgramado()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var programados = await _bienestarService.ObtenerContenidoProgramadoAsync(usuario.Id);

            return Json(new
            {
                success = true,
                contenidos = programados.Select(p => new
                {
                    id = p.Id,
                    fechaProgramada = p.FechaProgramada,
                    contenidoId = p.ContenidoBorradorId,
                    descripcion = p.ContenidoBorrador?.Descripcion?.Substring(0, Math.Min(100, p.ContenidoBorrador.Descripcion?.Length ?? 0)),
                    thumbnail = p.ContenidoBorrador?.Thumbnail
                })
            });
        }
    }
}
