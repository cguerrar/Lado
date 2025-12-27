using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(bool desktop = false)
        {
            // En móvil, redirigir directamente al feed público (experiencia tipo TikTok)
            // A menos que el usuario explícitamente quiera ver el landing (desktop=true)
            if (!desktop)
            {
                var userAgent = Request.Headers["User-Agent"].ToString().ToLower();
                var esMobile = userAgent.Contains("mobile") || userAgent.Contains("android") ||
                              (userAgent.Contains("iphone") || userAgent.Contains("ipad"));

                if (esMobile)
                {
                    return RedirectToAction("Index", "FeedPublico");
                }
            }

            // Obtener imagenes para el mosaico del hero (solo fotos, no videos)
            // IMPORTANTE: Solo contenido de LadoA (público) - NO mostrar LadoB en landing
            // Preferir thumbnail si existe para carga más rápida
            var contenidoMosaico = await _context.Contenidos
                .Where(c => c.EstaActivo
                        && !c.EsBorrador
                        && !c.Censurado
                        && !c.EsPrivado
                        && !c.EsContenidoSensible
                        && c.TipoLado == TipoLado.LadoA // Solo contenido público LadoA
                        && !string.IsNullOrEmpty(c.RutaArchivo)
                        && (c.RutaArchivo.EndsWith(".jpg")
                            || c.RutaArchivo.EndsWith(".jpeg")
                            || c.RutaArchivo.EndsWith(".png")
                            || c.RutaArchivo.EndsWith(".webp")
                            || c.RutaArchivo.EndsWith(".gif")))
                .OrderByDescending(c => c.NumeroLikes + c.NumeroVistas)
                .Take(100)
                .Select(c => !string.IsNullOrEmpty(c.Thumbnail) ? c.Thumbnail : c.RutaArchivo)
                .ToListAsync();

            ViewBag.ImagenesMosaico = contenidoMosaico;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Cookies()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }
    }
}
