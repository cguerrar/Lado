using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.DTOs.Common;
using Lado.DTOs.Feed;
using Lado.DTOs.Usuario;
using System.Security.Claims;

namespace Lado.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PerfilApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PerfilApiController> _logger;

        public PerfilApiController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILogger<PerfilApiController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Obtener perfil por username
        /// </summary>
        [HttpGet("{username}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<PerfilDto>>> GetPerfil(string username)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var usuario = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == username && u.EstaActivo);

                if (usuario == null)
                {
                    return NotFound(ApiResponse<PerfilDto>.Fail("Usuario no encontrado"));
                }

                // Verificar bloqueos
                bool esBloqueado = false;
                bool meBloqueo = false;
                if (!string.IsNullOrEmpty(userId) && userId != usuario.Id)
                {
                    esBloqueado = await _context.BloqueosUsuarios
                        .AnyAsync(b => b.BloqueadorId == userId && b.BloqueadoId == usuario.Id);
                    meBloqueo = await _context.BloqueosUsuarios
                        .AnyAsync(b => b.BloqueadorId == usuario.Id && b.BloqueadoId == userId);

                    if (meBloqueo)
                    {
                        return NotFound(ApiResponse<PerfilDto>.Fail("Usuario no disponible"));
                    }
                }

                // Verificar suscripcion
                bool estaSuscrito = false;
                if (!string.IsNullOrEmpty(userId))
                {
                    estaSuscrito = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == userId && s.CreadorId == usuario.Id && s.EstaActiva);
                }

                // Obtener estadisticas
                var totalPublicaciones = await _context.Contenidos
                    .CountAsync(c => c.UsuarioId == usuario.Id && c.EstaActivo);

                var totalSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == usuario.Id && s.EstaActiva);

                var totalSuscripciones = await _context.Suscripciones
                    .CountAsync(s => s.FanId == usuario.Id && s.EstaActiva);

                var perfil = new PerfilDto
                {
                    Id = usuario.Id,
                    UserName = usuario.UserName ?? "",
                    NombreCompleto = usuario.NombreCompleto ?? "",
                    Biografia = usuario.Biografia,
                    FotoPerfil = usuario.FotoPerfil,
                    FotoPortada = usuario.FotoPortada,
                    EsCreador = usuario.EsCreador,
                    EstaVerificado = usuario.CreadorVerificado,
                    TotalPublicaciones = totalPublicaciones,
                    TotalSuscriptores = totalSuscriptores,
                    TotalSuscripciones = totalSuscripciones,
                    PrecioSuscripcion = usuario.PrecioSuscripcion,
                    EstaSuscrito = estaSuscrito,
                    EsBloqueado = esBloqueado,
                    MeBloqueo = meBloqueo,
                    EsMiPerfil = userId == usuario.Id,
                    LadoPreferido = usuario.LadoPreferido.ToString(),
                    FechaRegistro = usuario.FechaRegistro
                };

                return Ok(ApiResponse<PerfilDto>.Ok(perfil));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo perfil {Username}", username);
                return StatusCode(500, ApiResponse<PerfilDto>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener contenido de un perfil
        /// </summary>
        [HttpGet("{username}/contenido")]
        public async Task<ActionResult<PaginatedResponse<ContenidoDto>>> GetContenidoPerfil(
            string username,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string lado = "A")
        {
            // Limitar paginación para prevenir DoS
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var usuario = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == username && u.EstaActivo);

                if (usuario == null)
                {
                    return NotFound(ApiResponse.Fail("Usuario no encontrado"));
                }

                // Verificar bloqueo
                if (!string.IsNullOrEmpty(userId) && userId != usuario.Id)
                {
                    var bloqueado = await _context.BloqueosUsuarios
                        .AnyAsync(b => (b.BloqueadorId == userId && b.BloqueadoId == usuario.Id) ||
                                      (b.BloqueadorId == usuario.Id && b.BloqueadoId == userId));
                    if (bloqueado)
                    {
                        return NotFound(ApiResponse.Fail("Usuario no disponible"));
                    }
                }

                // Verificar suscripcion para Lado B
                var estaSuscrito = !string.IsNullOrEmpty(userId) &&
                    await _context.Suscripciones
                        .AnyAsync(s => s.FanId == userId && s.CreadorId == usuario.Id && s.EstaActiva);

                var tipoLado = lado.ToUpper() == "B" ? TipoLado.LadoB : TipoLado.LadoA;
                var query = _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Archivos.OrderBy(a => a.Orden))
                    .Where(c => c.UsuarioId == usuario.Id && c.EstaActivo && c.TipoLado == tipoLado);

                // Si es Lado B y no esta suscrito, no mostrar
                if (lado.ToUpper() == "B" && !estaSuscrito && userId != usuario.Id)
                {
                    return Ok(PaginatedResponse<ContenidoDto>.Create(new List<ContenidoDto>(), 0, page, pageSize));
                }

                var totalItems = await query.CountAsync();

                var contenidos = await query
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var suscripcionesIds = new List<string>();
                if (!string.IsNullOrEmpty(userId))
                {
                    suscripcionesIds = await _context.Suscripciones
                        .Where(s => s.FanId == userId && s.EstaActiva)
                        .Select(s => s.CreadorId)
                        .ToListAsync();
                }

                var dtos = contenidos.Select(c => MapContenido(c, userId, suscripcionesIds)).ToList();

                return Ok(PaginatedResponse<ContenidoDto>.Create(dtos, totalItems, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo contenido de perfil {Username}", username);
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Editar mi perfil
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<PerfilDto>>> EditarPerfil([FromBody] EditarPerfilRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<PerfilDto>.Fail("No autenticado"));
                }

                var usuario = await _context.Users.FindAsync(userId);
                if (usuario == null)
                {
                    return NotFound(ApiResponse<PerfilDto>.Fail("Usuario no encontrado"));
                }

                // Actualizar campos
                if (request.NombreCompleto != null)
                    usuario.NombreCompleto = request.NombreCompleto;

                if (request.Biografia != null)
                    usuario.Biografia = request.Biografia;

                if (request.LadoPreferido != null)
                {
                    usuario.LadoPreferido = request.LadoPreferido.ToUpper() == "B"
                        ? TipoLado.LadoB
                        : TipoLado.LadoA;
                }

                await _context.SaveChangesAsync();

                // Retornar perfil actualizado
                var perfil = new PerfilDto
                {
                    Id = usuario.Id,
                    UserName = usuario.UserName ?? "",
                    NombreCompleto = usuario.NombreCompleto ?? "",
                    Biografia = usuario.Biografia,
                    FotoPerfil = usuario.FotoPerfil,
                    FotoPortada = usuario.FotoPortada,
                    EsCreador = usuario.EsCreador,
                    EstaVerificado = usuario.CreadorVerificado,
                    LadoPreferido = usuario.LadoPreferido.ToString(),
                    EsMiPerfil = true
                };

                return Ok(ApiResponse<PerfilDto>.Ok(perfil, "Perfil actualizado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editando perfil");
                return StatusCode(500, ApiResponse<PerfilDto>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Subir foto de perfil
        /// </summary>
        [HttpPost("foto-perfil")]
        public async Task<ActionResult<ApiResponse<string>>> SubirFotoPerfil(IFormFile archivo)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<string>.Fail("No autenticado"));
                }

                if (archivo == null || archivo.Length == 0)
                {
                    return BadRequest(ApiResponse<string>.Fail("Archivo requerido"));
                }

                var usuario = await _context.Users.FindAsync(userId);
                if (usuario == null)
                {
                    return NotFound(ApiResponse<string>.Fail("Usuario no encontrado"));
                }

                // Guardar archivo
                var extension = Path.GetExtension(archivo.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest(ApiResponse<string>.Fail("Formato de imagen no soportado"));
                }

                var fileName = $"{Guid.NewGuid()}{extension}";
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "perfiles");

                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                // Eliminar foto anterior si existe
                if (!string.IsNullOrEmpty(usuario.FotoPerfil))
                {
                    var oldPath = Path.Combine(_environment.WebRootPath, usuario.FotoPerfil.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                usuario.FotoPerfil = $"/uploads/perfiles/{fileName}";
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<string>.Ok(usuario.FotoPerfil, "Foto de perfil actualizada"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subiendo foto de perfil");
                return StatusCode(500, ApiResponse<string>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Subir foto de portada
        /// </summary>
        [HttpPost("foto-portada")]
        public async Task<ActionResult<ApiResponse<string>>> SubirFotoPortada(IFormFile archivo)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<string>.Fail("No autenticado"));
                }

                if (archivo == null || archivo.Length == 0)
                {
                    return BadRequest(ApiResponse<string>.Fail("Archivo requerido"));
                }

                var usuario = await _context.Users.FindAsync(userId);
                if (usuario == null)
                {
                    return NotFound(ApiResponse<string>.Fail("Usuario no encontrado"));
                }

                var extension = Path.GetExtension(archivo.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest(ApiResponse<string>.Fail("Formato de imagen no soportado"));
                }

                var fileName = $"{Guid.NewGuid()}{extension}";
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "portadas");

                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                if (!string.IsNullOrEmpty(usuario.FotoPortada))
                {
                    var oldPath = Path.Combine(_environment.WebRootPath, usuario.FotoPortada.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                usuario.FotoPortada = $"/uploads/portadas/{fileName}";
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<string>.Ok(usuario.FotoPortada, "Foto de portada actualizada"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subiendo foto de portada");
                return StatusCode(500, ApiResponse<string>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Bloquear usuario
        /// </summary>
        [HttpPost("{username}/bloquear")]
        public async Task<ActionResult<ApiResponse>> BloquearUsuario(string username)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var usuario = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == username);

                if (usuario == null)
                {
                    return NotFound(ApiResponse.Fail("Usuario no encontrado"));
                }

                if (usuario.Id == userId)
                {
                    return BadRequest(ApiResponse.Fail("No puedes bloquearte a ti mismo"));
                }

                var yaBloquedo = await _context.BloqueosUsuarios
                    .AnyAsync(b => b.BloqueadorId == userId && b.BloqueadoId == usuario.Id);

                if (yaBloquedo)
                {
                    return BadRequest(ApiResponse.Fail("Usuario ya bloqueado"));
                }

                _context.BloqueosUsuarios.Add(new BloqueoUsuario
                {
                    BloqueadorId = userId,
                    BloqueadoId = usuario.Id,
                    FechaBloqueo = DateTime.UtcNow
                });

                // Cancelar suscripciones mutuas
                var suscripciones = await _context.Suscripciones
                    .Where(s => s.EstaActiva &&
                               ((s.FanId == userId && s.CreadorId == usuario.Id) ||
                                (s.FanId == usuario.Id && s.CreadorId == userId)))
                    .ToListAsync();

                foreach (var sub in suscripciones)
                {
                    sub.EstaActiva = false;
                    sub.FechaCancelacion = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Ok(ApiResponse.Ok("Usuario bloqueado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bloqueando usuario");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Desbloquear usuario
        /// </summary>
        [HttpDelete("{username}/bloquear")]
        public async Task<ActionResult<ApiResponse>> DesbloquearUsuario(string username)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var usuario = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == username);

                if (usuario == null)
                {
                    return NotFound(ApiResponse.Fail("Usuario no encontrado"));
                }

                var bloqueo = await _context.BloqueosUsuarios
                    .FirstOrDefaultAsync(b => b.BloqueadorId == userId && b.BloqueadoId == usuario.Id);

                if (bloqueo != null)
                {
                    _context.BloqueosUsuarios.Remove(bloqueo);
                    await _context.SaveChangesAsync();
                }

                return Ok(ApiResponse.Ok("Usuario desbloqueado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error desbloqueando usuario");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        #region Private Methods

        private ContenidoDto MapContenido(Contenido c, string? userId, List<string> suscripcionesIds)
        {
            var estaDesbloqueado = c.EsGratis ||
                                   c.UsuarioId == userId ||
                                   suscripcionesIds.Contains(c.UsuarioId);

            return new ContenidoDto
            {
                Id = c.Id,
                Creador = new UsuarioDto
                {
                    Id = c.Usuario!.Id,
                    UserName = c.Usuario.UserName ?? "",
                    NombreCompleto = c.Usuario.NombreCompleto ?? "",
                    FotoPerfil = c.Usuario.FotoPerfil,
                    EsCreador = c.Usuario.EsCreador,
                    EstaVerificado = c.Usuario.CreadorVerificado
                },
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
                FechaPublicacion = c.FechaPublicacion,
                TiempoRelativo = GetTiempoRelativo(c.FechaPublicacion)
            };
        }

        private static string GetTiempoRelativo(DateTime fecha)
        {
            var diff = DateTime.Now - fecha;
            if (diff.TotalMinutes < 1) return "ahora";
            if (diff.TotalMinutes < 60) return $"hace {(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24) return $"hace {(int)diff.TotalHours}h";
            if (diff.TotalDays < 7) return $"hace {(int)diff.TotalDays}d";
            return fecha.ToString("dd/MM/yyyy");
        }

        #endregion
    }
}
