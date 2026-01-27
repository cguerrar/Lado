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
    public class UsuarioController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IInteresesService _interesesService;
        private readonly IFileValidationService _fileValidationService;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly ILogger<UsuarioController> _logger;

        public UsuarioController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IInteresesService interesesService,
            IFileValidationService fileValidationService,
            ILadoCoinsService ladoCoinsService,
            ILogger<UsuarioController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _environment = environment;
            _interesesService = interesesService;
            _fileValidationService = fileValidationService;
            _ladoCoinsService = ladoCoinsService;
            _logger = logger;
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
            IFormFile? fotoPortada,
            IFormFile? fotoPortadaLadoB)
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

            // Actualizar campos para completar perfil (LadoCoins)
            usuario.Pais = model.Pais;
            usuario.FechaNacimiento = model.FechaNacimiento;
            usuario.Genero = model.Genero;

            // Actualizar biografías
            usuario.Biografia = model.Biografia;
            usuario.BiografiaLadoB = model.BiografiaLadoB;

            // Actualizar seudónimo
            usuario.Seudonimo = model.Seudonimo;

            // Actualizar redes sociales
            usuario.Instagram = model.Instagram;
            usuario.Twitter = model.Twitter;
            usuario.Facebook = model.Facebook;
            usuario.YouTube = model.YouTube;
            usuario.TikTok = model.TikTok;
            usuario.OnlyFans = model.OnlyFans;

            try
            {
                // Procesar foto de perfil LadoA (público)
                if (fotoPerfil != null && fotoPerfil.Length > 0)
                {
                    var fileName = await GuardarArchivo(fotoPerfil, "perfiles");
                    if (fileName != null) usuario.FotoPerfil = fileName;
                }

                // Procesar foto de perfil LadoB (premium)
                if (fotoPerfilLadoB != null && fotoPerfilLadoB.Length > 0)
                {
                    var fileName = await GuardarArchivo(fotoPerfilLadoB, "perfiles-ladob");
                    if (fileName != null) usuario.FotoPerfilLadoB = fileName;
                }

                // Procesar foto de portada LadoA
                if (fotoPortada != null && fotoPortada.Length > 0)
                {
                    var fileName = await GuardarArchivo(fotoPortada, "portadas");
                    if (fileName != null) usuario.FotoPortada = fileName;
                }

                // Procesar foto de portada LadoB (premium)
                if (fotoPortadaLadoB != null && fotoPortadaLadoB.Length > 0)
                {
                    var fileName = await GuardarArchivo(fotoPortadaLadoB, "portadas-ladob");
                    if (fileName != null) usuario.FotoPortadaLadoB = fileName;
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }

            var result = await _userManager.UpdateAsync(usuario);

            if (result.Succeeded)
            {
                // ⭐ LADOCOINS: Verificar y entregar bono de perfil completo
                if (!usuario.BonoPerfilCompletoEntregado && usuario.PerfilCompletoParaBono())
                {
                    try
                    {
                        var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                            usuario.Id,
                            TipoTransaccionLadoCoin.BonoCompletarPerfil,
                            "Bono por completar tu perfil en LADO"
                        );

                        if (bonoEntregado)
                        {
                            usuario.BonoPerfilCompletoEntregado = true;
                            await _userManager.UpdateAsync(usuario);
                            _logger.LogInformation("⭐ Bono de perfil completo entregado a: {UserId}", usuario.Id);
                            TempData["Success"] = "¡Perfil actualizado! Además, ganaste LadoCoins por completar tu perfil.";
                            return RedirectToAction(nameof(Perfil));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al entregar bono de perfil completo a: {UserId}", usuario.Id);
                    }
                }

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
            string mensajeExito;

            if (lado == "Portada")
            {
                fotoPath = usuario.FotoPortada;
                usuario.FotoPortada = null;
                mensajeExito = "Foto de portada eliminada";
            }
            else if (lado == "PortadaB")
            {
                fotoPath = usuario.FotoPortadaLadoB;
                usuario.FotoPortadaLadoB = null;
                mensajeExito = "Foto de portada LadoB eliminada";
            }
            else if (lado == "B")
            {
                fotoPath = usuario.FotoPerfilLadoB;
                usuario.FotoPerfilLadoB = null;
                mensajeExito = "Foto de perfil LadoB eliminada";
            }
            else
            {
                fotoPath = usuario.FotoPerfil;
                usuario.FotoPerfil = null;
                mensajeExito = "Foto de perfil LadoA eliminada";
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

            TempData["Success"] = mensajeExito;
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

            try
            {
                // Interacciones
                usuario.EmailNuevosMensajes = form.ContainsKey("EmailNuevosMensajes");
                usuario.EmailComentarios = form.ContainsKey("EmailComentarios");
                usuario.EmailMenciones = form.ContainsKey("EmailMenciones");

                // Seguidores y suscripciones
                usuario.EmailNuevosSeguidores = form.ContainsKey("EmailNuevosSeguidores");
                usuario.EmailNuevasSuscripciones = form.ContainsKey("EmailNuevasSuscripciones");
                usuario.EmailPropinas = form.ContainsKey("EmailPropinas");

                // Contenido
                usuario.EmailNuevoContenido = form.ContainsKey("EmailNuevoContenido");
                usuario.EmailStories = form.ContainsKey("EmailStories");

                // Resúmenes
                usuario.EmailResumenSemanal = form.ContainsKey("EmailResumenSemanal");
                usuario.EmailReporteGanancias = form.ContainsKey("EmailReporteGanancias");

                // Consejos
                usuario.EmailConsejos = form.ContainsKey("EmailConsejos");

                await _userManager.UpdateAsync(usuario);
                TempData["Success"] = "Preferencias de notificaciones actualizadas correctamente";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar notificaciones para usuario {UserId}", usuario.Id);
                TempData["Error"] = "Error al actualizar las preferencias de notificaciones";
            }

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

            try
            {
                // Perfil privado (requiere aprobación de seguidores)
                usuario.PerfilPrivado = form.ContainsKey("PerfilPrivado");

                // Mostrar en búsquedas
                usuario.MostrarEnBusquedas = form.ContainsKey("MostrarBusquedas");

                // Quién puede enviar mensajes
                if (form.TryGetValue("QuienPuedeMensajear", out var mensajesValue) &&
                    Enum.TryParse<PermisoPrivacidad>(mensajesValue, out var permisoMensajes))
                {
                    usuario.QuienPuedeMensajear = permisoMensajes;
                }
                else
                {
                    // Compatibilidad con checkbox antiguo "MensajesDesconocidos"
                    usuario.QuienPuedeMensajear = form.ContainsKey("MensajesDesconocidos")
                        ? PermisoPrivacidad.Todos
                        : PermisoPrivacidad.Seguidores;
                }

                // Quién puede comentar
                if (form.TryGetValue("QuienPuedeComentar", out var comentarValue) &&
                    Enum.TryParse<PermisoPrivacidad>(comentarValue, out var permisoComentarios))
                {
                    usuario.QuienPuedeComentar = permisoComentarios;
                }

                // Mostrar seguidores
                usuario.MostrarSeguidores = form.ContainsKey("MostrarSeguidores");

                // Mostrar siguiendo
                usuario.MostrarSiguiendo = form.ContainsKey("MostrarSiguiendo");

                // Permitir etiquetas
                usuario.PermitirEtiquetas = form.ContainsKey("PermitirEtiquetas");

                // Detectar ubicación automáticamente (existente)
                usuario.DetectarUbicacionAutomaticamente = form.ContainsKey("DetectarUbicacionAutomaticamente");

                // Mostrar estado en línea (existente)
                usuario.MostrarEstadoEnLinea = form.ContainsKey("MostrarEstadoEnLinea");

                // Ocultar identidad LadoA (existente, solo para creadores LadoB)
                if (usuario.EsCreador && !string.IsNullOrEmpty(usuario.Seudonimo))
                {
                    usuario.OcultarIdentidadLadoA = form.ContainsKey("OcultarIdentidadLadoA");
                }

                await _userManager.UpdateAsync(usuario);
                TempData["Success"] = "Configuración de privacidad actualizada correctamente";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar privacidad para usuario {UserId}", usuario.Id);
                TempData["Error"] = "Error al actualizar la configuración de privacidad";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        // POST: /Usuario/CambiarIdioma
        [HttpPost]
        [ValidateAntiForgeryToken]
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

        // POST: /Usuario/CambiarZonaHoraria
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarZonaHoraria([FromBody] CambiarZonaHorariaRequest request)
        {
            // Validar zona horaria
            var zonasValidas = new[]
            {
                "America/Santiago", "America/Bogota", "America/Lima", "America/Mexico_City",
                "America/Argentina/Buenos_Aires", "America/Caracas", "America/New_York",
                "America/Los_Angeles", "America/Sao_Paulo", "Europe/Madrid", "Europe/London", "Europe/Paris"
            };

            if (string.IsNullOrEmpty(request.ZonaHoraria) || !zonasValidas.Contains(request.ZonaHoraria))
            {
                return Json(new { success = false, message = "Zona horaria no válida" });
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            usuario.ZonaHoraria = request.ZonaHoraria;
            await _userManager.UpdateAsync(usuario);

            _logger.LogInformation("Zona horaria cambiada: Usuario={UserId}, Zona={ZonaHoraria}",
                usuario.Id, request.ZonaHoraria);

            return Json(new { success = true, message = "Zona horaria actualizada" });
        }

        // POST: /Usuario/ActualizarDeteccionUbicacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarDeteccionUbicacion([FromBody] ActualizarDeteccionUbicacionRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            usuario.DetectarUbicacionAutomaticamente = request.Habilitado;
            await _userManager.UpdateAsync(usuario);

            return Json(new { success = true, message = request.Habilitado ? "Detección de ubicación habilitada" : "Detección de ubicación deshabilitada" });
        }

        // POST: /Usuario/ActualizarEstadoEnLinea
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarEstadoEnLinea([FromBody] ActualizarEstadoEnLineaRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            usuario.MostrarEstadoEnLinea = request.Mostrar;
            await _userManager.UpdateAsync(usuario);

            return Json(new { success = true, message = request.Mostrar ? "Estado en línea visible" : "Estado en línea oculto" });
        }

        // POST: /Usuario/ActualizarOcultarIdentidad
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarOcultarIdentidad([FromBody] ActualizarOcultarIdentidadRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            // Solo permitir para creadores con seudónimo
            if (!usuario.EsCreador || string.IsNullOrEmpty(usuario.Seudonimo))
            {
                return Json(new { success = false, message = "Esta opción solo está disponible para creadores con LadoB" });
            }

            usuario.OcultarIdentidadLadoA = request.Ocultar;
            await _userManager.UpdateAsync(usuario);

            return Json(new { success = true, message = request.Ocultar ? "Tu identidad LadoA ahora está oculta" : "Tu identidad LadoA ahora es pública" });
        }

        // POST: /Usuario/CambiarLadoPreferido
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarLadoPreferido([FromBody] CambiarLadoPreferidoRequest request)
        {
            // Validar lado
            if (request == null || request.LadoPreferido < 0 || request.LadoPreferido > 1)
            {
                return Json(new { success = false, message = "Lado no válido", debug = $"request null: {request == null}" });
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            // Guardar el lado preferido
            usuario.LadoPreferido = (TipoLado)request.LadoPreferido;
            var result = await _userManager.UpdateAsync(usuario);

            if (!result.Succeeded)
            {
                var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"Error al guardar: {errores}" });
            }

            var nombreLado = request.LadoPreferido == 0 ? "Lado A" : "Lado B";

            return Json(new {
                success = true,
                message = $"Lado preferido cambiado a {nombreLado}"
            });
        }

        // POST: /Usuario/CambiarBloqueoLadoB
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarBloqueoLadoB([FromBody] CambiarBloqueoLadoBRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Datos inválidos" });
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            usuario.BloquearLadoB = request.Bloquear;
            var result = await _userManager.UpdateAsync(usuario);

            if (!result.Succeeded)
            {
                var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"Error al guardar: {errores}" });
            }

            return Json(new {
                success = true,
                message = request.Bloquear
                    ? "Contenido LadoB bloqueado. No verás contenido para adultos."
                    : "Contenido LadoB desbloqueado."
            });
        }

        // POST: /Usuario/CambiarOcultarFeedPublico
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarOcultarFeedPublico([FromBody] CambiarOcultarFeedPublicoRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Datos inválidos" });
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            usuario.OcultarDeFeedPublico = request.Ocultar;
            var result = await _userManager.UpdateAsync(usuario);

            if (!result.Succeeded)
            {
                var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"Error al guardar: {errores}" });
            }

            return Json(new {
                success = true,
                message = request.Ocultar
                    ? "Tu contenido ya no aparecerá en el Feed Público."
                    : "Tu contenido ahora será visible en el Feed Público."
            });
        }

        // POST: /Usuario/CambiarMostrarLinkInBio
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarMostrarLinkInBio([FromBody] CambiarMostrarLinkInBioRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Datos inválidos" });
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            usuario.MostrarLinkInBio = request.Mostrar;
            var result = await _userManager.UpdateAsync(usuario);

            if (!result.Succeeded)
            {
                var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"Error al guardar: {errores}" });
            }

            return Json(new {
                success = true,
                message = request.Mostrar
                    ? "Tu Link in Bio ahora es visible en tu perfil."
                    : "Tu Link in Bio ya no aparecerá en tu perfil."
            });
        }

        // POST: /Usuario/CambiarPromocionLadoB
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarPromocionLadoB([FromBody] CambiarPromocionLadoBRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Tipo))
            {
                return Json(new { success = false, message = "Datos inválidos" });
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            // Solo creadores con LadoB pueden usar esta función
            if (!usuario.EsCreador || !usuario.TieneLadoB())
            {
                return Json(new { success = false, message = "Solo disponible para creadores con LadoB" });
            }

            string mensaje = "";

            if (request.Tipo == "teaser")
            {
                usuario.MostrarTeaserLadoB = request.Activo;
                mensaje = request.Activo ? "Teaser activado en tu perfil" : "Teaser desactivado";
            }
            else if (request.Tipo == "blur")
            {
                usuario.PermitirPreviewBlurLadoB = request.Activo;
                mensaje = request.Activo ? "Previews blur activados" : "Previews blur desactivados";
            }
            else
            {
                return Json(new { success = false, message = "Tipo no válido" });
            }

            var result = await _userManager.UpdateAsync(usuario);

            if (!result.Succeeded)
            {
                return Json(new { success = false, message = "Error al guardar" });
            }

            return Json(new { success = true, message = mensaje });
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

            var userId = usuario.Id;
            var userName = usuario.UserName;

            // Verificar confirmación (case insensitive)
            if (string.IsNullOrEmpty(confirmacion) || !confirmacion.Equals("ELIMINAR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = $"Debes escribir 'ELIMINAR' para confirmar. Recibido: '{confirmacion}'";
                return RedirectToAction(nameof(Configuracion));
            }

            try
            {
                // Usar una transacción para asegurar consistencia
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Batch 1: Notificaciones, Likes, Comentarios
                var notificaciones = _context.Notificaciones.Where(n => n.UsuarioId == usuario.Id);
                _context.Notificaciones.RemoveRange(notificaciones);
                var likes = _context.Likes.Where(l => l.UsuarioId == usuario.Id);
                _context.Likes.RemoveRange(likes);
                var likesEnMiContenido = _context.Likes.Where(l => l.Contenido.UsuarioId == usuario.Id);
                _context.Likes.RemoveRange(likesEnMiContenido);
                var comentarios = _context.Comentarios.Where(c => c.UsuarioId == usuario.Id);
                _context.Comentarios.RemoveRange(comentarios);
                var comentariosEnMiContenido = _context.Comentarios.Where(c => c.Contenido.UsuarioId == usuario.Id);
                _context.Comentarios.RemoveRange(comentariosEnMiContenido);
                await _context.SaveChangesAsync();

                // Batch 2: Stories y vistas
                var storyVistas = _context.StoryVistas.Where(sv => sv.UsuarioId == usuario.Id);
                _context.StoryVistas.RemoveRange(storyVistas);
                var vistasEnMisStories = _context.StoryVistas.Where(sv => sv.Story.CreadorId == usuario.Id);
                _context.StoryVistas.RemoveRange(vistasEnMisStories);
                var stories = _context.Stories.Where(s => s.CreadorId == usuario.Id);
                _context.Stories.RemoveRange(stories);
                await _context.SaveChangesAsync();

                // Batch 3: Reacciones y colecciones
                var reacciones = _context.Reacciones.Where(r => r.UsuarioId == usuario.Id);
                _context.Reacciones.RemoveRange(reacciones);
                var reaccionesEnMiContenido = _context.Reacciones.Where(r => r.Contenido.UsuarioId == usuario.Id);
                _context.Reacciones.RemoveRange(reaccionesEnMiContenido);
                var contenidoColecciones = _context.ContenidoColecciones
                    .Where(cc => cc.Coleccion.CreadorId == usuario.Id || cc.Contenido.UsuarioId == usuario.Id);
                _context.ContenidoColecciones.RemoveRange(contenidoColecciones);
                var colecciones = _context.Colecciones.Where(c => c.CreadorId == usuario.Id);
                _context.Colecciones.RemoveRange(colecciones);
                await _context.SaveChangesAsync();

                // Batch 4: Tips y desafios
                var tips = _context.Tips.Where(t => t.FanId == usuario.Id || t.CreadorId == usuario.Id);
                _context.Tips.RemoveRange(tips);
                var desafios = _context.Desafios.Where(d => d.FanId == usuario.Id);
                _context.Desafios.RemoveRange(desafios);
                var propuestasDesafios = _context.PropuestasDesafios.Where(p => p.CreadorId == usuario.Id);
                _context.PropuestasDesafios.RemoveRange(propuestasDesafios);
                await _context.SaveChangesAsync();

                // Batch 5: Suscripciones y mensajes
                var suscripcionesFan = _context.Suscripciones.Where(s => s.FanId == usuario.Id);
                _context.Suscripciones.RemoveRange(suscripcionesFan);
                var suscripcionesCreador = _context.Suscripciones.Where(s => s.CreadorId == usuario.Id);
                _context.Suscripciones.RemoveRange(suscripcionesCreador);
                var mensajes = _context.MensajesPrivados
                    .Where(m => m.RemitenteId == usuario.Id || m.DestinatarioId == usuario.Id);
                _context.MensajesPrivados.RemoveRange(mensajes);
                var chatMensajes = _context.ChatMensajes
                    .Where(m => m.RemitenteId == usuario.Id || m.DestinatarioId == usuario.Id);
                _context.ChatMensajes.RemoveRange(chatMensajes);
                await _context.SaveChangesAsync();

                // Batch 6: Transacciones, reportes, bloqueos
                var transacciones = _context.Transacciones.Where(t => t.UsuarioId == usuario.Id);
                _context.Transacciones.RemoveRange(transacciones);
                var reportes = _context.Reportes
                    .Where(r => r.UsuarioReportadorId == usuario.Id || r.UsuarioReportadoId == usuario.Id);
                _context.Reportes.RemoveRange(reportes);
                var bloqueos = _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == usuario.Id || b.BloqueadoId == usuario.Id);
                _context.BloqueosUsuarios.RemoveRange(bloqueos);
                await _context.SaveChangesAsync();

                // Batch 7: Intereses, preferencias, favoritos, interacciones
                var intereses = _context.InteresesUsuarios.Where(i => i.UsuarioId == usuario.Id);
                _context.InteresesUsuarios.RemoveRange(intereses);
                var preferencias = _context.PreferenciasAlgoritmoUsuario.Where(p => p.UsuarioId == usuario.Id);
                _context.PreferenciasAlgoritmoUsuario.RemoveRange(preferencias);
                var favoritos = _context.Favoritos.Where(f => f.UsuarioId == usuario.Id);
                _context.Favoritos.RemoveRange(favoritos);
                var interacciones = _context.InteraccionesContenidos.Where(i => i.UsuarioId == usuario.Id);
                _context.InteraccionesContenidos.RemoveRange(interacciones);
                var interaccionesEnMiContenido = _context.InteraccionesContenidos.Where(i => i.Contenido.UsuarioId == usuario.Id);
                _context.InteraccionesContenidos.RemoveRange(interaccionesEnMiContenido);
                await _context.SaveChangesAsync();

                // Batch 8: Compras, tokens
                var comprasContenido = _context.ComprasContenido.Where(c => c.UsuarioId == usuario.Id);
                _context.ComprasContenido.RemoveRange(comprasContenido);
                var comprasColeccion = _context.ComprasColeccion.Where(c => c.CompradorId == usuario.Id);
                _context.ComprasColeccion.RemoveRange(comprasColeccion);
                var refreshTokens = _context.RefreshTokens.Where(r => r.UserId == usuario.Id);
                _context.RefreshTokens.RemoveRange(refreshTokens);
                var activeTokens = _context.ActiveTokens.Where(a => a.UserId == usuario.Id);
                _context.ActiveTokens.RemoveRange(activeTokens);
                await _context.SaveChangesAsync();

                // Batch 8.5: Impresiones y clics de anuncios
                var impresionesAnuncios = _context.ImpresionesAnuncios.Where(i => i.UsuarioId == usuario.Id);
                _context.ImpresionesAnuncios.RemoveRange(impresionesAnuncios);
                var clicsAnuncios = _context.ClicsAnuncios.Where(c => c.UsuarioId == usuario.Id);
                _context.ClicsAnuncios.RemoveRange(clicsAnuncios);
                await _context.SaveChangesAsync();

                // Batch 8.6: Agencia y anuncios
                var agencia = _context.Agencias.FirstOrDefault(a => a.UsuarioId == usuario.Id);
                if (agencia != null)
                {
                    // Eliminar clics e impresiones de los anuncios de la agencia
                    var anunciosIds = _context.Anuncios.Where(a => a.AgenciaId == agencia.Id).Select(a => a.Id).ToList();
                    var impresionesAgencia = _context.ImpresionesAnuncios.Where(i => anunciosIds.Contains(i.AnuncioId));
                    _context.ImpresionesAnuncios.RemoveRange(impresionesAgencia);
                    var clicsAgencia = _context.ClicsAnuncios.Where(c => anunciosIds.Contains(c.AnuncioId));
                    _context.ClicsAnuncios.RemoveRange(clicsAgencia);
                    var segmentaciones = _context.SegmentacionesAnuncios.Where(s => anunciosIds.Contains(s.AnuncioId));
                    _context.SegmentacionesAnuncios.RemoveRange(segmentaciones);
                    var anuncios = _context.Anuncios.Where(a => a.AgenciaId == agencia.Id);
                    _context.Anuncios.RemoveRange(anuncios);
                    var transaccionesAgencia = _context.TransaccionesAgencias.Where(t => t.AgenciaId == agencia.Id);
                    _context.TransaccionesAgencias.RemoveRange(transaccionesAgencia);
                    _context.Agencias.Remove(agencia);
                }
                await _context.SaveChangesAsync();

                // Batch 9: Contenidos del usuario
                var contenidos = _context.Contenidos.Where(c => c.UsuarioId == usuario.Id).ToList();
                foreach (var contenido in contenidos)
                {
                    if (!string.IsNullOrEmpty(contenido.RutaArchivo))
                        EliminarArchivoFisico(contenido.RutaArchivo);
                    if (!string.IsNullOrEmpty(contenido.Thumbnail))
                        EliminarArchivoFisico(contenido.Thumbnail);
                }
                _context.Contenidos.RemoveRange(contenidos);

                // Batch 10: Archivos físicos del perfil
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
                if (!string.IsNullOrEmpty(usuario.FotoPortadaLadoB))
                {
                    EliminarArchivoFisico(usuario.FotoPortadaLadoB);
                }

                // 23. Guardar todos los cambios
                await _context.SaveChangesAsync();

                // 24. Eliminar usuario de Identity
                var result = await _userManager.DeleteAsync(usuario);

                if (result.Succeeded)
                {
                    // Confirmar la transaccion
                    await transaction.CommitAsync();

                    // Cerrar sesion
                    await _signInManager.SignOutAsync();

                    TempData["Success"] = "Tu cuenta ha sido eliminada permanentemente";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    // Revertir la transaccion si falla
                    await transaction.RollbackAsync();

                    var errores = string.Join(", ", result.Errors.Select(e => e.Description));

                    TempData["Error"] = "Error al eliminar la cuenta: " + errores;
                    return RedirectToAction(nameof(Configuracion));
                }
            }
            catch (Exception ex)
            {
                var errorMsg = ex.Message + (ex.InnerException != null ? " - " + ex.InnerException.Message : "");

                TempData["Error"] = "Error al eliminar la cuenta: " + errorMsg;
                return RedirectToAction(nameof(Configuracion));
            }
        }

        // Método auxiliar para guardar archivos con validación de magic bytes
        private async Task<string?> GuardarArchivo(IFormFile archivo, string carpeta)
        {
            if (archivo == null || archivo.Length == 0)
                return null;

            // ✅ SEGURIDAD: Validar archivo con magic bytes (no solo extensión)
            var validacionArchivo = await _fileValidationService.ValidarImagenAsync(archivo);
            if (!validacionArchivo.EsValido)
            {
                _logger.LogWarning("⚠️ Archivo de perfil rechazado: {FileName}, Error: {Error}",
                    archivo.FileName, validacionArchivo.MensajeError);
                throw new InvalidOperationException(validacionArchivo.MensajeError ?? "Tipo de archivo no válido");
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", carpeta);
            Directory.CreateDirectory(uploadsFolder);

            // Usar extensión validada
            var extension = validacionArchivo.Extension ?? Path.GetExtension(archivo.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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
            // Limitar cantidad para prevenir DoS
            cantidad = Math.Clamp(cantidad, 1, 50);
            pagina = Math.Max(1, pagina);

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarNotificacionLeida([FromForm] MarcarNotificacionRequest request)
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarNotificacion([FromForm] MarcarNotificacionRequest request)
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
        /// API: Eliminar TODAS las notificaciones del usuario
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarTodasNotificaciones()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            // Desactivar todas las notificaciones del usuario
            var notificaciones = await _context.Notificaciones
                .Where(n => n.UsuarioId == usuario.Id && n.EstaActiva)
                .ToListAsync();

            var totalEliminadas = notificaciones.Count;

            foreach (var notificacion in notificaciones)
            {
                notificacion.EstaActiva = false;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, eliminadas = totalEliminadas });
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

        // ========================================
        // ONBOARDING - SELECCION DE INTERESES
        // ========================================

        /// <summary>
        /// Vista de onboarding para seleccionar intereses
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SeleccionarIntereses()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return RedirectToAction("Login", "Account");

            // Obtener categorias principales (sin subcategorias en la vista principal)
            var categorias = await _context.CategoriasIntereses
                .Where(c => c.EstaActiva && c.CategoriaPadreId == null)
                .OrderBy(c => c.Orden)
                .ToListAsync();

            // Obtener intereses ya seleccionados por el usuario
            var interesesUsuario = await _context.InteresesUsuarios
                .Where(i => i.UsuarioId == usuario.Id)
                .Select(i => i.CategoriaInteresId)
                .ToListAsync();

            ViewBag.InteresesSeleccionados = interesesUsuario;
            ViewBag.EsNuevoUsuario = (DateTime.Now - usuario.FechaRegistro).TotalMinutes < 30;

            return View(categorias);
        }

        /// <summary>
        /// Guardar intereses seleccionados en onboarding
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarIntereses([FromBody] GuardarInteresesRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
                return Json(new { success = false, message = "No autenticado" });

            if (request.CategoriaIds == null || !request.CategoriaIds.Any())
            {
                return Json(new { success = false, message = "Selecciona al menos un interes" });
            }

            try
            {
                // Eliminar intereses explicitos anteriores
                var interesesAnteriores = await _context.InteresesUsuarios
                    .Where(i => i.UsuarioId == usuario.Id && i.Tipo == TipoInteres.Explicito)
                    .ToListAsync();

                _context.InteresesUsuarios.RemoveRange(interesesAnteriores);
                await _context.SaveChangesAsync();

                // Agregar nuevos intereses
                foreach (var categoriaId in request.CategoriaIds.Distinct())
                {
                    var categoriaExiste = await _context.CategoriasIntereses
                        .AnyAsync(c => c.Id == categoriaId && c.EstaActiva);

                    if (categoriaExiste)
                    {
                        await _interesesService.AgregarInteresExplicitoAsync(usuario.Id, categoriaId);
                    }
                }

                return Json(new { success = true, message = "Intereses guardados correctamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar: " + ex.Message });
            }
        }

        /// <summary>
        /// Omitir seleccion de intereses en onboarding
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult OmitirIntereses()
        {
            return Json(new { success = true, redirectUrl = "/Feed" });
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

    public class CambiarZonaHorariaRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("zonaHoraria")]
        public string ZonaHoraria { get; set; } = "America/Bogota";
    }

    public class ActualizarDeteccionUbicacionRequest
    {
        public bool Habilitado { get; set; } = false;
    }

    public class ActualizarEstadoEnLineaRequest
    {
        public bool Mostrar { get; set; } = true;
    }

    public class ActualizarOcultarIdentidadRequest
    {
        public bool Ocultar { get; set; } = false;
    }

    public class CambiarLadoPreferidoRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("ladoPreferido")]
        public int LadoPreferido { get; set; } = 0;
    }

    public class CambiarBloqueoLadoBRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("bloquear")]
        public bool Bloquear { get; set; } = false;
    }

    public class CambiarOcultarFeedPublicoRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("ocultar")]
        public bool Ocultar { get; set; } = false;
    }

    public class CambiarMostrarLinkInBioRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("mostrar")]
        public bool Mostrar { get; set; } = true;
    }

    public class CambiarPromocionLadoBRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("tipo")]
        public string Tipo { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("activo")]
        public bool Activo { get; set; } = false;
    }

    public class GuardarInteresesRequest
    {
        public List<int> CategoriaIds { get; set; } = new();
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