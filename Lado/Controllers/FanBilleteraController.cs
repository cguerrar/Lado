using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    [Authorize]
    public class FanBilleteraController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FanBilleteraController> _logger;

        public FanBilleteraController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FanBilleteraController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: /FanBilletera/Index - Redirige a Billetera
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Billetera");
        }

        // GET: /FanBilletera/CargarSaldo - Redirige a PayPal
        public IActionResult CargarSaldo()
        {
            // Redirigir a la página de recarga con PayPal
            return RedirectToAction("Recargar", "PayPal");
        }

        // GET: /FanBilletera/MiBilletera - Vista de billetera del fan
        public async Task<IActionResult> MiBilletera()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Obtener transacciones recientes
            var transacciones = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id)
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(10)
                .ToListAsync();

            // Obtener suscripciones activas
            var suscripciones = await _context.Suscripciones
                .Include(s => s.Creador)
                .Where(s => s.FanId == usuario.Id && s.EstaActiva)
                .ToListAsync();

            ViewBag.Saldo = usuario.Saldo;
            ViewBag.Transacciones = transacciones;
            ViewBag.Suscripciones = suscripciones;

            return View();
        }

        // POST: /FanBilletera/CancelarSuscripcion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarSuscripcion(int suscripcionId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no autenticado" });
            }

            try
            {
                var suscripcion = await _context.Suscripciones
                    .Include(s => s.Creador)
                    .FirstOrDefaultAsync(s => s.Id == suscripcionId && s.FanId == usuario.Id);

                if (suscripcion == null)
                {
                    return Json(new { success = false, message = "Suscripción no encontrada" });
                }

                if (!suscripcion.EstaActiva)
                {
                    return Json(new { success = false, message = "La suscripción ya está cancelada" });
                }

                // Desactivar renovación automática
                suscripcion.RenovacionAutomatica = false;
                suscripcion.FechaCancelacion = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Suscripción {suscripcionId} cancelada por usuario {usuario.Id}");

                return Json(new
                {
                    success = true,
                    message = $"Suscripción a {suscripcion.Creador?.NombreCompleto} cancelada. " +
                             $"Tendrás acceso hasta {suscripcion.ProximaRenovacion:dd/MM/yyyy}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar suscripción");
                return Json(new { success = false, message = "Error al procesar la cancelación" });
            }
        }

        // POST: /FanBilletera/ReactivarSuscripcion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivarSuscripcion(int suscripcionId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no autenticado" });
            }

            try
            {
                var suscripcion = await _context.Suscripciones
                    .Include(s => s.Creador)
                    .FirstOrDefaultAsync(s => s.Id == suscripcionId && s.FanId == usuario.Id);

                if (suscripcion == null)
                {
                    return Json(new { success = false, message = "Suscripción no encontrada" });
                }

                // Validar saldo suficiente
                if (usuario.Saldo < suscripcion.PrecioMensual)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Saldo insuficiente. Necesitas ${suscripcion.PrecioMensual:N2}. " +
                                 $"Tu saldo actual es ${usuario.Saldo:N2}"
                    });
                }

                // Reactivar renovación automática
                suscripcion.RenovacionAutomatica = true;
                suscripcion.FechaCancelacion = null;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Suscripción {suscripcionId} reactivada por usuario {usuario.Id}");

                return Json(new
                {
                    success = true,
                    message = $"Renovación automática reactivada para {suscripcion.Creador?.NombreCompleto}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reactivar suscripción");
                return Json(new { success = false, message = "Error al procesar la reactivación" });
            }
        }

        // GET: /FanBilletera/HistorialCompleto - Redirige a Billetera
        public IActionResult HistorialCompleto()
        {
            return RedirectToAction("HistorialCompleto", "Billetera");
        }
    }
}