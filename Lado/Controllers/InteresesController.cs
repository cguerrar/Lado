using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InteresesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IInteresesService _interesesService;
        private readonly ILogger<InteresesController> _logger;
        private readonly IRateLimitService _rateLimitService;

        public InteresesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInteresesService interesesService,
            ILogger<InteresesController> logger,
            IRateLimitService rateLimitService)
        {
            _context = context;
            _userManager = userManager;
            _interesesService = interesesService;
            _logger = logger;
            _rateLimitService = rateLimitService;
        }

        /// <summary>
        /// Obtiene todas las categorias de interes disponibles
        /// </summary>
        [HttpGet("categorias")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerCategorias()
        {
            var categorias = await _context.CategoriasIntereses
                .Where(c => c.EstaActiva && c.CategoriaPadreId == null)
                .OrderBy(c => c.Orden)
                .Select(c => new
                {
                    c.Id,
                    c.Nombre,
                    c.Descripcion,
                    c.Icono,
                    c.Color,
                    Subcategorias = c.Subcategorias
                        .Where(s => s.EstaActiva)
                        .OrderBy(s => s.Orden)
                        .Select(s => new
                        {
                            s.Id,
                            s.Nombre,
                            s.Descripcion,
                            s.Icono,
                            s.Color
                        }).ToList()
                })
                .ToListAsync();

            return Ok(categorias);
        }

        /// <summary>
        /// Obtiene los intereses del usuario actual
        /// </summary>
        [HttpGet("mis-intereses")]
        public async Task<IActionResult> ObtenerMisIntereses()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return Unauthorized();

            var intereses = await _interesesService.ObtenerInteresesUsuarioAsync(usuario.Id, 20);

            return Ok(intereses.Select(i => new
            {
                categoriaId = i.CategoriaInteresId,
                nombre = i.CategoriaInteres?.Nombre,
                icono = i.CategoriaInteres?.Icono,
                color = i.CategoriaInteres?.Color,
                tipo = i.Tipo.ToString(),
                peso = i.PesoInteres,
                interacciones = i.ContadorInteracciones,
                ultimaInteraccion = i.UltimaInteraccion
            }));
        }

        /// <summary>
        /// Agrega un interes explicito al usuario
        /// </summary>
        [HttpPost("agregar/{categoriaId}")]
        public async Task<IActionResult> AgregarInteres(int categoriaId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return Unauthorized();

            // Rate limiting: máximo 50 cambios de intereses por 5 minutos
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!await _rateLimitService.IsAllowedAsync(clientIp, $"intereses_user_{usuario.Id}", 50, TimeSpan.FromMinutes(5),
                TipoAtaque.Otro, "/api/Intereses/agregar", usuario.Id))
            {
                return StatusCode(429, new { message = "Demasiadas solicitudes. Espera unos minutos." });
            }

            var categoriaExiste = await _context.CategoriasIntereses
                .AnyAsync(c => c.Id == categoriaId && c.EstaActiva);

            if (!categoriaExiste)
                return NotFound(new { message = "Categoria no encontrada" });

            await _interesesService.AgregarInteresExplicitoAsync(usuario.Id, categoriaId);

            _logger.LogInformation("Usuario {UserId} agrego interes explicito: Categoria {CategoriaId}",
                usuario.Id, categoriaId);

            return Ok(new { success = true, message = "Interes agregado correctamente" });
        }

        /// <summary>
        /// Elimina un interes del usuario
        /// </summary>
        [HttpDelete("eliminar/{categoriaId}")]
        public async Task<IActionResult> EliminarInteres(int categoriaId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return Unauthorized();

            await _interesesService.EliminarInteresAsync(usuario.Id, categoriaId);

            _logger.LogInformation("Usuario {UserId} elimino interes: Categoria {CategoriaId}",
                usuario.Id, categoriaId);

            return Ok(new { success = true, message = "Interes eliminado correctamente" });
        }

        /// <summary>
        /// Recalcula los pesos de intereses del usuario basado en historial
        /// </summary>
        [HttpPost("recalcular")]
        public async Task<IActionResult> RecalcularPesos()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return Unauthorized();

            // Rate limiting: máximo 5 recálculos por hora (operación costosa)
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!await _rateLimitService.IsAllowedAsync(clientIp, $"recalcular_user_{usuario.Id}", 5, TimeSpan.FromHours(1),
                TipoAtaque.Otro, "/api/Intereses/recalcular", usuario.Id))
            {
                return StatusCode(429, new { message = "Demasiados recálculos. Espera una hora." });
            }

            await _interesesService.RecalcularPesosUsuarioAsync(usuario.Id);

            return Ok(new { success = true, message = "Pesos recalculados correctamente" });
        }

        /// <summary>
        /// Guarda multiples intereses explicitos (para onboarding)
        /// </summary>
        [HttpPost("guardar-multiples")]
        public async Task<IActionResult> GuardarMultiplesIntereses([FromBody] List<int> categoriaIds)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return Unauthorized();

            // Rate limiting: máximo 10 guardados por hora
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!await _rateLimitService.IsAllowedAsync(clientIp, $"guardar_intereses_user_{usuario.Id}", 10, TimeSpan.FromHours(1),
                TipoAtaque.Otro, "/api/Intereses/guardar-multiples", usuario.Id))
            {
                return StatusCode(429, new { message = "Demasiadas solicitudes. Espera una hora." });
            }

            if (categoriaIds == null || !categoriaIds.Any())
                return BadRequest(new { message = "Debe seleccionar al menos una categoria" });

            // Eliminar intereses explicitos anteriores
            var interesesAnteriores = await _context.InteresesUsuarios
                .Where(i => i.UsuarioId == usuario.Id && i.Tipo == TipoInteres.Explicito)
                .ToListAsync();

            _context.InteresesUsuarios.RemoveRange(interesesAnteriores);

            // OPTIMIZACIÓN: Pre-cargar todas las categorías válidas en una sola query
            var categoriasDistintas = categoriaIds.Distinct().ToList();
            var categoriasActivasList = await _context.CategoriasIntereses
                .Where(c => categoriasDistintas.Contains(c.Id) && c.EstaActiva)
                .Select(c => c.Id)
                .ToListAsync();
            var categoriasActivas = new HashSet<int>(categoriasActivasList);

            // Agregar nuevos intereses (sin N+1)
            foreach (var categoriaId in categoriasDistintas)
            {
                if (categoriasActivas.Contains(categoriaId))
                {
                    await _interesesService.AgregarInteresExplicitoAsync(usuario.Id, categoriaId);
                }
            }

            _logger.LogInformation("Usuario {UserId} guardo {Count} intereses explicitos",
                usuario.Id, categoriaIds.Count);

            return Ok(new { success = true, message = $"{categoriaIds.Count} intereses guardados" });
        }

        // ========================================
        // ENDPOINTS ADMIN
        // ========================================

        /// <summary>
        /// Obtiene estadisticas de intereses (solo admin)
        /// </summary>
        [HttpGet("admin/estadisticas")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ObtenerEstadisticasAdmin()
        {
            var totalUsuariosConIntereses = await _context.InteresesUsuarios
                .Select(i => i.UsuarioId)
                .Distinct()
                .CountAsync();

            var interesesPorCategoria = await _context.InteresesUsuarios
                .GroupBy(i => i.CategoriaInteresId)
                .Select(g => new
                {
                    CategoriaId = g.Key,
                    TotalUsuarios = g.Count(),
                    PromedioePeso = g.Average(i => i.PesoInteres)
                })
                .OrderByDescending(x => x.TotalUsuarios)
                .Take(10)
                .ToListAsync();

            var categoriasIds = interesesPorCategoria.Select(x => x.CategoriaId).ToList();
            var categorias = await _context.CategoriasIntereses
                .Where(c => categoriasIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Nombre);

            var resultado = interesesPorCategoria.Select(x => new
            {
                x.CategoriaId,
                Nombre = categorias.GetValueOrDefault(x.CategoriaId, "Desconocido"),
                x.TotalUsuarios,
                x.PromedioePeso
            });

            return Ok(new
            {
                totalUsuariosConIntereses,
                topCategorias = resultado,
                totalCategorias = await _context.CategoriasIntereses.CountAsync(c => c.EstaActiva),
                totalIntereses = await _context.InteresesUsuarios.CountAsync()
            });
        }

        /// <summary>
        /// Crea una nueva categoria de interes (solo admin)
        /// </summary>
        [HttpPost("admin/categorias")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CrearCategoria([FromBody] CrearCategoriaRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Nombre))
                return BadRequest(new { message = "El nombre es requerido" });

            var categoria = new CategoriaInteres
            {
                Nombre = request.Nombre.Trim(),
                Descripcion = request.Descripcion?.Trim(),
                Icono = request.Icono?.Trim(),
                Color = request.Color?.Trim(),
                CategoriaPadreId = request.CategoriaPadreId,
                Orden = request.Orden,
                EstaActiva = true
            };

            _context.CategoriasIntereses.Add(categoria);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Nueva categoria de interes creada: {Nombre} (ID: {Id})",
                categoria.Nombre, categoria.Id);

            return Ok(new { success = true, id = categoria.Id, message = "Categoria creada correctamente" });
        }

        /// <summary>
        /// Actualiza una categoria de interes (solo admin)
        /// </summary>
        [HttpPut("admin/categorias/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ActualizarCategoria(int id, [FromBody] CrearCategoriaRequest request)
        {
            var categoria = await _context.CategoriasIntereses.FindAsync(id);
            if (categoria == null)
                return NotFound(new { message = "Categoria no encontrada" });

            if (!string.IsNullOrWhiteSpace(request.Nombre))
                categoria.Nombre = request.Nombre.Trim();

            categoria.Descripcion = request.Descripcion?.Trim();
            categoria.Icono = request.Icono?.Trim();
            categoria.Color = request.Color?.Trim();
            categoria.CategoriaPadreId = request.CategoriaPadreId;
            categoria.Orden = request.Orden;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Categoria actualizada correctamente" });
        }

        /// <summary>
        /// Desactiva una categoria de interes (solo admin)
        /// </summary>
        [HttpDelete("admin/categorias/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DesactivarCategoria(int id)
        {
            var categoria = await _context.CategoriasIntereses.FindAsync(id);
            if (categoria == null)
                return NotFound(new { message = "Categoria no encontrada" });

            categoria.EstaActiva = false;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Categoria desactivada correctamente" });
        }
    }

    public class CrearCategoriaRequest
    {
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
        public string? Icono { get; set; }
        public string? Color { get; set; }
        public int? CategoriaPadreId { get; set; }
        public int Orden { get; set; }
    }
}
