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
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRateLimitService _rateLimitService;
        private readonly ILogEventoService _logEventoService;
        private readonly ILogger<ReportesController> _logger;

        public ReportesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IRateLimitService rateLimitService,
            ILogEventoService logEventoService,
            ILogger<ReportesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _rateLimitService = rateLimitService;
            _logEventoService = logEventoService;
            _logger = logger;
        }

        // GET: Reportar Usuario
        [HttpGet]
        public async Task<IActionResult> ReportarUsuario(string id)
        {
            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null) return NotFound();

            ViewBag.Usuario = usuario;
            return View();
        }

        // POST: Reportar Usuario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportarUsuario(string usuarioReportadoId, string motivo, string descripcion)
        {
            var usuarioReportador = await _userManager.GetUserAsync(User);

            // Verificar que no se reporte a sí mismo
            if (usuarioReportador.Id == usuarioReportadoId)
            {
                TempData["Error"] = "No puedes reportarte a ti mismo";
                return RedirectToAction("Index", "Dashboard");
            }

            var reporte = new Reporte
            {
                UsuarioReportadorId = usuarioReportador.Id,
                UsuarioReportadoId = usuarioReportadoId,
                TipoReporte = "Usuario",
                Motivo = motivo,
                Descripcion = descripcion,
                FechaReporte = DateTime.Now,
                Estado = "Pendiente"
            };

            _context.Reportes.Add(reporte);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Reporte enviado correctamente. Será revisado por nuestro equipo.";
            return RedirectToAction("Index", "Dashboard");
        }

        // GET: Reportar Contenido
        [HttpGet]
        public async Task<IActionResult> ReportarContenido(int id)
        {
            var contenido = await _context.Contenidos
                .Include(c => c.Usuario)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contenido == null) return NotFound();

            ViewBag.Contenido = contenido;
            return View();
        }

        // POST: Reportar Contenido
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportarContenido(int contenidoId, string motivo, string descripcion)
        {
            var usuarioReportador = await _userManager.GetUserAsync(User);
            var contenido = await _context.Contenidos.FindAsync(contenidoId);

            if (contenido == null)
            {
                TempData["Error"] = "El contenido no existe";
                return RedirectToAction("Index", "Dashboard");
            }

            // Verificar que no reporte su propio contenido
            if (contenido.UsuarioId == usuarioReportador.Id)
            {
                TempData["Error"] = "No puedes reportar tu propio contenido";
                return RedirectToAction("Index", "Dashboard");
            }

            var reporte = new Reporte
            {
                UsuarioReportadorId = usuarioReportador.Id,
                ContenidoReportadoId = contenidoId,
                TipoReporte = "Contenido",
                Motivo = motivo,
                Descripcion = descripcion,
                FechaReporte = DateTime.Now,
                Estado = "Pendiente"
            };

            _context.Reportes.Add(reporte);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Reporte enviado correctamente. Será revisado por nuestro equipo.";
            return RedirectToAction("Index", "Dashboard");
        }

        // Ver Mis Reportes
        public async Task<IActionResult> MisReportes()
        {
            var usuario = await _userManager.GetUserAsync(User);

            var reportes = await _context.Reportes
                .Include(r => r.UsuarioReportado)
                .Include(r => r.ContenidoReportado)
                .Include(r => r.Story)
                .Where(r => r.UsuarioReportadorId == usuario.Id)
                .OrderByDescending(r => r.FechaReporte)
                .ToListAsync();

            return View(reportes);
        }

        // POST: Reportar Story (API para uso desde modal en Feed)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportarStory([FromBody] ReportarStoryRequest request)
        {
            var usuarioReportador = await _userManager.GetUserAsync(User);
            if (usuarioReportador == null)
            {
                return Json(new { success = false, message = "Debes iniciar sesión para reportar" });
            }

            // Rate Limiting: máximo 10 reportes por hora por usuario
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers["User-Agent"].ToString();
            var rateLimitKey = $"report_story_{usuarioReportador.Id}";

            if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 10, TimeSpan.FromHours(1),
                TipoAtaque.SpamReportes, "/Reportes/ReportarStory", usuarioReportador.Id, userAgent))
            {
                _logger.LogWarning("⚠️ Rate limit excedido en ReportarStory: Usuario {UserId}", usuarioReportador.Id);
                return Json(new { success = false, message = "Has enviado demasiados reportes. Intenta más tarde." });
            }

            var story = await _context.Stories.FindAsync(request.StoryId);
            if (story == null)
            {
                return Json(new { success = false, message = "La story no existe" });
            }

            // Verificar que no reporte su propia story
            if (story.CreadorId == usuarioReportador.Id)
            {
                return Json(new { success = false, message = "No puedes reportar tu propia story" });
            }

            // Verificar si ya reportó esta story
            var reporteExistente = await _context.Reportes
                .AnyAsync(r => r.UsuarioReportadorId == usuarioReportador.Id
                    && r.StoryId == request.StoryId
                    && r.Estado == "Pendiente");

            if (reporteExistente)
            {
                return Json(new { success = false, message = "Ya has reportado esta story" });
            }

            var reporte = new Reporte
            {
                UsuarioReportadorId = usuarioReportador.Id,
                UsuarioReportadoId = story.CreadorId,
                StoryId = request.StoryId,
                TipoReporte = "Story",
                Motivo = request.Motivo,
                Descripcion = request.Descripcion,
                FechaReporte = DateTime.Now,
                Estado = "Pendiente"
            };

            _context.Reportes.Add(reporte);
            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Nuevo reporte de Story #{request.StoryId}",
                CategoriaEvento.Contenido,
                TipoLogEvento.Evento,
                usuarioReportador.Id,
                usuarioReportador.NombreCompleto ?? usuarioReportador.UserName,
                $"Motivo: {request.Motivo}"
            );

            return Json(new { success = true, message = "Reporte enviado. Será revisado por nuestro equipo." });
        }

        // POST: Reportar Comentario (API para uso desde modal en Feed)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportarComentario([FromBody] ReportarComentarioRequest request)
        {
            var usuarioReportador = await _userManager.GetUserAsync(User);
            if (usuarioReportador == null)
            {
                return Json(new { success = false, message = "Debes iniciar sesión para reportar" });
            }

            // Rate Limiting: máximo 10 reportes por hora por usuario
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers["User-Agent"].ToString();
            var rateLimitKey = $"report_comment_{usuarioReportador.Id}";

            if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 10, TimeSpan.FromHours(1),
                TipoAtaque.SpamReportes, "/Reportes/ReportarComentario", usuarioReportador.Id, userAgent))
            {
                _logger.LogWarning("⚠️ Rate limit excedido en ReportarComentario: Usuario {UserId}", usuarioReportador.Id);
                return Json(new { success = false, message = "Has enviado demasiados reportes. Intenta más tarde." });
            }

            var comentario = await _context.Comentarios.FindAsync(request.ComentarioId);
            if (comentario == null)
            {
                return Json(new { success = false, message = "El comentario no existe" });
            }

            // Verificar que no reporte su propio comentario
            if (comentario.UsuarioId == usuarioReportador.Id)
            {
                return Json(new { success = false, message = "No puedes reportar tu propio comentario" });
            }

            // Verificar si ya reportó este comentario
            var reporteExistente = await _context.Reportes
                .AnyAsync(r => r.UsuarioReportadorId == usuarioReportador.Id
                    && r.ComentarioId == request.ComentarioId
                    && r.Estado == "Pendiente");

            if (reporteExistente)
            {
                return Json(new { success = false, message = "Ya has reportado este comentario" });
            }

            var reporte = new Reporte
            {
                UsuarioReportadorId = usuarioReportador.Id,
                UsuarioReportadoId = comentario.UsuarioId,
                ComentarioId = request.ComentarioId,
                TipoReporte = "Comentario",
                Motivo = request.Motivo,
                Descripcion = request.Descripcion,
                FechaReporte = DateTime.Now,
                Estado = "Pendiente"
            };

            _context.Reportes.Add(reporte);
            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Nuevo reporte de Comentario #{request.ComentarioId}",
                CategoriaEvento.Contenido,
                TipoLogEvento.Evento,
                usuarioReportador.Id,
                usuarioReportador.NombreCompleto ?? usuarioReportador.UserName,
                $"Motivo: {request.Motivo}"
            );

            return Json(new { success = true, message = "Reporte enviado. Será revisado por nuestro equipo." });
        }
    }

    public class ReportarStoryRequest
    {
        public int StoryId { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    public class ReportarComentarioRequest
    {
        public int ComentarioId { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }
}