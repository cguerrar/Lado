using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Lado.Services
{
    public interface IAuditoriaService
    {
        Task RegistrarCambioAsync(
            TipoConfiguracion tipo,
            string campo,
            object? valorAnterior,
            object? valorNuevo,
            string adminId,
            string? entidadId = null,
            string? descripcion = null,
            string? ip = null,
            string? userAgent = null);

        Task<List<AuditoriaConfiguracion>> ObtenerHistorialAsync(
            TipoConfiguracion? tipo = null,
            string? adminId = null,
            DateTime? desde = null,
            DateTime? hasta = null,
            int cantidad = 100);

        Task<List<AuditoriaConfiguracion>> ObtenerHistorialPorEntidadAsync(
            TipoConfiguracion tipo,
            string entidadId,
            int cantidad = 50);

        Task<Dictionary<string, int>> ObtenerEstadisticasAsync(int dias = 30);
    }

    public class AuditoriaService : IAuditoriaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditoriaService> _logger;

        public AuditoriaService(
            ApplicationDbContext context,
            ILogger<AuditoriaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RegistrarCambioAsync(
            TipoConfiguracion tipo,
            string campo,
            object? valorAnterior,
            object? valorNuevo,
            string adminId,
            string? entidadId = null,
            string? descripcion = null,
            string? ip = null,
            string? userAgent = null)
        {
            try
            {
                var registro = new AuditoriaConfiguracion
                {
                    TipoConfiguracion = tipo,
                    Campo = campo,
                    ValorAnterior = SerializarValor(valorAnterior),
                    ValorNuevo = SerializarValor(valorNuevo),
                    ModificadoPorId = adminId,
                    EntidadId = entidadId,
                    Descripcion = descripcion ?? GenerarDescripcion(tipo, campo, valorAnterior, valorNuevo),
                    IpOrigen = ip,
                    UserAgent = userAgent?.Length > 500 ? userAgent.Substring(0, 500) : userAgent,
                    FechaModificacion = DateTime.Now
                };

                _context.AuditoriasConfiguracion.Add(registro);
                await _context.SaveChangesAsync();

                _logger.LogInformation("[Auditoría] {Tipo}.{Campo} modificado por {Admin}: {ValorAnterior} -> {ValorNuevo}",
                    tipo, campo, adminId, valorAnterior, valorNuevo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Auditoría] Error registrando cambio en {Tipo}.{Campo}", tipo, campo);
            }
        }

        public async Task<List<AuditoriaConfiguracion>> ObtenerHistorialAsync(
            TipoConfiguracion? tipo = null,
            string? adminId = null,
            DateTime? desde = null,
            DateTime? hasta = null,
            int cantidad = 100)
        {
            var query = _context.AuditoriasConfiguracion
                .Include(a => a.ModificadoPor)
                .AsQueryable();

            if (tipo.HasValue)
                query = query.Where(a => a.TipoConfiguracion == tipo.Value);

            if (!string.IsNullOrEmpty(adminId))
                query = query.Where(a => a.ModificadoPorId == adminId);

            if (desde.HasValue)
                query = query.Where(a => a.FechaModificacion >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(a => a.FechaModificacion <= hasta.Value);

            return await query
                .OrderByDescending(a => a.FechaModificacion)
                .Take(cantidad)
                .ToListAsync();
        }

        public async Task<List<AuditoriaConfiguracion>> ObtenerHistorialPorEntidadAsync(
            TipoConfiguracion tipo,
            string entidadId,
            int cantidad = 50)
        {
            return await _context.AuditoriasConfiguracion
                .Include(a => a.ModificadoPor)
                .Where(a => a.TipoConfiguracion == tipo && a.EntidadId == entidadId)
                .OrderByDescending(a => a.FechaModificacion)
                .Take(cantidad)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> ObtenerEstadisticasAsync(int dias = 30)
        {
            var desde = DateTime.Now.AddDays(-dias);

            var cambiosPorTipo = await _context.AuditoriasConfiguracion
                .Where(a => a.FechaModificacion >= desde)
                .GroupBy(a => a.TipoConfiguracion)
                .Select(g => new { Tipo = g.Key.ToString(), Cantidad = g.Count() })
                .ToDictionaryAsync(x => x.Tipo, x => x.Cantidad);

            return cambiosPorTipo;
        }

        private string? SerializarValor(object? valor)
        {
            if (valor == null) return null;
            if (valor is string s) return s.Length > 2000 ? s.Substring(0, 2000) : s;

            try
            {
                var json = JsonSerializer.Serialize(valor);
                return json.Length > 2000 ? json.Substring(0, 2000) : json;
            }
            catch
            {
                return valor.ToString();
            }
        }

        private string GenerarDescripcion(TipoConfiguracion tipo, string campo, object? anterior, object? nuevo)
        {
            var tipoNombre = tipo switch
            {
                TipoConfiguracion.Plataforma => "Configuración de plataforma",
                TipoConfiguracion.Algoritmo => "Algoritmo de feed",
                TipoConfiguracion.Confianza => "Sistema de confianza",
                TipoConfiguracion.LadoCoins => "LadoCoins",
                TipoConfiguracion.Seo => "Configuración SEO",
                TipoConfiguracion.Mantenimiento => "Modo mantenimiento",
                TipoConfiguracion.Permisos => "Permisos",
                TipoConfiguracion.Rol => "Roles",
                _ => tipo.ToString()
            };

            return $"{tipoNombre}: '{campo}' cambiado de '{anterior}' a '{nuevo}'";
        }
    }
}
