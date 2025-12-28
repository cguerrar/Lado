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
    public class ContenidoApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IImageService _imageService;
        private readonly ILogger<ContenidoApiController> _logger;

        public ContenidoApiController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IImageService imageService,
            ILogger<ContenidoApiController> logger)
        {
            _context = context;
            _environment = environment;
            _imageService = imageService;
            _logger = logger;
        }

        /// <summary>
        /// Obtener contenido por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ContenidoDto>>> GetContenido(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Archivos.OrderBy(a => a.Orden))
                    .FirstOrDefaultAsync(c => c.Id == id && c.EstaActivo);

                if (contenido == null)
                {
                    return NotFound(ApiResponse<ContenidoDto>.Fail("Contenido no encontrado"));
                }

                // Verificar bloqueos
                if (!string.IsNullOrEmpty(userId))
                {
                    var estaBloqueado = await _context.BloqueosUsuarios
                        .AnyAsync(b => (b.BloqueadorId == userId && b.BloqueadoId == contenido.UsuarioId) ||
                                      (b.BloqueadorId == contenido.UsuarioId && b.BloqueadoId == userId));

                    if (estaBloqueado)
                    {
                        return NotFound(ApiResponse<ContenidoDto>.Fail("Contenido no disponible"));
                    }
                }

                var dto = await MapContenidoAsync(contenido, userId);

                // Incrementar vistas
                contenido.NumeroVistas++;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<ContenidoDto>.Ok(dto));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo contenido {Id}", id);
                return StatusCode(500, ApiResponse<ContenidoDto>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Dar like a un contenido
        /// </summary>
        [HttpPost("{id}/like")]
        public async Task<ActionResult<ApiResponse<LikeResultDto>>> ToggleLike(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<LikeResultDto>.Fail("No autenticado"));
                }

                var contenido = await _context.Contenidos.FindAsync(id);
                if (contenido == null)
                {
                    return NotFound(ApiResponse<LikeResultDto>.Fail("Contenido no encontrado"));
                }

                var likeExistente = await _context.Likes
                    .FirstOrDefaultAsync(l => l.UsuarioId == userId && l.ContenidoId == id);

                bool meGusta;
                if (likeExistente != null)
                {
                    // Quitar like
                    _context.Likes.Remove(likeExistente);
                    contenido.NumeroLikes = Math.Max(0, contenido.NumeroLikes - 1);
                    meGusta = false;
                }
                else
                {
                    // Dar like
                    _context.Likes.Add(new Like
                    {
                        UsuarioId = userId,
                        ContenidoId = id,
                        FechaLike = DateTime.Now
                    });
                    contenido.NumeroLikes++;
                    meGusta = true;
                }

                await _context.SaveChangesAsync();

                return Ok(ApiResponse<LikeResultDto>.Ok(new LikeResultDto
                {
                    MeGusta = meGusta,
                    TotalLikes = contenido.NumeroLikes
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en like para contenido {Id}", id);
                return StatusCode(500, ApiResponse<LikeResultDto>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Agregar reaccion a un contenido
        /// </summary>
        [HttpPost("{id}/reaccion")]
        public async Task<ActionResult<ApiResponse>> AgregarReaccion(int id, [FromBody] ReaccionRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var contenido = await _context.Contenidos.FindAsync(id);
                if (contenido == null)
                {
                    return NotFound(ApiResponse.Fail("Contenido no encontrado"));
                }

                // Verificar si ya tiene reaccion
                var reaccionExistente = await _context.Reacciones
                    .FirstOrDefaultAsync(r => r.UsuarioId == userId && r.ContenidoId == id);

                if (reaccionExistente != null)
                {
                    // Actualizar reaccion
                    if (Enum.TryParse<TipoReaccion>(request.TipoReaccion, true, out var tipoNuevo))
                    {
                        reaccionExistente.TipoReaccion = tipoNuevo;
                        reaccionExistente.FechaReaccion = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Crear nueva reaccion
                    if (Enum.TryParse<TipoReaccion>(request.TipoReaccion, true, out var tipo))
                    {
                        _context.Reacciones.Add(new Reaccion
                        {
                            UsuarioId = userId,
                            ContenidoId = id,
                            TipoReaccion = tipo,
                            FechaReaccion = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(ApiResponse.Ok("Reaccion agregada"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error agregando reaccion a contenido {Id}", id);
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Quitar reaccion
        /// </summary>
        [HttpDelete("{id}/reaccion")]
        public async Task<ActionResult<ApiResponse>> QuitarReaccion(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var reaccion = await _context.Reacciones
                    .FirstOrDefaultAsync(r => r.UsuarioId == userId && r.ContenidoId == id);

                if (reaccion != null)
                {
                    _context.Reacciones.Remove(reaccion);
                    await _context.SaveChangesAsync();
                }

                return Ok(ApiResponse.Ok("Reaccion eliminada"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error quitando reaccion de contenido {Id}", id);
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Agregar/quitar de favoritos
        /// </summary>
        [HttpPost("{id}/favorito")]
        public async Task<ActionResult<ApiResponse<bool>>> ToggleFavorito(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<bool>.Fail("No autenticado"));
                }

                var contenido = await _context.Contenidos.FindAsync(id);
                if (contenido == null)
                {
                    return NotFound(ApiResponse<bool>.Fail("Contenido no encontrado"));
                }

                var favoritoExistente = await _context.Favoritos
                    .FirstOrDefaultAsync(f => f.UsuarioId == userId && f.ContenidoId == id);

                bool esFavorito;
                if (favoritoExistente != null)
                {
                    _context.Favoritos.Remove(favoritoExistente);
                    esFavorito = false;
                }
                else
                {
                    _context.Favoritos.Add(new Favorito
                    {
                        UsuarioId = userId,
                        ContenidoId = id,
                        FechaAgregado = DateTime.Now
                    });
                    esFavorito = true;
                }

                await _context.SaveChangesAsync();
                return Ok(ApiResponse<bool>.Ok(esFavorito, esFavorito ? "Agregado a favoritos" : "Quitado de favoritos"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en favorito para contenido {Id}", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener comentarios de un contenido
        /// </summary>
        [HttpGet("{id}/comentarios")]
        public async Task<ActionResult<PaginatedResponse<ComentarioDto>>> GetComentarios(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            // Limitar paginación para prevenir DoS
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var query = _context.Comentarios
                    .Include(c => c.Usuario)
                    .Where(c => c.ContenidoId == id && c.EstaActivo);

                var totalItems = await query.CountAsync();

                var comentarios = await query
                    .OrderByDescending(c => c.FechaCreacion)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new ComentarioDto
                    {
                        Id = c.Id,
                        Usuario = new UsuarioDto
                        {
                            Id = c.Usuario.Id,
                            UserName = c.Usuario.UserName ?? "",
                            NombreCompleto = c.Usuario.NombreCompleto ?? "",
                            FotoPerfil = c.Usuario.FotoPerfil,
                            EsCreador = c.Usuario.EsCreador,
                            EstaVerificado = c.Usuario.CreadorVerificado
                        },
                        Texto = c.Texto,
                        FechaCreacion = c.FechaCreacion,
                        TiempoRelativo = GetTiempoRelativo(c.FechaCreacion),
                        EsMio = c.UsuarioId == userId
                    })
                    .ToListAsync();

                return Ok(PaginatedResponse<ComentarioDto>.Create(comentarios, totalItems, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo comentarios de contenido {Id}", id);
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Agregar comentario
        /// </summary>
        [HttpPost("{id}/comentario")]
        public async Task<ActionResult<ApiResponse<ComentarioDto>>> AgregarComentario(
            int id,
            [FromBody] CrearComentarioRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<ComentarioDto>.Fail("No autenticado"));
                }

                if (string.IsNullOrWhiteSpace(request.Texto))
                {
                    return BadRequest(ApiResponse<ComentarioDto>.Fail("El comentario no puede estar vacio"));
                }

                var contenido = await _context.Contenidos.FindAsync(id);
                if (contenido == null)
                {
                    return NotFound(ApiResponse<ComentarioDto>.Fail("Contenido no encontrado"));
                }

                var usuario = await _context.Users.FindAsync(userId);
                if (usuario == null)
                {
                    return NotFound(ApiResponse<ComentarioDto>.Fail("Usuario no encontrado"));
                }

                var comentario = new Comentario
                {
                    ContenidoId = id,
                    UsuarioId = userId,
                    Texto = request.Texto.Trim(),
                    FechaCreacion = DateTime.Now,
                    EstaActivo = true
                };

                _context.Comentarios.Add(comentario);
                contenido.NumeroComentarios++;
                await _context.SaveChangesAsync();

                var dto = new ComentarioDto
                {
                    Id = comentario.Id,
                    Usuario = new UsuarioDto
                    {
                        Id = usuario.Id,
                        UserName = usuario.UserName ?? "",
                        NombreCompleto = usuario.NombreCompleto ?? "",
                        FotoPerfil = usuario.FotoPerfil,
                        EsCreador = usuario.EsCreador,
                        EstaVerificado = usuario.CreadorVerificado
                    },
                    Texto = comentario.Texto,
                    FechaCreacion = comentario.FechaCreacion,
                    TiempoRelativo = "ahora",
                    EsMio = true
                };

                return Ok(ApiResponse<ComentarioDto>.Ok(dto, "Comentario agregado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error agregando comentario a contenido {Id}", id);
                return StatusCode(500, ApiResponse<ComentarioDto>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Eliminar comentario
        /// </summary>
        [HttpDelete("comentario/{comentarioId}")]
        public async Task<ActionResult<ApiResponse>> EliminarComentario(int comentarioId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var comentario = await _context.Comentarios
                    .Include(c => c.Contenido)
                    .FirstOrDefaultAsync(c => c.Id == comentarioId);

                if (comentario == null)
                {
                    return NotFound(ApiResponse.Fail("Comentario no encontrado"));
                }

                // Verificar que es el autor del comentario o el dueño del contenido
                if (comentario.UsuarioId != userId && comentario.Contenido.UsuarioId != userId)
                {
                    return Forbid();
                }

                comentario.EstaActivo = false;
                comentario.Contenido.NumeroComentarios = Math.Max(0, comentario.Contenido.NumeroComentarios - 1);
                await _context.SaveChangesAsync();

                return Ok(ApiResponse.Ok("Comentario eliminado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando comentario {Id}", comentarioId);
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Comprar contenido
        /// </summary>
        [HttpPost("{id}/comprar")]
        public async Task<ActionResult<ApiResponse>> ComprarContenido(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .FirstOrDefaultAsync(c => c.Id == id && c.EstaActivo);

                if (contenido == null)
                {
                    return NotFound(ApiResponse.Fail("Contenido no encontrado"));
                }

                if (contenido.EsGratis)
                {
                    return BadRequest(ApiResponse.Fail("Este contenido es gratuito"));
                }

                // Verificar si ya lo compro
                var yaComprado = await _context.ComprasContenido
                    .AnyAsync(c => c.UsuarioId == userId && c.ContenidoId == id);

                if (yaComprado)
                {
                    return BadRequest(ApiResponse.Fail("Ya tienes este contenido"));
                }

                var usuario = await _context.Users.FindAsync(userId);
                if (usuario == null)
                {
                    return NotFound(ApiResponse.Fail("Usuario no encontrado"));
                }

                var precio = contenido.PrecioDesbloqueo ?? 0;
                if (usuario.Saldo < precio)
                {
                    return BadRequest(ApiResponse.Fail("Saldo insuficiente"));
                }

                // Realizar compra
                usuario.Saldo -= precio;

                var creador = contenido.Usuario;
                var comision = precio * 0.20m; // 20% comision
                creador.Saldo += precio - comision;
                creador.TotalGanancias += precio - comision;

                _context.ComprasContenido.Add(new CompraContenido
                {
                    UsuarioId = userId,
                    ContenidoId = id,
                    Monto = precio,
                    FechaCompra = DateTime.Now
                });

                await _context.SaveChangesAsync();

                return Ok(ApiResponse.Ok("Contenido desbloqueado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comprando contenido {Id}", id);
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener mis favoritos
        /// </summary>
        [HttpGet("favoritos")]
        public async Task<ActionResult<PaginatedResponse<ContenidoDto>>> GetMisFavoritos(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            // Limitar paginación para prevenir DoS
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var query = _context.Favoritos
                    .Include(f => f.Contenido)
                        .ThenInclude(c => c.Usuario)
                    .Include(f => f.Contenido)
                        .ThenInclude(c => c.Archivos.OrderBy(a => a.Orden))
                    .Where(f => f.UsuarioId == userId && f.Contenido.EstaActivo);

                var totalItems = await query.CountAsync();

                var favoritos = await query
                    .OrderByDescending(f => f.FechaAgregado)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(f => f.Contenido)
                    .ToListAsync();

                var suscripcionesIds = await _context.Suscripciones
                    .Where(s => s.FanId == userId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                var dtos = new List<ContenidoDto>();
                foreach (var contenido in favoritos)
                {
                    dtos.Add(await MapContenidoAsync(contenido, userId, suscripcionesIds));
                }

                return Ok(PaginatedResponse<ContenidoDto>.Create(dtos, totalItems, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo favoritos");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        #region Private Methods

        private async Task<ContenidoDto> MapContenidoAsync(
            Contenido contenido,
            string? userId,
            List<string>? suscripcionesIds = null)
        {
            suscripcionesIds ??= !string.IsNullOrEmpty(userId)
                ? await _context.Suscripciones
                    .Where(s => s.FanId == userId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync()
                : new List<string>();

            var estaDesbloqueado = contenido.EsGratis ||
                                   contenido.UsuarioId == userId ||
                                   suscripcionesIds.Contains(contenido.UsuarioId);

            if (!estaDesbloqueado && !string.IsNullOrEmpty(userId))
            {
                estaDesbloqueado = await _context.ComprasContenido
                    .AnyAsync(c => c.UsuarioId == userId && c.ContenidoId == contenido.Id);
            }

            var meGusta = !string.IsNullOrEmpty(userId) &&
                await _context.Likes.AnyAsync(l => l.UsuarioId == userId && l.ContenidoId == contenido.Id);

            var esFavorito = !string.IsNullOrEmpty(userId) &&
                await _context.Favoritos.AnyAsync(f => f.UsuarioId == userId && f.ContenidoId == contenido.Id);

            var miReaccion = !string.IsNullOrEmpty(userId)
                ? await _context.Reacciones
                    .Where(r => r.UsuarioId == userId && r.ContenidoId == contenido.Id)
                    .Select(r => r.TipoReaccion.ToString())
                    .FirstOrDefaultAsync()
                : null;

            return new ContenidoDto
            {
                Id = contenido.Id,
                Creador = new UsuarioDto
                {
                    Id = contenido.Usuario!.Id,
                    UserName = contenido.Usuario.UserName ?? "",
                    NombreCompleto = contenido.Usuario.NombreCompleto ?? "",
                    FotoPerfil = contenido.Usuario.FotoPerfil,
                    EsCreador = contenido.Usuario.EsCreador,
                    EstaVerificado = contenido.Usuario.CreadorVerificado
                },
                Texto = contenido.Descripcion,
                TipoContenido = contenido.TipoContenido.ToString(),
                TipoLado = contenido.TipoLado.ToString(),
                EsGratuito = contenido.EsGratis,
                PrecioDesbloqueo = contenido.PrecioDesbloqueo,
                Archivos = estaDesbloqueado
                    ? contenido.Archivos.Select(a => new ArchivoContenidoDto
                    {
                        Id = a.Id,
                        RutaArchivo = a.RutaArchivo,
                        TipoArchivo = a.TipoArchivo.ToString(),
                        Thumbnail = a.Thumbnail,
                        Duracion = a.DuracionSegundos,
                        Orden = a.Orden
                    }).ToList()
                    : new List<ArchivoContenidoDto>(),
                RutaPreview = contenido.RutaPreview,
                TienePreview = contenido.TienePreview,
                EstaDesbloqueado = estaDesbloqueado,
                CantidadArchivos = contenido.Archivos.Count,
                NumeroLikes = contenido.NumeroLikes,
                NumeroComentarios = contenido.NumeroComentarios,
                NumeroVistas = contenido.NumeroVistas,
                MeGusta = meGusta,
                EsFavorito = esFavorito,
                MiReaccion = miReaccion,
                FechaPublicacion = contenido.FechaPublicacion,
                TiempoRelativo = GetTiempoRelativo(contenido.FechaPublicacion)
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

    public class LikeResultDto
    {
        public bool MeGusta { get; set; }
        public int TotalLikes { get; set; }
    }
}
