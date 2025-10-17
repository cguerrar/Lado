using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    [Authorize]
    public class MensajesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MensajesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            List<string> contactosIds = new List<string>();

            if (usuario.TipoUsuario == 0) // Fan
            {
                var creadoresSuscritos = await _context.Suscripciones
                    .Where(s => s.FanId == usuario.Id && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                var conversacionesExistentes = await _context.MensajesPrivados
                    .Where(m => m.RemitenteId == usuario.Id || m.DestinatarioId == usuario.Id)
                    .Select(m => m.RemitenteId == usuario.Id ? m.DestinatarioId : m.RemitenteId)
                    .Distinct()
                    .ToListAsync();

                contactosIds = creadoresSuscritos
                    .Union(conversacionesExistentes)
                    .Distinct()
                    .ToList();
            }
            else if (usuario.TipoUsuario == 1) // Creador
            {
                contactosIds = await _context.MensajesPrivados
                    .Where(m => m.RemitenteId == usuario.Id || m.DestinatarioId == usuario.Id)
                    .Select(m => m.RemitenteId == usuario.Id ? m.DestinatarioId : m.RemitenteId)
                    .Distinct()
                    .ToListAsync();
            }

            var conversaciones = new List<ConversacionViewModel>();

            foreach (var contactoId in contactosIds)
            {
                var contacto = await _userManager.FindByIdAsync(contactoId);
                if (contacto == null) continue;

                var ultimoMensaje = await _context.MensajesPrivados
                    .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId) ||
                               (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id))
                    .OrderByDescending(m => m.FechaEnvio)
                    .FirstOrDefaultAsync();

                var mensajesNoLeidos = await _context.MensajesPrivados
                    .Where(m => m.RemitenteId == contactoId &&
                               m.DestinatarioId == usuario.Id &&
                               !m.Leido &&
                               !m.EliminadoPorDestinatario)
                    .CountAsync();

                conversaciones.Add(new ConversacionViewModel
                {
                    Contacto = contacto,
                    UltimoMensaje = ultimoMensaje,
                    MensajesNoLeidos = mensajesNoLeidos,
                    TieneConversacion = ultimoMensaje != null
                });
            }

            ViewBag.Conversaciones = conversaciones
                .OrderByDescending(c => c.MensajesNoLeidos > 0)
                .ThenByDescending(c => c.UltimoMensaje?.FechaEnvio ?? DateTime.MinValue)
                .ToList();

            return View(usuario);
        }

        public async Task<IActionResult> Chat(string id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var destinatario = await _userManager.FindByIdAsync(id);
            if (destinatario == null)
            {
                return NotFound();
            }

            if (usuario.TipoUsuario == 0) // Fan
            {
                var estaSuscrito = await _context.Suscripciones
                    .AnyAsync(s => s.FanId == usuario.Id &&
                                  s.CreadorId == id &&
                                  s.EstaActiva);

                if (!estaSuscrito)
                {
                    TempData["Error"] = "Debes estar suscrito para enviar mensajes a este creador.";
                    return RedirectToAction("Index");
                }
            }

            var mensajes = await _context.MensajesPrivados
                .Include(m => m.Remitente)
                .Include(m => m.Destinatario)
                .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == id) ||
                           (m.RemitenteId == id && m.DestinatarioId == usuario.Id))
                .Where(m => (m.RemitenteId == usuario.Id && !m.EliminadoPorRemitente) ||
                           (m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario))
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();

            var mensajesNoLeidos = mensajes.Where(m => m.DestinatarioId == usuario.Id && !m.Leido).ToList();
            foreach (var mensaje in mensajesNoLeidos)
            {
                mensaje.Leido = true;
            }
            await _context.SaveChangesAsync();

            ViewBag.Mensajes = mensajes;
            ViewBag.Destinatario = destinatario;

            return View(usuario);
        }

        [HttpPost]
        public async Task<IActionResult> EnviarMensaje(string destinatarioId, string contenido)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== ENVIAR MENSAJE - INICIO ===");
                System.Diagnostics.Debug.WriteLine($"DestinatarioId recibido: {destinatarioId}");
                System.Diagnostics.Debug.WriteLine($"Contenido recibido: {contenido}");

                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Usuario no autenticado");
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                System.Diagnostics.Debug.WriteLine($"Usuario autenticado: {usuario.UserName} (ID: {usuario.Id})");

                if (string.IsNullOrWhiteSpace(contenido))
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Contenido vacío");
                    return Json(new { success = false, message = "El mensaje no puede estar vacío" });
                }

                if (string.IsNullOrWhiteSpace(destinatarioId))
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: DestinatarioId vacío");
                    return Json(new { success = false, message = "Destinatario no especificado" });
                }

                var destinatario = await _userManager.FindByIdAsync(destinatarioId);
                if (destinatario == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Destinatario no encontrado con ID: {destinatarioId}");
                    return Json(new { success = false, message = "Destinatario no encontrado" });
                }

                System.Diagnostics.Debug.WriteLine($"Destinatario encontrado: {destinatario.UserName}");

                // Verificar permisos para fans
                if (usuario.TipoUsuario == 0)
                {
                    var estaSuscrito = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == usuario.Id &&
                                      s.CreadorId == destinatarioId &&
                                      s.EstaActiva);

                    System.Diagnostics.Debug.WriteLine($"¿Está suscrito?: {estaSuscrito}");

                    if (!estaSuscrito)
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR: No está suscrito");
                        return Json(new { success = false, message = "Debes estar suscrito para enviar mensajes" });
                    }
                }

                var mensaje = new MensajePrivado
                {
                    RemitenteId = usuario.Id,
                    DestinatarioId = destinatarioId,
                    Contenido = contenido,
                    FechaEnvio = DateTime.Now,
                    Leido = false
                };

                _context.MensajesPrivados.Add(mensaje);
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"✅ Mensaje guardado exitosamente. ID: {mensaje.Id}");
                System.Diagnostics.Debug.WriteLine("=== ENVIAR MENSAJE - FIN ===");

                return Json(new
                {
                    success = true,
                    mensaje = new
                    {
                        id = mensaje.Id,
                        contenido = mensaje.Contenido,
                        fechaEnvio = mensaje.FechaEnvio.ToString("HH:mm"),
                        remitenteId = mensaje.RemitenteId
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ EXCEPCIÓN en EnviarMensaje: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = "Error al enviar el mensaje: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarConversacion(string contactoId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var mensajes = await _context.MensajesPrivados
                .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId) ||
                           (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id))
                .ToListAsync();

            foreach (var mensaje in mensajes)
            {
                if (mensaje.RemitenteId == usuario.Id)
                {
                    mensaje.EliminadoPorRemitente = true;
                }
                else
                {
                    mensaje.EliminadoPorDestinatario = true;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Conversación eliminada correctamente";

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> CargarMensajes(string contactoId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false });
            }

            var mensajes = await _context.MensajesPrivados
                .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId) ||
                           (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id))
                .Where(m => (m.RemitenteId == usuario.Id && !m.EliminadoPorRemitente) ||
                           (m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario))
                .OrderBy(m => m.FechaEnvio)
                .Select(m => new
                {
                    id = m.Id,
                    remitenteId = m.RemitenteId,
                    contenido = m.Contenido,
                    fechaEnvio = m.FechaEnvio.ToString("HH:mm"),
                    leido = m.Leido
                })
                .ToListAsync();

            return Json(new { success = true, mensajes });
        }
    }

    public class ConversacionViewModel
    {
        public ApplicationUser Contacto { get; set; }
        public MensajePrivado UltimoMensaje { get; set; }
        public int MensajesNoLeidos { get; set; }
        public bool TieneConversacion { get; set; }
    }
}