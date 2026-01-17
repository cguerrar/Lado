using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface INotasInternasService
    {
        Task<NotaInterna> CrearNotaAsync(TipoEntidadNota tipo, string entidadId, string contenido, string adminId, PrioridadNota prioridad = PrioridadNota.Normal, string? tags = null);
        Task<NotaInterna?> ObtenerNotaAsync(int id);
        Task<List<NotaInterna>> ObtenerNotasPorEntidadAsync(TipoEntidadNota tipo, string entidadId);
        Task<List<NotaInterna>> ObtenerNotasRecientesAsync(int cantidad = 50);
        Task<List<NotaInterna>> BuscarNotasAsync(string termino, TipoEntidadNota? tipo = null);
        Task<NotaInterna?> EditarNotaAsync(int id, string contenido, string adminId, PrioridadNota? prioridad = null, string? tags = null);
        Task<bool> EliminarNotaAsync(int id, string adminId);
        Task<bool> FijarNotaAsync(int id, bool fijar);
        Task<int> ContarNotasPorEntidadAsync(TipoEntidadNota tipo, string entidadId);
    }

    public class NotasInternasService : INotasInternasService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotasInternasService> _logger;

        public NotasInternasService(
            ApplicationDbContext context,
            ILogger<NotasInternasService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<NotaInterna> CrearNotaAsync(
            TipoEntidadNota tipo,
            string entidadId,
            string contenido,
            string adminId,
            PrioridadNota prioridad = PrioridadNota.Normal,
            string? tags = null)
        {
            var nota = new NotaInterna
            {
                TipoEntidad = tipo,
                EntidadId = entidadId,
                Contenido = contenido,
                CreadoPorId = adminId,
                Prioridad = prioridad,
                Tags = tags,
                FechaCreacion = DateTime.Now,
                EstaActiva = true
            };

            _context.NotasInternas.Add(nota);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[NotasInternas] Nota creada por {AdminId} para {Tipo}/{EntidadId}",
                adminId, tipo, entidadId);

            return nota;
        }

        public async Task<NotaInterna?> ObtenerNotaAsync(int id)
        {
            return await _context.NotasInternas
                .Include(n => n.CreadoPor)
                .Include(n => n.EditadoPor)
                .FirstOrDefaultAsync(n => n.Id == id && n.EstaActiva);
        }

        public async Task<List<NotaInterna>> ObtenerNotasPorEntidadAsync(TipoEntidadNota tipo, string entidadId)
        {
            return await _context.NotasInternas
                .Include(n => n.CreadoPor)
                .Include(n => n.EditadoPor)
                .Where(n => n.TipoEntidad == tipo && n.EntidadId == entidadId && n.EstaActiva)
                .OrderByDescending(n => n.EsFijada)
                .ThenByDescending(n => n.Prioridad)
                .ThenByDescending(n => n.FechaCreacion)
                .ToListAsync();
        }

        public async Task<List<NotaInterna>> ObtenerNotasRecientesAsync(int cantidad = 50)
        {
            return await _context.NotasInternas
                .Include(n => n.CreadoPor)
                .Where(n => n.EstaActiva)
                .OrderByDescending(n => n.FechaCreacion)
                .Take(cantidad)
                .ToListAsync();
        }

        public async Task<List<NotaInterna>> BuscarNotasAsync(string termino, TipoEntidadNota? tipo = null)
        {
            var query = _context.NotasInternas
                .Include(n => n.CreadoPor)
                .Where(n => n.EstaActiva);

            if (!string.IsNullOrWhiteSpace(termino))
            {
                query = query.Where(n => n.Contenido.Contains(termino) ||
                                        (n.Tags != null && n.Tags.Contains(termino)));
            }

            if (tipo.HasValue)
            {
                query = query.Where(n => n.TipoEntidad == tipo.Value);
            }

            return await query
                .OrderByDescending(n => n.FechaCreacion)
                .Take(100)
                .ToListAsync();
        }

        public async Task<NotaInterna?> EditarNotaAsync(
            int id,
            string contenido,
            string adminId,
            PrioridadNota? prioridad = null,
            string? tags = null)
        {
            var nota = await _context.NotasInternas.FindAsync(id);
            if (nota == null || !nota.EstaActiva)
                return null;

            nota.Contenido = contenido;
            nota.FechaEdicion = DateTime.Now;
            nota.EditadoPorId = adminId;

            if (prioridad.HasValue)
                nota.Prioridad = prioridad.Value;

            if (tags != null)
                nota.Tags = tags;

            await _context.SaveChangesAsync();

            _logger.LogInformation("[NotasInternas] Nota {Id} editada por {AdminId}", id, adminId);

            return nota;
        }

        public async Task<bool> EliminarNotaAsync(int id, string adminId)
        {
            var nota = await _context.NotasInternas.FindAsync(id);
            if (nota == null)
                return false;

            nota.EstaActiva = false;
            nota.FechaEdicion = DateTime.Now;
            nota.EditadoPorId = adminId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("[NotasInternas] Nota {Id} eliminada por {AdminId}", id, adminId);

            return true;
        }

        public async Task<bool> FijarNotaAsync(int id, bool fijar)
        {
            var nota = await _context.NotasInternas.FindAsync(id);
            if (nota == null || !nota.EstaActiva)
                return false;

            nota.EsFijada = fijar;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<int> ContarNotasPorEntidadAsync(TipoEntidadNota tipo, string entidadId)
        {
            return await _context.NotasInternas
                .CountAsync(n => n.TipoEntidad == tipo && n.EntidadId == entidadId && n.EstaActiva);
        }
    }
}
