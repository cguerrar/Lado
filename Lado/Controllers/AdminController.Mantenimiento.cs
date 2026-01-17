using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lado.Services;
using Lado.Models;
using System.ComponentModel.DataAnnotations;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // MODO MANTENIMIENTO - ENDPOINTS
        // ========================================

        /// <summary>
        /// Vista principal de configuración de mantenimiento
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Mantenimiento([FromServices] IMantenimientoService mantenimientoService)
        {
            var config = await mantenimientoService.ObtenerConfiguracionAsync();
            var historial = await mantenimientoService.ObtenerHistorialAsync(10);
            var estaEnMantenimiento = await mantenimientoService.EstaEnMantenimientoAsync();

            ViewBag.EstaEnMantenimiento = estaEnMantenimiento;
            ViewBag.Historial = historial;

            return View(config);
        }

        /// <summary>
        /// Activar modo mantenimiento inmediatamente
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivarMantenimiento(
            [FromServices] IMantenimientoService mantenimientoService,
            string? mensaje,
            DateTime? finEstimado)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                await mantenimientoService.ActivarMantenimientoAsync(adminId, mensaje, finEstimado);

                return Json(new { success = true, mensaje = "Modo mantenimiento ACTIVADO" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error activando mantenimiento");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Desactivar modo mantenimiento
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesactivarMantenimiento([FromServices] IMantenimientoService mantenimientoService)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                await mantenimientoService.DesactivarMantenimientoAsync(adminId);

                return Json(new { success = true, mensaje = "Modo mantenimiento DESACTIVADO" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error desactivando mantenimiento");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Programar mantenimiento para una fecha futura
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProgramarMantenimiento(
            [FromServices] IMantenimientoService mantenimientoService,
            DateTime inicio,
            DateTime finEstimado,
            string? mensaje)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                if (inicio <= DateTime.Now)
                    return Json(new { success = false, error = "La fecha de inicio debe ser en el futuro" });

                if (finEstimado <= inicio)
                    return Json(new { success = false, error = "La fecha de fin debe ser posterior al inicio" });

                await mantenimientoService.ProgramarMantenimientoAsync(adminId, inicio, finEstimado, mensaje);

                return Json(new {
                    success = true,
                    mensaje = $"Mantenimiento programado para {inicio:dd/MM/yyyy HH:mm}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error programando mantenimiento");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Cancelar mantenimiento programado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarMantenimientoProgramado([FromServices] IMantenimientoService mantenimientoService)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                var config = await mantenimientoService.ObtenerConfiguracionAsync();

                if (!config.FechaInicio.HasValue || config.EstaActivo)
                    return Json(new { success = false, error = "No hay mantenimiento programado" });

                config.FechaInicio = null;
                config.FechaFinEstimado = null;
                config.NotificacionPreviaEnviada = false;

                await mantenimientoService.ActualizarConfiguracionAsync(config);

                await _logEventoService.RegistrarEventoAsync(
                    "Mantenimiento programado CANCELADO",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    adminId,
                    null,
                    null
                );

                return Json(new { success = true, mensaje = "Mantenimiento programado cancelado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error cancelando mantenimiento programado");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar configuración de mantenimiento (título, mensaje, rutas, etc.)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarConfigMantenimiento(
            [FromServices] IMantenimientoService mantenimientoService,
            [FromBody] MantenimientoConfigDto dto)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                var config = await mantenimientoService.ObtenerConfiguracionAsync();

                config.Titulo = dto.Titulo ?? config.Titulo;
                config.Mensaje = dto.Mensaje ?? config.Mensaje;
                config.RutasPermitidas = dto.RutasPermitidas ?? config.RutasPermitidas;
                config.MostrarCuentaRegresiva = dto.MostrarCuentaRegresiva;
                config.PermitirCreadoresVerificados = dto.PermitirCreadoresVerificados;
                config.NotificarMinutosAntes = dto.NotificarMinutosAntes;

                await mantenimientoService.ActualizarConfiguracionAsync(config);

                await _logEventoService.RegistrarEventoAsync(
                    "Configuración de mantenimiento actualizada",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    adminId,
                    null,
                    null
                );

                return Json(new { success = true, mensaje = "Configuración guardada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error actualizando config mantenimiento");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener estado actual del mantenimiento (para actualización en tiempo real)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerEstadoMantenimiento([FromServices] IMantenimientoService mantenimientoService)
        {
            try
            {
                var config = await mantenimientoService.ObtenerConfiguracionAsync();
                var estaActivo = await mantenimientoService.EstaEnMantenimientoAsync();

                return Json(new
                {
                    success = true,
                    estaActivo,
                    fechaInicio = config.FechaInicio?.ToString("yyyy-MM-ddTHH:mm"),
                    fechaFinEstimado = config.FechaFinEstimado?.ToString("yyyy-MM-ddTHH:mm"),
                    titulo = config.Titulo,
                    mensaje = config.Mensaje,
                    programado = config.FechaInicio.HasValue && !config.EstaActivo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo estado mantenimiento");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener historial de mantenimientos
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerHistorialMantenimiento(
            [FromServices] IMantenimientoService mantenimientoService,
            int cantidad = 20)
        {
            try
            {
                var historial = await mantenimientoService.ObtenerHistorialAsync(cantidad);

                return Json(new
                {
                    success = true,
                    historial = historial.Select(h => new
                    {
                        id = h.Id,
                        fechaInicio = h.FechaInicio.ToString("dd/MM/yyyy HH:mm"),
                        fechaFin = h.FechaFin?.ToString("dd/MM/yyyy HH:mm"),
                        titulo = h.Titulo,
                        mensaje = h.Mensaje,
                        duracionMinutos = h.DuracionMinutos,
                        activadoPor = h.ActivadoPor?.NombreCompleto ?? h.ActivadoPor?.UserName ?? "Sistema",
                        desactivadoPor = h.DesactivadoPor?.NombreCompleto ?? h.DesactivadoPor?.UserName ?? (h.FechaFin.HasValue ? "Sistema (auto)" : null),
                        notas = h.Notas
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo historial mantenimiento");
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    /// <summary>
    /// DTO para actualizar configuración de mantenimiento
    /// </summary>
    public class MantenimientoConfigDto
    {
        [StringLength(200)]
        public string? Titulo { get; set; }

        [StringLength(1000)]
        public string? Mensaje { get; set; }

        [StringLength(500)]
        public string? RutasPermitidas { get; set; }

        public bool MostrarCuentaRegresiva { get; set; } = true;
        public bool PermitirCreadoresVerificados { get; set; } = false;
        public int NotificarMinutosAntes { get; set; } = 30;
    }
}
