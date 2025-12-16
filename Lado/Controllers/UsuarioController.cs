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
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public UsuarioController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
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

            // Calcular estadísticas

            // Total de seguidores (del campo NumeroSeguidores del usuario)
            ViewBag.TotalSeguidores = usuario.NumeroSeguidores;

            // Total siguiendo (suscripciones activas que tiene el usuario)
            ViewBag.TotalSiguiendo = await _context.Suscripciones
                .CountAsync(s => s.FanId == usuario.Id && s.EstaActiva);

            // Total de publicaciones del usuario
            ViewBag.TotalPublicaciones = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.EstaActivo && !c.EsBorrador);

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

        // POST: /Usuario/EditarPerfil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPerfil(
            ApplicationUser model,
            IFormFile? fotoPerfil,
            IFormFile? fotoPerfilLadoB,
            IFormFile? fotoPortada)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Actualizar datos básicos
            usuario.NombreCompleto = model.NombreCompleto;
            usuario.UserName = model.UserName;
            usuario.Email = model.Email;
            usuario.PhoneNumber = model.PhoneNumber;

            // Actualizar biografías
            usuario.Biografia = model.Biografia;
            usuario.BiografiaLadoB = model.BiografiaLadoB;

            // Actualizar seudónimo
            usuario.Seudonimo = model.Seudonimo;

            // Procesar foto de perfil LadoA (público)
            if (fotoPerfil != null && fotoPerfil.Length > 0)
            {
                var fileName = await GuardarArchivo(fotoPerfil, "perfiles");
                usuario.FotoPerfil = fileName;
            }

            // Procesar foto de perfil LadoB (premium)
            if (fotoPerfilLadoB != null && fotoPerfilLadoB.Length > 0)
            {
                var fileName = await GuardarArchivo(fotoPerfilLadoB, "perfiles-ladob");
                usuario.FotoPerfilLadoB = fileName;
            }

            // Procesar foto de portada (opcional, solo creadores)
            if (fotoPortada != null && fotoPortada.Length > 0 && usuario.TipoUsuario == 1)
            {
                var fileName = await GuardarArchivo(fotoPortada, "portadas");
                usuario.FotoPortada = fileName;
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

        // GET/POST: /Usuario/EliminarFoto
        public async Task<IActionResult> EliminarFoto(string lado = "A")
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            string? fotoPath = null;

            if (lado == "B")
            {
                // Eliminar foto de LadoB
                fotoPath = usuario.FotoPerfilLadoB;
                usuario.FotoPerfilLadoB = null;
            }
            else
            {
                // Eliminar foto de LadoA
                fotoPath = usuario.FotoPerfil;
                usuario.FotoPerfil = null;
            }

            // Eliminar archivo físico si existe
            if (!string.IsNullOrEmpty(fotoPath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, fotoPath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            await _userManager.UpdateAsync(usuario);

            TempData["Success"] = $"Foto de perfil Lado{lado} eliminada";
            return RedirectToAction(nameof(EditarPerfil));
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

            // Datos para el gráfico - Últimos 6 meses
            var hoy = DateTime.Now;
            var ingresosPorMes = new List<decimal>();
            var nombresMeses = new List<string>();

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

        // GET: /Usuario/Estadisticas
        public async Task<IActionResult> Estadisticas()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var hoy = DateTime.Now;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioMesAnterior = inicioMes.AddMonths(-1);

            // === INGRESOS ===
            ViewBag.IngresosEsteMes = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado" &&
                            t.FechaTransaccion >= inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            var ingresosMesAnterior = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoPago == "Completado" &&
                            t.FechaTransaccion >= inicioMesAnterior &&
                            t.FechaTransaccion < inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            ViewBag.IngresosMesAnterior = ingresosMesAnterior;

            if (ingresosMesAnterior > 0)
            {
                ViewBag.CrecimientoIngresos = Math.Round((((decimal)ViewBag.IngresosEsteMes - ingresosMesAnterior) / ingresosMesAnterior) * 100, 1);
            }
            else
            {
                ViewBag.CrecimientoIngresos = (decimal)ViewBag.IngresosEsteMes > 0 ? 100 : 0;
            }

            ViewBag.TotalGanancias = usuario.TotalGanancias;

            // === SUSCRIPTORES ===
            ViewBag.SuscriptoresActivos = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id && s.EstaActiva);

            ViewBag.NuevosSuscriptoresMes = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id &&
                                s.FechaInicio >= inicioMes);

            ViewBag.SuscriptoresMesAnterior = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id &&
                                s.FechaInicio >= inicioMesAnterior &&
                                s.FechaInicio < inicioMes);

            var suscriptoresBajaMes = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id &&
                                !s.EstaActiva &&
                                s.FechaFin != null &&
                                s.FechaFin >= inicioMes);
            ViewBag.SuscriptoresBajaMes = suscriptoresBajaMes;

            // Tasa de retencion
            var totalSuscriptoresAlInicioMes = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id && s.FechaInicio < inicioMes);
            if (totalSuscriptoresAlInicioMes > 0)
            {
                ViewBag.TasaRetencion = Math.Round(((totalSuscriptoresAlInicioMes - suscriptoresBajaMes) / (decimal)totalSuscriptoresAlInicioMes) * 100, 1);
            }
            else
            {
                ViewBag.TasaRetencion = 100;
            }

            // === CONTENIDO ===
            ViewBag.TotalContenidos = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.EstaActivo && !c.EsBorrador);

            ViewBag.ContenidosLadoA = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.EstaActivo && !c.EsBorrador && c.TipoLado == TipoLado.LadoA);

            ViewBag.ContenidosLadoB = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.EstaActivo && !c.EsBorrador && c.TipoLado == TipoLado.LadoB);

            ViewBag.ContenidosEsteMes = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.FechaPublicacion >= inicioMes);

            // === INTERACCIONES ===
            ViewBag.TotalLikes = await _context.Likes
                .CountAsync(l => l.Contenido.UsuarioId == usuario.Id);

            ViewBag.LikesEsteMes = await _context.Likes
                .CountAsync(l => l.Contenido.UsuarioId == usuario.Id &&
                                l.FechaLike >= inicioMes);

            ViewBag.TotalComentarios = await _context.Comentarios
                .CountAsync(c => c.Contenido.UsuarioId == usuario.Id);

            ViewBag.ComentariosEsteMes = await _context.Comentarios
                .CountAsync(c => c.Contenido.UsuarioId == usuario.Id &&
                                c.FechaCreacion >= inicioMes);

            // Engagement rate
            var totalSuscriptores = (int)ViewBag.SuscriptoresActivos;
            var totalContenidos = (int)ViewBag.TotalContenidos;
            if (totalSuscriptores > 0 && totalContenidos > 0)
            {
                var totalInteracciones = (int)ViewBag.TotalLikes + (int)ViewBag.TotalComentarios;
                var posiblesInteracciones = totalSuscriptores * totalContenidos;
                ViewBag.Engagement = Math.Round((double)totalInteracciones / posiblesInteracciones * 100, 1);
            }
            else
            {
                ViewBag.Engagement = 0;
            }

            // === DATOS PARA GRAFICOS ===
            // Ingresos ultimos 6 meses
            var ingresosPorMes = new List<decimal>();
            var nombresMeses = new List<string>();
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
                nombresMeses.Add(fecha.ToString("MMM", new System.Globalization.CultureInfo("es-ES")));
            }
            ViewBag.IngresosMeses = string.Join(",", ingresosPorMes);
            ViewBag.NombresMeses = string.Join(",", nombresMeses.Select(n => $"'{n}'"));

            // Suscriptores ultimos 6 meses
            var suscriptoresPorMes = new List<int>();
            for (int i = 5; i >= 0; i--)
            {
                var fecha = hoy.AddMonths(-i);
                var finMesGrafico = new DateTime(fecha.Year, fecha.Month, 1).AddMonths(1);

                var subs = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == usuario.Id &&
                                    s.FechaInicio < finMesGrafico &&
                                    (s.EstaActiva || (s.FechaFin != null && s.FechaFin >= finMesGrafico)));

                suscriptoresPorMes.Add(subs);
            }
            ViewBag.SuscriptoresMeses = string.Join(",", suscriptoresPorMes);

            // Top contenidos mas populares
            ViewBag.TopContenidos = await _context.Contenidos
                .Where(c => c.UsuarioId == usuario.Id && c.EstaActivo)
                .OrderByDescending(c => c.Likes.Count)
                .Take(5)
                .Select(c => new {
                    c.Id,
                    c.Descripcion,
                    c.TipoContenido,
                    EsLadoB = c.TipoLado == TipoLado.LadoB,
                    c.FechaPublicacion,
                    TotalLikes = c.Likes.Count,
                    TotalComentarios = c.Comentarios.Count
                })
                .ToListAsync();

            // Proximo pago estimado
            ViewBag.ProximoPago = inicioMes.AddMonths(1);
            ViewBag.MontoEstimado = (int)ViewBag.SuscriptoresActivos * usuario.PrecioSuscripcion;

            // Visitas al perfil
            ViewBag.VisitasPerfil = usuario.VisitasPerfil;

            return View(usuario);
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

        // POST: /Usuario/CambiarIdioma
        [HttpPost]
        public async Task<IActionResult> CambiarIdioma([FromBody] CambiarIdiomaRequest request)
        {
            // Validar idioma
            var idiomasValidos = new[] { "es", "en", "pt" };
            if (string.IsNullOrEmpty(request.Idioma) || !idiomasValidos.Contains(request.Idioma))
            {
                return Json(new { success = false, message = "Idioma no válido" });
            }

            // Guardar en cookie para usuarios no autenticados
            Response.Cookies.Append("Lado.Language", request.Idioma, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddYears(1),
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax
            });

            // Si el usuario está autenticado, guardar en su perfil
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario != null)
            {
                usuario.Idioma = request.Idioma;
                await _userManager.UpdateAsync(usuario);
            }

            var nombreIdioma = request.Idioma switch
            {
                "es" => "Español",
                "en" => "English",
                "pt" => "Português",
                _ => request.Idioma
            };

            return Json(new { success = true, message = $"Idioma cambiado a {nombreIdioma}" });
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

            // Paginación
            var totalItems = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id)
                .CountAsync();

            ViewBag.TotalPaginas = (int)Math.Ceiling(totalItems / (double)itemsPorPagina);
            ViewBag.PaginaActual = pagina;

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

                // 10. Eliminar archivos físicos
                if (!string.IsNullOrEmpty(usuario.FotoPerfil))
                {
                    EliminarArchivoFisico(usuario.FotoPerfil);
                }
                if (!string.IsNullOrEmpty(usuario.FotoPerfilLadoB))
                {
                    EliminarArchivoFisico(usuario.FotoPerfilLadoB);
                }
                if (!string.IsNullOrEmpty(usuario.FotoPortada))
                {
                    EliminarArchivoFisico(usuario.FotoPortada);
                }

                // 11. Guardar cambios en la base de datos
                await _context.SaveChangesAsync();

                // 12. Eliminar usuario de Identity
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

        // Método auxiliar para eliminar archivos físicos
        private void EliminarArchivoFisico(string rutaArchivo)
        {
            if (string.IsNullOrEmpty(rutaArchivo)) return;

            try
            {
                var filePath = Path.Combine(_environment.WebRootPath, rutaArchivo.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch
            {
                // Si falla al eliminar el archivo, continuar sin detener el proceso
            }
        }

        // ========================================
        // SISTEMA DE BLOQUEO DE USUARIOS
        // ========================================

        /// <summary>
        /// Bloquear a un usuario
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> BloquearUsuario([FromBody] BloquearUsuarioRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            if (string.IsNullOrEmpty(request.UsuarioId))
            {
                return Json(new { success = false, message = "ID de usuario inválido" });
            }

            // No puede bloquearse a sí mismo
            if (request.UsuarioId == usuario.Id)
            {
                return Json(new { success = false, message = "No puedes bloquearte a ti mismo" });
            }

            // Verificar que el usuario a bloquear existe
            var usuarioABloquear = await _userManager.FindByIdAsync(request.UsuarioId);
            if (usuarioABloquear == null)
            {
                return Json(new { success = false, message = "Usuario no encontrado" });
            }

            // Verificar si ya está bloqueado
            var bloqueoExistente = await _context.BloqueosUsuarios
                .FirstOrDefaultAsync(b => b.BloqueadorId == usuario.Id && b.BloqueadoId == request.UsuarioId);

            if (bloqueoExistente != null)
            {
                return Json(new { success = false, message = "Este usuario ya está bloqueado" });
            }

            // Crear el bloqueo
            var bloqueo = new BloqueoUsuario
            {
                BloqueadorId = usuario.Id,
                BloqueadoId = request.UsuarioId,
                FechaBloqueo = DateTime.Now,
                Razon = request.Razon
            };

            _context.BloqueosUsuarios.Add(bloqueo);

            // Cancelar suscripciones mutuas si existen
            var suscripcionesACancelar = await _context.Suscripciones
                .Where(s => s.EstaActiva &&
                           ((s.FanId == usuario.Id && s.CreadorId == request.UsuarioId) ||
                            (s.FanId == request.UsuarioId && s.CreadorId == usuario.Id)))
                .ToListAsync();

            foreach (var suscripcion in suscripcionesACancelar)
            {
                suscripcion.EstaActiva = false;
                suscripcion.FechaCancelacion = DateTime.Now;
                suscripcion.RenovacionAutomatica = false;
            }

            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                message = $"Has bloqueado a {usuarioABloquear.NombreCompleto}",
                suscripcionesCanceladas = suscripcionesACancelar.Count
            });
        }

        /// <summary>
        /// Desbloquear a un usuario
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DesbloquearUsuario([FromBody] DesbloquearUsuarioRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var bloqueo = await _context.BloqueosUsuarios
                .FirstOrDefaultAsync(b => b.BloqueadorId == usuario.Id && b.BloqueadoId == request.UsuarioId);

            if (bloqueo == null)
            {
                return Json(new { success = false, message = "Este usuario no está bloqueado" });
            }

            _context.BloqueosUsuarios.Remove(bloqueo);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Usuario desbloqueado correctamente" });
        }

        /// <summary>
        /// Obtener lista de usuarios bloqueados
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UsuariosBloqueados()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var bloqueados = await _context.BloqueosUsuarios
                .Where(b => b.BloqueadorId == usuario.Id)
                .Include(b => b.Bloqueado)
                .OrderByDescending(b => b.FechaBloqueo)
                .Select(b => new UsuarioBloqueadoDto
                {
                    Id = b.Bloqueado!.Id,
                    NombreCompleto = b.Bloqueado.NombreCompleto,
                    UserName = b.Bloqueado.UserName ?? "",
                    FotoPerfil = b.Bloqueado.FotoPerfil,
                    FechaBloqueo = b.FechaBloqueo,
                    Razon = b.Razon
                })
                .ToListAsync();

            return View(bloqueados);
        }

        /// <summary>
        /// API: Verificar si un usuario está bloqueado
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> VerificarBloqueo(string usuarioId)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { bloqueado = false });
            }

            // Verificar si yo lo bloqueé
            var yoLoBloquee = await _context.BloqueosUsuarios
                .AnyAsync(b => b.BloqueadorId == usuario.Id && b.BloqueadoId == usuarioId);

            // Verificar si él me bloqueó
            var elMeBloqueo = await _context.BloqueosUsuarios
                .AnyAsync(b => b.BloqueadorId == usuarioId && b.BloqueadoId == usuario.Id);

            return Json(new {
                bloqueado = yoLoBloquee || elMeBloqueo,
                yoLoBloquee = yoLoBloquee,
                elMeBloqueo = elMeBloqueo
            });
        }

        /// <summary>
        /// API: Obtener IDs de usuarios bloqueados (para filtrar en Feed)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerIdsBloqueados()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { ids = new List<string>() });
            }

            // Usuarios que yo bloqueé + usuarios que me bloquearon
            var bloqueados = await _context.BloqueosUsuarios
                .Where(b => b.BloqueadorId == usuario.Id || b.BloqueadoId == usuario.Id)
                .Select(b => b.BloqueadorId == usuario.Id ? b.BloqueadoId : b.BloqueadorId)
                .Distinct()
                .ToListAsync();

            return Json(new { ids = bloqueados });
        }

        // ========================================
        // SISTEMA DE NOTIFICACIONES
        // ========================================

        /// <summary>
        /// Página de notificaciones del usuario
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Notificaciones()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var notificaciones = await _context.Notificaciones
                .Where(n => n.UsuarioId == usuario.Id && n.EstaActiva)
                .OrderByDescending(n => n.FechaCreacion)
                .Take(100)
                .ToListAsync();

            ViewBag.TotalNoLeidas = notificaciones.Count(n => !n.Leida);

            return View(notificaciones);
        }

        /// <summary>
        /// API: Obtener notificaciones del usuario
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerNotificaciones(int pagina = 1, int cantidad = 20)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                var skip = (pagina - 1) * cantidad;

                // Primero obtenemos los datos de la base de datos
                var notificacionesDb = await _context.Notificaciones
                    .Where(n => n.UsuarioId == usuario.Id && n.EstaActiva)
                    .OrderByDescending(n => n.FechaCreacion)
                    .Skip(skip)
                    .Take(cantidad)
                    .Select(n => new
                    {
                        n.Id,
                        n.Tipo,
                        n.Titulo,
                        n.Mensaje,
                        n.UrlDestino,
                        n.ImagenUrl,
                        n.FechaCreacion,
                        n.Leida
                    })
                    .ToListAsync();

                // Luego proyectamos en memoria para calcular el tiempo relativo
                var notificaciones = notificacionesDb.Select(n => new NotificacionDto
                {
                    Id = n.Id,
                    Tipo = n.Tipo,
                    Titulo = n.Titulo,
                    Mensaje = n.Mensaje,
                    UrlDestino = n.UrlDestino,
                    ImagenUrl = n.ImagenUrl,
                    FechaCreacion = n.FechaCreacion,
                    Leida = n.Leida,
                    TiempoRelativo = ObtenerTiempoRelativo(n.FechaCreacion)
                }).ToList();

                var totalNoLeidas = await _context.Notificaciones
                    .CountAsync(n => n.UsuarioId == usuario.Id && n.EstaActiva && !n.Leida);

                return Json(new {
                    success = true,
                    notificaciones,
                    noLeidas = totalNoLeidas,
                    hayMas = notificaciones.Count == cantidad
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// API: Contar notificaciones no leídas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ContarNotificacionesNoLeidas()
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { count = 0 });
                }

                var count = await _context.Notificaciones
                    .CountAsync(n => n.UsuarioId == usuario.Id && n.EstaActiva && !n.Leida);

                return Json(new { count });
            }
            catch
            {
                return Json(new { count = 0 });
            }
        }

        /// <summary>
        /// API: Marcar notificación como leída
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarcarNotificacionLeida([FromBody] MarcarNotificacionRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var notificacion = await _context.Notificaciones
                .FirstOrDefaultAsync(n => n.Id == request.NotificacionId && n.UsuarioId == usuario.Id);

            if (notificacion == null)
            {
                return Json(new { success = false, message = "Notificación no encontrada" });
            }

            notificacion.Leida = true;
            notificacion.FechaLectura = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        /// <summary>
        /// API: Marcar todas las notificaciones como leídas
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarcarTodasLeidas()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var notificacionesNoLeidas = await _context.Notificaciones
                .Where(n => n.UsuarioId == usuario.Id && !n.Leida && n.EstaActiva)
                .ToListAsync();

            foreach (var notificacion in notificacionesNoLeidas)
            {
                notificacion.Leida = true;
                notificacion.FechaLectura = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, marcadas = notificacionesNoLeidas.Count });
        }

        /// <summary>
        /// API: Eliminar una notificación
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarNotificacion([FromBody] MarcarNotificacionRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var notificacion = await _context.Notificaciones
                .FirstOrDefaultAsync(n => n.Id == request.NotificacionId && n.UsuarioId == usuario.Id);

            if (notificacion == null)
            {
                return Json(new { success = false, message = "Notificación no encontrada" });
            }

            notificacion.EstaActiva = false;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        /// <summary>
        /// Método auxiliar para obtener tiempo relativo
        /// </summary>
        private static string ObtenerTiempoRelativo(DateTime fecha)
        {
            var diferencia = DateTime.Now - fecha;

            if (diferencia.TotalMinutes < 1)
                return "Ahora";
            if (diferencia.TotalMinutes < 60)
                return $"Hace {(int)diferencia.TotalMinutes}m";
            if (diferencia.TotalHours < 24)
                return $"Hace {(int)diferencia.TotalHours}h";
            if (diferencia.TotalDays < 7)
                return $"Hace {(int)diferencia.TotalDays}d";
            if (diferencia.TotalDays < 30)
                return $"Hace {(int)(diferencia.TotalDays / 7)}sem";
            if (diferencia.TotalDays < 365)
                return $"Hace {(int)(diferencia.TotalDays / 30)}mes";

            return $"Hace {(int)(diferencia.TotalDays / 365)}año";
        }
    }

    // ========================================
    // DTOs para Bloqueos
    // ========================================
    public class BloquearUsuarioRequest
    {
        public string UsuarioId { get; set; } = string.Empty;
        public string? Razon { get; set; }
    }

    public class DesbloquearUsuarioRequest
    {
        public string UsuarioId { get; set; } = string.Empty;
    }

    public class UsuarioBloqueadoDto
    {
        public string Id { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? FotoPerfil { get; set; }
        public DateTime FechaBloqueo { get; set; }
        public string? Razon { get; set; }
    }

    public class CambiarIdiomaRequest
    {
        public string Idioma { get; set; } = "es";
    }

    // DTO para notificaciones
    public class NotificacionDto
    {
        public int Id { get; set; }
        public TipoNotificacion Tipo { get; set; }
        public string? Titulo { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string? UrlDestino { get; set; }
        public string? ImagenUrl { get; set; }
        public DateTime FechaCreacion { get; set; }
        public bool Leida { get; set; }
        public string TiempoRelativo { get; set; } = string.Empty;
    }

    public class MarcarNotificacionRequest
    {
        public int NotificacionId { get; set; }
    }
}