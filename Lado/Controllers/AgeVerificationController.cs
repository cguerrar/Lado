using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Lado.Models;
using Lado.Data;

namespace Lado.Controllers
{
    [Authorize]
    public class AgeVerificationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AgeVerificationController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Verify()
        {
            var user = await _userManager.GetUserAsync(User);

            // Si ya está verificado, redirigir al dashboard
            if (user.AgeVerified)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(AgeVerificationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            // Calcular edad
            var edad = DateTime.Today.Year - model.FechaNacimiento.Year;
            if (model.FechaNacimiento.Date > DateTime.Today.AddYears(-edad)) edad--;

            // Verificar mayoría de edad (18+ global, ajustable por país)
            int edadMinima = ObtenerEdadMinimaPorPais(model.Pais);

            if (edad < edadMinima)
            {
                ModelState.AddModelError("", $"Debes tener al menos {edadMinima} años para usar esta plataforma.");
                return View(model);
            }

            // Actualizar usuario
            user.FechaNacimiento = model.FechaNacimiento;
            user.Pais = model.Pais;
            user.AgeVerified = true;
            user.AgeVerifiedDate = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);

            // Log de verificación
            _context.AgeVerificationLogs.Add(new AgeVerificationLog
            {
                UserId = user.Id,
                FechaVerificacion = DateTime.UtcNow,
                Pais = model.Pais,
                EdadAlVerificar = edad,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Verificación de edad completada exitosamente.";
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCuenta()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                // Guardar el email para el log
                var email = user.Email;

                // Eliminar el usuario
                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    // Log de eliminación
                    _context.AgeVerificationLogs.Add(new AgeVerificationLog
                    {
                        UserId = "DELETED-" + email,
                        FechaVerificacion = DateTime.UtcNow,
                        Pais = "ACCOUNT_DELETED",
                        EdadAlVerificar = 0,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                    });
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Cuenta eliminada exitosamente" });
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Json(new { success = false, message = "Error al eliminar: " + errors });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        private int ObtenerEdadMinimaPorPais(string pais)
        {
            // Edad mínima por país según legislación
            var edadesPorPais = new Dictionary<string, int>
            {
                { "CL", 18 }, // Chile
                { "US", 18 }, // Estados Unidos
                { "MX", 18 }, // México
                { "AR", 18 }, // Argentina
                { "CO", 18 }, // Colombia
                { "ES", 18 }, // España
                { "PE", 18 }, // Perú
                { "KR", 19 }, // Corea del Sur
                { "JP", 20 }, // Japón (antes de 2022)
            };

            return edadesPorPais.ContainsKey(pais) ? edadesPorPais[pais] : 18; // Default 18
        }
    }

}
