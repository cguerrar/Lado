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

            // SEO Meta Tags - Pagina principal
            ViewData["Title"] = "Muestra tus dos lados";
            ViewData["MetaDescription"] = "Lado es la plataforma donde los creadores de contenido muestran su lado autentico y exclusivo. Unete gratis y conecta con tus creadores favoritos.";
            ViewData["MetaKeywords"] = "lado, red social, creadores de contenido, contenido exclusivo, fotos, videos, suscripciones";
            ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/";
            ViewData["OgTitle"] = "Lado - Muestra tus dos lados";
            ViewData["OgDescription"] = "La plataforma donde los creadores muestran su lado autentico y exclusivo.";
            ViewData["OgType"] = "website";

            return View();
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Politica de Privacidad";
            ViewData["MetaDescription"] = "Politica de privacidad de Lado. Conoce como protegemos tu informacion personal y tus datos.";
            ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/Home/Privacy";
            ViewData["Robots"] = "index, follow";
            return View();
        }

        public IActionResult Terms()
        {
            ViewData["Title"] = "Terminos y Condiciones";
            ViewData["MetaDescription"] = "Terminos y condiciones de uso de Lado. Lee las reglas y politicas de nuestra plataforma.";
            ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/Home/Terms";
            ViewData["Robots"] = "index, follow";
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Title"] = "Contacto";
            ViewData["MetaDescription"] = "Contacta con el equipo de Lado. Estamos aqui para ayudarte con cualquier consulta o problema.";
            ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/Home/Contact";
            return View();
        }

        public IActionResult Cookies()
        {
            ViewData["Title"] = "Politica de Cookies";
            ViewData["MetaDescription"] = "Politica de cookies de Lado. Conoce como usamos las cookies para mejorar tu experiencia.";
            ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/Home/Cookies";
            ViewData["Robots"] = "index, follow";
            return View();
        }

        public IActionResult About()
        {
            ViewData["Title"] = "Acerca de Lado";
            ViewData["MetaDescription"] = "Conoce mas sobre Lado, la plataforma de contenido exclusivo para creadores. Nuestra mision es conectar creadores con su audiencia.";
            ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/Home/About";
            return View();
        }
    }
}
