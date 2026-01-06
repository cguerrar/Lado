using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    [Authorize]
    public class SuscripcionesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SuscripcionesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Suscripciones/MisSuscripciones
        public async Task<IActionResult> MisSuscripciones()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Obtener configuración del usuario
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            var enModoLadoA = usuario?.LadoPreferido == TipoLado.LadoA;
            var ocultarLadoB = (usuario?.BloquearLadoB ?? false) || enModoLadoA;

            // ⭐ CARGAR SUSCRIPCIONES CON EL CREADOR
            var query = _context.Suscripciones
                .Include(s => s.Creador)
                .Where(s => s.FanId == usuarioId && s.EstaActiva);

            // Filtrar por modo: si está en LadoA, solo mostrar suscripciones LadoA
            if (ocultarLadoB)
            {
                query = query.Where(s => s.TipoLado == TipoLado.LadoA);
            }

            var suscripciones = await query
                .OrderByDescending(s => s.FechaInicio)
                .ToListAsync();

            ViewBag.ModoActual = enModoLadoA ? "LadoA" : "LadoB";
            return View(suscripciones);
        }

        // POST: /Suscripciones/Cancelar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var suscripcion = await _context.Suscripciones
                .FirstOrDefaultAsync(s => s.Id == id && s.FanId == usuarioId);

            if (suscripcion == null)
            {
                TempData["Error"] = "Suscripción no encontrada";
                return RedirectToAction(nameof(MisSuscripciones));
            }

            // Cancelar la suscripción
            suscripcion.EstaActiva = false;
            suscripcion.FechaCancelacion = DateTime.Now;
            suscripcion.RenovacionAutomatica = false;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Suscripción cancelada exitosamente";
            return RedirectToAction(nameof(MisSuscripciones));
        }
    }
}