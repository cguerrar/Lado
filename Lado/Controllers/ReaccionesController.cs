using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class ReaccionesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReaccionesController> _logger;
        private readonly IRateLimitService _rateLimitService;

        public ReaccionesController(
            ApplicationDbContext context,
            ILogger<ReaccionesController> logger,
            IRateLimitService rateLimitService)
        {
            _context = context;
            _logger = logger;
            _rateLimitService = rateLimitService;
        }

        // ========================================
        // VERIFICAR QUE ReaccionesController.cs tenga este formato
        // en el método Reaccionar
        // ========================================

        [HttpPost("Reaccionar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reaccionar(int contenidoId, string tipo)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Rate limiting: máximo 100 reacciones por minuto por usuario
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                if (!await _rateLimitService.IsAllowedAsync(
                    clientIp,
                    $"reaccion_user_{usuarioId}",
                    100,
                    TimeSpan.FromMinutes(1),
                    TipoAtaque.SpamContenido,
                    "/Reacciones/Reaccionar",
                    usuarioId))
                {
                    return Json(new { success = false, message = "Demasiadas reacciones. Espera un momento." });
                }

                // Parsear el tipo de reacción
                if (!Enum.TryParse<TipoReaccion>(tipo, true, out var tipoReaccion))
                {
                    return Json(new { success = false, message = "Tipo de reacción inválido" });
                }

                var contenido = await _context.Contenidos.FindAsync(contenidoId);

                if (contenido == null || !contenido.EstaActivo)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                // Verificar si ya reaccionó
                var reaccionExistente = await _context.Reacciones
                    .FirstOrDefaultAsync(r => r.ContenidoId == contenidoId
                                           && r.UsuarioId == usuarioId);

                if (reaccionExistente != null)
                {
                    // Cambiar tipo de reacción
                    reaccionExistente.TipoReaccion = tipoReaccion;
                    reaccionExistente.FechaReaccion = DateTime.Now;
                }
                else
                {
                    // Crear nueva reacción
                    var reaccion = new Reaccion
                    {
                        ContenidoId = contenidoId,
                        UsuarioId = usuarioId,
                        TipoReaccion = tipoReaccion,
                        FechaReaccion = DateTime.Now
                    };

                    _context.Reacciones.Add(reaccion);
                }

                await _context.SaveChangesAsync();

                // ✅ IMPORTANTE: Obtener conteo actualizado de reacciones POR TIPO
                var conteoReacciones = await _context.Reacciones
    .Where(r => r.ContenidoId == contenidoId)
    .GroupBy(r => r.TipoReaccion)
    .Select(g => new
    {
        tipo = g.Key.ToString().ToLower(), // ⭐ CRÍTICO: .ToLower()
        count = g.Count()
    })
    .ToListAsync();

                var totalReacciones = conteoReacciones.Sum(r => r.count);

                _logger.LogInformation("Reacción {Tipo} de usuario {UserId} en contenido {ContenidoId}. Total: {Total}",
                    tipo, usuarioId, contenidoId, totalReacciones);

                // ✅ RESPONSE CORRECTO
                return Json(new
                {
                    success = true,
                    reacciones = conteoReacciones, // ⭐ Array con {tipo, count}
                    totalReacciones = totalReacciones
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar reacción");
                return Json(new { success = false, message = "Error al procesar la reacción" });
            }
        }

        /// <summary>
        /// Quitar reacción de un contenido
        /// </summary>
        [HttpPost("Quitar/{contenidoId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarReaccion(int contenidoId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var reaccion = await _context.Reacciones
                    .FirstOrDefaultAsync(r => r.ContenidoId == contenidoId
                                           && r.UsuarioId == usuarioId);

                if (reaccion != null)
                {
                    _context.Reacciones.Remove(reaccion);
                    await _context.SaveChangesAsync();
                }

                // Obtener conteo actualizado
                var conteoReacciones = await _context.Reacciones
                    .Where(r => r.ContenidoId == contenidoId)
                    .GroupBy(r => r.TipoReaccion)
                    .Select(g => new
                    {
                        tipo = g.Key.ToString().ToLower(),
                        count = g.Count()
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    reacciones = conteoReacciones,
                    totalReacciones = conteoReacciones.Sum(r => r.count)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al quitar reacción");
                return Json(new { success = false, message = "Error al quitar la reacción" });
            }
        }

        /// <summary>
        /// Obtener reacciones de un contenido
        /// </summary>
        [HttpGet("Obtener/{contenidoId}")]
        public async Task<IActionResult> ObtenerReacciones(int contenidoId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var reacciones = await _context.Reacciones
                    .Where(r => r.ContenidoId == contenidoId)
                    .GroupBy(r => r.TipoReaccion)
                    .Select(g => new
                    {
                        tipo = g.Key.ToString().ToLower(),
                        count = g.Count()
                    })
                    .ToListAsync();

                // Verificar si el usuario actual ya reaccionó
                var reaccionUsuario = await _context.Reacciones
                    .Where(r => r.ContenidoId == contenidoId && r.UsuarioId == usuarioId)
                    .Select(r => r.TipoReaccion.ToString().ToLower())
                    .FirstOrDefaultAsync();

                return Json(new
                {
                    success = true,
                    reacciones,
                    totalReacciones = reacciones.Sum(r => r.count),
                    miReaccion = reaccionUsuario
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener reacciones del contenido {ContenidoId}", contenidoId);
                return Json(new { success = false, message = "Error al cargar reacciones" });
            }
        }

        /// <summary>
        /// Obtener lista de usuarios que reaccionaron con un tipo específico
        /// </summary>
        [HttpGet("Usuarios/{contenidoId}/{tipo}")]
        public async Task<IActionResult> ObtenerUsuariosReaccion(int contenidoId, string tipo)
        {
            try
            {
                if (!Enum.TryParse<TipoReaccion>(tipo, true, out var tipoReaccion))
                {
                    return Json(new { success = false, message = "Tipo de reacción inválido" });
                }

                var usuarios = await _context.Reacciones
                    .Include(r => r.Usuario)
                    .Where(r => r.ContenidoId == contenidoId && r.TipoReaccion == tipoReaccion)
                    .OrderByDescending(r => r.FechaReaccion)
                    .Select(r => new
                    {
                        id = r.Usuario.Id,
                        nombre = r.Usuario.NombreCompleto,
                        username = r.Usuario.UserName,
                        avatar = r.Usuario.FotoPerfil,
                        fechaReaccion = r.FechaReaccion
                    })
                    .Take(50)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    tipo = tipo.ToLower(),
                    usuarios
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios con reacción {Tipo} en contenido {ContenidoId}",
                    tipo, contenidoId);
                return Json(new { success = false, message = "Error al cargar usuarios" });
            }
        }

        // ========================================
        // MÉTODOS AUXILIARES
        // ========================================

        private async Task<bool> VerificarAccesoContenido(string usuarioId, Contenido contenido)
        {
            // Verificar suscripción
            var estaSuscrito = await _context.Suscripciones
                .AnyAsync(s => s.FanId == usuarioId
                            && s.CreadorId == contenido.UsuarioId
                            && s.EstaActiva);

            if (estaSuscrito)
                return true;

            // Verificar compra individual
            var comproContenido = await _context.ComprasContenido
                .AnyAsync(cc => cc.UsuarioId == usuarioId
                             && cc.ContenidoId == contenido.Id);

            if (comproContenido)
                return true;

            // Verificar si está en una colección comprada
            var contenidoEnColeccionComprada = await _context.ContenidoColecciones
                .Where(cc => cc.ContenidoId == contenido.Id)
                .Join(_context.ComprasColeccion,
                    cc => cc.ColeccionId,
                    coc => coc.ColeccionId,
                    (cc, coc) => new { cc, coc })
                .AnyAsync(x => x.coc.CompradorId == usuarioId);

            return contenidoEnColeccionComprada;
        }
    }
}