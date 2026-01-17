using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    public interface ITemplatesService
    {
        Task<List<TemplateRespuesta>> ObtenerTemplatesAsync(CategoriaTemplate? categoria = null);
        Task<TemplateRespuesta?> ObtenerTemplateAsync(int id);
        Task<TemplateRespuesta?> ObtenerTemplatePorAtajoAsync(string atajo);
        Task<TemplateRespuesta> CrearTemplateAsync(TemplateRespuesta template, string adminId);
        Task<TemplateRespuesta?> ActualizarTemplateAsync(int id, TemplateRespuesta template);
        Task<bool> EliminarTemplateAsync(int id);
        Task IncrementarUsoAsync(int id);
        string ProcesarTemplate(string contenido, Dictionary<string, string> variables);
    }

    public class TemplatesService : ITemplatesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TemplatesService> _logger;
        private const string CACHE_KEY = "TemplatesRespuesta";

        public TemplatesService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<TemplatesService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<TemplateRespuesta>> ObtenerTemplatesAsync(CategoriaTemplate? categoria = null)
        {
            var query = _context.TemplatesRespuesta
                .Where(t => t.EstaActivo)
                .OrderBy(t => t.Categoria)
                .ThenBy(t => t.Orden)
                .ThenBy(t => t.Nombre)
                .AsQueryable();

            if (categoria.HasValue)
                query = query.Where(t => t.Categoria == categoria.Value);

            return await query.ToListAsync();
        }

        public async Task<TemplateRespuesta?> ObtenerTemplateAsync(int id)
        {
            return await _context.TemplatesRespuesta.FindAsync(id);
        }

        public async Task<TemplateRespuesta?> ObtenerTemplatePorAtajoAsync(string atajo)
        {
            return await _context.TemplatesRespuesta
                .FirstOrDefaultAsync(t => t.Atajo == atajo && t.EstaActivo);
        }

        public async Task<TemplateRespuesta> CrearTemplateAsync(TemplateRespuesta template, string adminId)
        {
            template.CreadoPorId = adminId;
            template.FechaCreacion = DateTime.Now;
            template.EstaActivo = true;

            _context.TemplatesRespuesta.Add(template);
            await _context.SaveChangesAsync();

            InvalidateCache();
            _logger.LogInformation("[Templates] Template creado: {Nombre} por {Admin}", template.Nombre, adminId);

            return template;
        }

        public async Task<TemplateRespuesta?> ActualizarTemplateAsync(int id, TemplateRespuesta template)
        {
            var existente = await _context.TemplatesRespuesta.FindAsync(id);
            if (existente == null) return null;

            existente.Nombre = template.Nombre;
            existente.Categoria = template.Categoria;
            existente.Contenido = template.Contenido;
            existente.Descripcion = template.Descripcion;
            existente.Atajo = template.Atajo;
            existente.Orden = template.Orden;

            await _context.SaveChangesAsync();
            InvalidateCache();

            return existente;
        }

        public async Task<bool> EliminarTemplateAsync(int id)
        {
            var template = await _context.TemplatesRespuesta.FindAsync(id);
            if (template == null) return false;

            template.EstaActivo = false;
            await _context.SaveChangesAsync();
            InvalidateCache();

            return true;
        }

        public async Task IncrementarUsoAsync(int id)
        {
            var template = await _context.TemplatesRespuesta.FindAsync(id);
            if (template != null)
            {
                template.VecesUsado++;
                await _context.SaveChangesAsync();
            }
        }

        public string ProcesarTemplate(string contenido, Dictionary<string, string> variables)
        {
            var resultado = contenido;

            foreach (var variable in variables)
            {
                resultado = resultado.Replace($"{{{variable.Key}}}", variable.Value);
            }

            // Variables predefinidas
            resultado = resultado.Replace("{fecha}", DateTime.Now.ToString("dd/MM/yyyy"));
            resultado = resultado.Replace("{hora}", DateTime.Now.ToString("HH:mm"));

            return resultado;
        }

        private void InvalidateCache()
        {
            _cache.Remove(CACHE_KEY);
        }
    }
}
