using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    [Authorize(Roles = "Admin")]
    public partial class AdminController : Controller
    {
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