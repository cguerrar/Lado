using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    public class AyudaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AyudaController> _logger;

        public AyudaController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<AyudaController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // ========================================
        // CENTRO DE AYUDA - PRINCIPAL
        // ========================================
        public IActionResult Index()
        {
            return View();
        }

        // ========================================
        // PREGUNTAS FRECUENTES
        // ========================================
        public IActionResult FAQ()
        {
            return View();
        }

        // ========================================
        // GUIA DE INICIO
        // ========================================
        public IActionResult GuiaInicio()
        {
            return View();
        }

        // ========================================
        // SEGURIDAD Y PRIVACIDAD
        // ========================================
        public IActionResult Seguridad()
        {
            return View();
        }

        // ========================================
        // MONETIZACION
        // ========================================
        public IActionResult Monetizacion()
        {
            return View();
        }

        // ========================================
        // LADOCOINS - MONEDA VIRTUAL
        // ========================================
        public IActionResult LadoCoins()
        {
            return View();
        }

        // ========================================
        // FEEDBACK - GET
        // ========================================
        public async Task<IActionResult> Feedback()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario != null)
            {
                ViewBag.NombreUsuario = usuario.NombreCompleto;
                ViewBag.Email = usuario.Email;
            }
            return View();
        }

        // ========================================
        // FEEDBACK - POST
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Feedback(string nombre, string email, TipoFeedback tipo, string asunto, string mensaje)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(mensaje))
                {
                    TempData["Error"] = "Todos los campos son obligatorios";
                    return View();
                }

                var usuario = await _userManager.GetUserAsync(User);

                var feedback = new Feedback
                {
                    UsuarioId = usuario?.Id,
                    NombreUsuario = nombre,
                    Email = email,
                    Tipo = tipo,
                    Asunto = asunto,
                    Mensaje = mensaje,
                    FechaEnvio = DateTime.Now,
                    Estado = EstadoFeedback.Pendiente
                };

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Nuevo feedback recibido de {Email}: {Asunto}", email, asunto);

                TempData["Success"] = "Gracias por tu feedback. Lo revisaremos pronto.";
                return RedirectToAction("FeedbackEnviado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar feedback");
                TempData["Error"] = "Ocurrio un error al enviar tu feedback. Intenta de nuevo.";
                return View();
            }
        }

        // ========================================
        // FEEDBACK ENVIADO - CONFIRMACION
        // ========================================
        public IActionResult FeedbackEnviado()
        {
            return View();
        }

        // ========================================
        // MIS FEEDBACKS (USUARIOS AUTENTICADOS)
        // ========================================
        [Authorize]
        public async Task<IActionResult> MisFeedbacks()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var feedbacks = await _context.Feedbacks
                .Where(f => f.UsuarioId == usuario.Id)
                .OrderByDescending(f => f.FechaEnvio)
                .ToListAsync();

            return View(feedbacks);
        }
    }
}
