using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    [Authorize]
    public class UsuarioController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager; // ⭐ AGREGAR ESTO
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public UsuarioController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, // ⭐ AGREGAR ESTO
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager; // ⭐ AGREGAR ESTO
            _context = context;
            _environment = environment;
        }

        // GET: /Usuario/Perfil
        public async Task<IActionResult> Perfil()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

    
            return View(usuario);
        }

        // GET: /Usuario/EditarPerfil
        public async Task<IActionResult> EditarPerfil()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(usuario);
        }

        // POST: /Usuario/EliminarCuenta
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCuenta(string confirmacion)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Verificar confirmación
            if (confirmacion != "ELIMINAR")
            {
                TempData["Error"] = "Debes escribir 'ELIMINAR' para confirmar";
                return RedirectToAction(nameof(Configuracion));
            }

            try
            {
                // 1. Eliminar contenidos del usuario
                var contenidos = _context.Contenidos.Where(c => c.UsuarioId == usuario.Id);
                _context.Contenidos.RemoveRange(contenidos);

                // 2. Eliminar likes
                var likes = _context.Likes.Where(l => l.UsuarioId == usuario.Id);
                _context.Likes.RemoveRange(likes);

                // 3. Eliminar comentarios
                var comentarios = _context.Comentarios.Where(c => c.UsuarioId == usuario.Id);
                _context.Comentarios.RemoveRange(comentarios);

                // 4. Eliminar suscripciones (como fan)
                var suscripcionesFan = _context.Suscripciones
                    .Where(s => s.FanId == usuario.Id);
                _context.Suscripciones.RemoveRange(suscripcionesFan);

                // 5. Eliminar suscripciones (como creador)
                var suscripcionesCreador = _context.Suscripciones
                    .Where(s => s.CreadorId == usuario.Id);
                _context.Suscripciones.RemoveRange(suscripcionesCreador);

                // 6. Eliminar mensajes
                var mensajes = _context.MensajesPrivados
                    .Where(m => m.RemitenteId == usuario.Id || m.DestinatarioId == usuario.Id);
                _context.MensajesPrivados.RemoveRange(mensajes);

                // 7. Eliminar chat mensajes
                var chatMensajes = _context.ChatMensajes
                    .Where(m => m.RemitenteId == usuario.Id || m.DestinatarioId == usuario.Id);
                _context.ChatMensajes.RemoveRange(chatMensajes);

                // 8. Eliminar transacciones
                var transacciones = _context.Transacciones
                    .Where(t => t.UsuarioId == usuario.Id);
                _context.Transacciones.RemoveRange(transacciones);

                // 9. Eliminar reportes
                var reportes = _context.Reportes
                    .Where(r => r.UsuarioReportadorId == usuario.Id ||
                               r.UsuarioReportadoId == usuario.Id);
                _context.Reportes.RemoveRange(reportes);

                // 10. Guardar cambios en la base de datos
                await _context.SaveChangesAsync();

                // 11. Eliminar usuario de Identity
                var result = await _userManager.DeleteAsync(usuario);

                if (result.Succeeded)
                {
                    // Cerrar sesión
                    await _signInManager.SignOutAsync();

                    TempData["Success"] = "Tu cuenta ha sido eliminada permanentemente";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    TempData["Error"] = "Error al eliminar la cuenta: " +
                        string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Configuracion));
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar la cuenta: " + ex.Message;
                return RedirectToAction(nameof(Configuracion));
            }
        }




        // POST: /Usuario/EditarPerfil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPerfil(
            ApplicationUser model,
            IFormFile? fotoPerfil,
            IFormFile? fotoPortada)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Actualizar datos
            usuario.NombreCompleto = model.NombreCompleto;
            usuario.UserName = model.UserName;
            usuario.Email = model.Email;
            usuario.PhoneNumber = model.PhoneNumber;
            usuario.Biografia = model.Biografia;

     
            // Procesar foto de perfil
            if (fotoPerfil != null && fotoPerfil.Length > 0)
            {
                var fileName = await GuardarArchivo(fotoPerfil, "perfiles");
                usuario.FotoPerfil = fileName;
            }

      
            var result = await _userManager.UpdateAsync(usuario);

            if (result.Succeeded)
            {
                TempData["Success"] = "Perfil actualizado correctamente";
                return RedirectToAction(nameof(Perfil));
            }

            TempData["Error"] = "Error al actualizar el perfil";
            return View(model);
        }

        // GET: /Usuario/Billetera
        public async Task<IActionResult> Billetera()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Ingresos este mes
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ViewBag.IngresosEsteMes = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado" &&
                            t.FechaTransaccion >= inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            // Crecimiento del mes (comparado con mes anterior)
            var inicioMesAnterior = inicioMes.AddMonths(-1);
            var ingresosMesAnterior = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado" &&
                            t.FechaTransaccion >= inicioMesAnterior &&
                            t.FechaTransaccion < inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            if (ingresosMesAnterior > 0)
            {
                ViewBag.CrecimientoMes = Math.Round(((ViewBag.IngresosEsteMes - ingresosMesAnterior) / ingresosMesAnterior) * 100, 1);
            }
            else
            {
                ViewBag.CrecimientoMes = 0;
            }

            // Total retirado
            ViewBag.TotalRetirado = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion == TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado")
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            // Retiros pendientes
            ViewBag.RetirosPendientes = await _context.Transacciones
                .CountAsync(t => t.UsuarioId == usuario.Id &&
                                t.TipoTransaccion == TipoTransaccion.Retiro &&
                                t.EstadoPago == "Pendiente");

            // CORREGIDO: Datos para el gráfico - Últimos 6 meses
            var hoy = DateTime.Now;
            var ingresosPorMes = new List<decimal>();
            var nombresMeses = new List<string>();

            // Calcular correctamente los últimos 6 meses
            for (int i = 5; i >= 0; i--)
            {
                var fecha = hoy.AddMonths(-i);
                var inicioMesGrafico = new DateTime(fecha.Year, fecha.Month, 1);
                var finMesGrafico = inicioMesGrafico.AddMonths(1);

                var ingresos = await _context.Transacciones
                    .Where(t => t.UsuarioId == usuario.Id &&
                               t.TipoTransaccion != TipoTransaccion.Retiro &&
                               t.EstadoPago == "Completado" &&
                               t.FechaTransaccion >= inicioMesGrafico &&
                               t.FechaTransaccion < finMesGrafico)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;

                ingresosPorMes.Add(ingresos);
                nombresMeses.Add(fecha.ToString("MMMM", new System.Globalization.CultureInfo("es-ES")));

                System.Diagnostics.Debug.WriteLine($"Mes {fecha:MMMM yyyy}: ${ingresos}");
            }

            ViewBag.IngresosMes1 = ingresosPorMes[0];
            ViewBag.IngresosMes2 = ingresosPorMes[1];
            ViewBag.IngresosMes3 = ingresosPorMes[2];
            ViewBag.IngresosMes4 = ingresosPorMes[3];
            ViewBag.IngresosMes5 = ingresosPorMes[4];
            ViewBag.IngresosMes6 = ingresosPorMes[5];

            ViewBag.NombreMes1 = nombresMeses[0];
            ViewBag.NombreMes2 = nombresMeses[1];
            ViewBag.NombreMes3 = nombresMeses[2];
            ViewBag.NombreMes4 = nombresMeses[3];
            ViewBag.NombreMes5 = nombresMeses[4];
            ViewBag.NombreMes6 = nombresMeses[5];

            // Transacciones recientes (últimas 10)
            ViewBag.Transacciones = await _context.Transacciones
     .Where(t => t.UsuarioId == usuario.Id)
     .OrderByDescending(t => t.FechaTransaccion)
     .Take(10)
     .Select(t => new TransaccionDto
     {
         Id = t.Id,
         FechaTransaccion = t.FechaTransaccion,
         Tipo = t.TipoTransaccion == TipoTransaccion.Retiro ? "Retiro" : "Ingreso",
         Descripcion = t.Descripcion,
         Monto = t.Monto,
         Estado = t.EstadoPago ?? "Completado",
         TipoTransaccion = t.TipoTransaccion
     })
     .ToListAsync();

            // Método de pago configurado
            ViewBag.MetodoPago = "Transferencia Bancaria";
            ViewBag.CuentaBancaria = "**** **** 1234";

            // Próximo pago
            var proximoPago = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1);
            ViewBag.ProximoPago = proximoPago;

            // Monto estimado del próximo pago
            var suscriptoresActivos = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id && s.EstaActiva);

            ViewBag.MontoEstimado = suscriptoresActivos * usuario.PrecioSuscripcion;

            return View("~/Views/Billetera/Index.cshtml", usuario);
        }

        // GET: /Usuario/Configuracion
        public async Task<IActionResult> Configuracion()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Cargar configuraciones adicionales
            ViewBag.MetodoPago = "No configurado";
            ViewBag.CuentaBancaria = "No configurado";

            return View(usuario);
        }

        // POST: /Usuario/ActualizarCuenta
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarCuenta(string userName)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            usuario.UserName = userName;
            var result = await _userManager.UpdateAsync(usuario);

            if (result.Succeeded)
            {
                TempData["Success"] = "Cuenta actualizada correctamente";
            }
            else
            {
                TempData["Error"] = "Error al actualizar la cuenta";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        // POST: /Usuario/CambiarContrasena
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(
            string contrasenaActual,
            string nuevaContrasena,
            string confirmarContrasena)
        {
            if (nuevaContrasena != confirmarContrasena)
            {
                TempData["Error"] = "Las contraseñas no coinciden";
                return RedirectToAction(nameof(Configuracion));
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _userManager.ChangePasswordAsync(
                usuario,
                contrasenaActual,
                nuevaContrasena);

            if (result.Succeeded)
            {
                TempData["Success"] = "Contraseña cambiada correctamente";
            }
            else
            {
                TempData["Error"] = "Error al cambiar la contraseña. Verifica que la contraseña actual sea correcta.";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        // POST: /Usuario/ActualizarNotificaciones
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarNotificaciones(IFormCollection form)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Aquí guardarías las preferencias de notificaciones
            TempData["Success"] = "Preferencias de notificaciones actualizadas";
            return RedirectToAction(nameof(Configuracion));
        }

        // POST: /Usuario/ActualizarPrivacidad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPrivacidad(IFormCollection form)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Aquí guardarías las preferencias de privacidad
            TempData["Success"] = "Configuración de privacidad actualizada";
            return RedirectToAction(nameof(Configuracion));
        }

        // GET: /Usuario/ActividadReciente
        public async Task<IActionResult> ActividadReciente(int pagina = 1, string tipo = "todas")
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int itemsPorPagina = 20;

            {
                // Actividades para fans
                var suscripciones = await _context.Suscripciones
                    .Where(s => s.FanId == usuario.Id)
                    .Include(s => s.Creador)
                    .OrderByDescending(s => s.FechaInicio)
                    .ToListAsync();

                ViewBag.ActividadesFan = suscripciones.Select(s => new
                {
                    Descripcion = $"Te suscribiste a {s.Creador.NombreCompleto}",
                    Tipo = "suscripcion",
                    Fecha = s.FechaInicio
                }).ToList();

                ViewBag.Suscripciones = suscripciones;

                ViewBag.GastosEstaSemana = await _context.Transacciones
                    .Where(t => t.UsuarioId == usuario.Id &&
                                t.FechaTransaccion >= DateTime.Now.AddDays(-7))
                    .SumAsync(t => (decimal?)t.Monto) ?? 0;
            }

            // Paginación
            var totalItems = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id)
                .CountAsync();

            ViewBag.TotalPaginas = (int)Math.Ceiling(totalItems / (double)itemsPorPagina);
            ViewBag.PaginaActual = pagina;

            return View(usuario);
        }

        // GET/POST: /Usuario/EliminarFoto
        public async Task<IActionResult> EliminarFoto()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Eliminar archivo físico si existe
            if (!string.IsNullOrEmpty(usuario.FotoPerfil))
            {
                var filePath = Path.Combine(_environment.WebRootPath, usuario.FotoPerfil.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            usuario.FotoPerfil = null;
            await _userManager.UpdateAsync(usuario);

            TempData["Success"] = "Foto de perfil eliminada";
            return RedirectToAction(nameof(EditarPerfil));
        }

        // Método auxiliar para guardar archivos
        private async Task<string> GuardarArchivo(IFormFile archivo, string carpeta)
        {
            if (archivo == null || archivo.Length == 0)
                return null;

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", carpeta);
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + archivo.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await archivo.CopyToAsync(fileStream);
            }

            return $"/uploads/{carpeta}/{uniqueFileName}";
        }
    }
}