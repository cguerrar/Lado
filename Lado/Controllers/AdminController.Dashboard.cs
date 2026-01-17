using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lado.Services;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // DASHBOARD METRICS - ENDPOINTS
        // ========================================

        /// <summary>
        /// Vista del dashboard con métricas y gráficos
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Dashboard([FromServices] IDashboardMetricsService metricsService)
        {
            var metrics = await metricsService.ObtenerMetricasAsync();
            return View(metrics);
        }

        /// <summary>
        /// Obtener métricas del dashboard en JSON (para actualización en tiempo real)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerMetricasDashboard([FromServices] IDashboardMetricsService metricsService)
        {
            try
            {
                var metrics = await metricsService.ObtenerMetricasAsync();
                return Json(new { success = true, data = metrics });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo métricas dashboard");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener serie temporal para gráficos
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerSerieTemporal(
            [FromServices] IDashboardMetricsService metricsService,
            string tipo,
            int dias = 30)
        {
            try
            {
                var serie = await metricsService.ObtenerSerieTemporalAsync(tipo, dias);
                return Json(new
                {
                    success = true,
                    data = serie.Select(p => new
                    {
                        fecha = p.Fecha.ToString("yyyy-MM-dd"),
                        valor = p.Valor,
                        label = p.Label
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo serie temporal {Tipo}", tipo);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener resumen rápido para header/widget
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerResumenRapido([FromServices] IDashboardMetricsService metricsService)
        {
            try
            {
                var resumen = await metricsService.ObtenerResumenRapidoAsync();
                return Json(new { success = true, data = resumen });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo resumen rápido");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener múltiples series para comparación
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerSeriesComparativas(
            [FromServices] IDashboardMetricsService metricsService,
            int dias = 30)
        {
            try
            {
                var usuarios = await metricsService.ObtenerSerieTemporalAsync("usuarios", dias);
                var contenidos = await metricsService.ObtenerSerieTemporalAsync("contenidos", dias);
                var ingresos = await metricsService.ObtenerSerieTemporalAsync("ingresos", dias);
                var suscripciones = await metricsService.ObtenerSerieTemporalAsync("suscripciones", dias);

                return Json(new
                {
                    success = true,
                    usuarios = usuarios.Select(p => new { fecha = p.Fecha.ToString("yyyy-MM-dd"), valor = p.Valor }),
                    contenidos = contenidos.Select(p => new { fecha = p.Fecha.ToString("yyyy-MM-dd"), valor = p.Valor }),
                    ingresos = ingresos.Select(p => new { fecha = p.Fecha.ToString("yyyy-MM-dd"), valor = p.Valor }),
                    suscripciones = suscripciones.Select(p => new { fecha = p.Fecha.ToString("yyyy-MM-dd"), valor = p.Valor })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo series comparativas");
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
