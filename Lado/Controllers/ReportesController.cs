using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    [Authorize]
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
                .Where(r => r.UsuarioReportadorId == usuario.Id)
                .OrderByDescending(r => r.FechaReporte)
                .ToListAsync();

            return View(reportes);
        }
    }
}