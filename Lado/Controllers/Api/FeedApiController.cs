using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.DTOs.Common;
using Lado.DTOs.Feed;
using Lado.DTOs.Usuario;
using Lado.Services;
using System.Security.Claims;

namespace Lado.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class FeedApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IFeedAlgorithmService _feedService;
        private readonly ILogger<FeedApiController> _logger;

        public FeedApiController(
            ApplicationDbContext context,
            IFeedAlgorithmService feedService,
            ILogger<FeedApiController> logger)
        {
            _context = context;
            _feedService = feedService;
            _logger = logger;
        }

        /// <summary>
        /// Obtener feed principal del usuario
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<ContenidoDto>>> GetFeed(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string lado = "A") // A = publico, B = premium
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                // Obtener IDs de creadores a los que esta suscrito
                var suscripcionesIds = await _context.Suscripciones
                    .Where(s => s.FanId == userId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                // Obtener IDs de usuarios bloqueados
                var bloqueadosIds = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == userId || b.BloqueadoId == userId)
                    .Select(b => b.BloqueadorId == userId ? b.BloqueadoId : b.BloqueadorId)
                    .ToListAsync();

                var query = _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Archivos.OrderBy(a => a.Orden))
                    .Where(c => c.EstaActivo && !bloqueadosIds.Contains(c.UsuarioId));

                // Filtrar por lado
                if (lado.ToUpper() == "B")
                {
                    // Lado B: contenido de suscripciones
                    query = query.Where(c => c.TipoLado == TipoLado.LadoB && suscripcionesIds.Contains(c.UsuarioId));
                }
                else
                {
                    // Lado A: contenido publico de suscripciones
                    query = query.Where(c => c.TipoLado == TipoLado.LadoA && suscripcionesIds.Contains(c.UsuarioId));
                }

                var totalItems = await query.CountAsync();

                var contenidos = await query
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var contenidoDtos = await MapContenidosAsync(contenidos, userId, suscripcionesIds);

                return Ok(PaginatedResponse<ContenidoDto>.Create(contenidoDtos, totalItems, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo feed");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener contenido para explorar (descubrimiento)
        /// </summary>
        [HttpGet("explorar")]
        public async Task<ActionResult<PaginatedResponse<ContenidoDto>>> GetExplorar(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? categoria = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                // Obtener IDs de usuarios bloqueados
                var bloqueadosIds = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == userId || b.BloqueadoId == userId)
                    .Select(b => b.BloqueadorId == userId ? b.BloqueadoId : b.BloqueadorId)
                    .ToListAsync();

                // Obtener suscripciones
                var suscripcionesIds = await _context.Suscripciones
                    .Where(s => s.FanId == userId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                var query = _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Archivos.OrderBy(a => a.Orden))
                    .Where(c => c.EstaActivo &&
                               c.TipoLado == TipoLado.LadoA && // Solo contenido publico
                               !bloqueadosIds.Contains(c.UsuarioId) &&
                               !suscripcionesIds.Contains(c.UsuarioId)); // Excluir suscripciones

                if (!string.IsNullOrEmpty(categoria) && int.TryParse(categoria, out var catId))
                {
                    query = query.Where(c => c.CategoriaInteresId == catId);
                }

                var totalItems = await query.CountAsync();

                // Ordenar por engagement (likes + comentarios)
                var contenidos = await query
                    .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var contenidoDtos = await MapContenidosAsync(contenidos, userId, suscripcionesIds);

                return Ok(PaginatedResponse<ContenidoDto>.Create(contenidoDtos, totalItems, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo explorar");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener stories del feed
        /// </summary>
        [HttpGet("stories")]
        public async Task<ActionResult<ApiResponse<List<StoriesCreadorDto>>>> GetStories(
            [FromQuery] string lado = "A")
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<List<StoriesCreadorDto>>.Fail("No autenticado"));
                }

                // Obtener suscripciones
                var suscripcionesIds = await _context.Suscripciones
                    .Where(s => s.FanId == userId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                // Obtener stories activas de suscripciones
                var stories = await _context.Stories
                    .Include(s => s.Creador)
                    .Include(s => s.Vistas)
                    .Where(s => s.EstaActivo &&
                               s.FechaExpiracion > DateTime.UtcNow &&
                               s.TipoLado == (lado.ToUpper() == "B" ? TipoLado.LadoB : TipoLado.LadoA) &&
                               suscripcionesIds.Contains(s.CreadorId))
                    .OrderByDescending(s => s.FechaPublicacion)
                    .ToListAsync();

                // Agrupar por creador
                var storiesPorCreador = stories
                    .GroupBy(s => s.CreadorId)
                    .Select(g => new StoriesCreadorDto
                    {
                        Creador = MapUsuario(g.First().Creador),
                        Stories = g.Select(s => new StoryDto
                        {
                            Id = s.Id,
                            Creador = MapUsuario(s.Creador),
                            RutaArchivo = s.RutaArchivo,
                            TipoArchivo = s.TipoContenido.ToString(),
                            Texto = s.Texto,
                            TipoLado = s.TipoLado.ToString(),
                            NumeroVistas = s.NumeroVistas,
                            FechaPublicacion = s.FechaPublicacion,
                            FechaExpiracion = s.FechaExpiracion,
                            YaVista = s.Vistas.Any(v => v.UsuarioId == userId)
                        }).ToList(),
                        TotalStories = g.Count(),
                        TodasVistas = g.All(s => s.Vistas.Any(v => v.UsuarioId == userId))
                    })
                    .OrderBy(s => s.TodasVistas) // Stories no vistas primero
                    .ThenByDescending(s => s.Stories.Max(st => st.FechaPublicacion))
                    .ToList();

                return Ok(ApiResponse<List<StoriesCreadorDto>>.Ok(storiesPorCreador));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo stories");
                return StatusCode(500, ApiResponse<List<StoriesCreadorDto>>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Marcar story como vista
        /// </summary>
        [HttpPost("stories/{storyId}/vista")]
        public async Task<ActionResult<ApiResponse>> MarcarStoryVista(int storyId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var story = await _context.Stories.FindAsync(storyId);
                if (story == null)
                {
                    return NotFound(ApiResponse.Fail("Story no encontrada"));
                }

                // Verificar si ya existe vista
                var vistaExistente = await _context.StoryVistas
                    .AnyAsync(v => v.StoryId == storyId && v.UsuarioId == userId);

                if (!vistaExistente)
                {
                    _context.StoryVistas.Add(new StoryVista
                    {
                        StoryId = storyId,
                        UsuarioId = userId,
                        FechaVista = DateTime.UtcNow
                    });

                    story.NumeroVistas++;
                    await _context.SaveChangesAsync();
                }

                return Ok(ApiResponse.Ok("Vista registrada"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marcando story vista");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Buscar creadores
        /// </summary>
        [HttpGet("buscar/creadores")]
        public async Task<ActionResult<PaginatedResponse<CreadorDto>>> BuscarCreadores(
            [FromQuery] string q = "",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var query = _context.Users
                    .Where(u => u.EsCreador && u.EstaActivo);

                if (!string.IsNullOrEmpty(q))
                {
                    q = q.ToLower();
                    query = query.Where(u =>
                        (u.UserName != null && u.UserName.ToLower().Contains(q)) ||
                        (u.NombreCompleto != null && u.NombreCompleto.ToLower().Contains(q)));
                }

                var totalItems = await query.CountAsync();

                // Obtener suscripciones del usuario actual
                var suscripcionesIds = !string.IsNullOrEmpty(userId)
                    ? await _context.Suscripciones
                        .Where(s => s.FanId == userId && s.EstaActiva)
                        .Select(s => s.CreadorId)
                        .ToListAsync()
                    : new List<string>();

                var creadores = await query
                    .OrderByDescending(u => u.Suscriptores.Count(s => s.EstaActiva))
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new CreadorDto
                    {
                        Id = u.Id,
                        UserName = u.UserName ?? "",
                        NombreCompleto = u.NombreCompleto ?? "",
                        FotoPerfil = u.FotoPerfil,
                        FotoPortada = u.FotoPortada,
                        EstaVerificado = u.CreadorVerificado,
                        PrecioSuscripcion = u.PrecioSuscripcion,
                        TotalSuscriptores = u.Suscriptores.Count(s => s.EstaActiva),
                        TotalPublicaciones = _context.Contenidos.Count(c => c.UsuarioId == u.Id && c.EstaActivo),
                        Biografia = u.Biografia,
                        EstaSuscrito = suscripcionesIds.Contains(u.Id)
                    })
                    .ToListAsync();

                return Ok(PaginatedResponse<CreadorDto>.Create(creadores, totalItems, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error buscando creadores");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener categorias de interes
        /// </summary>
        [HttpGet("categorias")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<CategoriaInteresDto>>>> GetCategorias()
        {
            try
            {
                var categorias = await _context.CategoriasIntereses
                    .Where(c => c.EstaActiva && c.CategoriaPadreId == null)
                    .Include(c => c.Subcategorias.Where(s => s.EstaActiva))
                    .OrderBy(c => c.Orden)
                    .Select(c => new CategoriaInteresDto
                    {
                        Id = c.Id,
                        Nombre = c.Nombre,
                        Icono = c.Icono,
                        Subcategorias = c.Subcategorias
                            .OrderBy(s => s.Orden)
                            .Select(s => new CategoriaInteresDto
                            {
                                Id = s.Id,
                                Nombre = s.Nombre,
                                Icono = s.Icono
                            }).ToList()
                    })
                    .ToListAsync();

                return Ok(ApiResponse<List<CategoriaInteresDto>>.Ok(categorias));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo categorias");
                return StatusCode(500, ApiResponse<List<CategoriaInteresDto>>.Fail("Error interno del servidor"));
            }
        }

        #region Private Methods

        private async Task<List<ContenidoDto>> MapContenidosAsync(
            List<Contenido> contenidos,
            string userId,
            List<string> suscripcionesIds)
        {
            // Obtener IDs de contenidos
            var contenidoIds = contenidos.Select(c => c.Id).ToList();

            // Obtener likes del usuario
            var misLikes = await _context.Likes
                .Where(l => l.UsuarioId == userId && contenidoIds.Contains(l.ContenidoId))
                .Select(l => l.ContenidoId)
                .ToListAsync();

            // Obtener favoritos del usuario
            var misFavoritos = await _context.Favoritos
                .Where(f => f.UsuarioId == userId && contenidoIds.Contains(f.ContenidoId))
                .Select(f => f.ContenidoId)
                .ToListAsync();

            // Obtener reacciones del usuario
            var misReacciones = await _context.Reacciones
                .Where(r => r.UsuarioId == userId && contenidoIds.Contains(r.ContenidoId))
                .ToDictionaryAsync(r => r.ContenidoId, r => r.TipoReaccion.ToString());

            // Obtener compras del usuario
            var misCompras = await _context.ComprasContenido
                .Where(c => c.UsuarioId == userId && contenidoIds.Contains(c.ContenidoId))
                .Select(c => c.ContenidoId)
                .ToListAsync();

            return contenidos.Select(c =>
            {
                var estaDesbloqueado = c.EsGratis ||
                                       c.UsuarioId == userId ||
                                       suscripcionesIds.Contains(c.UsuarioId) ||
                                       misCompras.Contains(c.Id);

                return new ContenidoDto
                {
                    Id = c.Id,
                    Creador = MapUsuario(c.Usuario!),
                    Texto = c.Descripcion,
                    TipoContenido = c.TipoContenido.ToString(),
                    TipoLado = c.TipoLado.ToString(),
                    EsGratuito = c.EsGratis,
                    PrecioDesbloqueo = c.PrecioDesbloqueo,
                    Archivos = estaDesbloqueado
                        ? c.Archivos.Select(a => new ArchivoContenidoDto
                        {
                            Id = a.Id,
                            RutaArchivo = a.RutaArchivo,
                            TipoArchivo = a.TipoArchivo.ToString(),
                            Thumbnail = a.Thumbnail,
                            Duracion = a.DuracionSegundos,
                            Orden = a.Orden
                        }).ToList()
                        : new List<ArchivoContenidoDto>(),
                    RutaPreview = c.RutaPreview,
                    TienePreview = c.TienePreview,
                    EstaDesbloqueado = estaDesbloqueado,
                    CantidadArchivos = c.Archivos.Count,
                    NumeroLikes = c.NumeroLikes,
                    NumeroComentarios = c.NumeroComentarios,
                    NumeroVistas = c.NumeroVistas,
                    MeGusta = misLikes.Contains(c.Id),
                    EsFavorito = misFavoritos.Contains(c.Id),
                    MiReaccion = misReacciones.GetValueOrDefault(c.Id),
                    FechaPublicacion = c.FechaPublicacion,
                    TiempoRelativo = GetTiempoRelativo(c.FechaPublicacion)
                };
            }).ToList();
        }

        private static UsuarioDto MapUsuario(ApplicationUser user)
        {
            return new UsuarioDto
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                NombreCompleto = user.NombreCompleto ?? "",
                FotoPerfil = user.FotoPerfil,
                EsCreador = user.EsCreador,
                EstaVerificado = user.CreadorVerificado
            };
        }

        private static string GetTiempoRelativo(DateTime fecha)
        {
            var diff = DateTime.Now - fecha;

            if (diff.TotalMinutes < 1) return "ahora";
            if (diff.TotalMinutes < 60) return $"hace {(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24) return $"hace {(int)diff.TotalHours}h";
            if (diff.TotalDays < 7) return $"hace {(int)diff.TotalDays}d";
            if (diff.TotalDays < 30) return $"hace {(int)(diff.TotalDays / 7)}sem";
            if (diff.TotalDays < 365) return $"hace {(int)(diff.TotalDays / 30)}mes";
            return $"hace {(int)(diff.TotalDays / 365)}año";
        }

        #endregion
    }

    /// <summary>
    /// DTO de categoria de interes
    /// </summary>
    public class CategoriaInteresDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Icono { get; set; }
        public List<CategoriaInteresDto> Subcategorias { get; set; } = new();
    }
}
