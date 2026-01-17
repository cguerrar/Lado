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
        // CALENDARIO DE EVENTOS ADMIN
        // ========================================

        /// <summary>
        /// Vista del calendario
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Calendario([FromServices] ICalendarioService calendarioService)
        {
            var proximosEventos = await calendarioService.ObtenerProximosEventosAsync(5);

            // Obtener admins/supervisores por roles
            var adminRoles = new[] { "Admin", "Supervisor" };
            var adminUserIds = await _context.UserRoles
                .Join(_context.Roles.Where(r => adminRoles.Contains(r.Name)),
                    ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId)
                .ToListAsync();

            var admins = await _context.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName })
                .ToListAsync();

            ViewBag.ProximosEventos = proximosEventos;
            ViewBag.Admins = admins;
            return View();
        }

        /// <summary>
        /// Obtener eventos para el calendario (JSON)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerEventos(
            [FromServices] ICalendarioService calendarioService,
            DateTime start,
            DateTime end)
        {
            try
            {
                var eventos = await calendarioService.ObtenerEventosAsync(start, end);

                var eventosJson = eventos.Select(e => new
                {
                    id = e.Id,
                    title = e.Titulo,
                    start = e.FechaInicio.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = e.FechaFin?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    allDay = e.TodoElDia,
                    color = GetColorHex(e.Color),
                    extendedProps = new
                    {
                        tipo = e.Tipo.ToString(),
                        descripcion = e.Descripcion,
                        ubicacion = e.Ubicacion,
                        creadoPor = e.CreadoPor?.UserName,
                        participantes = e.Participantes.Count
                    }
                });

                return Json(eventosJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo eventos");
                return Json(new List<object>());
            }
        }

        /// <summary>
        /// Obtener detalle de un evento
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerEvento(
            [FromServices] ICalendarioService calendarioService,
            int id)
        {
            try
            {
                var evento = await calendarioService.ObtenerEventoAsync(id);
                if (evento == null)
                    return Json(new { success = false, error = "Evento no encontrado" });

                return Json(new
                {
                    success = true,
                    evento = new
                    {
                        id = evento.Id,
                        titulo = evento.Titulo,
                        descripcion = evento.Descripcion,
                        tipo = (int)evento.Tipo,
                        color = (int)evento.Color,
                        fechaInicio = evento.FechaInicio.ToString("yyyy-MM-ddTHH:mm"),
                        fechaFin = evento.FechaFin?.ToString("yyyy-MM-ddTHH:mm"),
                        todoElDia = evento.TodoElDia,
                        ubicacion = evento.Ubicacion,
                        enviarRecordatorio = evento.EnviarRecordatorio,
                        minutosAnteRecordatorio = evento.MinutosAnteRecordatorio,
                        notas = evento.Notas,
                        creadoPor = evento.CreadoPor?.UserName,
                        participantes = evento.Participantes.Select(p => new
                        {
                            usuarioId = p.UsuarioId,
                            nombre = p.Usuario?.UserName,
                            estado = p.Estado.ToString()
                        })
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error obteniendo evento {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Crear nuevo evento
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearEvento(
            [FromServices] ICalendarioService calendarioService,
            [FromBody] EventoDto dto)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, error = "No autenticado" });

                var evento = new EventoAdmin
                {
                    Titulo = dto.Titulo,
                    Descripcion = dto.Descripcion,
                    Tipo = dto.Tipo,
                    Color = dto.Color,
                    FechaInicio = dto.FechaInicio,
                    FechaFin = dto.FechaFin,
                    TodoElDia = dto.TodoElDia,
                    Ubicacion = dto.Ubicacion,
                    EnviarRecordatorio = dto.EnviarRecordatorio,
                    MinutosAnteRecordatorio = dto.MinutosAnteRecordatorio,
                    Notas = dto.Notas,
                    CreadoPorId = userId
                };

                await calendarioService.CrearEventoAsync(evento);

                // Agregar participantes si los hay
                if (dto.ParticipantesIds?.Any() == true)
                {
                    foreach (var participanteId in dto.ParticipantesIds)
                    {
                        _context.ParticipantesEventos.Add(new ParticipanteEvento
                        {
                            EventoId = evento.Id,
                            UsuarioId = participanteId,
                            Estado = EstadoParticipacion.Pendiente
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, id = evento.Id, mensaje = "Evento creado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error creando evento");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Actualizar evento
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarEvento(
            [FromServices] ICalendarioService calendarioService,
            [FromBody] EventoDto dto)
        {
            try
            {
                var evento = new EventoAdmin
                {
                    Titulo = dto.Titulo,
                    Descripcion = dto.Descripcion,
                    Tipo = dto.Tipo,
                    Color = dto.Color,
                    FechaInicio = dto.FechaInicio,
                    FechaFin = dto.FechaFin,
                    TodoElDia = dto.TodoElDia,
                    Ubicacion = dto.Ubicacion,
                    EnviarRecordatorio = dto.EnviarRecordatorio,
                    MinutosAnteRecordatorio = dto.MinutosAnteRecordatorio,
                    Notas = dto.Notas
                };

                var resultado = await calendarioService.ActualizarEventoAsync(dto.Id, evento);

                if (resultado == null)
                    return Json(new { success = false, error = "Evento no encontrado" });

                return Json(new { success = true, mensaje = "Evento actualizado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error actualizando evento {Id}", dto.Id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Mover evento (drag & drop)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MoverEvento(
            int id,
            DateTime start,
            DateTime? end)
        {
            try
            {
                var evento = await _context.EventosAdmin.FindAsync(id);
                if (evento == null)
                    return Json(new { success = false, error = "Evento no encontrado" });

                evento.FechaInicio = start;
                evento.FechaFin = end;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error moviendo evento {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar evento
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarEvento(
            [FromServices] ICalendarioService calendarioService,
            int id)
        {
            try
            {
                var resultado = await calendarioService.EliminarEventoAsync(id);

                if (!resultado)
                    return Json(new { success = false, error = "Evento no encontrado" });

                return Json(new { success = true, mensaje = "Evento eliminado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error eliminando evento {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Responder a invitación de evento
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ResponderEvento(
            [FromServices] ICalendarioService calendarioService,
            int eventoId,
            EstadoParticipacion estado)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, error = "No autenticado" });

                await calendarioService.ResponderEventoAsync(eventoId, userId, estado);

                return Json(new { success = true, mensaje = "Respuesta registrada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error respondiendo evento");
                return Json(new { success = false, error = ex.Message });
            }
        }

        private static string GetColorHex(ColorEvento color)
        {
            return color switch
            {
                ColorEvento.Azul => "#3b82f6",
                ColorEvento.Verde => "#10b981",
                ColorEvento.Rojo => "#ef4444",
                ColorEvento.Amarillo => "#f59e0b",
                ColorEvento.Morado => "#8b5cf6",
                ColorEvento.Rosa => "#ec4899",
                ColorEvento.Naranja => "#f97316",
                ColorEvento.Gris => "#64748b",
                _ => "#3b82f6"
            };
        }
    }

    public class EventoDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = "";
        public string? Descripcion { get; set; }
        public TipoEventoAdmin Tipo { get; set; }
        public ColorEvento Color { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public bool TodoElDia { get; set; }
        public string? Ubicacion { get; set; }
        public bool EnviarRecordatorio { get; set; }
        public int MinutosAnteRecordatorio { get; set; }
        public string? Notas { get; set; }
        public List<string>? ParticipantesIds { get; set; }
    }
}
