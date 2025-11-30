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
        private readonly StripeSimuladoService _stripeService;
        private readonly ILogger<FanBilleteraController> _logger;

        public FanBilleteraController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            StripeSimuladoService stripeService,
            ILogger<FanBilleteraController> logger)
        {
            _context = context;
            _userManager = userManager;
            _stripeService = stripeService;
            _logger = logger;
        }

        // GET: /FanBilletera/Index - Redirige a Billetera
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Billetera");
        }

        // POST: /FanBilletera/CargarSaldo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CargarSaldo(decimal monto, string metodoPago)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

           

            if (monto < 5)
            {
                TempData["Error"] = "El monto mínimo de recarga es $5.00";
                return RedirectToAction("Index", "Billetera");
            }

            if (monto > 1000)
            {
                TempData["Error"] = "El monto máximo de recarga es $1,000.00";
                return RedirectToAction("Index", "Billetera");
            }

            try
            {
                // 🎭 SIMULACIÓN DE PAGO CON STRIPE
                var pagoExitoso = await _stripeService.ProcesarPagoSimulado(
                    usuario.Email,
                    monto,
                    metodoPago
                );

                if (!pagoExitoso)
                {
                    TempData["Error"] = "Error al procesar el pago. Por favor intenta nuevamente.";
                    return RedirectToAction("Index", "Billetera");
                }

                // Crear transacción de recarga
                var recarga = new Transaccion
                {
                    UsuarioId = usuario.Id,
                    TipoTransaccion = TipoTransaccion.Recarga,
                    Monto = monto,
                    MontoNeto = monto, // En recarga no hay comisión para el fan
                    Comision = 0,
                    Descripcion = $"Recarga de saldo vía {metodoPago}",
                    EstadoPago = "Completado",
                    MetodoPago = metodoPago,
                    FechaTransaccion = DateTime.Now,
                    Notas = "Pago simulado - Listo para integración real"
                };

                _context.Transacciones.Add(recarga);

                // Actualizar saldo del usuario
                usuario.Saldo += monto;
                await _userManager.UpdateAsync(usuario);

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Recarga exitosa: Usuario {usuario.Id}, Monto ${monto}");

                TempData["Success"] = $"¡Recarga exitosa! Se agregaron ${monto:N2} a tu billetera.";
                return RedirectToAction("Index", "Billetera");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar recarga de saldo");
                TempData["Error"] = "Error al procesar la recarga. Por favor, intenta de nuevo.";
                return RedirectToAction("Index", "Billetera");
            }
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