using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Services
{
    public interface ICalendarioService
    {
        Task<List<EventoAdmin>> ObtenerEventosAsync(DateTime inicio, DateTime fin, string? usuarioId = null);
        Task<EventoAdmin?> ObtenerEventoAsync(int id);
        Task<EventoAdmin> CrearEventoAsync(EventoAdmin evento);
        Task<EventoAdmin?> ActualizarEventoAsync(int id, EventoAdmin evento);
        Task<bool> EliminarEventoAsync(int id);
        Task<bool> ResponderEventoAsync(int eventoId, string usuarioId, EstadoParticipacion estado);
        Task<List<EventoAdmin>> ObtenerProximosEventosAsync(int cantidad = 5);
        Task<Dictionary<string, int>> ObtenerEstadisticasMesAsync(int ano, int mes);
    }

    public class CalendarioService : ICalendarioService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CalendarioService> _logger;

        public CalendarioService(ApplicationDbContext context, ILogger<CalendarioService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<EventoAdmin>> ObtenerEventosAsync(DateTime inicio, DateTime fin, string? usuarioId = null)
        {
            var query = _context.EventosAdmin
                .Include(e => e.CreadoPor)
                .Include(e => e.Participantes)
                    .ThenInclude(p => p.Usuario)
                .Where(e => !e.Cancelado)
                .Where(e =>
                    (e.FechaInicio >= inicio && e.FechaInicio <= fin) ||
                    (e.FechaFin.HasValue && e.FechaFin >= inicio && e.FechaInicio <= fin))
                .AsQueryable();

            if (!string.IsNullOrEmpty(usuarioId))
            {
                query = query.Where(e =>
                    e.CreadoPorId == usuarioId ||
                    e.Participantes.Any(p => p.UsuarioId == usuarioId));
            }

            return await query
                .OrderBy(e => e.FechaInicio)
                .ToListAsync();
        }

        public async Task<EventoAdmin?> ObtenerEventoAsync(int id)
        {
            return await _context.EventosAdmin
                .Include(e => e.CreadoPor)
                .Include(e => e.Participantes)
                    .ThenInclude(p => p.Usuario)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<EventoAdmin> CrearEventoAsync(EventoAdmin evento)
        {
            evento.FechaCreacion = DateTime.UtcNow;
            _context.EventosAdmin.Add(evento);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[Calendario] Evento #{Id} creado: {Titulo}", evento.Id, evento.Titulo);
            return evento;
        }

        public async Task<EventoAdmin?> ActualizarEventoAsync(int id, EventoAdmin eventoActualizado)
        {
            var evento = await _context.EventosAdmin.FindAsync(id);
            if (evento == null) return null;

            evento.Titulo = eventoActualizado.Titulo;
            evento.Descripcion = eventoActualizado.Descripcion;
            evento.Tipo = eventoActualizado.Tipo;
            evento.Color = eventoActualizado.Color;
            evento.FechaInicio = eventoActualizado.FechaInicio;
            evento.FechaFin = eventoActualizado.FechaFin;
            evento.TodoElDia = eventoActualizado.TodoElDia;
            evento.Ubicacion = eventoActualizado.Ubicacion;
            evento.EnviarRecordatorio = eventoActualizado.EnviarRecordatorio;
            evento.MinutosAnteRecordatorio = eventoActualizado.MinutosAnteRecordatorio;
            evento.Notas = eventoActualizado.Notas;

            await _context.SaveChangesAsync();

            _logger.LogInformation("[Calendario] Evento #{Id} actualizado", id);
            return evento;
        }

        public async Task<bool> EliminarEventoAsync(int id)
        {
            var evento = await _context.EventosAdmin.FindAsync(id);
            if (evento == null) return false;

            evento.Cancelado = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("[Calendario] Evento #{Id} eliminado/cancelado", id);
            return true;
        }

        public async Task<bool> ResponderEventoAsync(int eventoId, string usuarioId, EstadoParticipacion estado)
        {
            var participante = await _context.ParticipantesEventos
                .FirstOrDefaultAsync(p => p.EventoId == eventoId && p.UsuarioId == usuarioId);

            if (participante == null)
            {
                // Crear participante si no existe
                participante = new ParticipanteEvento
                {
                    EventoId = eventoId,
                    UsuarioId = usuarioId,
                    Estado = estado,
                    FechaRespuesta = DateTime.UtcNow
                };
                _context.ParticipantesEventos.Add(participante);
            }
            else
            {
                participante.Estado = estado;
                participante.FechaRespuesta = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<EventoAdmin>> ObtenerProximosEventosAsync(int cantidad = 5)
        {
            var ahora = DateTime.UtcNow;

            return await _context.EventosAdmin
                .Include(e => e.CreadoPor)
                .Where(e => !e.Cancelado && e.FechaInicio >= ahora)
                .OrderBy(e => e.FechaInicio)
                .Take(cantidad)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> ObtenerEstadisticasMesAsync(int ano, int mes)
        {
            var inicioMes = new DateTime(ano, mes, 1);
            var finMes = inicioMes.AddMonths(1).AddDays(-1);

            var eventos = await _context.EventosAdmin
                .Where(e => !e.Cancelado)
                .Where(e => e.FechaInicio >= inicioMes && e.FechaInicio <= finMes)
                .ToListAsync();

            return new Dictionary<string, int>
            {
                ["total"] = eventos.Count,
                ["reuniones"] = eventos.Count(e => e.Tipo == TipoEventoAdmin.Reunion),
                ["mantenimientos"] = eventos.Count(e => e.Tipo == TipoEventoAdmin.Mantenimiento),
                ["lanzamientos"] = eventos.Count(e => e.Tipo == TipoEventoAdmin.Lanzamiento),
                ["deadlines"] = eventos.Count(e => e.Tipo == TipoEventoAdmin.Deadline)
            };
        }
    }
}
