using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    [Authorize(Roles = "Admin")]
    public partial class AdminController : Controller
    {
        // =========================================
        // GENERACIÓN DE THUMBNAILS
        // =========================================

        /// <summary>
        /// Genera thumbnails para todo el contenido que no tiene thumbnail
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerarThumbnails()
        {
            var imageService = HttpContext.RequestServices.GetRequiredService<IImageService>();
            var environment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

            // Obtener contenido sin thumbnail
            var contenidoSinThumbnail = await _context.Contenidos
                .Where(c => c.EstaActivo
                        && string.IsNullOrEmpty(c.Thumbnail)
                        && !string.IsNullOrEmpty(c.RutaArchivo)
                        && (c.RutaArchivo.EndsWith(".jpg")
                            || c.RutaArchivo.EndsWith(".jpeg")
                            || c.RutaArchivo.EndsWith(".png")
                            || c.RutaArchivo.EndsWith(".webp")
                            || c.RutaArchivo.EndsWith(".gif")))
                .Take(100) // Procesar en lotes de 100
                .ToListAsync();

            var procesados = 0;
            var errores = 0;

            foreach (var contenido in contenidoSinThumbnail)
            {
                try
                {
                    var rutaFisica = Path.Combine(environment.WebRootPath, contenido.RutaArchivo!.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

                    if (System.IO.File.Exists(rutaFisica))
                    {
                        var thumbnail = await imageService.GenerarThumbnailAsync(rutaFisica, 400, 400, 75);
                        if (!string.IsNullOrEmpty(thumbnail))
                        {
                            contenido.Thumbnail = thumbnail;
                            procesados++;
                        }
                    }
                }
                catch
                {
                    errores++;
                }
            }

            await _context.SaveChangesAsync();

            // También procesar archivos de carrusel
            var archivosSinThumbnail = await _context.ArchivosContenido
                .Where(a => string.IsNullOrEmpty(a.Thumbnail)
                        && a.TipoArchivo == TipoArchivo.Foto
                        && !string.IsNullOrEmpty(a.RutaArchivo))
                .Take(100)
                .ToListAsync();

            foreach (var archivo in archivosSinThumbnail)
            {
                try
                {
                    var rutaFisica = Path.Combine(environment.WebRootPath, archivo.RutaArchivo.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

                    if (System.IO.File.Exists(rutaFisica))
                    {
                        var thumbnail = await imageService.GenerarThumbnailAsync(rutaFisica, 400, 400, 75);
                        if (!string.IsNullOrEmpty(thumbnail))
                        {
                            archivo.Thumbnail = thumbnail;
                            procesados++;
                        }
                    }
                }
                catch
                {
                    errores++;
                }
            }

            await _context.SaveChangesAsync();

            var pendientes = await _context.Contenidos
                .CountAsync(c => c.EstaActivo
                        && string.IsNullOrEmpty(c.Thumbnail)
                        && !string.IsNullOrEmpty(c.RutaArchivo)
                        && (c.RutaArchivo.EndsWith(".jpg")
                            || c.RutaArchivo.EndsWith(".jpeg")
                            || c.RutaArchivo.EndsWith(".png")
                            || c.RutaArchivo.EndsWith(".webp")
                            || c.RutaArchivo.EndsWith(".gif")));

            if (pendientes > 0)
            {
                TempData["Warning"] = $"Se procesaron {procesados} thumbnails ({errores} errores). Quedan {pendientes} pendientes - ejecutar de nuevo.";
            }
            else
            {
                TempData["Success"] = $"Se generaron {procesados} thumbnails exitosamente ({errores} errores). Todos los contenidos tienen thumbnail.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================
        // ACCIONES MASIVAS DE CONTENIDO
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CensurarMasivo(int[] ids, string razon)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No se selecciono ningun contenido.";
                return RedirectToAction(nameof(Contenido));
            }

            var contenidos = await _context.Contenidos
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            foreach (var contenido in contenidos)
            {
                contenido.Censurado = true;
                contenido.RazonCensura = razon ?? "Censura masiva por administrador";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{contenidos.Count} contenidos censurados exitosamente.";
            return RedirectToAction(nameof(Contenido));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarMasivo(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No se selecciono ningun contenido.";
                return RedirectToAction(nameof(Contenido));
            }

            var contenidos = await _context.Contenidos
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            _context.Contenidos.RemoveRange(contenidos);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{contenidos.Count} contenidos eliminados permanentemente.";
            return RedirectToAction(nameof(Contenido));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DescensurarMasivo(int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                TempData["Error"] = "No se selecciono ningun contenido.";
                return RedirectToAction(nameof(Contenido));
            }

            var contenidos = await _context.Contenidos
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            foreach (var contenido in contenidos)
            {
                contenido.Censurado = false;
                contenido.RazonCensura = null;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{contenidos.Count} contenidos descensurados exitosamente.";
            return RedirectToAction(nameof(Contenido));
        }
    }
}