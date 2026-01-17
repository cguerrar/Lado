using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Services
{
    public interface ITicketsService
    {
        Task<List<TicketInterno>> ObtenerTicketsAsync(
            EstadoTicket? estado = null,
            CategoriaTicket? categoria = null,
            string? asignadoAId = null,
            int pagina = 1,
            int porPagina = 20);

        Task<int> ObtenerTotalTicketsAsync(
            EstadoTicket? estado = null,
            CategoriaTicket? categoria = null,
            string? asignadoAId = null);

        Task<TicketInterno?> ObtenerTicketAsync(int id);

        Task<TicketInterno> CrearTicketAsync(TicketInterno ticket);

        Task<TicketInterno?> ActualizarTicketAsync(int id, TicketInterno ticket);

        Task<bool> AsignarTicketAsync(int id, string asignadoAId);

        Task<bool> CambiarEstadoAsync(int id, EstadoTicket estado, string usuarioId);

        Task<RespuestaTicket> AgregarRespuestaAsync(int ticketId, string contenido, string autorId, bool esNotaInterna = false);

        Task<Dictionary<string, int>> ObtenerEstadisticasAsync();

        Task<List<TicketInterno>> ObtenerTicketsMiosAsync(string userId, int limite = 10);
    }

    public class TicketsService : ITicketsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TicketsService> _logger;

        public TicketsService(ApplicationDbContext context, ILogger<TicketsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<TicketInterno>> ObtenerTicketsAsync(
            EstadoTicket? estado = null,
            CategoriaTicket? categoria = null,
            string? asignadoAId = null,
            int pagina = 1,
            int porPagina = 20)
        {
            var query = _context.TicketsInternos
                .Include(t => t.CreadoPor)
                .Include(t => t.AsignadoA)
                .AsQueryable();

            if (estado.HasValue)
                query = query.Where(t => t.Estado == estado.Value);

            if (categoria.HasValue)
                query = query.Where(t => t.Categoria == categoria.Value);

            if (!string.IsNullOrEmpty(asignadoAId))
                query = query.Where(t => t.AsignadoAId == asignadoAId);

            return await query
                .OrderByDescending(t => t.Prioridad)
                .ThenByDescending(t => t.FechaCreacion)
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();
        }

        public async Task<int> ObtenerTotalTicketsAsync(
            EstadoTicket? estado = null,
            CategoriaTicket? categoria = null,
            string? asignadoAId = null)
        {
            var query = _context.TicketsInternos.AsQueryable();

            if (estado.HasValue)
                query = query.Where(t => t.Estado == estado.Value);

            if (categoria.HasValue)
                query = query.Where(t => t.Categoria == categoria.Value);

            if (!string.IsNullOrEmpty(asignadoAId))
                query = query.Where(t => t.AsignadoAId == asignadoAId);

            return await query.CountAsync();
        }

        public async Task<TicketInterno?> ObtenerTicketAsync(int id)
        {
            return await _context.TicketsInternos
                .Include(t => t.CreadoPor)
                .Include(t => t.AsignadoA)
                .Include(t => t.Respuestas.OrderBy(r => r.FechaCreacion))
                    .ThenInclude(r => r.Autor)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TicketInterno> CrearTicketAsync(TicketInterno ticket)
        {
            ticket.FechaCreacion = DateTime.UtcNow;
            _context.TicketsInternos.Add(ticket);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[Tickets] Ticket #{Id} creado por {UserId}", ticket.Id, ticket.CreadoPorId);
            return ticket;
        }

        public async Task<TicketInterno?> ActualizarTicketAsync(int id, TicketInterno ticketActualizado)
        {
            var ticket = await _context.TicketsInternos.FindAsync(id);
            if (ticket == null) return null;

            ticket.Titulo = ticketActualizado.Titulo;
            ticket.Descripcion = ticketActualizado.Descripcion;
            ticket.Categoria = ticketActualizado.Categoria;
            ticket.Prioridad = ticketActualizado.Prioridad;
            ticket.Etiquetas = ticketActualizado.Etiquetas;
            ticket.FechaActualizacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ticket;
        }

        public async Task<bool> AsignarTicketAsync(int id, string asignadoAId)
        {
            var ticket = await _context.TicketsInternos.FindAsync(id);
            if (ticket == null) return false;

            ticket.AsignadoAId = asignadoAId;
            ticket.FechaActualizacion = DateTime.UtcNow;

            if (ticket.Estado == EstadoTicket.Abierto)
                ticket.Estado = EstadoTicket.EnProgreso;

            await _context.SaveChangesAsync();

            _logger.LogInformation("[Tickets] Ticket #{Id} asignado a {UserId}", id, asignadoAId);
            return true;
        }

        public async Task<bool> CambiarEstadoAsync(int id, EstadoTicket estado, string usuarioId)
        {
            var ticket = await _context.TicketsInternos.FindAsync(id);
            if (ticket == null) return false;

            ticket.Estado = estado;
            ticket.FechaActualizacion = DateTime.UtcNow;

            if (estado == EstadoTicket.Cerrado || estado == EstadoTicket.Resuelto)
                ticket.FechaCierre = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("[Tickets] Ticket #{Id} cambió a estado {Estado} por {UserId}", id, estado, usuarioId);
            return true;
        }

        public async Task<RespuestaTicket> AgregarRespuestaAsync(int ticketId, string contenido, string autorId, bool esNotaInterna = false)
        {
            var ticket = await _context.TicketsInternos.FindAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Ticket no encontrado");

            var respuesta = new RespuestaTicket
            {
                TicketId = ticketId,
                Contenido = contenido,
                AutorId = autorId,
                EsNotaInterna = esNotaInterna,
                FechaCreacion = DateTime.UtcNow
            };

            _context.RespuestasTickets.Add(respuesta);

            ticket.FechaActualizacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("[Tickets] Respuesta agregada al ticket #{Id} por {UserId}", ticketId, autorId);
            return respuesta;
        }

        public async Task<Dictionary<string, int>> ObtenerEstadisticasAsync()
        {
            var stats = new Dictionary<string, int>
            {
                ["total"] = await _context.TicketsInternos.CountAsync(),
                ["abiertos"] = await _context.TicketsInternos.CountAsync(t => t.Estado == EstadoTicket.Abierto),
                ["enProgreso"] = await _context.TicketsInternos.CountAsync(t => t.Estado == EstadoTicket.EnProgreso),
                ["enEspera"] = await _context.TicketsInternos.CountAsync(t => t.Estado == EstadoTicket.EnEspera),
                ["resueltos"] = await _context.TicketsInternos.CountAsync(t => t.Estado == EstadoTicket.Resuelto),
                ["cerrados"] = await _context.TicketsInternos.CountAsync(t => t.Estado == EstadoTicket.Cerrado),
                ["criticos"] = await _context.TicketsInternos.CountAsync(t => t.Prioridad == PrioridadTicket.Critica && t.Estado != EstadoTicket.Cerrado && t.Estado != EstadoTicket.Resuelto)
            };

            return stats;
        }

        public async Task<List<TicketInterno>> ObtenerTicketsMiosAsync(string userId, int limite = 10)
        {
            return await _context.TicketsInternos
                .Include(t => t.CreadoPor)
                .Where(t => t.AsignadoAId == userId || t.CreadoPorId == userId)
                .Where(t => t.Estado != EstadoTicket.Cerrado)
                .OrderByDescending(t => t.Prioridad)
                .ThenByDescending(t => t.FechaCreacion)
                .Take(limite)
                .ToListAsync();
        }
    }
}
