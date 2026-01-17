using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Services;
using Lado.Models;

namespace Lado.Controllers
{
    public partial class AdminController
    {
        // ========================================
        // SISTEMA DE TICKETS INTERNO
        // ========================================

        /// <summary>
        /// Vista principal de tickets
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Tickets(
            [FromServices] ITicketsService ticketsService,
            EstadoTicket? estado,
            CategoriaTicket? categoria,
            int pagina = 1)
        {
            var tickets = await ticketsService.ObtenerTicketsAsync(estado, categoria, null, pagina, 20);
            var total = await ticketsService.ObtenerTotalTicketsAsync(estado, categoria);
            var stats = await ticketsService.ObtenerEstadisticasAsync();

            // Obtener admins/supervisores para asignación
            var adminRoles = new[] { "Admin", "Supervisor" };
            var adminUserIds = await _context.UserRoles
                .Join(_context.Roles.Where(r => adminRoles.Contains(r.Name)),
                    ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId)
                .ToListAsync();

            var admins = await _context.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync();

            ViewBag.Tickets = tickets;
            ViewBag.Total = total;
            ViewBag.Pagina = pagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling(total / 20.0);
            ViewBag.Stats = stats;
            ViewBag.Admins = admins;
            ViewBag.EstadoFiltro = estado;
            ViewBag.CategoriaFiltro = categoria;

            return View();
        }

        /// <summary>
        /// Detalle de un ticket
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DetalleTicket(
            [FromServices] ITicketsService ticketsService,
            int id)
        {
            var ticket = await ticketsService.ObtenerTicketAsync(id);
            if (ticket == null)
                return NotFound();

            var adminRoles = new[] { "Admin", "Supervisor" };
            var adminUserIds = await _context.UserRoles
                .Join(_context.Roles.Where(r => adminRoles.Contains(r.Name)),
                    ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId)
                .ToListAsync();

            var admins = await _context.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync();

            ViewBag.Admins = admins;
            return View(ticket);
        }

        /// <summary>
        /// Crear nuevo ticket
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearTicket(
            [FromServices] ITicketsService ticketsService,
            [FromBody] TicketDto dto)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, error = "No autenticado" });

                var ticket = new TicketInterno
                {
                    Titulo = dto.Titulo,
                    Descripcion = dto.Descripcion,
                    Categoria = dto.Categoria,
                    Prioridad = dto.Prioridad,
                    CreadoPorId = userId,
                    AsignadoAId = dto.AsignadoAId,
                    Etiquetas = dto.Etiquetas,
                    ItemRelacionadoId = dto.ItemRelacionadoId,
                    TipoItemRelacionado = dto.TipoItemRelacionado
                };

                await ticketsService.CrearTicketAsync(ticket);

                return Json(new { success = true, id = ticket.Id, mensaje = "Ticket creado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error creando ticket");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar ticket
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarTicket(
            [FromServices] ITicketsService ticketsService,
            [FromBody] TicketDto dto)
        {
            try
            {
                var ticket = new TicketInterno
                {
                    Titulo = dto.Titulo,
                    Descripcion = dto.Descripcion,
                    Categoria = dto.Categoria,
                    Prioridad = dto.Prioridad,
                    Etiquetas = dto.Etiquetas
                };

                var resultado = await ticketsService.ActualizarTicketAsync(dto.Id, ticket);

                if (resultado == null)
                    return Json(new { success = false, error = "Ticket no encontrado" });

                return Json(new { success = true, mensaje = "Ticket actualizado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error actualizando ticket {Id}", dto.Id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Asignar ticket a un usuario
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AsignarTicket(
            [FromServices] ITicketsService ticketsService,
            int id,
            string asignadoAId)
        {
            try
            {
                var resultado = await ticketsService.AsignarTicketAsync(id, asignadoAId);

                if (!resultado)
                    return Json(new { success = false, error = "Ticket no encontrado" });

                return Json(new { success = true, mensaje = "Ticket asignado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error asignando ticket {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Cambiar estado del ticket
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CambiarEstadoTicket(
            [FromServices] ITicketsService ticketsService,
            int id,
            EstadoTicket estado)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, error = "No autenticado" });

                var resultado = await ticketsService.CambiarEstadoAsync(id, estado, userId);

                if (!resultado)
                    return Json(new { success = false, error = "Ticket no encontrado" });

                return Json(new { success = true, mensaje = "Estado actualizado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error cambiando estado del ticket {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Agregar respuesta al ticket
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResponderTicket(
            [FromServices] ITicketsService ticketsService,
            int ticketId,
            string contenido,
            bool esNotaInterna = false)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, error = "No autenticado" });

                var respuesta = await ticketsService.AgregarRespuestaAsync(ticketId, contenido, userId, esNotaInterna);

                return Json(new
                {
                    success = true,
                    respuesta = new
                    {
                        id = respuesta.Id,
                        contenido = respuesta.Contenido,
                        autor = User.Identity?.Name,
                        fecha = respuesta.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                        esNotaInterna = respuesta.EsNotaInterna
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error respondiendo ticket {Id}", ticketId);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener tickets (para AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerTickets(
            [FromServices] ITicketsService ticketsService,
            EstadoTicket? estado,
            CategoriaTicket? categoria,
            string? asignadoAId,
            int pagina = 1)
        {
            try
            {
                var tickets = await ticketsService.ObtenerTicketsAsync(estado, categoria, asignadoAId, pagina, 20);
                var total = await ticketsService.ObtenerTotalTicketsAsync(estado, categoria, asignadoAId);

                return Json(new
                {
                    success = true,
                    tickets = tickets.Select(t => new
                    {
                        id = t.Id,
                        titulo = t.Titulo,
                        descripcion = t.Descripcion.Length > 100 ? t.Descripcion.Substring(0, 100) + "..." : t.Descripcion,
                        categoria = t.Categoria.ToString(),
                        prioridad = t.Prioridad.ToString(),
                        estado = t.Estado.ToString(),
                        creadoPor = t.CreadoPor?.UserName,
                        asignadoA = t.AsignadoA?.UserName,
                        fecha = t.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                        etiquetas = t.Etiquetas?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    }),
                    total,
                    pagina,
                    totalPaginas = (int)Math.Ceiling(total / 20.0)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo tickets");
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    public class TicketDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public CategoriaTicket Categoria { get; set; }
        public PrioridadTicket Prioridad { get; set; }
        public string? AsignadoAId { get; set; }
        public string? Etiquetas { get; set; }
        public int? ItemRelacionadoId { get; set; }
        public TipoItemTicket? TipoItemRelacionado { get; set; }
    }
}
