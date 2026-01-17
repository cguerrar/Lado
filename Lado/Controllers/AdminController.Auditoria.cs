using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lado.Services;
using Lado.Models;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // AUDITORÍA - ENDPOINTS
        // ========================================

        /// <summary>
        /// Vista del historial de auditoría
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Auditoria([FromServices] IAuditoriaService auditoriaService)
        {
            var historial = await auditoriaService.ObtenerHistorialAsync(cantidad: 200);
            var estadisticas = await auditoriaService.ObtenerEstadisticasAsync(30);

            ViewBag.Estadisticas = estadisticas;

            return View(historial);
        }

        /// <summary>
        /// Obtener historial de auditoría con filtros
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerHistorialAuditoria(
            [FromServices] IAuditoriaService auditoriaService,
            TipoConfiguracion? tipo,
            string? adminId,
            DateTime? desde,
            DateTime? hasta,
            int cantidad = 100)
        {
            try
            {
                var historial = await auditoriaService.ObtenerHistorialAsync(tipo, adminId, desde, hasta, cantidad);

                return Json(new
                {
                    success = true,
                    registros = historial.Select(a => new
                    {
                        id = a.Id,
                        tipo = a.TipoConfiguracion.ToString(),
                        campo = a.Campo,
                        valorAnterior = a.ValorAnterior,
                        valorNuevo = a.ValorNuevo,
                        entidadId = a.EntidadId,
                        descripcion = a.Descripcion,
                        modificadoPor = a.ModificadoPor?.NombreCompleto ?? a.ModificadoPor?.UserName ?? "Admin",
                        fecha = a.FechaModificacion.ToString("dd/MM/yyyy HH:mm:ss"),
                        ip = a.IpOrigen
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo historial auditoría");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener historial de una entidad específica
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerHistorialEntidad(
            [FromServices] IAuditoriaService auditoriaService,
            TipoConfiguracion tipo,
            string entidadId)
        {
            try
            {
                var historial = await auditoriaService.ObtenerHistorialPorEntidadAsync(tipo, entidadId, 50);

                return Json(new
                {
                    success = true,
                    registros = historial.Select(a => new
                    {
                        id = a.Id,
                        campo = a.Campo,
                        valorAnterior = a.ValorAnterior,
                        valorNuevo = a.ValorNuevo,
                        descripcion = a.Descripcion,
                        modificadoPor = a.ModificadoPor?.NombreCompleto ?? a.ModificadoPor?.UserName ?? "Admin",
                        fecha = a.FechaModificacion.ToString("dd/MM/yyyy HH:mm:ss")
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo historial entidad");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener estadísticas de auditoría
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerEstadisticasAuditoria(
            [FromServices] IAuditoriaService auditoriaService,
            int dias = 30)
        {
            try
            {
                var estadisticas = await auditoriaService.ObtenerEstadisticasAsync(dias);
                return Json(new { success = true, estadisticas });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo estadísticas auditoría");
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
