using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.DTOs.Common;
using Lado.DTOs.Usuario;
using System.Security.Claims;

namespace Lado.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SuscripcionesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SuscripcionesApiController> _logger;

        public SuscripcionesApiController(
            ApplicationDbContext context,
            ILogger<SuscripcionesApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtener mis suscripciones
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SuscripcionDto>>>> GetMisSuscripciones()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<List<SuscripcionDto>>.Fail("No autenticado"));
                }

                var suscripciones = await _context.Suscripciones
                    .Include(s => s.Creador)
                    .Where(s => s.FanId == userId && s.EstaActiva)
                    .OrderByDescending(s => s.FechaInicio)
                    .Select(s => new SuscripcionDto
                    {
                        Id = s.Id,
                        Creador = new CreadorDto
                        {
                            Id = s.Creador.Id,
                            UserName = s.Creador.UserName ?? "",
                            NombreCompleto = s.Creador.NombreCompleto ?? "",
                            FotoPerfil = s.Creador.FotoPerfil,
                            FotoPortada = s.Creador.FotoPortada,
                            EstaVerificado = s.Creador.CreadorVerificado,
                            PrecioSuscripcion = s.Creador.PrecioSuscripcion,
                            TotalSuscriptores = _context.Suscripciones.Count(x => x.CreadorId == s.CreadorId && x.EstaActiva),
                            TotalPublicaciones = _context.Contenidos.Count(x => x.UsuarioId == s.CreadorId && x.EstaActivo),
                            EstaSuscrito = true
                        },
                        PrecioMensual = s.PrecioMensual,
                        FechaInicio = s.FechaInicio,
                        ProximaRenovacion = s.ProximaRenovacion,
                        RenovacionAutomatica = s.RenovacionAutomatica
                    })
                    .ToListAsync();

                return Ok(ApiResponse<List<SuscripcionDto>>.Ok(suscripciones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo suscripciones");
                return StatusCode(500, ApiResponse<List<SuscripcionDto>>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener mis suscriptores (si soy creador)
        /// </summary>
        [HttpGet("suscriptores")]
        public async Task<ActionResult<PaginatedResponse<SuscriptorDto>>> GetMisSuscriptores(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var query = _context.Suscripciones
                    .Include(s => s.Fan)
                    .Where(s => s.CreadorId == userId && s.EstaActiva);

                var totalItems = await query.CountAsync();

                var suscriptores = await query
                    .OrderByDescending(s => s.FechaInicio)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new SuscriptorDto
                    {
                        Id = s.Fan.Id,
                        UserName = s.Fan.UserName ?? "",
                        NombreCompleto = s.Fan.NombreCompleto ?? "",
                        FotoPerfil = s.Fan.FotoPerfil,
                        FechaSuscripcion = s.FechaInicio,
                        ProximaRenovacion = s.ProximaRenovacion
                    })
                    .ToListAsync();

                return Ok(PaginatedResponse<SuscriptorDto>.Create(suscriptores, totalItems, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo suscriptores");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Suscribirse a un creador
        /// </summary>
        [HttpPost("{creadorId}")]
        public async Task<ActionResult<ApiResponse<SuscripcionDto>>> Suscribirse(string creadorId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<SuscripcionDto>.Fail("No autenticado"));
                }

                if (userId == creadorId)
                {
                    return BadRequest(ApiResponse<SuscripcionDto>.Fail("No puedes suscribirte a ti mismo"));
                }

                var creador = await _context.Users.FindAsync(creadorId);
                if (creador == null || !creador.EsCreador || !creador.EstaActivo)
                {
                    return NotFound(ApiResponse<SuscripcionDto>.Fail("Creador no encontrado"));
                }

                // Verificar bloqueo
                var bloqueado = await _context.BloqueosUsuarios
                    .AnyAsync(b => (b.BloqueadorId == userId && b.BloqueadoId == creadorId) ||
                                  (b.BloqueadorId == creadorId && b.BloqueadoId == userId));
                if (bloqueado)
                {
                    return BadRequest(ApiResponse<SuscripcionDto>.Fail("No puedes suscribirte a este creador"));
                }

                // Verificar si ya esta suscrito
                var suscripcionExistente = await _context.Suscripciones
                    .FirstOrDefaultAsync(s => s.FanId == userId && s.CreadorId == creadorId && s.EstaActiva);

                if (suscripcionExistente != null)
                {
                    return BadRequest(ApiResponse<SuscripcionDto>.Fail("Ya estas suscrito a este creador"));
                }

                var usuario = await _context.Users.FindAsync(userId);
                if (usuario == null)
                {
                    return NotFound(ApiResponse<SuscripcionDto>.Fail("Usuario no encontrado"));
                }

                var precio = creador.PrecioSuscripcion;

                // Verificar saldo
                if (usuario.Saldo < precio)
                {
                    return BadRequest(ApiResponse<SuscripcionDto>.Fail("Saldo insuficiente"));
                }

                // Realizar suscripcion
                usuario.Saldo -= precio;

                var comision = precio * 0.20m; // 20% comision
                creador.Saldo += precio - comision;
                creador.TotalGanancias += precio - comision;

                var suscripcion = new Suscripcion
                {
                    FanId = userId,
                    CreadorId = creadorId,
                    PrecioMensual = precio,
                    Precio = precio,
                    FechaInicio = DateTime.Now,
                    ProximaRenovacion = DateTime.Now.AddMonths(1),
                    EstaActiva = true,
                    RenovacionAutomatica = true
                };

                _context.Suscripciones.Add(suscripcion);
                await _context.SaveChangesAsync();

                var dto = new SuscripcionDto
                {
                    Id = suscripcion.Id,
                    Creador = new CreadorDto
                    {
                        Id = creador.Id,
                        UserName = creador.UserName ?? "",
                        NombreCompleto = creador.NombreCompleto ?? "",
                        FotoPerfil = creador.FotoPerfil,
                        EstaVerificado = creador.CreadorVerificado,
                        PrecioSuscripcion = creador.PrecioSuscripcion,
                        EstaSuscrito = true
                    },
                    PrecioMensual = suscripcion.PrecioMensual,
                    FechaInicio = suscripcion.FechaInicio,
                    ProximaRenovacion = suscripcion.ProximaRenovacion,
                    RenovacionAutomatica = suscripcion.RenovacionAutomatica
                };

                return Ok(ApiResponse<SuscripcionDto>.Ok(dto, "Suscripcion exitosa"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al suscribirse");
                return StatusCode(500, ApiResponse<SuscripcionDto>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Cancelar suscripcion
        /// </summary>
        [HttpDelete("{creadorId}")]
        public async Task<ActionResult<ApiResponse>> CancelarSuscripcion(string creadorId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var suscripcion = await _context.Suscripciones
                    .FirstOrDefaultAsync(s => s.FanId == userId && s.CreadorId == creadorId && s.EstaActiva);

                if (suscripcion == null)
                {
                    return NotFound(ApiResponse.Fail("Suscripcion no encontrada"));
                }

                suscripcion.EstaActiva = false;
                suscripcion.FechaCancelacion = DateTime.Now;
                suscripcion.RenovacionAutomatica = false;

                await _context.SaveChangesAsync();

                return Ok(ApiResponse.Ok("Suscripcion cancelada"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelando suscripcion");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Toggle renovacion automatica
        /// </summary>
        [HttpPut("{suscripcionId}/renovacion")]
        public async Task<ActionResult<ApiResponse>> ToggleRenovacion(int suscripcionId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var suscripcion = await _context.Suscripciones
                    .FirstOrDefaultAsync(s => s.Id == suscripcionId && s.FanId == userId);

                if (suscripcion == null)
                {
                    return NotFound(ApiResponse.Fail("Suscripcion no encontrada"));
                }

                suscripcion.RenovacionAutomatica = !suscripcion.RenovacionAutomatica;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse.Ok(suscripcion.RenovacionAutomatica
                    ? "Renovacion automatica activada"
                    : "Renovacion automatica desactivada"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando renovacion");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }
    }

    public class SuscripcionDto
    {
        public int Id { get; set; }
        public CreadorDto Creador { get; set; } = new();
        public decimal PrecioMensual { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime ProximaRenovacion { get; set; }
        public bool RenovacionAutomatica { get; set; }
    }

    public class SuscriptorDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoPerfil { get; set; }
        public DateTime FechaSuscripcion { get; set; }
        public DateTime ProximaRenovacion { get; set; }
    }
}
