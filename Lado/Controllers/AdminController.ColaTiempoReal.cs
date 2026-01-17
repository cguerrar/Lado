using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Lado.Hubs;
using Lado.Models;
using Lado.Models.Moderacion;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // COLA DE MODERACIÓN EN TIEMPO REAL
        // ========================================

        /// <summary>
        /// Vista de cola de moderación en tiempo real con WebSocket
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ColaTiempoReal()
        {
            // Obtener conteos iniciales
            var stats = new
            {
                ReportesPendientes = await _context.Reportes.CountAsync(r => r.Estado == "Pendiente"),
                ApelacionesPendientes = await _context.Apelaciones.CountAsync(a => a.Estado == EstadoApelacion.Pendiente),
                ContenidosPendientes = await _context.ColaModeracion.CountAsync(c => c.Estado == EstadoModeracion.Pendiente),
                ModeradoresOnline = ModeracionHub.ObtenerConteoModeradores()
            };

            ViewBag.Stats = stats;
            return View();
        }

        /// <summary>
        /// Obtener items de la cola en tiempo real (AJAX polling backup)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerColaModeracion(string tipo = "todos", int limite = 20)
        {
            try
            {
                var items = new List<object>();

                // Reportes
                if (tipo == "todos" || tipo == "reportes")
                {
                    var reportes = await _context.Reportes
                        .Include(r => r.UsuarioReportador)
                        .Where(r => r.Estado == "Pendiente")
                        .OrderByDescending(r => r.FechaReporte)
                        .Take(limite)
                        .Select(r => new
                        {
                            id = r.Id,
                            tipo = "reporte",
                            titulo = $"Reporte #{r.Id}",
                            descripcion = r.Motivo ?? "Sin motivo",
                            tipoReporte = r.TipoReporte ?? "Otro",
                            usuario = r.UsuarioReportador != null ? r.UsuarioReportador.UserName : "Anónimo",
                            fecha = r.FechaReporte,
                            prioridad = "normal"
                        })
                        .ToListAsync();

                    items.AddRange(reportes.Cast<object>());
                }

                // Apelaciones
                if (tipo == "todos" || tipo == "apelaciones")
                {
                    var apelaciones = await _context.Apelaciones
                        .Include(a => a.Usuario)
                        .Where(a => a.Estado == EstadoApelacion.Pendiente)
                        .OrderByDescending(a => a.FechaCreacion)
                        .Take(limite)
                        .Select(a => new
                        {
                            id = a.Id,
                            tipo = "apelacion",
                            titulo = $"Apelación #{a.Id}",
                            descripcion = a.Argumentos ?? "Sin argumentos",
                            tipoApelacion = a.TipoContenido ?? "Otro",
                            usuario = a.Usuario != null ? a.Usuario.UserName : "Desconocido",
                            fecha = a.FechaCreacion,
                            prioridad = "alta"
                        })
                        .ToListAsync();

                    items.AddRange(apelaciones.Cast<object>());
                }

                // Cola de moderación (contenidos pendientes)
                if (tipo == "todos" || tipo == "contenidos")
                {
                    var colaItems = await _context.ColaModeracion
                        .Include(c => c.Contenido)
                            .ThenInclude(c => c.Usuario)
                        .Where(c => c.Estado == EstadoModeracion.Pendiente)
                        .OrderByDescending(c => c.FechaCreacion)
                        .Take(limite)
                        .Select(c => new
                        {
                            id = c.Id,
                            contenidoId = c.ContenidoId,
                            tipo = "contenido",
                            titulo = c.Contenido.Descripcion != null ? (c.Contenido.Descripcion.Length > 50 ? c.Contenido.Descripcion.Substring(0, 50) + "..." : c.Contenido.Descripcion) : $"Contenido #{c.ContenidoId}",
                            descripcion = c.Contenido.Descripcion ?? "Sin descripción",
                            tipoContenido = c.Contenido.TipoContenido.ToString(),
                            usuario = c.Contenido.Usuario != null ? c.Contenido.Usuario.UserName : "Desconocido",
                            fecha = c.FechaCreacion,
                            prioridad = c.Prioridad.ToString().ToLower()
                        })
                        .ToListAsync();

                    items.AddRange(colaItems.Cast<object>());
                }

                // Ordenar por fecha
                var itemsOrdenados = items
                    .OrderByDescending(i => ((dynamic)i).fecha)
                    .Take(limite)
                    .ToList();

                return Json(new
                {
                    success = true,
                    items = itemsOrdenados,
                    total = itemsOrdenados.Count,
                    moderadoresOnline = ModeracionHub.ObtenerConteoModeradores()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo cola de moderación");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Tomar un item para moderación (bloquea para otros)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> TomarItemModeracion(
            [FromServices] IHubContext<ModeracionHub> hubContext,
            int itemId,
            string tipoItem)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var adminName = User.Identity?.Name;

                if (string.IsNullOrEmpty(adminId))
                    return Json(new { success = false, error = "No autenticado" });

                // Actualizar estado según tipo
                switch (tipoItem.ToLower())
                {
                    case "reporte":
                        var reporte = await _context.Reportes.FindAsync(itemId);
                        if (reporte != null && reporte.Estado == "Pendiente")
                        {
                            reporte.Estado = "EnRevision";
                            await _context.SaveChangesAsync();
                        }
                        break;

                    case "apelacion":
                        var apelacion = await _context.Apelaciones.FindAsync(itemId);
                        if (apelacion != null && apelacion.Estado == EstadoApelacion.Pendiente)
                        {
                            apelacion.Estado = EstadoApelacion.EnRevision;
                            apelacion.AdministradorId = adminId;
                            await _context.SaveChangesAsync();
                        }
                        break;

                    case "contenido":
                        var colaItem = await _context.ColaModeracion.FindAsync(itemId);
                        if (colaItem != null && colaItem.Estado == EstadoModeracion.Pendiente)
                        {
                            colaItem.Estado = EstadoModeracion.EnRevision;
                            colaItem.SupervisorAsignadoId = adminId;
                            colaItem.FechaAsignacion = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                        }
                        break;
                }

                // Notificar a otros moderadores
                await hubContext.Clients.Group("moderadores").SendAsync("ItemTomado", new
                {
                    itemId,
                    tipoItem,
                    moderadorId = adminId,
                    moderadorNombre = adminName,
                    timestamp = DateTime.UtcNow
                });

                return Json(new { success = true, mensaje = "Item tomado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error tomando item de moderación");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Liberar un item (devolverlo a la cola)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> LiberarItemModeracion(
            [FromServices] IHubContext<ModeracionHub> hubContext,
            int itemId,
            string tipoItem)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var adminName = User.Identity?.Name;

                // Devolver a estado pendiente
                switch (tipoItem.ToLower())
                {
                    case "reporte":
                        var reporte = await _context.Reportes.FindAsync(itemId);
                        if (reporte != null)
                        {
                            reporte.Estado = "Pendiente";
                            await _context.SaveChangesAsync();
                        }
                        break;

                    case "apelacion":
                        var apelacion = await _context.Apelaciones.FindAsync(itemId);
                        if (apelacion != null)
                        {
                            apelacion.Estado = EstadoApelacion.Pendiente;
                            apelacion.AdministradorId = null;
                            await _context.SaveChangesAsync();
                        }
                        break;

                    case "contenido":
                        var colaItem = await _context.ColaModeracion.FindAsync(itemId);
                        if (colaItem != null)
                        {
                            colaItem.Estado = EstadoModeracion.Pendiente;
                            colaItem.SupervisorAsignadoId = null;
                            colaItem.FechaAsignacion = null;
                            await _context.SaveChangesAsync();
                        }
                        break;
                }

                // Notificar a otros moderadores
                await hubContext.Clients.Group("moderadores").SendAsync("ItemLiberado", new
                {
                    itemId,
                    tipoItem,
                    moderadorId = adminId,
                    moderadorNombre = adminName,
                    timestamp = DateTime.UtcNow
                });

                return Json(new { success = true, mensaje = "Item liberado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error liberando item de moderación");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Notificar nuevo item en la cola (llamado desde otros controladores)
        /// </summary>
        public static async Task NotificarNuevoItem(IHubContext<ModeracionHub> hubContext, object item)
        {
            await hubContext.Clients.Group("moderadores").SendAsync("NuevoItem", item);
        }
    }
}
