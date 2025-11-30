using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Models;
using Lado.Data;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Lado.Controllers
{
    [Authorize]
    public class CreatorVerificationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CreatorVerificationController> _logger;

        public CreatorVerificationController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILogger<CreatorVerificationController> logger)
        {
            _userManager = userManager;
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Request()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

      
                var solicitudExistente = await _context.CreatorVerificationRequests
                    .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Estado == "Pendiente");

                if (solicitudExistente != null)
                {
                    ViewBag.SolicitudPendiente = true;
                    ViewBag.FechaSolicitud = solicitudExistente.FechaSolicitud;
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar página de verificación");
                TempData["Error"] = "Error al cargar la página. Por favor intenta nuevamente.";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Request(CreatorVerificationRequestViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Modelo inválido en verificación");
                    return View(model);
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogError("Usuario no encontrado en verificación");
                    return RedirectToAction("Login", "Account");
                }

                // Validar archivos obligatorios
                if (model.DocumentoIdentidad == null || model.SelfieConDocumento == null)
                {
                    _logger.LogWarning("Archivos obligatorios faltantes - Usuario: {UserId}", user.Id);
                    ModelState.AddModelError("", "Debes subir ambos documentos obligatorios.");
                    return View(model);
                }

                // Validar formatos
                var formatosPermitidos = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                var extDocumento = Path.GetExtension(model.DocumentoIdentidad.FileName).ToLower();
                var extSelfie = Path.GetExtension(model.SelfieConDocumento.FileName).ToLower();

                if (!formatosPermitidos.Contains(extDocumento) || !formatosPermitidos.Contains(extSelfie))
                {
                    _logger.LogWarning("Formato de archivo inválido - Usuario: {UserId}, Documento: {Ext1}, Selfie: {Ext2}",
                        user.Id, extDocumento, extSelfie);
                    ModelState.AddModelError("", "Solo se permiten archivos JPG, PNG o PDF.");
                    return View(model);
                }

                // Validar tamaño de archivos (10MB máximo)
                const long maxFileSize = 10 * 1024 * 1024;
                if (model.DocumentoIdentidad.Length > maxFileSize || model.SelfieConDocumento.Length > maxFileSize)
                {
                    _logger.LogWarning("Archivo demasiado grande - Usuario: {UserId}", user.Id);
                    ModelState.AddModelError("", "El tamaño máximo por archivo es 10MB.");
                    return View(model);
                }

                // ✅ MEJORADO: Obtener WebRootPath con múltiples fallbacks
                var webRootPath = GetWebRootPath();
                _logger.LogInformation("WebRootPath detectado: {Path}", webRootPath);

                // ✅ MEJORADO: Crear estructura de carpetas con validación detallada
                var uploadsFolder = await CrearCarpetasUpload(webRootPath, user.Id);
                if (uploadsFolder == null)
                {
                    ModelState.AddModelError("", "Error al crear carpeta de archivos. Verifica los permisos del servidor.");
                    return View(model);
                }

                // Guardar archivos
                string documentoPath = null;
                string selfiePath = null;
                string pruebaDireccionPath = null;

                try
                {
                    _logger.LogInformation("Guardando documento de identidad - Usuario: {UserId}", user.Id);
                    documentoPath = await GuardarArchivo(model.DocumentoIdentidad, uploadsFolder, "documento", user.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar documento de identidad - Usuario: {UserId}", user.Id);
                    ModelState.AddModelError("", $"Error al guardar el documento de identidad: {ex.Message}");
                    return View(model);
                }

                try
                {
                    _logger.LogInformation("Guardando selfie - Usuario: {UserId}", user.Id);
                    selfiePath = await GuardarArchivo(model.SelfieConDocumento, uploadsFolder, "selfie", user.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar selfie - Usuario: {UserId}", user.Id);
                    ModelState.AddModelError("", $"Error al guardar la selfie con documento: {ex.Message}");
                    return View(model);
                }

                // Prueba de dirección (opcional)
                if (model.PruebaDireccion != null)
                {
                    var extDireccion = Path.GetExtension(model.PruebaDireccion.FileName).ToLower();
                    if (formatosPermitidos.Contains(extDireccion))
                    {
                        try
                        {
                            _logger.LogInformation("Guardando prueba de dirección - Usuario: {UserId}", user.Id);
                            pruebaDireccionPath = await GuardarArchivo(model.PruebaDireccion, uploadsFolder, "direccion", user.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error al guardar prueba de dirección (opcional) - Usuario: {UserId}", user.Id);
                        }
                    }
                }

                // Crear solicitud en base de datos
                try
                {
                    var solicitud = new CreatorVerificationRequest
                    {
                        UserId = user.Id,
                        NombreCompleto = model.NombreCompleto,
                        TipoDocumento = model.TipoDocumento,
                        NumeroDocumento = model.NumeroDocumento,
                        Pais = model.Pais,
                        Ciudad = model.Ciudad,
                        Direccion = model.Direccion,
                        Telefono = model.Telefono,
                        DocumentoIdentidadPath = documentoPath,
                        SelfieConDocumentoPath = selfiePath,
                        PruebaDireccionPath = pruebaDireccionPath,
                        Estado = "Pendiente",
                        FechaSolicitud = DateTime.Now
                    };

                    _context.CreatorVerificationRequests.Add(solicitud);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Solicitud de verificación creada exitosamente - Usuario: {UserId}, SolicitudId: {SolicitudId}",
                        user.Id, solicitud.Id);

                    TempData["Success"] = "Solicitud enviada exitosamente. Te notificaremos cuando sea revisada.";
                    return RedirectToAction("Index", "Dashboard");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar solicitud en BD - Usuario: {UserId}", user.Id);
                    ModelState.AddModelError("", "Error al guardar la solicitud en la base de datos.");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en CreatorVerification.Request");
                ModelState.AddModelError("", $"Ocurrió un error inesperado: {ex.Message}");
                return View(model);
            }
        }

        // ✅ NUEVO: Método para detectar WebRootPath con múltiples fallbacks
        private string GetWebRootPath()
        {
            var webRootPath = _environment.WebRootPath;

            if (string.IsNullOrEmpty(webRootPath))
            {
                // Fallback 1: ContentRootPath + wwwroot
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
                _logger.LogWarning("WebRootPath estaba vacío, usando ContentRootPath: {Path}", webRootPath);
            }

            if (!Directory.Exists(webRootPath))
            {
                // Fallback 2: Directorio actual + wwwroot
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                _logger.LogWarning("WebRootPath no existe, usando CurrentDirectory: {Path}", webRootPath);
            }

            return webRootPath;
        }

        // ✅ NUEVO: Método robusto para crear carpetas con validación de permisos
        private async Task<string> CrearCarpetasUpload(string webRootPath, string userId)
        {
            try
            {
                // Carpeta base de uploads
                var uploadsBase = Path.Combine(webRootPath, "uploads");
                _logger.LogInformation("Creando carpeta base: {Path}", uploadsBase);

                if (!Directory.Exists(uploadsBase))
                {
                    Directory.CreateDirectory(uploadsBase);
                    _logger.LogInformation("✅ Carpeta uploads creada");
                }

                // Carpeta de verifications
                var verificationsFolder = Path.Combine(uploadsBase, "verifications");
                if (!Directory.Exists(verificationsFolder))
                {
                    Directory.CreateDirectory(verificationsFolder);
                    _logger.LogInformation("✅ Carpeta verifications creada");
                }

                // Carpeta del usuario específico
                var userFolder = Path.Combine(verificationsFolder, userId);
                if (!Directory.Exists(userFolder))
                {
                    Directory.CreateDirectory(userFolder);
                    _logger.LogInformation("✅ Carpeta del usuario creada: {UserId}", userId);
                }

                // ✅ NUEVO: Verificar permisos de escritura
                var testFile = Path.Combine(userFolder, ".test_write_permission");
                try
                {
                    await System.IO.File.WriteAllTextAsync(testFile, "test");
                    System.IO.File.Delete(testFile);
                    _logger.LogInformation("✅ Permisos de escritura verificados correctamente");
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "❌ ERROR: No hay permisos de escritura en {Path}", userFolder);
                    _logger.LogError("SOLUCIÓN: Dar permisos de escritura a IIS_IUSRS en la carpeta uploads");
                    return null;
                }

                return userFolder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al crear carpetas de upload. Ruta base: {Path}", webRootPath);
                _logger.LogError("Detalles del error: {Message}", ex.Message);
                _logger.LogError("StackTrace: {StackTrace}", ex.StackTrace);

                // Información adicional para diagnóstico
                _logger.LogError("Usuario de Windows ejecutando la app: {User}",
                    WindowsIdentity.GetCurrent()?.Name ?? "DESCONOCIDO");

                return null;
            }
        }

        private async Task<string> GuardarArchivo(IFormFile archivo, string folder, string prefix, string userId)
        {
            try
            {
                var extension = Path.GetExtension(archivo.FileName).ToLower();
                var fileName = $"{prefix}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(folder, fileName);

                _logger.LogInformation("Guardando archivo: {FileName} en {Path}", fileName, filePath);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await archivo.CopyToAsync(stream);
                    await stream.FlushAsync();
                }

                if (!System.IO.File.Exists(filePath))
                {
                    throw new Exception($"El archivo {fileName} no se pudo guardar correctamente");
                }

                var fileInfo = new FileInfo(filePath);
                _logger.LogInformation("✅ Archivo guardado: {FileName}, Tamaño: {Size} bytes", fileName, fileInfo.Length);

                return $"/uploads/verifications/{userId}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GuardarArchivo - Prefix: {Prefix}", prefix);
                throw new Exception($"Error al guardar {prefix}: {ex.Message}", ex);
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminPanel()
        {
            try
            {
                var solicitudes = await _context.CreatorVerificationRequests
                    .Include(x => x.User)
                    .OrderByDescending(x => x.FechaSolicitud)
                    .ToListAsync();

                return View(solicitudes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar panel de admin");
                TempData["Error"] = "Error al cargar las solicitudes.";
                return RedirectToAction("Index", "Admin");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            try
            {
                var solicitud = await _context.CreatorVerificationRequests
                    .Include(x => x.User)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (solicitud == null)
                {
                    _logger.LogWarning("Solicitud no encontrada: {Id}", id);
                    return NotFound();
                }

                return View(solicitud);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al revisar solicitud: {Id}", id);
                TempData["Error"] = "Error al cargar la solicitud.";
                return RedirectToAction("AdminPanel");
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessVerification(int id, string accion, string motivo)
        {
            try
            {
                var solicitud = await _context.CreatorVerificationRequests
                    .Include(x => x.User)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (solicitud == null)
                {
                    _logger.LogWarning("Solicitud no encontrada para procesar: {Id}", id);
                    return NotFound();
                }

                var admin = await _userManager.GetUserAsync(User);

                if (accion == "aprobar")
                {
                    solicitud.Estado = "Aprobada";
                    solicitud.FechaRevision = DateTime.Now;
                    solicitud.RevisadoPor = admin.Id;

                    solicitud.User.CreadorVerificado = true;
                    solicitud.User.FechaVerificacion = DateTime.Now;
                    solicitud.User.EsCreador = true;

                    await _userManager.UpdateAsync(solicitud.User);

                    _logger.LogInformation("Creador aprobado - Usuario: {UserId}, Admin: {AdminId}",
                        solicitud.UserId, admin.Id);

                    TempData["Success"] = $"Creador {solicitud.User.UserName} aprobado exitosamente.";
                }
                else if (accion == "rechazar")
                {
                    solicitud.Estado = "Rechazada";
                    solicitud.FechaRevision = DateTime.Now;
                    solicitud.RevisadoPor = admin.Id;
                    solicitud.MotivoRechazo = motivo;

                    _logger.LogInformation("Solicitud rechazada - SolicitudId: {Id}, Motivo: {Motivo}", id, motivo);

                    TempData["Warning"] = $"Solicitud rechazada. El creador será notificado.";
                }

                await _context.SaveChangesAsync();

                return RedirectToAction("AdminPanel");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar verificación: {Id}", id);
                TempData["Error"] = "Error al procesar la solicitud.";
                return RedirectToAction("AdminPanel");
            }
        }
    }
}