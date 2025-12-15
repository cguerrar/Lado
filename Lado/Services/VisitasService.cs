using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface IVisitasService
    {
        Task RegistrarVisitaAsync(string? ipAddress, string? userAgent, string? pagina, string? usuarioId);
        Task<int> ObtenerTotalVisitasAsync();
        Task<int> ObtenerVisitasHoyAsync();
        Task<int> ObtenerVisitantesUnicosHoyAsync();
        Task<int> ObtenerVisitantesUnicosTotalAsync();
        Task<List<VisitaApp>> ObtenerVisitasUltimos7DiasAsync();
        Task<List<VisitaApp>> ObtenerVisitasUltimos30DiasAsync();
    }

    public class VisitasService : IVisitasService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VisitasService> _logger;
        private static readonly HashSet<string> _visitantesHoy = new();
        private static DateTime _ultimaLimpieza = DateTime.Today;

        public VisitasService(ApplicationDbContext context, ILogger<VisitasService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RegistrarVisitaAsync(string? ipAddress, string? userAgent, string? pagina, string? usuarioId)
        {
            try
            {
                var hoy = DateTime.Today;

                // Limpiar el HashSet si es un nuevo dia
                if (_ultimaLimpieza < hoy)
                {
                    _visitantesHoy.Clear();
                    _ultimaLimpieza = hoy;
                }

                // Verificar si es un nuevo visitante
                var identificador = ipAddress ?? "unknown";
                var esNuevoVisitante = !_visitantesHoy.Contains(identificador);

                if (esNuevoVisitante)
                {
                    _visitantesHoy.Add(identificador);
                }

                // Registrar detalle de la visita
                var visitaDetalle = new VisitaDetalle
                {
                    FechaHora = DateTime.Now,
                    IpAddress = ipAddress,
                    UserAgent = userAgent?.Length > 500 ? userAgent.Substring(0, 500) : userAgent,
                    Pagina = pagina?.Length > 200 ? pagina.Substring(0, 200) : pagina,
                    UsuarioId = usuarioId,
                    EsNuevoVisitante = esNuevoVisitante
                };

                _context.VisitasDetalle.Add(visitaDetalle);

                // Actualizar contador diario
                var visitaDiaria = await _context.VisitasApp
                    .FirstOrDefaultAsync(v => v.Fecha == hoy);

                if (visitaDiaria == null)
                {
                    visitaDiaria = new VisitaApp
                    {
                        Fecha = hoy,
                        Contador = 1,
                        VisitasUnicas = esNuevoVisitante ? 1 : 0
                    };
                    _context.VisitasApp.Add(visitaDiaria);
                }
                else
                {
                    visitaDiaria.Contador++;
                    if (esNuevoVisitante)
                    {
                        visitaDiaria.VisitasUnicas++;
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar visita");
            }
        }

        public async Task<int> ObtenerTotalVisitasAsync()
        {
            return await _context.VisitasApp.SumAsync(v => v.Contador);
        }

        public async Task<int> ObtenerVisitasHoyAsync()
        {
            var hoy = DateTime.Today;
            var visita = await _context.VisitasApp.FirstOrDefaultAsync(v => v.Fecha == hoy);
            return visita?.Contador ?? 0;
        }

        public async Task<int> ObtenerVisitantesUnicosHoyAsync()
        {
            var hoy = DateTime.Today;
            var visita = await _context.VisitasApp.FirstOrDefaultAsync(v => v.Fecha == hoy);
            return visita?.VisitasUnicas ?? 0;
        }

        public async Task<int> ObtenerVisitantesUnicosTotalAsync()
        {
            return await _context.VisitasApp.SumAsync(v => v.VisitasUnicas);
        }

        public async Task<List<VisitaApp>> ObtenerVisitasUltimos7DiasAsync()
        {
            var hace7Dias = DateTime.Today.AddDays(-6);
            return await _context.VisitasApp
                .Where(v => v.Fecha >= hace7Dias)
                .OrderBy(v => v.Fecha)
                .ToListAsync();
        }

        public async Task<List<VisitaApp>> ObtenerVisitasUltimos30DiasAsync()
        {
            var hace30Dias = DateTime.Today.AddDays(-29);
            return await _context.VisitasApp
                .Where(v => v.Fecha >= hace30Dias)
                .OrderBy(v => v.Fecha)
                .ToListAsync();
        }
    }
}
