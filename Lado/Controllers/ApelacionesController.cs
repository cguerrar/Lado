using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    public class ApelacionesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogEventoService _logEventoService;

        public ApelacionesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogEventoService logEventoService)
        {
            _context = context;
            _userManager = userManager;
            _logEventoService = logEventoService;
        }

        // GET: Mis Apelaciones
        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var apelaciones = await _context.Apelaciones
                .Include(a => a.Contenido)
                .Include(a => a.Story)
                .Include(a => a.Administrador)
                .Where(a => a.UsuarioId == usuario.Id)
                .OrderByDescending(a => a.FechaCreacion)
                .ToListAsync();

            return View(apelaciones);
        }

        // GET: Crear Apelacion para Contenido
        [HttpGet]
        public async Task<IActionResult> CrearContenido(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido == null) return NotFound();

            // Verificar que el contenido pertenece al usuario
            if (contenido.UsuarioId != usuario.Id)
            {
                TempData["Error"] = "No tienes permiso para apelar este contenido";
                return RedirectToAction("Index");
            }

            // Verificar que no hay una apelacion pendiente
            var apelacionExistente = await _context.Apelaciones
                .AnyAsync(a => a.ContenidoId == id &&
                              a.UsuarioId == usuario.Id &&
                              (a.Estado == EstadoApelacion.Pendiente || a.Estado == EstadoApelacion.EnRevision));

            if (apelacionExistente)
            {
                TempData["Error"] = "Ya tienes una apelacion pendiente para este contenido";
                return RedirectToAction("Index");
            }

            ViewBag.Contenido = contenido;
            return View();
        }

        // POST: Crear Apelacion para Contenido
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearContenido(int contenidoId, string razonRechazo, string argumentos, string? evidencia)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var contenido = await _context.Contenidos.FindAsync(contenidoId);
            if (contenido == null)
            {
                TempData["Error"] = "El contenido no existe";
                return RedirectToAction("Index");
            }

            if (contenido.UsuarioId != usuario.Id)
            {
                TempData["Error"] = "No tienes permiso para apelar este contenido";
                return RedirectToAction("Index");
            }

            // Generar numero de referencia unico
            var numeroReferencia = $"APL-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            var apelacion = new Apelacion
            {
                UsuarioId = usuario.Id,
                TipoContenido = "Publicacion",
                ContenidoId = contenidoId,
                RazonRechazo = razonRechazo,
                Argumentos = argumentos,
                EvidenciaAdicional = evidencia,
                Estado = EstadoApelacion.Pendiente,
                FechaCreacion = DateTime.Now,
                NumeroReferencia = numeroReferencia,
                Prioridad = usuario.EsCreador ? PrioridadApelacion.Alta : PrioridadApelacion.Normal
            };

            _context.Apelaciones.Add(apelacion);
            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Nueva apelacion creada: {numeroReferencia}",
                CategoriaEvento.Contenido,
                TipoLogEvento.Evento,
                usuario.Id,
                usuario.NombreCompleto ?? usuario.UserName,
                $"Tipo: Publicacion, ContenidoId: {contenidoId}"
            );

            TempData["Success"] = $"Tu apelacion ha sido enviada con el numero de referencia: {numeroReferencia}. Te notificaremos cuando sea revisada.";
            return RedirectToAction("Index");
        }

        // GET: Crear Apelacion para Story
        [HttpGet]
        public async Task<IActionResult> CrearStory(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();

            if (story.CreadorId != usuario.Id)
            {
                TempData["Error"] = "No tienes permiso para apelar esta story";
                return RedirectToAction("Index");
            }

            var apelacionExistente = await _context.Apelaciones
                .AnyAsync(a => a.StoryId == id &&
                              a.UsuarioId == usuario.Id &&
                              (a.Estado == EstadoApelacion.Pendiente || a.Estado == EstadoApelacion.EnRevision));

            if (apelacionExistente)
            {
                TempData["Error"] = "Ya tienes una apelacion pendiente para esta story";
                return RedirectToAction("Index");
            }

            ViewBag.Story = story;
            return View();
        }

        // POST: Crear Apelacion para Story
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearStory(int storyId, string razonRechazo, string argumentos, string? evidencia)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                TempData["Error"] = "La story no existe";
                return RedirectToAction("Index");
            }

            if (story.CreadorId != usuario.Id)
            {
                TempData["Error"] = "No tienes permiso para apelar esta story";
                return RedirectToAction("Index");
            }

            var numeroReferencia = $"APL-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            var apelacion = new Apelacion
            {
                UsuarioId = usuario.Id,
                TipoContenido = "Story",
                StoryId = storyId,
                RazonRechazo = razonRechazo,
                Argumentos = argumentos,
                EvidenciaAdicional = evidencia,
                Estado = EstadoApelacion.Pendiente,
                FechaCreacion = DateTime.Now,
                NumeroReferencia = numeroReferencia,
                Prioridad = PrioridadApelacion.Normal
            };

            _context.Apelaciones.Add(apelacion);
            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Nueva apelacion de Story: {numeroReferencia}",
                CategoriaEvento.Contenido,
                TipoLogEvento.Evento,
                usuario.Id,
                usuario.NombreCompleto ?? usuario.UserName,
                $"StoryId: {storyId}"
            );

            TempData["Success"] = $"Tu apelacion ha sido enviada con el numero de referencia: {numeroReferencia}";
            return RedirectToAction("Index");
        }

        // GET: Ver detalle de apelacion
        public async Task<IActionResult> Detalle(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var apelacion = await _context.Apelaciones
                .Include(a => a.Contenido)
                .ThenInclude(c => c!.Archivos)
                .Include(a => a.Story)
                .Include(a => a.Administrador)
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuario.Id);

            if (apelacion == null) return NotFound();

            return View(apelacion);
        }

        // POST: Cancelar apelacion (solo si esta pendiente)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Json(new { success = false, message = "No autenticado" });

            var apelacion = await _context.Apelaciones
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuario.Id);

            if (apelacion == null)
            {
                return Json(new { success = false, message = "Apelacion no encontrada" });
            }

            if (apelacion.Estado != EstadoApelacion.Pendiente)
            {
                return Json(new { success = false, message = "Solo puedes cancelar apelaciones pendientes" });
            }

            _context.Apelaciones.Remove(apelacion);
            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Apelacion cancelada: {apelacion.NumeroReferencia}",
                CategoriaEvento.Contenido,
                TipoLogEvento.Evento,
                usuario.Id,
                usuario.NombreCompleto ?? usuario.UserName,
                null
            );

            return Json(new { success = true, message = "Apelacion cancelada correctamente" });
        }

        // API: Verificar si puede apelar contenido
        [HttpGet]
        public async Task<IActionResult> PuedeApelar(string tipo, int id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Json(new { puede = false, razon = "No autenticado" });

            bool existeApelacion = false;

            if (tipo == "contenido")
            {
                existeApelacion = await _context.Apelaciones
                    .AnyAsync(a => a.ContenidoId == id &&
                                  a.UsuarioId == usuario.Id &&
                                  (a.Estado == EstadoApelacion.Pendiente || a.Estado == EstadoApelacion.EnRevision));
            }
            else if (tipo == "story")
            {
                existeApelacion = await _context.Apelaciones
                    .AnyAsync(a => a.StoryId == id &&
                                  a.UsuarioId == usuario.Id &&
                                  (a.Estado == EstadoApelacion.Pendiente || a.Estado == EstadoApelacion.EnRevision));
            }

            if (existeApelacion)
            {
                return Json(new { puede = false, razon = "Ya existe una apelacion pendiente" });
            }

            return Json(new { puede = true });
        }
    }
}
