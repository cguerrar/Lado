using ClosedXML.Excel;
using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Lado.Controllers
{
    [Authorize(Roles = "Admin")]
    public partial class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IServerMetricsService _serverMetrics;
        private readonly IConfiguration _configuration;
        private readonly IVisitasService _visitasService;
        private readonly IClaudeClassificationService _claudeService;
        private readonly IEmailService _emailService;
        private readonly ILogEventoService _logEventoService;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IRateLimitService _rateLimitService;
        private readonly IBulkEmailService _bulkEmailService;
        private readonly IMediaIntegrityService _mediaIntegrity;
        private readonly IMediaConversionService _mediaConversion;
        private readonly ILogger<AdminController> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly IMemoryCache _cache;
        private readonly ISeoConfigService _seoConfigService;
        private readonly IFileValidationService _fileValidationService;

        // Lista de IDs de contenidos que fallaron durante la clasificación por lotes
        // Se limpia cuando se inicia una nueva reclasificación masiva
        private static HashSet<int> _contenidosFallidosClasificacion = new();
        private static readonly object _lockFallidos = new();

        // Estado de migración de medios
        private static bool _migracionEnProgreso = false;
        private static int _migracionTotal = 0;
        private static int _migracionProcesados = 0;
        private static int _migracionExitosos = 0;
        private static int _migracionFallidos = 0;
        private static string _migracionMensaje = "";

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IServerMetricsService serverMetrics,
            IConfiguration configuration,
            IVisitasService visitasService,
            IClaudeClassificationService claudeService,
            IEmailService emailService,
            ILogEventoService logEventoService,
            IWebHostEnvironment hostEnvironment,
            IRateLimitService rateLimitService,
            IBulkEmailService bulkEmailService,
            IMediaIntegrityService mediaIntegrity,
            IMediaConversionService mediaConversion,
            ILogger<AdminController> logger,
            IServiceScopeFactory serviceScopeFactory,
            ILadoCoinsService ladoCoinsService,
            IMemoryCache cache,
            ISeoConfigService seoConfigService,
            IFileValidationService fileValidationService)
        {
            _context = context;
            _userManager = userManager;
            _serverMetrics = serverMetrics;
            _configuration = configuration;
            _visitasService = visitasService;
            _claudeService = claudeService;
            _emailService = emailService;
            _logEventoService = logEventoService;
            _hostEnvironment = hostEnvironment;
            _rateLimitService = rateLimitService;
            _bulkEmailService = bulkEmailService;
            _mediaIntegrity = mediaIntegrity;
            _mediaConversion = mediaConversion;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _ladoCoinsService = ladoCoinsService;
            _cache = cache;
            _seoConfigService = seoConfigService;
            _fileValidationService = fileValidationService;
        }

        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            ViewBag.ReportesPendientes = _context.Reportes.Count(r => r.Estado == "Pendiente");
        }

        public async Task<IActionResult> Index()
        {
            // ═══════════════════════════════════════════════════════════
            // ESTADÍSTICAS DE USUARIOS
            // ═══════════════════════════════════════════════════════════
            ViewBag.TotalUsuarios = await _context.Users.CountAsync();
            ViewBag.TotalCreadores = await _context.Users.CountAsync(u => u.EsCreador);
            ViewBag.TotalFans = await _context.Users.CountAsync(u => !u.EsCreador);
            ViewBag.CreadoresVerificados = await _context.Users.CountAsync(u => u.EsCreador && u.CreadorVerificado);
            ViewBag.UsuariosBloqueados = await _context.Users.CountAsync(u => !u.EstaActivo);
            ViewBag.UsuariosHoy = await _context.Users.CountAsync(u => u.FechaRegistro.Date == DateTime.Today);

            // ═══════════════════════════════════════════════════════════
            // ESTADÍSTICAS DE CONTENIDO
            // ═══════════════════════════════════════════════════════════
            ViewBag.TotalPublicaciones = await _context.Contenidos.CountAsync();
            ViewBag.PublicacionesHoy = await _context.Contenidos
                .CountAsync(c => c.FechaPublicacion.Date == DateTime.Today);
            ViewBag.ContenidoCensurado = await _context.Contenidos.CountAsync(c => c.Censurado);
            ViewBag.TotalArchivos = await _context.ArchivosContenido.CountAsync();

            // ═══════════════════════════════════════════════════════════
            // ESTADÍSTICAS DE SUSCRIPCIONES
            // ═══════════════════════════════════════════════════════════
            ViewBag.TotalSuscripciones = await _context.Suscripciones.CountAsync(s => s.EstaActiva);
            ViewBag.SuscripcionesHoy = await _context.Suscripciones
                .CountAsync(s => s.FechaInicio.Date == DateTime.Today);

            // ═══════════════════════════════════════════════════════════
            // ESTADÍSTICAS DE MODERACIÓN Y SUPERVISORES
            // ═══════════════════════════════════════════════════════════
            ViewBag.ColaPendiente = await _context.ColaModeracion
                .CountAsync(c => c.Estado == Models.Moderacion.EstadoModeracion.Pendiente);
            ViewBag.ColaEnRevision = await _context.ColaModeracion
                .CountAsync(c => c.Estado == Models.Moderacion.EstadoModeracion.EnRevision);
            ViewBag.ColaEscalada = await _context.ColaModeracion
                .CountAsync(c => c.Estado == Models.Moderacion.EstadoModeracion.Escalado);
            ViewBag.RevisadosHoy = await _context.ColaModeracion
                .CountAsync(c => c.FechaResolucion.HasValue && c.FechaResolucion.Value.Date == DateTime.Today);
            ViewBag.SupervisoresActivos = await _context.UsuariosSupervisor
                .CountAsync(s => s.EstaActivo && s.EstaDisponible);
            ViewBag.TotalSupervisores = await _context.UsuariosSupervisor
                .CountAsync(s => s.EstaActivo);

            // ═══════════════════════════════════════════════════════════
            // ESTADÍSTICAS DE VISITAS
            // ═══════════════════════════════════════════════════════════
            ViewBag.TotalVisitas = await _visitasService.ObtenerTotalVisitasAsync();
            ViewBag.VisitasHoy = await _visitasService.ObtenerVisitasHoyAsync();
            ViewBag.VisitantesUnicosHoy = await _visitasService.ObtenerVisitantesUnicosHoyAsync();
            ViewBag.VisitasUltimos7Dias = await _visitasService.ObtenerVisitasUltimos7DiasAsync();

            // ═══════════════════════════════════════════════════════════
            // ESTADÍSTICAS FINANCIERAS
            // ═══════════════════════════════════════════════════════════
            var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            ViewBag.IngresosMes = await _context.Transacciones
                .Where(t => t.FechaTransaccion >= inicioMes && t.EstadoTransaccion == Models.EstadoTransaccion.Completada)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;
            ViewBag.RetirosPendientes = await _context.Transacciones
                .CountAsync(t => t.TipoTransaccion == Models.TipoTransaccion.Retiro && t.EstadoTransaccion == Models.EstadoTransaccion.Pendiente);

            // ═══════════════════════════════════════════════════════════
            // VERIFICACIONES PENDIENTES
            // ═══════════════════════════════════════════════════════════
            ViewBag.VerificacionesPendientes = await _context.CreatorVerificationRequests
                .CountAsync(v => v.Estado == "Pendiente");

            return View();
        }

        // ========================================
        // USUARIOS
        // ========================================
        public async Task<IActionResult> Usuarios()
        {
            // OPTIMIZADO: Evitar N+1 usando una sola consulta con join
            var usuariosConRoles = await _context.Users
                .OrderByDescending(u => u.FechaRegistro)
                .Select(u => new
                {
                    Usuario = u,
                    Rol = _context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                        .FirstOrDefault() ?? "Sin Rol"
                })
                .ToListAsync();

            var resultado = usuariosConRoles
                .Select(x => (x.Usuario, x.Rol))
                .ToList();

            ViewBag.VerificacionesPendientes = await _context.CreatorVerificationRequests
                .Include(v => v.User)
                .Where(v => v.Estado == "Pendiente")
                .OrderByDescending(v => v.FechaSolicitud)
                .ToListAsync();

            return View(resultado);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BloquearUsuario(string id)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario != null)
            {
                usuario.EstaActivo = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Usuario {usuario.NombreCompleto} bloqueado exitosamente.";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesbloquearUsuario(string id)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario != null)
            {
                usuario.EstaActivo = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Usuario {usuario.NombreCompleto} desbloqueado exitosamente.";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearUsuario(
            string nombreCompleto,
            string userName,
            string email,
            string password,
            string rol,
            bool esCreador = true,
            bool emailConfirmado = true,
            bool creadorVerificado = false,
            bool enviarEmailBienvenida = false)
        {
            try
            {
                // Validaciones basicas
                if (string.IsNullOrWhiteSpace(nombreCompleto) ||
                    string.IsNullOrWhiteSpace(userName) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(password))
                {
                    TempData["Error"] = "Todos los campos obligatorios deben estar completos.";
                    return RedirectToAction(nameof(Usuarios));
                }

                // Verificar que el email no exista
                var existeEmail = await _userManager.FindByEmailAsync(email);
                if (existeEmail != null)
                {
                    TempData["Error"] = $"Ya existe un usuario con el email {email}.";
                    return RedirectToAction(nameof(Usuarios));
                }

                // Verificar que el username no exista
                var existeUserName = await _userManager.FindByNameAsync(userName);
                if (existeUserName != null)
                {
                    TempData["Error"] = $"Ya existe un usuario con el nombre de usuario {userName}.";
                    return RedirectToAction(nameof(Usuarios));
                }

                // Crear el usuario
                var usuario = new ApplicationUser
                {
                    UserName = userName,
                    Email = email,
                    NombreCompleto = nombreCompleto,
                    EmailConfirmed = emailConfirmado,
                    FechaRegistro = DateTime.UtcNow,
                    EstaActivo = true,
                    Saldo = 0,
                    ComisionRetiro = 20, // Comision por defecto
                    MontoMinimoRetiro = 50, // Minimo por defecto
                    UsarRetencionPais = true
                };

                // Si es rol Creador o Admin con esCreador=true, marcar como creador
                if (rol == "Creador" || (rol == "Admin" && esCreador))
                {
                    usuario.EsCreador = true;
                    usuario.CreadorVerificado = creadorVerificado;
                    usuario.PrecioSuscripcion = 5.99m; // Precio por defecto
                }

                var result = await _userManager.CreateAsync(usuario, password);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    TempData["Error"] = $"Error al crear usuario: {errors}";
                    return RedirectToAction(nameof(Usuarios));
                }

                // Asignar rol
                string rolAsignar = rol switch
                {
                    "Admin" => "Admin",
                    "Creador" => "Creador",
                    _ => "Usuario"
                };

                // Verificar que el rol existe, si no crearlo
                var roleManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
                if (!await roleManager.RoleExistsAsync(rolAsignar))
                {
                    await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(rolAsignar));
                }

                await _userManager.AddToRoleAsync(usuario, rolAsignar);

                // Enviar email de bienvenida si se solicito
                if (enviarEmailBienvenida)
                {
                    var emailEnviado = await _emailService.SendWelcomeEmailAsync(email, nombreCompleto, userName, password);
                    if (emailEnviado)
                    {
                        TempData["Success"] = $"Usuario {nombreCompleto} (@{userName}) creado exitosamente con rol {rolAsignar}. Email de bienvenida enviado.";
                    }
                    else
                    {
                        TempData["Success"] = $"Usuario {nombreCompleto} (@{userName}) creado exitosamente con rol {rolAsignar}. (Error al enviar email)";
                    }
                }
                else
                {
                    TempData["Success"] = $"Usuario {nombreCompleto} (@{userName}) creado exitosamente con rol {rolAsignar}.";
                }
                return RedirectToAction(nameof(Usuarios));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error inesperado: {ex.Message}";
                return RedirectToAction(nameof(Usuarios));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(string id, string nuevaContrasena)
        {
            if (string.IsNullOrWhiteSpace(nuevaContrasena) || nuevaContrasena.Length < 8)
            {
                TempData["Error"] = "La contraseña debe tener al menos 8 caracteres.";
                return RedirectToAction(nameof(Usuarios));
            }

            var usuario = await _userManager.FindByIdAsync(id);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction(nameof(Usuarios));
            }

            // Verificar que no sea un Admin (solo otro admin puede cambiar la contraseña de un admin)
            var roles = await _userManager.GetRolesAsync(usuario);
            var currentUser = await _userManager.GetUserAsync(User);

            if (roles.Contains("Admin") && currentUser?.Id != usuario.Id)
            {
                // Solo el propio admin puede cambiar su contraseña
                TempData["Error"] = "No puedes cambiar la contraseña de otro administrador.";
                return RedirectToAction(nameof(Usuarios));
            }

            // Eliminar la contraseña actual y establecer la nueva
            var removeResult = await _userManager.RemovePasswordAsync(usuario);
            if (!removeResult.Succeeded)
            {
                TempData["Error"] = "Error al procesar la solicitud.";
                return RedirectToAction(nameof(Usuarios));
            }

            var addResult = await _userManager.AddPasswordAsync(usuario, nuevaContrasena);
            if (addResult.Succeeded)
            {
                TempData["Success"] = $"Contraseña de {usuario.NombreCompleto} actualizada exitosamente.";
            }
            else
            {
                // Mostrar errores de validación
                var errors = string.Join(" ", addResult.Errors.Select(e => e.Description));
                TempData["Error"] = $"Error al cambiar contraseña: {errors}";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarUsuario(string id)
        {
            // Log de inicio
            var adminActual = await _userManager.GetUserAsync(User);
            var adminId = adminActual?.Id;
            var adminNombre = adminActual?.NombreCompleto ?? "Admin";

            await _logEventoService.RegistrarEventoAsync(
                $"[ELIMINAR-INICIO] Solicitud de eliminación recibida para userId: {id}",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                adminId,
                adminNombre,
                $"Método: EliminarUsuario, Id recibido: '{id}', Request Method: {Request.Method}"
            );

            if (string.IsNullOrEmpty(id))
            {
                await _logEventoService.RegistrarEventoAsync(
                    "[ELIMINAR-ERROR] Id de usuario vacío o nulo",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Error,
                    adminId,
                    adminNombre,
                    "El parámetro id llegó vacío"
                );
                TempData["Error"] = "ID de usuario no proporcionado.";
                return RedirectToAction(nameof(Usuarios));
            }

            var usuario = await _context.Users.FindAsync(id);

            if (usuario == null)
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"[ELIMINAR-ERROR] Usuario no encontrado: {id}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Error,
                    adminId,
                    adminNombre,
                    $"No se encontró usuario con Id: {id}"
                );
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction(nameof(Usuarios));
            }

            var usuarioNombre = usuario.NombreCompleto ?? usuario.UserName ?? "Sin nombre";
            var usuarioEmail = usuario.Email ?? "Sin email";

            await _logEventoService.RegistrarEventoAsync(
                $"[ELIMINAR-PROCESO] Iniciando eliminación de: {usuarioNombre}",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                adminId,
                adminNombre,
                $"Usuario a eliminar: {usuarioNombre} ({usuarioEmail}), Id: {id}"
            );

            // Verificar que no sea un Admin
            var roles = await _userManager.GetRolesAsync(usuario);
            if (roles.Contains("Admin"))
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"[ELIMINAR-BLOQUEADO] Intento de eliminar administrador: {usuarioNombre}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Warning,
                    adminId,
                    adminNombre,
                    $"Se intentó eliminar al admin {usuarioNombre}"
                );
                TempData["Error"] = "No se puede eliminar un usuario administrador.";
                return RedirectToAction(nameof(Usuarios));
            }

            try
            {
                // ========================================
                // FASE 1: Eliminar entidades con FK Restrict (orden importante)
                // ========================================

                // 1. Eliminar likes en comentarios del usuario
                var likesComentarios = await _context.LikesComentarios
                    .Where(lc => lc.UsuarioId == id)
                    .ToListAsync();
                if (likesComentarios.Any())
                    _context.LikesComentarios.RemoveRange(likesComentarios);

                // 2. Eliminar reacciones del usuario
                var reacciones = await _context.Reacciones
                    .Where(r => r.UsuarioId == id)
                    .ToListAsync();
                if (reacciones.Any())
                    _context.Reacciones.RemoveRange(reacciones);

                // 3. Eliminar vistas de stories del usuario
                var storyVistas = await _context.StoryVistas
                    .Where(sv => sv.UsuarioId == id)
                    .ToListAsync();
                if (storyVistas.Any())
                    _context.StoryVistas.RemoveRange(storyVistas);

                // 4. Eliminar likes de stories del usuario
                var storyLikes = await _context.StoryLikes
                    .Where(sl => sl.UsuarioId == id)
                    .ToListAsync();
                if (storyLikes.Any())
                    _context.StoryLikes.RemoveRange(storyLikes);

                // 5. Eliminar compras de contenido del usuario
                var comprasContenido = await _context.ComprasContenido
                    .Where(cc => cc.UsuarioId == id)
                    .ToListAsync();
                if (comprasContenido.Any())
                    _context.ComprasContenido.RemoveRange(comprasContenido);

                // 6. Eliminar compras de colecciones del usuario
                var comprasColeccion = await _context.ComprasColeccion
                    .Where(cc => cc.CompradorId == id)
                    .ToListAsync();
                if (comprasColeccion.Any())
                    _context.ComprasColeccion.RemoveRange(comprasColeccion);

                // 7. Eliminar propinas donde es fan o creador
                var tipsFan = await _context.Tips
                    .Where(t => t.FanId == id)
                    .ToListAsync();
                if (tipsFan.Any())
                    _context.Tips.RemoveRange(tipsFan);

                var tipsCreador = await _context.Tips
                    .Where(t => t.CreadorId == id)
                    .ToListAsync();
                if (tipsCreador.Any())
                    _context.Tips.RemoveRange(tipsCreador);

                // 8. Eliminar fotos destacadas del usuario
                var fotosDestacadas = await _context.FotosDestacadas
                    .Where(f => f.UsuarioId == id)
                    .ToListAsync();
                if (fotosDestacadas.Any())
                    _context.FotosDestacadas.RemoveRange(fotosDestacadas);

                // 9. Eliminar mensajes privados (remitente y destinatario)
                var mensajesRemitente = await _context.MensajesPrivados
                    .Where(m => m.RemitenteId == id)
                    .ToListAsync();
                if (mensajesRemitente.Any())
                    _context.MensajesPrivados.RemoveRange(mensajesRemitente);

                var mensajesDestinatario = await _context.MensajesPrivados
                    .Where(m => m.DestinatarioId == id)
                    .ToListAsync();
                if (mensajesDestinatario.Any())
                    _context.MensajesPrivados.RemoveRange(mensajesDestinatario);

                // 10. Eliminar chat mensajes (remitente y destinatario)
                var chatRemitente = await _context.ChatMensajes
                    .Where(m => m.RemitenteId == id)
                    .ToListAsync();
                if (chatRemitente.Any())
                    _context.ChatMensajes.RemoveRange(chatRemitente);

                var chatDestinatario = await _context.ChatMensajes
                    .Where(m => m.DestinatarioId == id)
                    .ToListAsync();
                if (chatDestinatario.Any())
                    _context.ChatMensajes.RemoveRange(chatDestinatario);

                // 11. Eliminar propuestas de desafíos del usuario (como creador)
                var propuestas = await _context.PropuestasDesafios
                    .Where(p => p.CreadorId == id)
                    .ToListAsync();
                if (propuestas.Any())
                    _context.PropuestasDesafios.RemoveRange(propuestas);

                // 12. Eliminar desafíos donde participa (fan, creador objetivo o asignado)
                var desafiosFan = await _context.Desafios
                    .Where(d => d.FanId == id)
                    .ToListAsync();
                if (desafiosFan.Any())
                    _context.Desafios.RemoveRange(desafiosFan);

                var desafiosCreadorObj = await _context.Desafios
                    .Where(d => d.CreadorObjetivoId == id)
                    .ToListAsync();
                if (desafiosCreadorObj.Any())
                    _context.Desafios.RemoveRange(desafiosCreadorObj);

                var desafiosCreadorAsig = await _context.Desafios
                    .Where(d => d.CreadorAsignadoId == id)
                    .ToListAsync();
                if (desafiosCreadorAsig.Any())
                    _context.Desafios.RemoveRange(desafiosCreadorAsig);

                // 13. Eliminar bloqueos de usuario (bloqueador y bloqueado)
                var bloqueosBloqueador = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == id)
                    .ToListAsync();
                if (bloqueosBloqueador.Any())
                    _context.BloqueosUsuarios.RemoveRange(bloqueosBloqueador);

                var bloqueosBloqueado = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadoId == id)
                    .ToListAsync();
                if (bloqueosBloqueado.Any())
                    _context.BloqueosUsuarios.RemoveRange(bloqueosBloqueado);

                // 14. Eliminar historias silenciadas
                var silenciadosUsuario = await _context.HistoriasSilenciadas
                    .Where(h => h.UsuarioId == id)
                    .ToListAsync();
                if (silenciadosUsuario.Any())
                    _context.HistoriasSilenciadas.RemoveRange(silenciadosUsuario);

                var silenciadosComo = await _context.HistoriasSilenciadas
                    .Where(h => h.SilenciadoId == id)
                    .ToListAsync();
                if (silenciadosComo.Any())
                    _context.HistoriasSilenciadas.RemoveRange(silenciadosComo);

                // 15. Eliminar referidos (referidor y referido)
                var referidosComoReferidor = await _context.Referidos
                    .Where(r => r.ReferidorId == id)
                    .ToListAsync();
                if (referidosComoReferidor.Any())
                    _context.Referidos.RemoveRange(referidosComoReferidor);

                var referidosComoReferido = await _context.Referidos
                    .Where(r => r.ReferidoUsuarioId == id)
                    .ToListAsync();
                if (referidosComoReferido.Any())
                    _context.Referidos.RemoveRange(referidosComoReferido);

                // 16. Eliminar agencia del usuario (y sus anuncios en cascada)
                var agencia = await _context.Agencias
                    .FirstOrDefaultAsync(a => a.UsuarioId == id);
                if (agencia != null)
                    _context.Agencias.Remove(agencia);

                // 17. Eliminar impresiones de anuncios del usuario
                var impresionesAnuncios = await _context.ImpresionesAnuncios
                    .Where(i => i.UsuarioId == id)
                    .ToListAsync();
                if (impresionesAnuncios.Any())
                    _context.ImpresionesAnuncios.RemoveRange(impresionesAnuncios);

                // ========================================
                // FASE 2: Eliminar entidades con FK Cascade (explícito por seguridad)
                // ========================================

                // 18. Eliminar stories del usuario (vistas y likes se eliminan en cascada)
                var stories = await _context.Stories
                    .Where(s => s.CreadorId == id)
                    .ToListAsync();
                if (stories.Any())
                    _context.Stories.RemoveRange(stories);

                // 18. Eliminar story drafts del usuario
                var storyDrafts = await _context.StoryDrafts
                    .Where(sd => sd.UsuarioId == id)
                    .ToListAsync();
                if (storyDrafts.Any())
                    _context.StoryDrafts.RemoveRange(storyDrafts);

                // 19. Eliminar colecciones del usuario
                var colecciones = await _context.Colecciones
                    .Where(c => c.CreadorId == id)
                    .ToListAsync();
                if (colecciones.Any())
                    _context.Colecciones.RemoveRange(colecciones);

                // 20. Eliminar favoritos del usuario
                var favoritos = await _context.Favoritos
                    .Where(f => f.UsuarioId == id)
                    .ToListAsync();
                if (favoritos.Any())
                    _context.Favoritos.RemoveRange(favoritos);

                // 21. Eliminar notificaciones del usuario
                var notificaciones = await _context.Notificaciones
                    .Where(n => n.UsuarioId == id)
                    .ToListAsync();
                if (notificaciones.Any())
                    _context.Notificaciones.RemoveRange(notificaciones);

                // 22. Eliminar intereses del usuario
                var intereses = await _context.InteresesUsuarios
                    .Where(i => i.UsuarioId == id)
                    .ToListAsync();
                if (intereses.Any())
                    _context.InteresesUsuarios.RemoveRange(intereses);

                // 23. Eliminar preferencias de algoritmo
                var prefAlgoritmo = await _context.PreferenciasAlgoritmoUsuario
                    .Where(p => p.UsuarioId == id)
                    .ToListAsync();
                if (prefAlgoritmo.Any())
                    _context.PreferenciasAlgoritmoUsuario.RemoveRange(prefAlgoritmo);

                // 24. Eliminar LadoCoins del usuario
                var ladoCoin = await _context.LadoCoins
                    .FirstOrDefaultAsync(l => l.UsuarioId == id);
                if (ladoCoin != null)
                    _context.LadoCoins.Remove(ladoCoin);

                // 25. Eliminar transacciones de LadoCoins
                var transaccionesLC = await _context.TransaccionesLadoCoins
                    .Where(t => t.UsuarioId == id)
                    .ToListAsync();
                if (transaccionesLC.Any())
                    _context.TransaccionesLadoCoins.RemoveRange(transaccionesLC);

                // 26. Eliminar racha del usuario
                var racha = await _context.RachasUsuarios
                    .FirstOrDefaultAsync(r => r.UsuarioId == id);
                if (racha != null)
                    _context.RachasUsuarios.Remove(racha);

                // 27. Eliminar refresh tokens
                var refreshTokens = await _context.RefreshTokens
                    .Where(r => r.UserId == id)
                    .ToListAsync();
                if (refreshTokens.Any())
                    _context.RefreshTokens.RemoveRange(refreshTokens);

                // 28. Eliminar active tokens
                var activeTokens = await _context.ActiveTokens
                    .Where(a => a.UserId == id)
                    .ToListAsync();
                if (activeTokens.Any())
                    _context.ActiveTokens.RemoveRange(activeTokens);

                // 29. Eliminar feedbacks del usuario
                var feedbacks = await _context.Feedbacks
                    .Where(f => f.UsuarioId == id)
                    .ToListAsync();
                if (feedbacks.Any())
                    _context.Feedbacks.RemoveRange(feedbacks);

                // ========================================
                // FASE 3: Eliminar entidades originales
                // ========================================

                // 30. Eliminar contenidos del usuario (archivos, objetos detectados, etc. en cascada)
                var contenidos = await _context.Contenidos
                    .Where(c => c.UsuarioId == id)
                    .ToListAsync();
                if (contenidos.Any())
                    _context.Contenidos.RemoveRange(contenidos);

                // 31. Eliminar suscripciones donde es creador
                var suscripcionesCreador = await _context.Suscripciones
                    .Where(s => s.CreadorId == id)
                    .ToListAsync();
                if (suscripcionesCreador.Any())
                    _context.Suscripciones.RemoveRange(suscripcionesCreador);

                // 32. Eliminar suscripciones donde es fan
                var suscripcionesFan = await _context.Suscripciones
                    .Where(s => s.FanId == id)
                    .ToListAsync();
                if (suscripcionesFan.Any())
                    _context.Suscripciones.RemoveRange(suscripcionesFan);

                // 33. Eliminar transacciones
                var transacciones = await _context.Transacciones
                    .Where(t => t.UsuarioId == id)
                    .ToListAsync();
                if (transacciones.Any())
                    _context.Transacciones.RemoveRange(transacciones);

                // 34. Eliminar likes
                var likes = await _context.Likes
                    .Where(l => l.UsuarioId == id)
                    .ToListAsync();
                if (likes.Any())
                    _context.Likes.RemoveRange(likes);

                // 35. Eliminar comentarios
                var comentarios = await _context.Comentarios
                    .Where(c => c.UsuarioId == id)
                    .ToListAsync();
                if (comentarios.Any())
                    _context.Comentarios.RemoveRange(comentarios);

                // 36. Eliminar reportes hechos por el usuario
                var reportesHechos = await _context.Reportes
                    .Where(r => r.UsuarioReportadorId == id)
                    .ToListAsync();
                if (reportesHechos.Any())
                    _context.Reportes.RemoveRange(reportesHechos);

                // 37. Eliminar reportes contra el usuario
                var reportesContra = await _context.Reportes
                    .Where(r => r.UsuarioReportadoId == id)
                    .ToListAsync();
                if (reportesContra.Any())
                    _context.Reportes.RemoveRange(reportesContra);

                // 38. Eliminar solicitud de verificación
                var verificacion = await _context.CreatorVerificationRequests
                    .FirstOrDefaultAsync(v => v.UserId == id);
                if (verificacion != null)
                    _context.CreatorVerificationRequests.Remove(verificacion);

                // ========================================
                // FASE 4: Guardar cambios y eliminar usuario
                // ========================================
                await _logEventoService.RegistrarEventoAsync(
                    $"[ELIMINAR-GUARDANDO] Guardando cambios en BD para: {usuarioNombre}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    adminId,
                    adminNombre,
                    "Ejecutando SaveChangesAsync para eliminar entidades relacionadas"
                );

                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"[ELIMINAR-DELETEUSER] Eliminando usuario con UserManager: {usuarioNombre}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    adminId,
                    adminNombre,
                    $"Entidades relacionadas eliminadas, procediendo a DeleteAsync"
                );

                // Finalmente, eliminar el usuario con UserManager
                var result = await _userManager.DeleteAsync(usuario);

                if (result.Succeeded)
                {
                    await _logEventoService.RegistrarEventoAsync(
                        $"[ELIMINAR-EXITO] Usuario eliminado correctamente: {usuarioNombre}",
                        CategoriaEvento.Admin,
                        TipoLogEvento.Evento,
                        adminId,
                        adminNombre,
                        $"Usuario {usuarioNombre} ({usuarioEmail}) eliminado permanentemente por {adminNombre}"
                    );
                    TempData["Success"] = $"Usuario {usuarioNombre} eliminado permanentemente junto con todos sus datos.";
                }
                else
                {
                    var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                    await _logEventoService.RegistrarEventoAsync(
                        $"[ELIMINAR-ERROR-IDENTITY] Error de Identity al eliminar: {usuarioNombre}",
                        CategoriaEvento.Admin,
                        TipoLogEvento.Error,
                        adminId,
                        adminNombre,
                        $"Errores de Identity: {errores}"
                    );
                    TempData["Error"] = "Error al eliminar el usuario: " + errores;
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(
                    ex,
                    CategoriaEvento.Admin,
                    adminId,
                    adminNombre
                );
                TempData["Error"] = $"Error al eliminar el usuario: {ex.Message}";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        /// <summary>
        /// Endpoint AJAX para eliminar usuario con respuesta JSON detallada
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken] // AJAX endpoint - ya protegido por [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EliminarUsuarioAjax([FromBody] EliminarUsuarioRequest request)
        {
            var adminActual = await _userManager.GetUserAsync(User);
            var adminId = adminActual?.Id;
            var adminNombre = adminActual?.NombreCompleto ?? "Admin";

            await _logEventoService.RegistrarEventoAsync(
                $"[AJAX-ELIMINAR-INICIO] Request recibido",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                adminId,
                adminNombre,
                $"UserId: '{request?.UserId}', Request Body: {System.Text.Json.JsonSerializer.Serialize(request)}"
            );

            if (request == null || string.IsNullOrEmpty(request.UserId))
            {
                await _logEventoService.RegistrarEventoAsync(
                    "[AJAX-ELIMINAR-ERROR] Request inválido",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Error,
                    adminId,
                    adminNombre,
                    $"Request es null: {request == null}, UserId vacío: {string.IsNullOrEmpty(request?.UserId)}"
                );
                return Json(new { success = false, message = "ID de usuario no proporcionado", step = "validation" });
            }

            var usuario = await _context.Users.FindAsync(request.UserId);
            if (usuario == null)
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"[AJAX-ELIMINAR-ERROR] Usuario no encontrado: {request.UserId}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Error,
                    adminId,
                    adminNombre,
                    $"No existe usuario con Id: {request.UserId}"
                );
                return Json(new { success = false, message = "Usuario no encontrado", step = "find_user" });
            }

            var usuarioNombre = usuario.NombreCompleto ?? usuario.UserName ?? "Sin nombre";
            var usuarioEmail = usuario.Email ?? "Sin email";

            // Verificar que no sea Admin
            var roles = await _userManager.GetRolesAsync(usuario);
            if (roles.Contains("Admin"))
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"[AJAX-ELIMINAR-BLOQUEADO] Intento de eliminar admin: {usuarioNombre}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Warning,
                    adminId,
                    adminNombre,
                    "No se puede eliminar un administrador"
                );
                return Json(new { success = false, message = "No se puede eliminar un administrador", step = "admin_check" });
            }

            try
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"[AJAX-ELIMINAR-PROCESO] Eliminando datos de: {usuarioNombre}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    adminId,
                    adminNombre,
                    "Iniciando eliminación de entidades relacionadas"
                );

                // Eliminar todas las entidades relacionadas (mismo código que EliminarUsuario)
                var id = request.UserId;

                // FASE 1: Entidades con FK Restrict
                _context.LikesComentarios.RemoveRange(await _context.LikesComentarios.Where(lc => lc.UsuarioId == id).ToListAsync());
                _context.Reacciones.RemoveRange(await _context.Reacciones.Where(r => r.UsuarioId == id).ToListAsync());
                _context.StoryVistas.RemoveRange(await _context.StoryVistas.Where(sv => sv.UsuarioId == id).ToListAsync());
                _context.StoryLikes.RemoveRange(await _context.StoryLikes.Where(sl => sl.UsuarioId == id).ToListAsync());
                _context.ComprasContenido.RemoveRange(await _context.ComprasContenido.Where(cc => cc.UsuarioId == id).ToListAsync());
                _context.ComprasColeccion.RemoveRange(await _context.ComprasColeccion.Where(cc => cc.CompradorId == id).ToListAsync());
                _context.Tips.RemoveRange(await _context.Tips.Where(t => t.FanId == id || t.CreadorId == id).ToListAsync());
                _context.FotosDestacadas.RemoveRange(await _context.FotosDestacadas.Where(f => f.UsuarioId == id).ToListAsync());
                _context.MensajesPrivados.RemoveRange(await _context.MensajesPrivados.Where(m => m.RemitenteId == id || m.DestinatarioId == id).ToListAsync());
                _context.ChatMensajes.RemoveRange(await _context.ChatMensajes.Where(m => m.RemitenteId == id || m.DestinatarioId == id).ToListAsync());
                _context.PropuestasDesafios.RemoveRange(await _context.PropuestasDesafios.Where(p => p.CreadorId == id).ToListAsync());
                _context.Desafios.RemoveRange(await _context.Desafios.Where(d => d.FanId == id || d.CreadorObjetivoId == id || d.CreadorAsignadoId == id).ToListAsync());
                _context.BloqueosUsuarios.RemoveRange(await _context.BloqueosUsuarios.Where(b => b.BloqueadorId == id || b.BloqueadoId == id).ToListAsync());
                _context.HistoriasSilenciadas.RemoveRange(await _context.HistoriasSilenciadas.Where(h => h.UsuarioId == id || h.SilenciadoId == id).ToListAsync());
                _context.Referidos.RemoveRange(await _context.Referidos.Where(r => r.ReferidorId == id || r.ReferidoUsuarioId == id).ToListAsync());

                var agencia = await _context.Agencias.FirstOrDefaultAsync(a => a.UsuarioId == id);
                if (agencia != null) _context.Agencias.Remove(agencia);

                // Eliminar impresiones de anuncios del usuario
                _context.ImpresionesAnuncios.RemoveRange(await _context.ImpresionesAnuncios.Where(i => i.UsuarioId == id).ToListAsync());

                // FASE 2: Entidades con FK Cascade
                _context.Stories.RemoveRange(await _context.Stories.Where(s => s.CreadorId == id).ToListAsync());
                _context.StoryDrafts.RemoveRange(await _context.StoryDrafts.Where(sd => sd.UsuarioId == id).ToListAsync());
                _context.Colecciones.RemoveRange(await _context.Colecciones.Where(c => c.CreadorId == id).ToListAsync());
                _context.Favoritos.RemoveRange(await _context.Favoritos.Where(f => f.UsuarioId == id).ToListAsync());
                _context.Notificaciones.RemoveRange(await _context.Notificaciones.Where(n => n.UsuarioId == id).ToListAsync());
                _context.InteresesUsuarios.RemoveRange(await _context.InteresesUsuarios.Where(i => i.UsuarioId == id).ToListAsync());
                _context.PreferenciasAlgoritmoUsuario.RemoveRange(await _context.PreferenciasAlgoritmoUsuario.Where(p => p.UsuarioId == id).ToListAsync());

                var ladoCoin = await _context.LadoCoins.FirstOrDefaultAsync(l => l.UsuarioId == id);
                if (ladoCoin != null) _context.LadoCoins.Remove(ladoCoin);

                _context.TransaccionesLadoCoins.RemoveRange(await _context.TransaccionesLadoCoins.Where(t => t.UsuarioId == id).ToListAsync());

                var racha = await _context.RachasUsuarios.FirstOrDefaultAsync(r => r.UsuarioId == id);
                if (racha != null) _context.RachasUsuarios.Remove(racha);

                _context.RefreshTokens.RemoveRange(await _context.RefreshTokens.Where(r => r.UserId == id).ToListAsync());
                _context.ActiveTokens.RemoveRange(await _context.ActiveTokens.Where(a => a.UserId == id).ToListAsync());
                _context.Feedbacks.RemoveRange(await _context.Feedbacks.Where(f => f.UsuarioId == id).ToListAsync());

                // FASE 3: Entidades principales
                _context.Contenidos.RemoveRange(await _context.Contenidos.Where(c => c.UsuarioId == id).ToListAsync());
                _context.Suscripciones.RemoveRange(await _context.Suscripciones.Where(s => s.CreadorId == id || s.FanId == id).ToListAsync());
                _context.Transacciones.RemoveRange(await _context.Transacciones.Where(t => t.UsuarioId == id).ToListAsync());
                _context.Likes.RemoveRange(await _context.Likes.Where(l => l.UsuarioId == id).ToListAsync());
                _context.Comentarios.RemoveRange(await _context.Comentarios.Where(c => c.UsuarioId == id).ToListAsync());
                _context.Reportes.RemoveRange(await _context.Reportes.Where(r => r.UsuarioReportadorId == id || r.UsuarioReportadoId == id).ToListAsync());

                var verificacion = await _context.CreatorVerificationRequests.FirstOrDefaultAsync(v => v.UserId == id);
                if (verificacion != null) _context.CreatorVerificationRequests.Remove(verificacion);

                // Guardar cambios
                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"[AJAX-ELIMINAR-DELETEUSER] Eliminando con UserManager: {usuarioNombre}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    adminId,
                    adminNombre,
                    "Entidades eliminadas, ejecutando DeleteAsync"
                );

                var result = await _userManager.DeleteAsync(usuario);

                if (result.Succeeded)
                {
                    await _logEventoService.RegistrarEventoAsync(
                        $"[AJAX-ELIMINAR-EXITO] Usuario eliminado: {usuarioNombre}",
                        CategoriaEvento.Admin,
                        TipoLogEvento.Evento,
                        adminId,
                        adminNombre,
                        $"Usuario {usuarioNombre} ({usuarioEmail}) eliminado por {adminNombre}"
                    );
                    return Json(new { success = true, message = $"Usuario {usuarioNombre} eliminado correctamente", step = "completed" });
                }
                else
                {
                    var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                    await _logEventoService.RegistrarEventoAsync(
                        $"[AJAX-ELIMINAR-ERROR-IDENTITY] {usuarioNombre}",
                        CategoriaEvento.Admin,
                        TipoLogEvento.Error,
                        adminId,
                        adminNombre,
                        $"Errores: {errores}"
                    );
                    return Json(new { success = false, message = $"Error de Identity: {errores}", step = "delete_user" });
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin, adminId, adminNombre);
                return Json(new {
                    success = false,
                    message = $"Error: {ex.Message}",
                    step = "exception",
                    details = ex.InnerException?.Message
                });
            }
        }

        public class EliminarUsuarioRequest
        {
            public string UserId { get; set; } = string.Empty;
        }

        // ========================================
        // DETALLE USUARIO
        // ========================================
        public async Task<IActionResult> DetalleUsuario(string id)
        {
            var usuario = await _context.Users.FindAsync(id);
            if (usuario == null)
            {
                return NotFound();
            }

            ViewBag.Suscripciones = await _context.Suscripciones
                .Include(s => s.Creador)
                .Where(s => s.FanId == id && s.EstaActiva)
                .ToListAsync();

            ViewBag.Contenidos = await _context.Contenidos
                .Where(c => c.UsuarioId == id && c.EstaActivo)
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(20)
                .ToListAsync();

            ViewBag.Transacciones = await _context.Transacciones
                .Where(t => t.UsuarioId == id)
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(20)
                .ToListAsync();

            return View(usuario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspenderUsuario(string userId)
        {
            // Prevenir auto-suspensión
            var adminActual = await _userManager.GetUserAsync(User);
            if (adminActual != null && userId == adminActual.Id)
            {
                return Json(new { success = false, message = "No puedes suspenderte a ti mismo" });
            }

            var usuario = await _context.Users.FindAsync(userId);
            if (usuario != null)
            {
                usuario.EstaActivo = false;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivarUsuario(string userId)
        {
            var usuario = await _context.Users.FindAsync(userId);
            if (usuario != null)
            {
                usuario.EstaActivo = true;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerificarUsuario(string userId)
        {
            var usuario = await _context.Users.FindAsync(userId);
            if (usuario != null)
            {
                usuario.EsVerificado = true;
                usuario.CreadorVerificado = true; // Habilita LadoB para monetización
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCreadorVerificado(string userId, bool habilitar)
        {
            var usuario = await _context.Users.FindAsync(userId);
            if (usuario != null)
            {
                usuario.CreadorVerificado = habilitar;
                if (habilitar)
                {
                    usuario.EsCreador = true; // Activar también EsCreador
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Usuario no encontrado" });
        }

        // ========================================
        // CONTENIDO
        // ========================================
        public async Task<IActionResult> Contenido(string? tipo = null, string? estado = null)
        {
            var query = _context.Contenidos
                .Include(c => c.Usuario)
                .Include(c => c.Archivos)
                .Include(c => c.CategoriaInteres)
                .Include(c => c.ObjetosDetectados)
                .AsQueryable();

            // Filtrar por tipo
            if (tipo == "foto")
                query = query.Where(c => c.TipoContenido == TipoContenido.Foto);
            else if (tipo == "video")
                query = query.Where(c => c.TipoContenido == TipoContenido.Video);

            // Filtrar por estado
            if (estado == "censurado")
                query = query.Where(c => c.Censurado);
            else if (estado == "sensible")
                query = query.Where(c => c.EsContenidoSensible);
            else if (estado == "shadowhide")
                query = query.Where(c => c.OcultoSilenciosamente);

            var contenidos = await query
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(100)
                .ToListAsync();

            return View(contenidos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReclasificarContenido(int id)
        {
            try
            {
                var contenido = await _context.Contenidos.FindAsync(id);
                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado", detalle = "El ID no existe en la base de datos" });
                }

                // Obtener la imagen del contenido
                byte[]? imagenBytes = null;
                string? mimeType = null;
                string? rutaUsada = null;

                if (!string.IsNullOrEmpty(contenido.RutaArchivo) || !string.IsNullOrEmpty(contenido.Thumbnail))
                {
                    var rutaArchivo = !string.IsNullOrEmpty(contenido.Thumbnail)
                        ? contenido.Thumbnail
                        : contenido.RutaArchivo;

                    rutaUsada = rutaArchivo;

                    // Convertir ruta relativa a absoluta
                    var rutaFisica = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", rutaArchivo?.TrimStart('/').Replace("/", "\\") ?? "");

                    if (System.IO.File.Exists(rutaFisica))
                    {
                        imagenBytes = await System.IO.File.ReadAllBytesAsync(rutaFisica);
                        var extension = Path.GetExtension(rutaFisica).ToLower();
                        mimeType = extension switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            ".gif" => "image/gif",
                            ".webp" => "image/webp",
                            _ => "image/jpeg"
                        };
                    }
                    else
                    {
                        return Json(new {
                            success = false,
                            message = "Archivo no encontrado",
                            detalle = $"No existe el archivo: {rutaArchivo}"
                        });
                    }
                }

                // Usar metodo combinado para clasificar Y detectar objetos
                var resultado = await _claudeService.ClasificarYDetectarObjetosAsync(
                    imagenBytes,
                    contenido.Descripcion,
                    mimeType);

                if (resultado.Clasificacion.Exito && resultado.Clasificacion.CategoriaId.HasValue)
                {
                    contenido.CategoriaInteresId = resultado.Clasificacion.CategoriaId.Value;

                    // Guardar objetos detectados
                    int objetosGuardados = 0;
                    if (resultado.ObjetosDetectados.Any())
                    {
                        // Eliminar objetos anteriores si existían
                        var objetosAnteriores = await _context.ObjetosContenido
                            .Where(o => o.ContenidoId == contenido.Id)
                            .ToListAsync();
                        if (objetosAnteriores.Any())
                        {
                            _context.ObjetosContenido.RemoveRange(objetosAnteriores);
                        }

                        // Agregar nuevos objetos
                        foreach (var obj in resultado.ObjetosDetectados)
                        {
                            _context.ObjetosContenido.Add(new Models.ObjetoContenido
                            {
                                ContenidoId = contenido.Id,
                                NombreObjeto = obj.Nombre,
                                Confianza = obj.Confianza,
                                FechaDeteccion = DateTime.Now
                            });
                            objetosGuardados++;
                        }
                    }

                    await _context.SaveChangesAsync();

                    var categoria = await _context.CategoriasIntereses
                        .Where(c => c.Id == resultado.Clasificacion.CategoriaId.Value)
                        .Select(c => new { c.Id, c.Nombre, c.Icono, c.Color })
                        .FirstOrDefaultAsync();

                    var mensaje = resultado.Clasificacion.CategoriaCreada
                        ? $"Nueva categoria creada: {categoria?.Nombre}"
                        : "Contenido reclasificado correctamente";

                    return Json(new
                    {
                        success = true,
                        message = mensaje,
                        categoriaId = resultado.Clasificacion.CategoriaId,
                        categoriaNombre = categoria?.Nombre,
                        categoriaIcono = categoria?.Icono,
                        categoriaColor = categoria?.Color,
                        categoriaCreada = resultado.Clasificacion.CategoriaCreada,
                        objetosDetectados = resultado.ObjetosDetectados.Select(o => o.Nombre).ToList(),
                        objetosGuardados,
                        tiempoMs = resultado.Clasificacion.TiempoMs
                    });
                }

                return Json(new {
                    success = false,
                    message = resultado.Clasificacion.Error ?? "Error desconocido",
                    detalle = resultado.Clasificacion.DetalleError,
                    tiempoMs = resultado.Clasificacion.TiempoMs
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error interno", detalle = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CensurarContenido(int id, string razon)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                contenido.Censurado = true;
                contenido.RazonCensura = razon;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido censurado exitosamente.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DescensurarContenido(int id)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                contenido.Censurado = false;
                contenido.RazonCensura = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido descensurado exitosamente.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarContenido(int id)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                _context.Contenidos.Remove(contenido);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido eliminado permanentemente.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        /// <summary>
        /// Eliminar múltiples contenidos de un usuario
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarContenidosMasivo([FromBody] EliminarContenidosMasivoRequest request)
        {
            if (request?.Ids == null || !request.Ids.Any())
            {
                return Json(new { success = false, message = "No se seleccionaron contenidos" });
            }

            try
            {
                var resultado = await EliminarContenidosPorIdsAsync(request.Ids);
                return Json(resultado);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar: " + ex.Message });
            }
        }

        /// <summary>
        /// Eliminar TODO el contenido de un usuario
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarTodoContenidoUsuario([FromBody] EliminarContenidoUsuarioRequest request)
        {
            if (string.IsNullOrEmpty(request?.UserId))
            {
                return Json(new { success = false, message = "UserId no proporcionado" });
            }

            try
            {
                // Obtener todos los IDs de contenido del usuario
                var ids = await _context.Contenidos
                    .Where(c => c.UsuarioId == request.UserId)
                    .Select(c => c.Id)
                    .ToListAsync();

                if (!ids.Any())
                {
                    return Json(new { success = false, message = "El usuario no tiene contenidos" });
                }

                var resultado = await EliminarContenidosPorIdsAsync(ids);
                return Json(resultado);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar: " + ex.Message });
            }
        }

        /// <summary>
        /// Método interno para eliminar contenidos por IDs
        /// </summary>
        private async Task<object> EliminarContenidosPorIdsAsync(List<int> ids)
        {
            var contenidos = await _context.Contenidos
                .Include(c => c.Archivos)
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            if (!contenidos.Any())
            {
                return new { success = false, message = "No se encontraron los contenidos" };
            }

            int archivosEliminados = 0;

            // Eliminar archivos físicos
            foreach (var contenido in contenidos)
            {
                // Archivo principal
                if (!string.IsNullOrEmpty(contenido.RutaArchivo))
                {
                    var ruta = contenido.RutaArchivo.TrimStart('/').Replace("/", "\\");
                    var rutaCompleta = Path.Combine(_hostEnvironment.WebRootPath, ruta);

                    if (System.IO.File.Exists(rutaCompleta))
                    {
                        System.IO.File.Delete(rutaCompleta);
                        archivosEliminados++;
                    }
                }

                // Archivos adicionales
                if (contenido.Archivos != null)
                {
                    foreach (var archivo in contenido.Archivos)
                    {
                        if (!string.IsNullOrEmpty(archivo.RutaArchivo))
                        {
                            var ruta = archivo.RutaArchivo.TrimStart('/').Replace("/", "\\");
                            var rutaArchivo = Path.Combine(_hostEnvironment.WebRootPath, ruta);

                            if (System.IO.File.Exists(rutaArchivo))
                            {
                                System.IO.File.Delete(rutaArchivo);
                                archivosEliminados++;
                            }
                        }
                    }
                }
            }

            // Eliminar de la base de datos (cascade elimina los archivos relacionados)
            _context.Contenidos.RemoveRange(contenidos);
            await _context.SaveChangesAsync();

            return new {
                success = true,
                message = $"{contenidos.Count} contenidos eliminados ({archivosEliminados} archivos físicos)",
                count = contenidos.Count,
                files = archivosEliminados
            };
        }

        public class EliminarContenidosMasivoRequest
        {
            public List<int> Ids { get; set; } = new();
        }

        public class EliminarContenidoUsuarioRequest
        {
            public string UserId { get; set; } = string.Empty;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarContenidoSensible(int id)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                contenido.EsContenidoSensible = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido marcado como sensible.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarContenidoSensible(int id)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                contenido.EsContenidoSensible = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Marca de contenido sensible quitada.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        /// <summary>
        /// Shadow Hide: Oculta el contenido silenciosamente para todos excepto el creador
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OcultarContenidoSilencioso(int id)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                var adminId = _userManager.GetUserId(User);
                contenido.OcultoSilenciosamente = true;
                contenido.FechaOcultoSilenciosamente = DateTime.Now;
                contenido.OcultadoPorAdminId = adminId;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido ocultado silenciosamente. El creador no lo sabrá.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        /// <summary>
        /// Quita el Shadow Hide y hace visible el contenido nuevamente
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MostrarContenidoSilencioso(int id)
        {
            var contenido = await _context.Contenidos.FindAsync(id);
            if (contenido != null)
            {
                contenido.OcultoSilenciosamente = false;
                contenido.FechaOcultoSilenciosamente = null;
                contenido.OcultadoPorAdminId = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Contenido visible nuevamente.";
            }

            return RedirectToAction(nameof(Contenido));
        }

        /// <summary>
        /// Obtiene los archivos de un contenido (para carruseles)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerArchivosContenido(int id)
        {
            var archivos = await _context.ArchivosContenido
                .Where(a => a.ContenidoId == id)
                .OrderBy(a => a.Orden)
                .Select(a => new
                {
                    id = a.Id,
                    url = a.RutaArchivo,
                    tipo = a.TipoArchivo.ToString(),
                    esVideo = a.TipoArchivo == TipoArchivo.Video,
                    orden = a.Orden,
                    ocultoSilenciosamente = a.OcultoSilenciosamente
                })
                .ToListAsync();

            return Json(new { success = true, archivos });
        }

        /// <summary>
        /// Shadow Hide para archivo individual de un carrusel
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OcultarArchivoSilencioso(int id)
        {
            var archivo = await _context.ArchivosContenido.FindAsync(id);
            if (archivo == null)
                return Json(new { success = false, message = "Archivo no encontrado" });

            archivo.OcultoSilenciosamente = true;
            archivo.FechaOcultoSilenciosamente = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        /// <summary>
        /// Quita el Shadow Hide de un archivo individual
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MostrarArchivoSilencioso(int id)
        {
            var archivo = await _context.ArchivosContenido.FindAsync(id);
            if (archivo == null)
                return Json(new { success = false, message = "Archivo no encontrado" });

            archivo.OcultoSilenciosamente = false;
            archivo.FechaOcultoSilenciosamente = null;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> BuscarUsuarios(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Json(new List<object>());
            }

            var usuarios = await _context.Users
                .Where(u => u.UserName != null && u.UserName.Contains(q))
                .Take(10)
                .Select(u => new
                {
                    id = u.Id,
                    userName = u.UserName,
                    fotoPerfil = u.FotoPerfil,
                    esCreador = u.EsCreador
                })
                .ToListAsync();

            return Json(usuarios);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferirContenido(int id, string nuevoUsuarioId, bool cambiarTipoLado = false)
        {
            var contenido = await _context.Contenidos
                .Include(c => c.Usuario)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contenido == null)
            {
                TempData["Error"] = "Contenido no encontrado.";
                return RedirectToAction(nameof(Contenido));
            }

            var nuevoUsuario = await _context.Users.FindAsync(nuevoUsuarioId);
            if (nuevoUsuario == null)
            {
                TempData["Error"] = "Usuario destino no encontrado.";
                return RedirectToAction(nameof(Contenido));
            }

            var usuarioAnterior = contenido.Usuario?.UserName ?? "Desconocido";
            contenido.UsuarioId = nuevoUsuarioId;

            if (cambiarTipoLado)
            {
                contenido.TipoLado = contenido.TipoLado == TipoLado.LadoA ? TipoLado.LadoB : TipoLado.LadoA;
            }

            contenido.FechaActualizacion = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Contenido transferido de {usuarioAnterior} a {nuevoUsuario.UserName}" +
                                  (cambiarTipoLado ? $" (cambiado a {contenido.TipoLado})" : "") + ".";

            return RedirectToAction(nameof(Contenido));
        }

        // ========================================
        // REPORTES
        // ========================================
        public async Task<IActionResult> Reportes()
        {
            var reportes = await _context.Reportes
                .Include(r => r.UsuarioReportador)
                .Include(r => r.UsuarioReportado)
                .Include(r => r.ContenidoReportado)
                .ThenInclude(c => c.Usuario)
                .OrderByDescending(r => r.FechaReporte)
                .ToListAsync();

            return View(reportes);
        }

        [HttpPost]
        public async Task<IActionResult> ResolverReporte(int id, string accion, string? detalles)
        {
            var reporte = await _context.Reportes
                .Include(r => r.ContenidoReportado)
                .ThenInclude(c => c.Usuario)
                .Include(r => r.UsuarioReportado)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reporte == null)
            {
                TempData["Error"] = "Reporte no encontrado.";
                return RedirectToAction(nameof(Reportes));
            }

            reporte.Estado = "Resuelto";
            reporte.FechaResolucion = DateTime.Now;
            reporte.Accion = accion;

            var mensajeAccion = "";

            // Ejecutar acción según la selección
            switch (accion)
            {
                case "Usuario bloqueado":
                    // Bloquear al usuario reportado
                    var usuarioABloquear = reporte.TipoReporte == "Usuario"
                        ? reporte.UsuarioReportado
                        : reporte.ContenidoReportado?.Usuario;

                    if (usuarioABloquear != null)
                    {
                        usuarioABloquear.EstaActivo = false;
                        mensajeAccion = $"Usuario @{usuarioABloquear.UserName} bloqueado.";
                    }
                    break;

                case "Contenido eliminado":
                    if (reporte.ContenidoReportado != null)
                    {
                        reporte.ContenidoReportado.EstaActivo = false;
                        mensajeAccion = $"Contenido #{reporte.ContenidoReportado.Id} eliminado.";
                    }
                    break;

                case "Contenido censurado":
                    if (reporte.ContenidoReportado != null)
                    {
                        reporte.ContenidoReportado.Censurado = true;
                        reporte.ContenidoReportado.RazonCensura = $"Reporte #{reporte.Id}: {reporte.Motivo}";
                        mensajeAccion = $"Contenido #{reporte.ContenidoReportado.Id} censurado.";
                    }
                    break;

                case "Advertencia enviada":
                    // Crear notificación de advertencia al usuario
                    var usuarioAdvertido = reporte.TipoReporte == "Usuario"
                        ? reporte.UsuarioReportado
                        : reporte.ContenidoReportado?.Usuario;

                    if (usuarioAdvertido != null)
                    {
                        var notificacion = new Notificacion
                        {
                            UsuarioId = usuarioAdvertido.Id,
                            Tipo = TipoNotificacion.Sistema,
                            Titulo = "Advertencia",
                            Mensaje = $"Has recibido una advertencia por: {reporte.Motivo}. Por favor revisa nuestros términos de servicio.",
                            FechaCreacion = DateTime.Now,
                            Leida = false
                        };
                        _context.Notificaciones.Add(notificacion);
                        mensajeAccion = $"Advertencia enviada a @{usuarioAdvertido.UserName}.";
                    }
                    break;

                case "Reporte infundado":
                    mensajeAccion = "Reporte marcado como infundado. No se tomó ninguna acción.";
                    break;

                default:
                    mensajeAccion = $"Acción: {accion}";
                    break;
            }

            // Agregar detalles si los hay
            if (!string.IsNullOrEmpty(detalles))
            {
                reporte.Accion = $"{accion} - {detalles}";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Reporte resuelto. {mensajeAccion}";

            return RedirectToAction(nameof(Reportes));
        }

        // ========================================
        // ESTADISTICAS COMPLETAS
        // ========================================
        public async Task<IActionResult> Estadisticas()
        {
            var hoy = DateTime.Today;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioMesAnterior = inicioMes.AddMonths(-1);
            var hace7Dias = hoy.AddDays(-7);
            var hace30Dias = hoy.AddDays(-30);

            // ========================================
            // KPIs PRINCIPALES
            // ========================================

            // Usuarios
            var totalUsuarios = await _context.Users.CountAsync();
            var usuariosMesActual = await _context.Users.CountAsync(u => u.FechaRegistro >= inicioMes);
            var usuariosMesAnterior = await _context.Users.CountAsync(u => u.FechaRegistro >= inicioMesAnterior && u.FechaRegistro < inicioMes);
            var crecimientoUsuarios = usuariosMesAnterior > 0 ? ((double)(usuariosMesActual - usuariosMesAnterior) / usuariosMesAnterior * 100) : 100;

            ViewBag.TotalUsuarios = totalUsuarios;
            ViewBag.UsuariosNuevosMes = usuariosMesActual;
            ViewBag.CrecimientoUsuarios = Math.Round(crecimientoUsuarios, 1);

            // Usuarios activos (que han publicado o interactuado en los ultimos 7 dias)
            var usuariosActivosIds = await _context.Contenidos
                .Where(c => c.FechaPublicacion >= hace7Dias)
                .Select(c => c.UsuarioId)
                .Union(_context.Likes.Where(l => l.FechaLike >= hace7Dias).Select(l => l.UsuarioId))
                .Union(_context.Comentarios.Where(c => c.FechaCreacion >= hace7Dias).Select(c => c.UsuarioId))
                .Distinct()
                .CountAsync();
            ViewBag.UsuariosActivos7Dias = usuariosActivosIds;

            // Creadores
            var totalCreadores = await _context.Users.CountAsync(u => u.EsCreador);
            var creadoresVerificados = await _context.Users.CountAsync(u => u.EsCreador && u.CreadorVerificado);
            ViewBag.TotalCreadores = totalCreadores;
            ViewBag.CreadoresVerificados = creadoresVerificados;

            // Suscripciones
            var suscripcionesActivas = await _context.Suscripciones.CountAsync(s => s.EstaActiva);
            var suscripcionesMesActual = await _context.Suscripciones.CountAsync(s => s.FechaInicio >= inicioMes);
            var suscripcionesCanceladas = await _context.Suscripciones.CountAsync(s => s.FechaCancelacion != null && s.FechaCancelacion >= inicioMes);
            ViewBag.SuscripcionesActivas = suscripcionesActivas;
            ViewBag.SuscripcionesNuevasMes = suscripcionesMesActual;
            ViewBag.SuscripcionesCanceladas = suscripcionesCanceladas;

            // Contenido
            var totalContenido = await _context.Contenidos.CountAsync();
            var contenidoHoy = await _context.Contenidos.CountAsync(c => c.FechaPublicacion.Date == hoy);
            var contenidoMes = await _context.Contenidos.CountAsync(c => c.FechaPublicacion >= inicioMes);
            var contenidoCensurado = await _context.Contenidos.CountAsync(c => c.Censurado);
            ViewBag.TotalContenido = totalContenido;
            ViewBag.ContenidoHoy = contenidoHoy;
            ViewBag.ContenidoMes = contenidoMes;
            ViewBag.ContenidoCensurado = contenidoCensurado;

            // ========================================
            // METRICAS FINANCIERAS
            // ========================================
            var ingresosTotales = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            var ingresosMes = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada && t.FechaTransaccion >= inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            var ingresosMesAnterior = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada &&
                       t.FechaTransaccion >= inicioMesAnterior && t.FechaTransaccion < inicioMes)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            var crecimientoIngresos = ingresosMesAnterior > 0 ? ((double)(ingresosMes - ingresosMesAnterior) / (double)ingresosMesAnterior * 100) : 100;

            var comisionesTotales = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada && t.Comision != null)
                .SumAsync(t => (decimal?)t.Comision) ?? 0;

            ViewBag.IngresosTotales = ingresosTotales;
            ViewBag.IngresosMes = ingresosMes;
            ViewBag.CrecimientoIngresos = Math.Round(crecimientoIngresos, 1);
            ViewBag.ComisionesPlataforma = comisionesTotales;

            // ========================================
            // METRICAS DE ENGAGEMENT
            // ========================================
            var totalLikes = await _context.Likes.CountAsync();
            var totalComentarios = await _context.Comentarios.CountAsync();
            var promedioLikesPorPost = totalContenido > 0 ? (double)totalLikes / totalContenido : 0;
            var promedioComentariosPorPost = totalContenido > 0 ? (double)totalComentarios / totalContenido : 0;

            ViewBag.TotalLikes = totalLikes;
            ViewBag.TotalComentarios = totalComentarios;
            ViewBag.PromedioLikes = Math.Round(promedioLikesPorPost, 1);
            ViewBag.PromedioComentarios = Math.Round(promedioComentariosPorPost, 1);

            // ========================================
            // DATOS PARA GRAFICOS
            // ========================================

            // Registros por mes (ultimos 12 meses)
            var hace12Meses = hoy.AddMonths(-12);
            var registrosPorMes = await _context.Users
                .Where(u => u.FechaRegistro >= hace12Meses)
                .GroupBy(u => new { u.FechaRegistro.Year, u.FechaRegistro.Month })
                .Select(g => new
                {
                    Anio = g.Key.Year,
                    Mes = g.Key.Month,
                    Total = g.Count()
                })
                .OrderBy(x => x.Anio).ThenBy(x => x.Mes)
                .ToListAsync();
            ViewBag.RegistrosPorMes = registrosPorMes;

            // Contenido por tipo
            var contenidoPorTipo = await _context.Contenidos
                .GroupBy(c => c.TipoContenido)
                .Select(g => new
                {
                    Tipo = g.Key.ToString(),
                    Total = g.Count()
                })
                .ToListAsync();
            ViewBag.ContenidoPorTipo = contenidoPorTipo;

            // Ingresos por dia (ultimos 30 dias)
            var ingresosPorDia = await _context.Transacciones
                .Where(t => t.EstadoTransaccion == EstadoTransaccion.Completada && t.FechaTransaccion >= hace30Dias)
                .GroupBy(t => t.FechaTransaccion.Date)
                .Select(g => new
                {
                    Fecha = g.Key,
                    Total = g.Sum(t => t.Monto)
                })
                .OrderBy(x => x.Fecha)
                .ToListAsync();
            ViewBag.IngresosPorDia = ingresosPorDia;

            // Usuarios por pais
            var usuariosPorPais = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.Pais))
                .GroupBy(u => u.Pais)
                .Select(g => new
                {
                    Pais = g.Key,
                    Total = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .Take(10)
                .ToListAsync();
            ViewBag.UsuariosPorPais = usuariosPorPais;

            // ========================================
            // TOP RANKINGS
            // ========================================

            // Top 10 creadores con mas suscriptores
            var topCreadoresSuscriptores = await _context.Suscripciones
                .Where(s => s.EstaActiva)
                .GroupBy(s => s.CreadorId)
                .Select(g => new
                {
                    CreadorId = g.Key,
                    TotalSuscriptores = g.Count()
                })
                .OrderByDescending(x => x.TotalSuscriptores)
                .Take(10)
                .ToListAsync();

            var topSuscriptoresConNombres = new List<object>();
            foreach (var item in topCreadoresSuscriptores)
            {
                var usuario = await _context.Users.FindAsync(item.CreadorId);
                topSuscriptoresConNombres.Add(new
                {
                    Nombre = usuario?.UserName ?? "Desconocido",
                    FotoPerfil = usuario?.FotoPerfil,
                    Total = item.TotalSuscriptores
                });
            }
            ViewBag.TopCreadoresSuscriptores = topSuscriptoresConNombres;

            // Top 10 creadores con mas contenido
            var topCreadoresContenido = await _context.Contenidos
                .GroupBy(c => c.UsuarioId)
                .Select(g => new
                {
                    CreadorId = g.Key,
                    TotalContenido = g.Count()
                })
                .OrderByDescending(x => x.TotalContenido)
                .Take(10)
                .ToListAsync();

            var topContenidoConNombres = new List<object>();
            foreach (var item in topCreadoresContenido)
            {
                var usuario = await _context.Users.FindAsync(item.CreadorId);
                topContenidoConNombres.Add(new
                {
                    Nombre = usuario?.UserName ?? "Desconocido",
                    FotoPerfil = usuario?.FotoPerfil,
                    Total = item.TotalContenido
                });
            }
            ViewBag.TopCreadoresContenido = topContenidoConNombres;

            // Top 10 contenidos mas populares (likes + comentarios)
            var topContenidos = await _context.Contenidos
                .Include(c => c.Usuario)
                .Include(c => c.Likes)
                .Include(c => c.Comentarios)
                .OrderByDescending(c => c.Likes.Count + c.Comentarios.Count)
                .Take(10)
                .Select(c => new
                {
                    c.Id,
                    c.Descripcion,
                    c.TipoContenido,
                    c.Thumbnail,
                    c.RutaArchivo,
                    CreadorNombre = c.Usuario != null ? c.Usuario.UserName : "Desconocido",
                    TotalLikes = c.Likes.Count,
                    TotalComentarios = c.Comentarios.Count
                })
                .ToListAsync();
            ViewBag.TopContenidos = topContenidos;

            // ========================================
            // ALERTAS Y TENDENCIAS
            // ========================================

            // Creadores sin publicar hace 7 dias (que tenian actividad antes)
            var creadoresInactivos = await _context.Users
                .Where(u => u.EsCreador)
                .Where(u => !_context.Contenidos.Any(c => c.UsuarioId == u.Id && c.FechaPublicacion >= hace7Dias))
                .Where(u => _context.Contenidos.Any(c => c.UsuarioId == u.Id))
                .Take(10)
                .Select(u => new { u.Id, u.UserName, u.FotoPerfil })
                .ToListAsync();
            ViewBag.CreadoresInactivos = creadoresInactivos;

            // Reportes pendientes
            var reportesPendientes = await _context.Reportes.CountAsync(r => r.Estado == "Pendiente");
            ViewBag.ReportesPendientesTotal = reportesPendientes;

            // Verificaciones pendientes
            var verificacionesPendientes = await _context.CreatorVerificationRequests
                .CountAsync(v => v.Estado == "Pendiente");
            ViewBag.VerificacionesPendientes = verificacionesPendientes;

            return View();
        }

        // ========================================
        // CONFIGURACIÓN
        // ========================================
        public async Task<IActionResult> Configuracion()
        {
            // Cargar configuraciones de billetera, regional, archivos y feed
            var configuraciones = await _context.ConfiguracionesPlataforma
                .Where(c => c.Categoria == "Billetera" || c.Categoria == "General" || c.Categoria == "Regional" || c.Categoria == "Archivos" || c.Categoria == "Feed")
                .ToDictionaryAsync(c => c.Clave, c => c.Valor);

            ViewBag.ComisionBilleteraElectronica = configuraciones.TryGetValue(ConfiguracionPlataforma.COMISION_BILLETERA_ELECTRONICA, out var comision) ? comision : "2.5";
            ViewBag.TiempoProcesoRetiro = configuraciones.TryGetValue(ConfiguracionPlataforma.TIEMPO_PROCESO_RETIRO, out var tiempo) ? tiempo : "3-5 dias habiles";
            ViewBag.MontoMinimoRecarga = configuraciones.TryGetValue(ConfiguracionPlataforma.MONTO_MINIMO_RECARGA, out var minRecarga) ? minRecarga : "5";
            ViewBag.MontoMaximoRecarga = configuraciones.TryGetValue(ConfiguracionPlataforma.MONTO_MAXIMO_RECARGA, out var maxRecarga) ? maxRecarga : "1000";
            ViewBag.ZonaHoraria = configuraciones.TryGetValue(ConfiguracionPlataforma.ZONA_HORARIA, out var zonaHoraria) ? zonaHoraria : "America/Bogota";

            // Límites de archivos
            ViewBag.LimiteFotoMB = configuraciones.TryGetValue(ConfiguracionPlataforma.LIMITE_TAMANO_FOTO_MB, out var fotoMb) ? fotoMb : "10";
            ViewBag.LimiteVideoMB = configuraciones.TryGetValue(ConfiguracionPlataforma.LIMITE_TAMANO_VIDEO_MB, out var videoMb) ? videoMb : "100";
            ViewBag.LimiteCantidadArchivos = configuraciones.TryGetValue(ConfiguracionPlataforma.LIMITE_CANTIDAD_ARCHIVOS, out var cantArchivos) ? cantArchivos : "10";

            // Distribución del Feed - LadoB Preview
            ViewBag.LadoBPreviewCantidad = configuraciones.TryGetValue(ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD, out var previewCant) ? previewCant : "1";
            ViewBag.LadoBPreviewIntervalo = configuraciones.TryGetValue(ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO, out var previewInt) ? previewInt : "5";

            // ⚡ NUEVO: Límites del Feed
            ViewBag.FeedLimiteLadoA = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_LIMITE_LADOA, out var fla) ? fla : "30";
            ViewBag.FeedLimiteLadoBSuscriptos = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_LIMITE_LADOB_SUSCRIPTOS, out var flbs) ? flbs : "15";
            ViewBag.FeedLimiteLadoBPropio = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_LIMITE_LADOB_PROPIO, out var flbp) ? flbp : "10";
            ViewBag.FeedLimiteComprado = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_LIMITE_COMPRADO, out var flc) ? flc : "10";
            ViewBag.FeedLimiteTotal = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_LIMITE_TOTAL, out var flt) ? flt : "50";

            // ⚡ NUEVO: Descubrimiento del Feed
            ViewBag.FeedDescubrimientoLadoA = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOA_CANTIDAD, out var fdla) ? fdla : "5";
            ViewBag.FeedDescubrimientoLadoB = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOB_CANTIDAD, out var fdlb) ? fdlb : "5";
            ViewBag.FeedDescubrimientoUsuarios = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_USUARIOS_CANTIDAD, out var fdu) ? fdu : "5";
            ViewBag.FeedMaxPostsConsecutivos = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_MAX_POSTS_CONSECUTIVOS_CREADOR, out var fmpc) ? fmpc : "2";

            // ⚡ NUEVO: Anuncios del Feed
            ViewBag.FeedAnunciosCantidad = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_ANUNCIOS_CANTIDAD, out var fac) ? fac : "3";
            ViewBag.FeedAnunciosIntervalo = configuraciones.TryGetValue(ConfiguracionPlataforma.FEED_ANUNCIOS_INTERVALO, out var fai) ? fai : "8";

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarComision(decimal comisionVal)
        {
            TempData["Success"] = $"Comision actualizada a {comisionVal}%";
            return RedirectToAction(nameof(Configuracion));
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarConfiguracionBilletera(
            string comisionBilletera,
            string tiempoProcesoRetiro,
            string montoMinimoRecarga,
            string montoMaximoRecarga)
        {
            try
            {
                var ahora = DateTime.Now;

                // Actualizar cada configuración
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.COMISION_BILLETERA_ELECTRONICA, comisionBilletera, ahora);
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.TIEMPO_PROCESO_RETIRO, tiempoProcesoRetiro, ahora);
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.MONTO_MINIMO_RECARGA, montoMinimoRecarga, ahora);
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.MONTO_MAXIMO_RECARGA, montoMaximoRecarga, ahora);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Configuracion de billetera actualizada correctamente";
            }
            catch (Exception)
            {
                TempData["Error"] = "Error al actualizar la configuracion";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarZonaHoraria(string zonaHoraria)
        {
            try
            {
                var ahora = DateTime.Now;
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.ZONA_HORARIA, zonaHoraria, ahora, "Regional");
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Zona horaria actualizada a {zonaHoraria}";
            }
            catch (Exception)
            {
                TempData["Error"] = "Error al actualizar la zona horaria";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarLimitesContenido(
            int limiteFotoMB,
            int limiteVideoMB,
            int limiteCantidadArchivos)
        {
            try
            {
                var ahora = DateTime.Now;

                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.LIMITE_TAMANO_FOTO_MB, limiteFotoMB.ToString(), ahora, "Archivos");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.LIMITE_TAMANO_VIDEO_MB, limiteVideoMB.ToString(), ahora, "Archivos");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.LIMITE_CANTIDAD_ARCHIVOS, limiteCantidadArchivos.ToString(), ahora, "Archivos");

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Limites actualizados: Fotos {limiteFotoMB}MB, Videos {limiteVideoMB}MB, Max archivos {limiteCantidadArchivos}";
            }
            catch (Exception)
            {
                TempData["Error"] = "Error al actualizar los limites de contenido";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        private async Task ActualizarConfiguracionAsync(string clave, string valor, DateTime fecha, string categoria = "Billetera")
        {
            var config = await _context.ConfiguracionesPlataforma.FirstOrDefaultAsync(c => c.Clave == clave);
            if (config != null)
            {
                config.Valor = valor;
                config.UltimaModificacion = fecha;
            }
            else
            {
                _context.ConfiguracionesPlataforma.Add(new ConfiguracionPlataforma
                {
                    Clave = clave,
                    Valor = valor,
                    Categoria = categoria,
                    UltimaModificacion = fecha
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarDistribucionFeed(
            int ladoBPreviewCantidad,
            int ladoBPreviewIntervalo,
            int anunciosCantidad = 3,
            int anunciosIntervalo = 8)
        {
            try
            {
                // Validar valores
                ladoBPreviewCantidad = Math.Max(1, Math.Min(5, ladoBPreviewCantidad));
                ladoBPreviewIntervalo = Math.Max(2, Math.Min(20, ladoBPreviewIntervalo));
                anunciosCantidad = Math.Max(1, Math.Min(10, anunciosCantidad));
                anunciosIntervalo = Math.Max(3, Math.Min(20, anunciosIntervalo));

                var ahora = DateTime.Now;

                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD, ladoBPreviewCantidad.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO, ladoBPreviewIntervalo.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_ANUNCIOS_CANTIDAD, anunciosCantidad.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_ANUNCIOS_INTERVALO, anunciosIntervalo.ToString(), ahora, "Feed");

                await _context.SaveChangesAsync();

                // ⚡ Invalidar cache de configuración del Feed para aplicar cambios inmediatamente
                _cache.Remove("feed_config_all");

                TempData["Success"] = $"Distribucion actualizada: {ladoBPreviewCantidad} preview(s) cada {ladoBPreviewIntervalo} posts, {anunciosCantidad} anuncio(s) cada {anunciosIntervalo} posts";
            }
            catch (Exception)
            {
                TempData["Error"] = "Error al actualizar la distribucion del feed";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarLimitesFeed(
            int limiteLadoA,
            int limiteLadoBSuscriptos,
            int limiteLadoBPropio,
            int limiteComprado,
            int limiteTotal)
        {
            try
            {
                // Validar valores
                limiteLadoA = Math.Max(10, Math.Min(100, limiteLadoA));
                limiteLadoBSuscriptos = Math.Max(5, Math.Min(50, limiteLadoBSuscriptos));
                limiteLadoBPropio = Math.Max(5, Math.Min(30, limiteLadoBPropio));
                limiteComprado = Math.Max(5, Math.Min(30, limiteComprado));
                limiteTotal = Math.Max(20, Math.Min(100, limiteTotal));

                var ahora = DateTime.Now;

                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_LIMITE_LADOA, limiteLadoA.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_LIMITE_LADOB_SUSCRIPTOS, limiteLadoBSuscriptos.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_LIMITE_LADOB_PROPIO, limiteLadoBPropio.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_LIMITE_COMPRADO, limiteComprado.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_LIMITE_TOTAL, limiteTotal.ToString(), ahora, "Feed");

                await _context.SaveChangesAsync();

                // ⚡ Invalidar cache de configuración del Feed para aplicar cambios inmediatamente
                _cache.Remove("feed_config_all");

                TempData["Success"] = "Limites del Feed actualizados correctamente";
            }
            catch (Exception)
            {
                TempData["Error"] = "Error al actualizar los limites del feed";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarDescubrimientoFeed(
            int descubrimientoLadoA,
            int descubrimientoLadoB,
            int descubrimientoUsuarios,
            int maxPostsConsecutivos)
        {
            try
            {
                // Validar valores
                descubrimientoLadoA = Math.Max(0, Math.Min(15, descubrimientoLadoA));
                descubrimientoLadoB = Math.Max(0, Math.Min(15, descubrimientoLadoB));
                descubrimientoUsuarios = Math.Max(3, Math.Min(10, descubrimientoUsuarios));
                maxPostsConsecutivos = Math.Max(1, Math.Min(5, maxPostsConsecutivos));

                var ahora = DateTime.Now;

                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOA_CANTIDAD, descubrimientoLadoA.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOB_CANTIDAD, descubrimientoLadoB.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_USUARIOS_CANTIDAD, descubrimientoUsuarios.ToString(), ahora, "Feed");
                await ActualizarConfiguracionAsync(ConfiguracionPlataforma.FEED_MAX_POSTS_CONSECUTIVOS_CREADOR, maxPostsConsecutivos.ToString(), ahora, "Feed");

                await _context.SaveChangesAsync();

                // ⚡ Invalidar cache de configuración del Feed para aplicar cambios inmediatamente
                _cache.Remove("feed_config_all");

                TempData["Success"] = $"Descubrimiento actualizado: LadoA={descubrimientoLadoA}, LadoB={descubrimientoLadoB}, Usuarios={descubrimientoUsuarios}, MaxConsecutivos={maxPostsConsecutivos}";
            }
            catch (Exception)
            {
                TempData["Error"] = "Error al actualizar el descubrimiento del feed";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        // ========================================
        // COMISIONES POR USUARIO
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarComisionUsuario(
            string userId,
            decimal comisionRetiro,
            decimal montoMinimoRetiro,
            bool usarRetencionPais,
            decimal? retencionImpuestos)
        {
            var usuario = await _userManager.FindByIdAsync(userId);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction(nameof(Usuarios));
            }

            // Validar rangos
            if (comisionRetiro < 0 || comisionRetiro > 100)
            {
                TempData["Error"] = "La comisión debe estar entre 0% y 100%";
                return RedirectToAction(nameof(Usuarios));
            }

            if (montoMinimoRetiro < 0)
            {
                TempData["Error"] = "El monto mínimo no puede ser negativo";
                return RedirectToAction(nameof(Usuarios));
            }

            if (retencionImpuestos.HasValue && (retencionImpuestos < 0 || retencionImpuestos > 100))
            {
                TempData["Error"] = "La retención debe estar entre 0% y 100%";
                return RedirectToAction(nameof(Usuarios));
            }

            usuario.ComisionRetiro = comisionRetiro;
            usuario.MontoMinimoRetiro = montoMinimoRetiro;
            usuario.UsarRetencionPais = usarRetencionPais;
            usuario.RetencionImpuestos = usarRetencionPais ? null : retencionImpuestos;

            var result = await _userManager.UpdateAsync(usuario);
            if (result.Succeeded)
            {
                var retencionMsg = usarRetencionPais
                    ? "Retención: según país"
                    : $"Retención: {retencionImpuestos}%";
                TempData["Success"] = $"Configuración de {usuario.NombreCompleto} actualizada: Comisión {comisionRetiro}% - Mínimo ${montoMinimoRetiro} - {retencionMsg}";
            }
            else
            {
                TempData["Error"] = "Error al actualizar la configuración";
            }

            return RedirectToAction(nameof(Usuarios));
        }

        // ========================================
        // TASAS DE CAMBIO
        // ========================================

        public async Task<IActionResult> TasasCambio()
        {
            var tasas = await _context.TasasCambio.OrderBy(t => t.CodigoMoneda).ToListAsync();

            // Si no hay tasas, crear las por defecto
            if (!tasas.Any())
            {
                foreach (var moneda in MonedasSoportadas.Monedas)
                {
                    var tasa = new TasaCambio
                    {
                        CodigoMoneda = moneda.Key,
                        NombreMoneda = moneda.Value.Nombre,
                        Simbolo = moneda.Value.Simbolo,
                        TasaVsUSD = moneda.Value.TasaDefault,
                        Activa = true,
                        UltimaActualizacion = DateTime.Now
                    };
                    _context.TasasCambio.Add(tasa);
                }
                await _context.SaveChangesAsync();
                tasas = await _context.TasasCambio.OrderBy(t => t.CodigoMoneda).ToListAsync();
            }

            return View(tasas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarTasaCambio(int id, decimal tasaVsUSD, bool activa)
        {
            var tasa = await _context.TasasCambio.FindAsync(id);
            if (tasa == null)
            {
                TempData["Error"] = "Tasa no encontrada";
                return RedirectToAction(nameof(TasasCambio));
            }

            if (tasaVsUSD <= 0)
            {
                TempData["Error"] = "La tasa debe ser mayor a 0";
                return RedirectToAction(nameof(TasasCambio));
            }

            tasa.TasaVsUSD = tasaVsUSD;
            tasa.Activa = activa;
            tasa.UltimaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Tasa de {tasa.CodigoMoneda} actualizada: 1 USD = {tasaVsUSD} {tasa.CodigoMoneda}";

            return RedirectToAction(nameof(TasasCambio));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarTasaCambio(string codigoMoneda, string nombreMoneda, string simbolo, decimal tasaVsUSD)
        {
            if (string.IsNullOrEmpty(codigoMoneda) || string.IsNullOrEmpty(nombreMoneda))
            {
                TempData["Error"] = "Código y nombre de moneda son requeridos";
                return RedirectToAction(nameof(TasasCambio));
            }

            // Verificar si ya existe
            var existente = await _context.TasasCambio.FirstOrDefaultAsync(t => t.CodigoMoneda == codigoMoneda.ToUpper());
            if (existente != null)
            {
                TempData["Error"] = $"La moneda {codigoMoneda} ya existe";
                return RedirectToAction(nameof(TasasCambio));
            }

            var tasa = new TasaCambio
            {
                CodigoMoneda = codigoMoneda.ToUpper(),
                NombreMoneda = nombreMoneda,
                Simbolo = string.IsNullOrEmpty(simbolo) ? "$" : simbolo,
                TasaVsUSD = tasaVsUSD > 0 ? tasaVsUSD : 1,
                Activa = true,
                UltimaActualizacion = DateTime.Now
            };

            _context.TasasCambio.Add(tasa);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Moneda {codigoMoneda} agregada correctamente";
            return RedirectToAction(nameof(TasasCambio));
        }

        // ========================================
        // RETENCIONES POR PAÍS
        // ========================================

        public async Task<IActionResult> RetencionesPaises()
        {
            var retenciones = await _context.RetencionesPaises.OrderBy(r => r.NombrePais).ToListAsync();

            // Si no hay retenciones, crear las predeterminadas
            if (!retenciones.Any())
            {
                foreach (var pais in RetencionesPredeterminadas.Paises)
                {
                    var retencion = new RetencionPais
                    {
                        CodigoPais = pais.Key,
                        NombrePais = pais.Value.Nombre,
                        PorcentajeRetencion = pais.Value.Retencion,
                        Descripcion = pais.Value.Descripcion,
                        Activo = true,
                        UltimaActualizacion = DateTime.Now
                    };
                    _context.RetencionesPaises.Add(retencion);
                }
                await _context.SaveChangesAsync();
                retenciones = await _context.RetencionesPaises.OrderBy(r => r.NombrePais).ToListAsync();
            }

            return View(retenciones);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarRetencionPais(int id, decimal porcentajeRetencion, bool activo, string? descripcion)
        {
            var retencion = await _context.RetencionesPaises.FindAsync(id);
            if (retencion == null)
            {
                TempData["Error"] = "Retención no encontrada";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            if (porcentajeRetencion < 0 || porcentajeRetencion > 100)
            {
                TempData["Error"] = "El porcentaje debe estar entre 0% y 100%";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            retencion.PorcentajeRetencion = porcentajeRetencion;
            retencion.Activo = activo;
            retencion.Descripcion = descripcion;
            retencion.UltimaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Retención de {retencion.NombrePais} actualizada: {porcentajeRetencion}%";

            return RedirectToAction(nameof(RetencionesPaises));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarRetencionPais(string codigoPais, string nombrePais, decimal porcentajeRetencion, string? descripcion)
        {
            if (string.IsNullOrEmpty(codigoPais) || string.IsNullOrEmpty(nombrePais))
            {
                TempData["Error"] = "Código y nombre del país son requeridos";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            // Verificar si ya existe
            var existente = await _context.RetencionesPaises.FirstOrDefaultAsync(r => r.CodigoPais == codigoPais.ToUpper());
            if (existente != null)
            {
                TempData["Error"] = $"El país {codigoPais} ya existe";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            var retencion = new RetencionPais
            {
                CodigoPais = codigoPais.ToUpper(),
                NombrePais = nombrePais,
                PorcentajeRetencion = porcentajeRetencion >= 0 && porcentajeRetencion <= 100 ? porcentajeRetencion : 0,
                Descripcion = descripcion,
                Activo = true,
                UltimaActualizacion = DateTime.Now
            };

            _context.RetencionesPaises.Add(retencion);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"País {nombrePais} agregado correctamente con retención de {porcentajeRetencion}%";
            return RedirectToAction(nameof(RetencionesPaises));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarRetencionPais(int id)
        {
            var retencion = await _context.RetencionesPaises.FindAsync(id);
            if (retencion == null)
            {
                TempData["Error"] = "País no encontrado";
                return RedirectToAction(nameof(RetencionesPaises));
            }

            var nombrePais = retencion.NombrePais;
            _context.RetencionesPaises.Remove(retencion);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"País {nombrePais} eliminado correctamente";
            return RedirectToAction(nameof(RetencionesPaises));
        }

        // Método helper para obtener la retención de un usuario
        public async Task<decimal> ObtenerRetencionUsuario(ApplicationUser usuario)
        {
            // Si no usa retención del país, devolver la retención personalizada
            if (!usuario.UsarRetencionPais && usuario.RetencionImpuestos.HasValue)
            {
                return usuario.RetencionImpuestos.Value;
            }

            // Buscar la retención del país del usuario
            if (!string.IsNullOrEmpty(usuario.Pais))
            {
                var retencionPais = await _context.RetencionesPaises
                    .FirstOrDefaultAsync(r => r.CodigoPais == usuario.Pais && r.Activo);

                if (retencionPais != null)
                {
                    return retencionPais.PorcentajeRetencion;
                }
            }

            // Si no hay país configurado o no existe la retención, devolver 0
            return 0;
        }

        // ========================================
        // AGENCIAS
        // ========================================
        public async Task<IActionResult> Agencias(string estado = "")
        {
            var query = _context.Agencias
                .Include(a => a.Usuario)
                .Include(a => a.Anuncios)
                .AsQueryable();

            if (!string.IsNullOrEmpty(estado))
            {
                if (Enum.TryParse<EstadoAgencia>(estado, out var estadoEnum))
                {
                    query = query.Where(a => a.Estado == estadoEnum);
                }
            }

            var agencias = await query
                .OrderByDescending(a => a.FechaRegistro)
                .ToListAsync();

            ViewBag.EstadoFiltro = estado;
            ViewBag.AgenciasPendientes = await _context.Agencias.CountAsync(a => a.Estado == EstadoAgencia.Pendiente);
            ViewBag.AgenciasActivas = await _context.Agencias.CountAsync(a => a.Estado == EstadoAgencia.Activa);
            ViewBag.AgenciasSuspendidas = await _context.Agencias.CountAsync(a => a.Estado == EstadoAgencia.Suspendida);

            return View(agencias);
        }

        public async Task<IActionResult> DetalleAgencia(int id)
        {
            var agencia = await _context.Agencias
                .Include(a => a.Usuario)
                .Include(a => a.Anuncios)
                .Include(a => a.Transacciones)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (agencia == null)
            {
                return NotFound();
            }

            ViewBag.AnunciosActivos = agencia.Anuncios.Count(a => a.Estado == EstadoAnuncio.Activo);
            ViewBag.AnunciosPendientes = agencia.Anuncios.Count(a => a.Estado == EstadoAnuncio.EnRevision);
            ViewBag.TotalImpresiones = agencia.Anuncios.Sum(a => a.Impresiones);
            ViewBag.TotalClics = agencia.Anuncios.Sum(a => a.Clics);

            return View(agencia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarAgencia(int id)
        {
            var agencia = await _context.Agencias.FindAsync(id);
            if (agencia == null)
            {
                TempData["Error"] = "Agencia no encontrada.";
                return RedirectToAction(nameof(Agencias));
            }

            agencia.Estado = EstadoAgencia.Activa;
            agencia.EstaVerificada = true;
            agencia.FechaAprobacion = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"La agencia '{agencia.NombreEmpresa}' ha sido aprobada exitosamente.";
            return RedirectToAction(nameof(Agencias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarAgencia(int id, string razon)
        {
            var agencia = await _context.Agencias.FindAsync(id);
            if (agencia == null)
            {
                TempData["Error"] = "Agencia no encontrada.";
                return RedirectToAction(nameof(Agencias));
            }

            agencia.Estado = EstadoAgencia.Rechazada;
            agencia.MotivoRechazo = razon;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"La agencia '{agencia.NombreEmpresa}' ha sido rechazada.";
            return RedirectToAction(nameof(Agencias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspenderAgencia(int id, string razon)
        {
            var agencia = await _context.Agencias.FindAsync(id);
            if (agencia == null)
            {
                TempData["Error"] = "Agencia no encontrada.";
                return RedirectToAction(nameof(Agencias));
            }

            agencia.Estado = EstadoAgencia.Suspendida;
            agencia.MotivoSuspension = razon;
            agencia.FechaSuspension = DateTime.Now;

            // Pausar todos los anuncios activos
            var anunciosActivos = await _context.Anuncios
                .Where(a => a.AgenciaId == id && a.Estado == EstadoAnuncio.Activo)
                .ToListAsync();

            foreach (var anuncio in anunciosActivos)
            {
                anuncio.Estado = EstadoAnuncio.Pausado;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"La agencia '{agencia.NombreEmpresa}' ha sido suspendida.";
            return RedirectToAction(nameof(Agencias));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivarAgencia(int id)
        {
            var agencia = await _context.Agencias.FindAsync(id);
            if (agencia == null)
            {
                TempData["Error"] = "Agencia no encontrada.";
                return RedirectToAction(nameof(Agencias));
            }

            agencia.Estado = EstadoAgencia.Activa;
            agencia.MotivoSuspension = null;
            agencia.FechaSuspension = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"La agencia '{agencia.NombreEmpresa}' ha sido reactivada.";
            return RedirectToAction(nameof(Agencias));
        }

        // ========================================
        // GESTION DE ANUNCIOS (desde Admin)
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarAnuncio(int id)
        {
            var anuncio = await _context.Anuncios.FindAsync(id);
            if (anuncio == null)
            {
                TempData["Error"] = "Anuncio no encontrado.";
                return RedirectToAction(nameof(Agencias));
            }

            anuncio.Estado = EstadoAnuncio.Activo;
            anuncio.FechaInicio = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"El anuncio '{anuncio.Titulo}' ha sido aprobado y esta activo.";
            return RedirectToAction(nameof(DetalleAgencia), new { id = anuncio.AgenciaId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarAnuncio(int id, string razon)
        {
            var anuncio = await _context.Anuncios.FindAsync(id);
            if (anuncio == null)
            {
                TempData["Error"] = "Anuncio no encontrado.";
                return RedirectToAction(nameof(Agencias));
            }

            anuncio.Estado = EstadoAnuncio.Rechazado;
            anuncio.MotivoRechazo = razon;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"El anuncio '{anuncio.Titulo}' ha sido rechazado.";
            return RedirectToAction(nameof(DetalleAgencia), new { id = anuncio.AgenciaId });
        }

        // ========================================
        // METRICAS DEL SERVIDOR
        // ========================================
        [HttpGet]
        public async Task<IActionResult> GetServerMetrics()
        {
            try
            {
                var metrics = await _serverMetrics.GetMetricsAsync();
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        cpu = new
                        {
                            usage = Math.Round(metrics.CpuUsagePercent, 1),
                            cores = metrics.ProcessorCount
                        },
                        memory = new
                        {
                            used = metrics.MemoryUsedMB,
                            total = metrics.MemoryTotalMB,
                            usagePercent = metrics.MemoryUsagePercent
                        },
                        disks = metrics.Disks.Select(d => new
                        {
                            name = d.DriveName,
                            label = d.DriveLabel,
                            totalGB = d.TotalGB,
                            usedGB = d.UsedGB,
                            freeGB = d.FreeGB,
                            usagePercent = d.UsagePercent,
                            format = d.DriveFormat
                        }),
                        server = new
                        {
                            name = metrics.ServerName,
                            os = metrics.OsDescription,
                            uptime = new
                            {
                                days = metrics.Uptime.Days,
                                hours = metrics.Uptime.Hours,
                                minutes = metrics.Uptime.Minutes
                            }
                        },
                        timestamp = metrics.Timestamp.ToString("HH:mm:ss")
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        public IActionResult Servidor()
        {
            return View();
        }

        // ========================================
        // BIBLIOTECA MUSICAL
        // ========================================
        public async Task<IActionResult> Musica()
        {
            var pistas = await _context.PistasMusica
                .OrderByDescending(p => p.FechaCreacion)
                .ToListAsync();

            ViewBag.TotalPistas = pistas.Count;
            ViewBag.PistasActivas = pistas.Count(p => p.Activo);
            ViewBag.TotalGeneros = pistas.Select(p => p.Genero).Distinct().Count();
            ViewBag.TotalUsos = pistas.Sum(p => p.ContadorUsos);

            return View(pistas);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPista(int id)
        {
            var pista = await _context.PistasMusica.FindAsync(id);
            if (pista == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = pista.Id,
                titulo = pista.Titulo,
                artista = pista.Artista,
                album = pista.Album,
                genero = pista.Genero,
                duracion = pista.Duracion,
                bpm = pista.Bpm,
                energia = pista.Energia,
                estadoAnimo = pista.EstadoAnimo,
                rutaArchivo = pista.RutaArchivo,
                rutaPortada = pista.RutaPortada,
                activo = pista.Activo
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarPista(
            int Id,
            string Titulo,
            string Artista,
            string? Album,
            string Genero,
            int Duracion,
            int? Bpm,
            string? Energia,
            string? EstadoAnimo,
            IFormFile? ArchivoAudio,
            IFormFile? ImagenPortada)
        {
            try
            {
                PistaMusical pista;
                bool esNueva = Id == 0;

                if (esNueva)
                {
                    pista = new PistaMusical();
                }
                else
                {
                    pista = await _context.PistasMusica.FindAsync(Id);
                    if (pista == null)
                    {
                        TempData["Error"] = "Pista no encontrada.";
                        return RedirectToAction(nameof(Musica));
                    }
                }

                // Actualizar propiedades
                pista.Titulo = Titulo;
                pista.Artista = Artista;
                pista.Album = Album;
                pista.Genero = Genero;
                pista.Duracion = Duracion;
                pista.Bpm = Bpm;
                pista.Energia = Energia;
                pista.EstadoAnimo = EstadoAnimo;

                // Procesar archivo de audio con validación de seguridad
                if (ArchivoAudio != null && ArchivoAudio.Length > 0)
                {
                    // Validar que sea un archivo de audio válido
                    var audioValidation = await _fileValidationService.ValidarAudioAsync(ArchivoAudio);
                    if (!audioValidation.EsValido)
                    {
                        _logger.LogWarning("Intento de subir archivo de audio inválido: {Error}", audioValidation.MensajeError);
                        TempData["Error"] = $"El archivo de audio no es válido: {audioValidation.MensajeError}";
                        return RedirectToAction(nameof(Musica));
                    }

                    // Validar tamaño máximo (50MB para audio)
                    if (ArchivoAudio.Length > 50 * 1024 * 1024)
                    {
                        TempData["Error"] = "El archivo de audio no puede superar los 50MB.";
                        return RedirectToAction(nameof(Musica));
                    }

                    var audioFileName = $"{Guid.NewGuid()}{audioValidation.Extension}";
                    var audioPath = Path.Combine("wwwroot", "audio", "biblioteca", audioFileName);

                    // Crear directorio si no existe
                    var audioDir = Path.GetDirectoryName(audioPath);
                    if (!string.IsNullOrEmpty(audioDir) && !Directory.Exists(audioDir))
                    {
                        Directory.CreateDirectory(audioDir);
                    }

                    using (var stream = new FileStream(audioPath, FileMode.Create))
                    {
                        await ArchivoAudio.CopyToAsync(stream);
                    }

                    // Eliminar archivo anterior si existe
                    if (!string.IsNullOrEmpty(pista.RutaArchivo))
                    {
                        var oldPath = Path.Combine("wwwroot", pista.RutaArchivo.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    pista.RutaArchivo = $"/audio/biblioteca/{audioFileName}";
                }
                else if (esNueva)
                {
                    TempData["Error"] = "El archivo de audio es requerido para una nueva pista.";
                    return RedirectToAction(nameof(Musica));
                }

                // Procesar imagen de portada con validación de seguridad
                if (ImagenPortada != null && ImagenPortada.Length > 0)
                {
                    // Validar que sea una imagen válida
                    var imagenValidation = await _fileValidationService.ValidarImagenAsync(ImagenPortada);
                    if (!imagenValidation.EsValido)
                    {
                        _logger.LogWarning("Intento de subir imagen de portada inválida: {Error}", imagenValidation.MensajeError);
                        TempData["Error"] = $"La imagen de portada no es válida: {imagenValidation.MensajeError}";
                        return RedirectToAction(nameof(Musica));
                    }

                    // Validar tamaño máximo (10MB para imagen)
                    if (ImagenPortada.Length > 10 * 1024 * 1024)
                    {
                        TempData["Error"] = "La imagen de portada no puede superar los 10MB.";
                        return RedirectToAction(nameof(Musica));
                    }

                    var coverFileName = $"{Guid.NewGuid()}{imagenValidation.Extension}";
                    var coverPath = Path.Combine("wwwroot", "audio", "biblioteca", "covers", coverFileName);

                    // Crear directorio si no existe
                    var coverDir = Path.GetDirectoryName(coverPath);
                    if (!string.IsNullOrEmpty(coverDir) && !Directory.Exists(coverDir))
                    {
                        Directory.CreateDirectory(coverDir);
                    }

                    using (var stream = new FileStream(coverPath, FileMode.Create))
                    {
                        await ImagenPortada.CopyToAsync(stream);
                    }

                    // Eliminar portada anterior si existe
                    if (!string.IsNullOrEmpty(pista.RutaPortada))
                    {
                        var oldPath = Path.Combine("wwwroot", pista.RutaPortada.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    pista.RutaPortada = $"/audio/biblioteca/covers/{coverFileName}";
                }

                if (esNueva)
                {
                    pista.FechaCreacion = DateTime.UtcNow;
                    _context.PistasMusica.Add(pista);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = esNueva ? "Pista agregada exitosamente." : "Pista actualizada exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al guardar la pista: {ex.Message}";
            }

            return RedirectToAction(nameof(Musica));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPista(int id)
        {
            var pista = await _context.PistasMusica.FindAsync(id);
            if (pista == null)
            {
                TempData["Error"] = "Pista no encontrada.";
                return RedirectToAction(nameof(Musica));
            }

            try
            {
                // Eliminar archivo de audio
                if (!string.IsNullOrEmpty(pista.RutaArchivo))
                {
                    var audioPath = Path.Combine("wwwroot", pista.RutaArchivo.TrimStart('/'));
                    if (System.IO.File.Exists(audioPath))
                    {
                        System.IO.File.Delete(audioPath);
                    }
                }

                // Eliminar imagen de portada
                if (!string.IsNullOrEmpty(pista.RutaPortada))
                {
                    var coverPath = Path.Combine("wwwroot", pista.RutaPortada.TrimStart('/'));
                    if (System.IO.File.Exists(coverPath))
                    {
                        System.IO.File.Delete(coverPath);
                    }
                }

                _context.PistasMusica.Remove(pista);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Pista '{pista.Titulo}' eliminada exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar la pista: {ex.Message}";
            }

            return RedirectToAction(nameof(Musica));
        }

        [HttpPost]
        public async Task<IActionResult> TogglePistaEstado([FromBody] TogglePistaRequest request)
        {
            var pista = await _context.PistasMusica.FindAsync(request.Id);
            if (pista == null)
            {
                return Json(new { success = false, message = "Pista no encontrada" });
            }

            pista.Activo = request.Activo;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class TogglePistaRequest
        {
            public int Id { get; set; }
            public bool Activo { get; set; }
        }

        // ========================================
        // IMPORTADOR DE MÚSICA
        // ========================================
        public IActionResult ImportarMusica()
        {
            // Leer Client ID de Jamendo desde configuración
            ViewBag.JamendoClientId = _configuration["Jamendo:ClientId"] ?? "";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EscanearCarpetaMusica([FromBody] EscanearCarpetaRequest request)
        {
            try
            {
                var carpetaRuta = Path.Combine(Directory.GetCurrentDirectory(), request.Carpeta);

                if (!Directory.Exists(carpetaRuta))
                {
                    // Crear la carpeta si no existe
                    Directory.CreateDirectory(carpetaRuta);
                    return Json(new { success = false, message = $"La carpeta fue creada. Coloca archivos MP3 en: {carpetaRuta}" });
                }

                var archivosMP3 = Directory.GetFiles(carpetaRuta, "*.mp3", SearchOption.TopDirectoryOnly);

                if (archivosMP3.Length == 0)
                {
                    return Json(new { success = false, message = "No se encontraron archivos MP3 en la carpeta." });
                }

                var tracks = new List<object>();

                foreach (var archivo in archivosMP3)
                {
                    try
                    {
                        using var tagFile = TagLib.File.Create(archivo);
                        var tag = tagFile.Tag;

                        string? portadaBase64 = null;
                        if (tag.Pictures?.Length > 0)
                        {
                            var picture = tag.Pictures[0];
                            portadaBase64 = Convert.ToBase64String(picture.Data.Data);
                        }

                        // Detectar género desde tag o inferir del nombre
                        var genero = !string.IsNullOrEmpty(tag.FirstGenre)
                            ? MapearGenero(tag.FirstGenre)
                            : "Pop";

                        tracks.Add(new
                        {
                            archivo = Path.GetFileName(archivo),
                            rutaCompleta = archivo,
                            rutaTemporal = $"/audio/importar/{Path.GetFileName(archivo)}",
                            titulo = !string.IsNullOrEmpty(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(archivo),
                            artista = !string.IsNullOrEmpty(tag.FirstPerformer) ? tag.FirstPerformer : "Artista Desconocido",
                            album = tag.Album,
                            genero = genero,
                            duracion = (int)tagFile.Properties.Duration.TotalSeconds,
                            bpm = tag.BeatsPerMinute > 0 ? (int?)tag.BeatsPerMinute : null,
                            anio = tag.Year > 0 ? (int?)tag.Year : null,
                            portadaBase64 = portadaBase64
                        });
                    }
                    catch (Exception)
                    {
                        // Si no se puede leer el archivo, agregar con datos básicos
                        tracks.Add(new
                        {
                            archivo = Path.GetFileName(archivo),
                            rutaCompleta = archivo,
                            rutaTemporal = $"/audio/importar/{Path.GetFileName(archivo)}",
                            titulo = Path.GetFileNameWithoutExtension(archivo),
                            artista = "Artista Desconocido",
                            album = (string?)null,
                            genero = "Pop",
                            duracion = 0,
                            bpm = (int?)null,
                            anio = (int?)null,
                            portadaBase64 = (string?)null
                        });
                    }
                }

                return Json(new { success = true, tracks = tracks });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al escanear: {ex.Message}" });
            }
        }

        private string MapearGenero(string generoOriginal)
        {
            var generoLower = generoOriginal.ToLower();

            // Mapear géneros comunes a los géneros del sistema
            if (generoLower.Contains("pop")) return "Pop";
            if (generoLower.Contains("rock")) return "Rock";
            if (generoLower.Contains("hip") || generoLower.Contains("hop") || generoLower.Contains("rap")) return "Hip Hop";
            if (generoLower.Contains("electro") || generoLower.Contains("edm") || generoLower.Contains("house") || generoLower.Contains("techno")) return "Electronica";
            if (generoLower.Contains("jazz")) return "Jazz";
            if (generoLower.Contains("classic")) return "Clasica";
            if (generoLower.Contains("reggae")) return "Reggaeton";
            if (generoLower.Contains("latin") || generoLower.Contains("salsa") || generoLower.Contains("bachata")) return "Latino";
            if (generoLower.Contains("indie")) return "Indie";
            if (generoLower.Contains("ambient") || generoLower.Contains("chill")) return "Ambiental";
            if (generoLower.Contains("acoustic") || generoLower.Contains("folk")) return "Acustico";
            if (generoLower.Contains("lofi") || generoLower.Contains("lo-fi")) return "Lo-Fi";
            if (generoLower.Contains("cinema") || generoLower.Contains("soundtrack") || generoLower.Contains("epic")) return "Cinematico";
            if (generoLower.Contains("r&b") || generoLower.Contains("soul")) return "R&B";
            if (generoLower.Contains("country")) return "Country";
            if (generoLower.Contains("metal")) return "Metal";
            if (generoLower.Contains("funk")) return "Funk";

            return "Pop"; // Default
        }

        [HttpPost]
        public async Task<IActionResult> ImportarPistaEscaneada([FromBody] ImportarPistaRequest request)
        {
            try
            {
                // Mover archivo de importar a biblioteca
                var nombreArchivo = $"{Guid.NewGuid()}.mp3";
                var rutaDestino = Path.Combine("wwwroot", "audio", "biblioteca", nombreArchivo);
                var rutaOrigen = request.RutaCompleta;

                // Crear directorio si no existe
                var dirDestino = Path.GetDirectoryName(rutaDestino);
                if (!string.IsNullOrEmpty(dirDestino) && !Directory.Exists(dirDestino))
                {
                    Directory.CreateDirectory(dirDestino);
                }

                // Copiar archivo (no mover, por si quiere reimportar)
                System.IO.File.Copy(rutaOrigen, rutaDestino, true);

                // Guardar portada si existe
                string? rutaPortada = null;
                if (!string.IsNullOrEmpty(request.PortadaBase64))
                {
                    var nombrePortada = $"{Guid.NewGuid()}.jpg";
                    var rutaPortadaDestino = Path.Combine("wwwroot", "audio", "biblioteca", "covers", nombrePortada);

                    var dirCovers = Path.GetDirectoryName(rutaPortadaDestino);
                    if (!string.IsNullOrEmpty(dirCovers) && !Directory.Exists(dirCovers))
                    {
                        Directory.CreateDirectory(dirCovers);
                    }

                    var bytesPortada = Convert.FromBase64String(request.PortadaBase64);
                    await System.IO.File.WriteAllBytesAsync(rutaPortadaDestino, bytesPortada);
                    rutaPortada = $"/audio/biblioteca/covers/{nombrePortada}";
                }

                // Crear registro en base de datos
                var pista = new PistaMusical
                {
                    Titulo = request.Titulo ?? "Sin Titulo",
                    Artista = request.Artista ?? "Artista Desconocido",
                    Album = request.Album,
                    Genero = request.Genero ?? "Pop",
                    Duracion = request.Duracion,
                    Bpm = request.Bpm,
                    RutaArchivo = $"/audio/biblioteca/{nombreArchivo}",
                    RutaPortada = rutaPortada,
                    EsLibreDeRegalias = true,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.PistasMusica.Add(pista);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = pista.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // JAMENDO API
        // ========================================
        [HttpPost]
        public IActionResult GuardarJamendoClientId([FromBody] JamendoClientIdRequest request)
        {
            // En producción guardarías esto en la base de datos o en un archivo de configuración seguro
            // Por ahora solo confirmamos que se recibió
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> BuscarJamendoMusic([FromBody] JamendoSearchRequest request)
        {
            try
            {
                using var httpClient = new HttpClient();

                var queryParams = new List<string>
                {
                    $"client_id={request.ClientId}",
                    "format=json",
                    "limit=50",
                    "include=musicinfo",
                    "audioformat=mp32"
                };

                if (!string.IsNullOrEmpty(request.Query))
                {
                    queryParams.Add($"namesearch={Uri.EscapeDataString(request.Query)}");
                }

                if (!string.IsNullOrEmpty(request.Tags))
                {
                    queryParams.Add($"tags={Uri.EscapeDataString(request.Tags)}");
                }

                var url = $"https://api.jamendo.com/v3.0/tracks/?{string.Join("&", queryParams)}";

                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Error de API: {response.StatusCode}" });
                }

                var content = await response.Content.ReadAsStringAsync();
                var jamendoResponse = System.Text.Json.JsonSerializer.Deserialize<JamendoMusicResponse>(content,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (jamendoResponse?.Results == null || jamendoResponse.Results.Count == 0)
                {
                    return Json(new { success = false, message = "No se encontraron resultados." });
                }

                var tracks = jamendoResponse.Results.Select(r => new
                {
                    id = r.Id,
                    title = r.Name ?? "Sin titulo",
                    artistName = r.Artist_name ?? "Desconocido",
                    albumName = r.Album_name,
                    albumImage = r.Album_image,
                    duration = r.Duration,
                    audioUrl = r.Audio,
                    audioDownload = r.Audiodownload,
                    license = r.License_ccurl,
                    releaseDate = r.Releasedate
                }).ToList();

                return Json(new { success = true, tracks = tracks });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DescargarJamendoTrack([FromBody] JamendoTrackDownload request)
        {
            try
            {
                using var httpClient = new HttpClient();

                // Descargar el archivo de audio
                var audioBytes = await httpClient.GetByteArrayAsync(request.AudioDownload);

                // Generar nombre único
                var nombreArchivo = $"{Guid.NewGuid()}.mp3";
                var rutaArchivo = Path.Combine("wwwroot", "audio", "biblioteca", nombreArchivo);

                // Crear directorio si no existe
                var dirDestino = Path.GetDirectoryName(rutaArchivo);
                if (!string.IsNullOrEmpty(dirDestino) && !Directory.Exists(dirDestino))
                {
                    Directory.CreateDirectory(dirDestino);
                }

                // Guardar archivo de audio
                await System.IO.File.WriteAllBytesAsync(rutaArchivo, audioBytes);

                // Descargar y guardar portada si existe
                string? rutaPortada = null;
                if (!string.IsNullOrEmpty(request.AlbumImage))
                {
                    try
                    {
                        var coverBytes = await httpClient.GetByteArrayAsync(request.AlbumImage);
                        var nombrePortada = $"{Guid.NewGuid()}.jpg";
                        var rutaPortadaArchivo = Path.Combine("wwwroot", "audio", "biblioteca", "covers", nombrePortada);

                        var dirCovers = Path.GetDirectoryName(rutaPortadaArchivo);
                        if (!string.IsNullOrEmpty(dirCovers) && !Directory.Exists(dirCovers))
                        {
                            Directory.CreateDirectory(dirCovers);
                        }

                        await System.IO.File.WriteAllBytesAsync(rutaPortadaArchivo, coverBytes);
                        rutaPortada = $"/audio/biblioteca/covers/{nombrePortada}";
                    }
                    catch
                    {
                        // Si falla la descarga de portada, continuar sin ella
                    }
                }

                // Crear registro en base de datos
                var pista = new PistaMusical
                {
                    Titulo = request.Title ?? "Sin Titulo",
                    Artista = request.ArtistName ?? "Jamendo",
                    Album = request.AlbumName,
                    Genero = "Pop", // Jamendo no siempre proporciona género
                    Duracion = request.Duration,
                    RutaArchivo = $"/audio/biblioteca/{nombreArchivo}",
                    RutaPortada = rutaPortada,
                    EsLibreDeRegalias = true,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.PistasMusica.Add(pista);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = pista.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al descargar: {ex.Message}" });
            }
        }

        // ========================================
        // ACTUALIZAR PISTAS DESDE EXCEL
        // ========================================

        /// <summary>
        /// Actualiza las pistas existentes con datos del Excel (artist_name, popularity, genre)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ActualizarPistasDesdeExcel(IFormFile archivoExcel)
        {
            if (archivoExcel == null || archivoExcel.Length == 0)
            {
                return Json(new { success = false, message = "No se recibió el archivo Excel" });
            }

            try
            {
                var actualizadas = 0;
                var noEncontradas = new List<string>();
                var errores = new List<string>();

                using var stream = archivoExcel.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Obtener todas las pistas de la base de datos
                var pistasDb = await _context.PistasMusica.ToListAsync();

                // Leer filas del Excel (empezando desde la fila 2, la 1 es el header)
                var lastRowUsed = worksheet.LastRowUsed()?.RowNumber() ?? 1;

                for (int row = 2; row <= lastRowUsed; row++)
                {
                    try
                    {
                        var trackName = worksheet.Cell(row, 2).GetString()?.Trim(); // track_name
                        var artistName = worksheet.Cell(row, 4).GetString()?.Trim(); // artist_name
                        var popularityStr = worksheet.Cell(row, 8).GetString()?.Trim(); // popularity
                        var genre = worksheet.Cell(row, 23).GetString()?.Trim(); // genre

                        if (string.IsNullOrEmpty(trackName))
                            continue;

                        // Buscar pista por título (case-insensitive y parcial)
                        var pista = pistasDb.FirstOrDefault(p =>
                            p.Titulo.Equals(trackName, StringComparison.OrdinalIgnoreCase) ||
                            p.Titulo.Contains(trackName, StringComparison.OrdinalIgnoreCase) ||
                            trackName.Contains(p.Titulo, StringComparison.OrdinalIgnoreCase));

                        if (pista == null)
                        {
                            // No agregar todos a la lista, solo los primeros 20
                            if (noEncontradas.Count < 20)
                                noEncontradas.Add(trackName);
                            continue;
                        }

                        // Actualizar campos
                        var cambios = false;

                        if (!string.IsNullOrEmpty(artistName) && pista.Artista != artistName)
                        {
                            pista.Artista = artistName;
                            cambios = true;
                        }

                        if (int.TryParse(popularityStr, out int popularity) && pista.ContadorUsos != popularity)
                        {
                            pista.ContadorUsos = popularity;
                            cambios = true;
                        }

                        if (!string.IsNullOrEmpty(genre))
                        {
                            var generoMapeado = MapearGeneroExcel(genre);
                            if (pista.Genero != generoMapeado)
                            {
                                pista.Genero = generoMapeado;
                                cambios = true;
                            }
                        }

                        if (cambios)
                            actualizadas++;
                    }
                    catch (Exception ex)
                    {
                        if (errores.Count < 10)
                            errores.Add($"Fila {row}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Se actualizaron {actualizadas} pistas",
                    actualizadas,
                    noEncontradas = noEncontradas.Take(20).ToList(),
                    totalNoEncontradas = noEncontradas.Count,
                    errores
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al procesar Excel: {ex.Message}" });
            }
        }

        /// <summary>
        /// Mapea géneros del Excel a los géneros del sistema
        /// </summary>
        private string MapearGeneroExcel(string generoExcel)
        {
            if (string.IsNullOrEmpty(generoExcel))
                return "Pop";

            var generoLower = generoExcel.ToLower();

            return generoLower switch
            {
                var g when g.Contains("tiktok") || g.Contains("dance") || g.Contains("edm") => "Electrónica",
                var g when g.Contains("hip hop") || g.Contains("hiphop") || g.Contains("rap") => "Hip Hop",
                var g when g.Contains("r&b") || g.Contains("rnb") || g.Contains("soul") => "R&B",
                var g when g.Contains("rock") => "Rock",
                var g when g.Contains("latin") || g.Contains("latino") => "Latino",
                var g when g.Contains("reggaeton") || g.Contains("reggaetón") => "Reggaetón",
                var g when g.Contains("indie") || g.Contains("alternative") => "Indie",
                var g when g.Contains("lofi") || g.Contains("lo-fi") || g.Contains("chill") => "Lo-Fi",
                var g when g.Contains("ambient") || g.Contains("ambiental") => "Ambiental",
                var g when g.Contains("acoustic") || g.Contains("acústico") => "Acústico",
                var g when g.Contains("cinematic") || g.Contains("film") || g.Contains("movie") => "Cinemático",
                var g when g.Contains("electronic") || g.Contains("house") || g.Contains("techno") => "Electrónica",
                var g when g.Contains("pop") => "Pop",
                _ => "Pop" // Default
            };
        }

        // Request/Response classes para el importador
        public class EscanearCarpetaRequest
        {
            public string Carpeta { get; set; } = "";
        }

        public class ImportarPistaRequest
        {
            public string? Archivo { get; set; }
            public string RutaCompleta { get; set; } = "";
            public string? Titulo { get; set; }
            public string? Artista { get; set; }
            public string? Album { get; set; }
            public string? Genero { get; set; }
            public int Duracion { get; set; }
            public int? Bpm { get; set; }
            public string? PortadaBase64 { get; set; }
        }

        public class JamendoClientIdRequest
        {
            public string ClientId { get; set; } = "";
        }

        public class JamendoSearchRequest
        {
            public string ClientId { get; set; } = "";
            public string? Query { get; set; }
            public string? Tags { get; set; }
        }

        public class JamendoTrackDownload
        {
            public string Id { get; set; } = "";
            public string? Title { get; set; }
            public string? ArtistName { get; set; }
            public string? AlbumName { get; set; }
            public string? AlbumImage { get; set; }
            public int Duration { get; set; }
            public string AudioDownload { get; set; } = "";
        }

        public class JamendoMusicResponse
        {
            public JamendoHeaders? Headers { get; set; }
            public List<JamendoTrack> Results { get; set; } = new();
        }

        public class JamendoHeaders
        {
            public string? Status { get; set; }
            public int Code { get; set; }
            public string? Error_message { get; set; }
            public int Results_count { get; set; }
        }

        public class JamendoTrack
        {
            public string Id { get; set; } = "";
            public string? Name { get; set; }
            public int Duration { get; set; }
            public string? Artist_id { get; set; }
            public string? Artist_name { get; set; }
            public string? Album_name { get; set; }
            public string? Album_id { get; set; }
            public string? Album_image { get; set; }
            public string? License_ccurl { get; set; }
            public string? Audio { get; set; }
            public string? Audiodownload { get; set; }
            public string? Releasedate { get; set; }
        }

        // ========================================
        // GESTION DE FEEDBACKS
        // ========================================
        public async Task<IActionResult> Feedbacks(EstadoFeedback? estado = null)
        {
            var query = _context.Feedbacks
                .Include(f => f.Usuario)
                .AsQueryable();

            if (estado.HasValue)
            {
                query = query.Where(f => f.Estado == estado.Value);
            }

            var feedbacks = await query
                .OrderByDescending(f => f.FechaEnvio)
                .ToListAsync();

            ViewBag.FeedbacksPendientes = await _context.Feedbacks.CountAsync(f => f.Estado == EstadoFeedback.Pendiente);
            ViewBag.FeedbacksEnRevision = await _context.Feedbacks.CountAsync(f => f.Estado == EstadoFeedback.EnRevision);
            ViewBag.FeedbacksRespondidos = await _context.Feedbacks.CountAsync(f => f.Estado == EstadoFeedback.Respondido);
            ViewBag.TotalFeedbacks = await _context.Feedbacks.CountAsync();
            ViewBag.EstadoFiltro = estado;

            return View(feedbacks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoFeedback(int id, EstadoFeedback estado)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                TempData["Error"] = "Feedback no encontrado.";
                return RedirectToAction(nameof(Feedbacks));
            }

            feedback.Estado = estado;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Estado del feedback actualizado.";
            return RedirectToAction(nameof(Feedbacks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResponderFeedback(int id, string respuesta)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                TempData["Error"] = "Feedback no encontrado.";
                return RedirectToAction(nameof(Feedbacks));
            }

            if (string.IsNullOrWhiteSpace(respuesta))
            {
                TempData["Error"] = "La respuesta no puede estar vacia.";
                return RedirectToAction(nameof(Feedbacks));
            }

            var admin = await _userManager.GetUserAsync(User);

            feedback.RespuestaAdmin = respuesta;
            feedback.FechaRespuesta = DateTime.Now;
            feedback.AdminId = admin?.Id;
            feedback.Estado = EstadoFeedback.Respondido;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Respuesta enviada exitosamente.";
            return RedirectToAction(nameof(Feedbacks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFeedback(int id)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                TempData["Error"] = "Feedback no encontrado.";
                return RedirectToAction(nameof(Feedbacks));
            }

            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Feedback eliminado.";
            return RedirectToAction(nameof(Feedbacks));
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerFeedback(int id)
        {
            var feedback = await _context.Feedbacks
                .Include(f => f.Usuario)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = feedback.Id,
                nombreUsuario = feedback.NombreUsuario,
                email = feedback.Email,
                tipo = feedback.Tipo.ToString(),
                asunto = feedback.Asunto,
                mensaje = feedback.Mensaje,
                fechaEnvio = feedback.FechaEnvio.ToString("dd/MM/yyyy HH:mm"),
                estado = feedback.Estado.ToString(),
                respuestaAdmin = feedback.RespuestaAdmin,
                fechaRespuesta = feedback.FechaRespuesta?.ToString("dd/MM/yyyy HH:mm"),
                usuarioId = feedback.UsuarioId,
                usuarioFoto = feedback.Usuario?.FotoPerfil
            });
        }

        // ========================================
        // PUBLICIDAD DE LADO
        // ========================================

        /// <summary>
        /// Panel de gestión de publicidad propia de Lado
        /// </summary>
        public async Task<IActionResult> Publicidad()
        {
            var anunciosLado = await _context.Anuncios
                .Where(a => a.EsAnuncioLado)
                .OrderByDescending(a => a.FechaCreacion)
                .ToListAsync();

            // Estadísticas
            ViewBag.TotalAnuncios = anunciosLado.Count;
            ViewBag.AnunciosActivos = anunciosLado.Count(a => a.Estado == EstadoAnuncio.Activo);
            ViewBag.TotalImpresiones = anunciosLado.Sum(a => a.Impresiones);
            ViewBag.TotalClics = anunciosLado.Sum(a => a.Clics);

            // Estadísticas de anuncios de agencias para comparación
            var anunciosAgencias = await _context.Anuncios
                .Where(a => !a.EsAnuncioLado)
                .ToListAsync();
            ViewBag.AnunciosAgencias = anunciosAgencias.Count;
            ViewBag.ImpresionesAgencias = anunciosAgencias.Sum(a => a.Impresiones);

            return View(anunciosLado);
        }

        /// <summary>
        /// Obtiene los datos de un anuncio de Lado para edición
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerAnuncioLado(int id)
        {
            var anuncio = await _context.Anuncios.FindAsync(id);
            if (anuncio == null || !anuncio.EsAnuncioLado)
            {
                return NotFound();
            }

            return Json(new
            {
                id = anuncio.Id,
                titulo = anuncio.Titulo,
                descripcion = anuncio.Descripcion,
                urlDestino = anuncio.UrlDestino,
                urlCreativo = anuncio.UrlCreativo,
                tipoCreativo = anuncio.TipoCreativo.ToString(),
                textoBoton = anuncio.TextoBoton.ToString(),
                textoBotonPersonalizado = anuncio.TextoBotonPersonalizado,
                prioridad = anuncio.Prioridad,
                estado = anuncio.Estado.ToString(),
                fechaInicio = anuncio.FechaInicio?.ToString("yyyy-MM-dd"),
                fechaFin = anuncio.FechaFin?.ToString("yyyy-MM-dd"),
                impresiones = anuncio.Impresiones,
                clics = anuncio.Clics,
                mostrarEnStoriesGlobal = anuncio.MostrarEnStoriesGlobal
            });
        }

        /// <summary>
        /// Crea o actualiza un anuncio de Lado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarAnuncioLado(
            int Id,
            string Titulo,
            string? Descripcion,
            string UrlDestino,
            int Prioridad,
            string TextoBoton,
            string? TextoBotonPersonalizado,
            string? FechaInicio,
            string? FechaFin,
            string Estado,
            bool MostrarEnStoriesGlobal,
            IFormFile? ImagenCreativo)
        {
            try
            {
                Anuncio anuncio;
                bool esNuevo = Id == 0;

                if (esNuevo)
                {
                    anuncio = new Anuncio
                    {
                        EsAnuncioLado = true,
                        FechaCreacion = DateTime.Now
                    };
                }
                else
                {
                    anuncio = await _context.Anuncios.FindAsync(Id);
                    if (anuncio == null || !anuncio.EsAnuncioLado)
                    {
                        TempData["Error"] = "Anuncio no encontrado.";
                        return RedirectToAction(nameof(Publicidad));
                    }
                }

                // Actualizar propiedades
                anuncio.Titulo = Titulo;
                anuncio.Descripcion = Descripcion;
                anuncio.UrlDestino = UrlDestino;
                anuncio.Prioridad = Prioridad;
                anuncio.UltimaActualizacion = DateTime.Now;

                // Texto del botón
                if (Enum.TryParse<TextoBotonAnuncio>(TextoBoton, out var textoBotonEnum))
                {
                    anuncio.TextoBoton = textoBotonEnum;
                }
                anuncio.TextoBotonPersonalizado = TextoBotonPersonalizado;

                // Fechas
                if (!string.IsNullOrEmpty(FechaInicio) && DateTime.TryParse(FechaInicio, out var fechaInicioDate))
                {
                    anuncio.FechaInicio = fechaInicioDate;
                }
                if (!string.IsNullOrEmpty(FechaFin) && DateTime.TryParse(FechaFin, out var fechaFinDate))
                {
                    anuncio.FechaFin = fechaFinDate;
                }

                // Estado
                if (Enum.TryParse<EstadoAnuncio>(Estado, out var estadoEnum))
                {
                    anuncio.Estado = estadoEnum;
                }

                // Mostrar en Stories Global - Solo uno puede estar activo
                if (MostrarEnStoriesGlobal && !anuncio.MostrarEnStoriesGlobal)
                {
                    // Desactivar otros anuncios globales
                    var otrosGlobales = await _context.Anuncios
                        .Where(a => a.MostrarEnStoriesGlobal && a.Id != Id)
                        .ToListAsync();
                    foreach (var otro in otrosGlobales)
                    {
                        otro.MostrarEnStoriesGlobal = false;
                    }
                }
                anuncio.MostrarEnStoriesGlobal = MostrarEnStoriesGlobal;

                // Procesar imagen del creativo
                if (ImagenCreativo != null && ImagenCreativo.Length > 0)
                {
                    var nombreArchivo = $"anuncio_lado_{Guid.NewGuid()}{Path.GetExtension(ImagenCreativo.FileName)}";
                    var rutaArchivo = Path.Combine("wwwroot", "uploads", "anuncios", nombreArchivo);

                    // Crear directorio si no existe
                    var directorio = Path.GetDirectoryName(rutaArchivo);
                    if (!string.IsNullOrEmpty(directorio) && !Directory.Exists(directorio))
                    {
                        Directory.CreateDirectory(directorio);
                    }

                    // Eliminar archivo anterior si existe
                    if (!string.IsNullOrEmpty(anuncio.UrlCreativo))
                    {
                        var rutaAnterior = Path.Combine("wwwroot", anuncio.UrlCreativo.TrimStart('/'));
                        if (System.IO.File.Exists(rutaAnterior))
                        {
                            System.IO.File.Delete(rutaAnterior);
                        }
                    }

                    using (var stream = new FileStream(rutaArchivo, FileMode.Create))
                    {
                        await ImagenCreativo.CopyToAsync(stream);
                    }

                    anuncio.UrlCreativo = $"/uploads/anuncios/{nombreArchivo}";

                    // Determinar tipo de creativo
                    var extension = Path.GetExtension(ImagenCreativo.FileName).ToLower();
                    anuncio.TipoCreativo = extension switch
                    {
                        ".mp4" or ".webm" or ".mov" => TipoCreativo.Video,
                        _ => TipoCreativo.Imagen
                    };
                }
                else if (esNuevo)
                {
                    TempData["Error"] = "La imagen del creativo es requerida para un nuevo anuncio.";
                    return RedirectToAction(nameof(Publicidad));
                }

                if (esNuevo)
                {
                    _context.Anuncios.Add(anuncio);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = esNuevo ? "Anuncio creado exitosamente." : "Anuncio actualizado exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al guardar el anuncio: {ex.Message}";
            }

            return RedirectToAction(nameof(Publicidad));
        }

        /// <summary>
        /// Cambia el estado de un anuncio de Lado (activar/pausar)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleEstadoAnuncioLado([FromBody] ToggleAnuncioRequest request)
        {
            var anuncio = await _context.Anuncios.FindAsync(request.Id);
            if (anuncio == null || !anuncio.EsAnuncioLado)
            {
                return Json(new { success = false, message = "Anuncio no encontrado" });
            }

            anuncio.Estado = request.Activo ? EstadoAnuncio.Activo : EstadoAnuncio.Pausado;
            anuncio.UltimaActualizacion = DateTime.Now;

            if (request.Activo && anuncio.FechaInicio == null)
            {
                anuncio.FechaInicio = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class ToggleAnuncioRequest
        {
            public int Id { get; set; }
            public bool Activo { get; set; }
        }

        /// <summary>
        /// Elimina un anuncio de Lado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarAnuncioLado(int id)
        {
            var anuncio = await _context.Anuncios.FindAsync(id);
            if (anuncio == null || !anuncio.EsAnuncioLado)
            {
                TempData["Error"] = "Anuncio no encontrado.";
                return RedirectToAction(nameof(Publicidad));
            }

            try
            {
                // Eliminar archivo del creativo
                if (!string.IsNullOrEmpty(anuncio.UrlCreativo))
                {
                    var rutaArchivo = Path.Combine("wwwroot", anuncio.UrlCreativo.TrimStart('/'));
                    if (System.IO.File.Exists(rutaArchivo))
                    {
                        System.IO.File.Delete(rutaArchivo);
                    }
                }

                // Eliminar registros de impresiones y clics asociados
                var impresiones = await _context.ImpresionesAnuncios
                    .Where(i => i.AnuncioId == id)
                    .ToListAsync();
                if (impresiones.Any())
                {
                    _context.ImpresionesAnuncios.RemoveRange(impresiones);
                }

                var clics = await _context.ClicsAnuncios
                    .Where(c => c.AnuncioId == id)
                    .ToListAsync();
                if (clics.Any())
                {
                    _context.ClicsAnuncios.RemoveRange(clics);
                }

                _context.Anuncios.Remove(anuncio);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Anuncio '{anuncio.Titulo}' eliminado exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar el anuncio: {ex.Message}";
            }

            return RedirectToAction(nameof(Publicidad));
        }

        // ========================================
        // RETIROS
        // ========================================

        /// <summary>
        /// Panel de gestión de solicitudes de retiro
        /// </summary>
        public async Task<IActionResult> Retiros(string filtro = "pendientes")
        {
            var query = _context.Transacciones
                .Include(t => t.Usuario)
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro);

            // Aplicar filtro
            switch (filtro.ToLower())
            {
                case "pendientes":
                    query = query.Where(t => t.EstadoPago == "Pendiente");
                    break;
                case "porpagar":
                    // Aprobados pero aún no pagados (pendientes de envío manual en PayPal)
                    query = query.Where(t => t.EstadoPago == "Aprobado");
                    break;
                case "pagados":
                    // Ya se envió el dinero
                    query = query.Where(t => t.EstadoPago == "Pagado" || t.EstadoPago == "Completado");
                    break;
                case "rechazados":
                    query = query.Where(t => t.EstadoPago == "Rechazado");
                    break;
                // "todos" no aplica filtro adicional
            }

            var retiros = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .ToListAsync();

            // Estadísticas
            ViewBag.TotalPendientes = await _context.Transacciones
                .CountAsync(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoPago == "Pendiente");

            ViewBag.MontoPendiente = await _context.Transacciones
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoPago == "Pendiente")
                .SumAsync(t => (decimal?)t.MontoNeto) ?? 0;

            // Por Pagar = Aprobados pero no enviado el dinero todavía
            ViewBag.TotalPorPagar = await _context.Transacciones
                .CountAsync(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoPago == "Aprobado");

            ViewBag.MontoPorPagar = await _context.Transacciones
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoPago == "Aprobado")
                .SumAsync(t => (decimal?)t.MontoNeto) ?? 0;

            // Pagados = dinero ya enviado
            ViewBag.TotalPagados = await _context.Transacciones
                .CountAsync(t => t.TipoTransaccion == TipoTransaccion.Retiro &&
                    (t.EstadoPago == "Pagado" || t.EstadoPago == "Completado"));

            ViewBag.MontoPagado = await _context.Transacciones
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro &&
                    (t.EstadoPago == "Pagado" || t.EstadoPago == "Completado"))
                .SumAsync(t => (decimal?)t.MontoNeto) ?? 0;

            ViewBag.TotalRechazados = await _context.Transacciones
                .CountAsync(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoPago == "Rechazado");

            ViewBag.FiltroActual = filtro;

            return View(retiros);
        }

        /// <summary>
        /// Aprobar una solicitud de retiro (pasa a estado "Aprobado" - pendiente de pago manual)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarRetiro(int id, string? notas)
        {
            var retiro = await _context.Transacciones
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id && t.TipoTransaccion == TipoTransaccion.Retiro);

            if (retiro == null)
            {
                TempData["Error"] = "Retiro no encontrado.";
                return RedirectToAction(nameof(Retiros));
            }

            if (retiro.EstadoPago != "Pendiente")
            {
                TempData["Error"] = "Este retiro ya fue procesado.";
                return RedirectToAction(nameof(Retiros));
            }

            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminName = User.Identity?.Name ?? "Admin";

            // Estado "Aprobado" = listo para pagar manualmente en PayPal
            retiro.EstadoPago = "Aprobado";
            retiro.Notas = string.IsNullOrEmpty(notas)
                ? $"Aprobado por {adminName} - Pendiente de pago"
                : $"{notas} (Aprobado por {adminName})";

            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Retiro #{id} aprobado - Pendiente de pago manual",
                CategoriaEvento.Pago,
                TipoLogEvento.Evento,
                adminId,
                adminName,
                $"Usuario: {retiro.Usuario?.UserName}, Monto neto: ${retiro.MontoNeto:N2}, Método: {retiro.MetodoPago}, Cuenta: {retiro.Usuario?.CuentaRetiro}"
            );

            TempData["Success"] = $"Retiro #{id} aprobado. Ahora debes enviar ${retiro.MontoNeto:N2} a {retiro.Usuario?.CuentaRetiro ?? "cuenta del usuario"} por {retiro.MetodoPago}";
            return RedirectToAction("Retiros", new { filtro = "porpagar" });
        }

        /// <summary>
        /// Marcar un retiro como pagado (después de enviar el dinero manualmente en PayPal)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarComoPagado(int id, string? referencia)
        {
            var retiro = await _context.Transacciones
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id && t.TipoTransaccion == TipoTransaccion.Retiro);

            if (retiro == null)
            {
                TempData["Error"] = "Retiro no encontrado.";
                return RedirectToAction(nameof(Retiros));
            }

            if (retiro.EstadoPago != "Aprobado")
            {
                TempData["Error"] = "Solo se pueden marcar como pagados los retiros aprobados.";
                return RedirectToAction(nameof(Retiros));
            }

            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminName = User.Identity?.Name ?? "Admin";

            retiro.EstadoPago = "Pagado";
            retiro.EstadoTransaccion = EstadoTransaccion.Completada;

            var notaAnterior = retiro.Notas ?? "";
            retiro.Notas = string.IsNullOrEmpty(referencia)
                ? $"{notaAnterior} | Pagado por {adminName} el {DateTime.Now:dd/MM/yyyy HH:mm}"
                : $"{notaAnterior} | Pagado por {adminName} el {DateTime.Now:dd/MM/yyyy HH:mm}. Ref: {referencia}";

            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Retiro #{id} marcado como PAGADO",
                CategoriaEvento.Pago,
                TipoLogEvento.Evento,
                adminId,
                adminName,
                $"Usuario: {retiro.Usuario?.UserName}, Monto: ${retiro.MontoNeto:N2}, Referencia: {referencia ?? "N/A"}"
            );

            TempData["Success"] = $"Retiro #{id} marcado como pagado. ${retiro.MontoNeto:N2} enviado a {retiro.Usuario?.NombreCompleto ?? "Usuario"}";
            return RedirectToAction(nameof(Retiros));
        }

        /// <summary>
        /// Rechazar una solicitud de retiro y devolver el saldo al usuario
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarRetiro(int id, string razon)
        {
            var retiro = await _context.Transacciones
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id && t.TipoTransaccion == TipoTransaccion.Retiro);

            if (retiro == null)
            {
                TempData["Error"] = "Retiro no encontrado.";
                return RedirectToAction(nameof(Retiros));
            }

            if (retiro.EstadoPago != "Pendiente")
            {
                TempData["Error"] = "Este retiro ya fue procesado.";
                return RedirectToAction(nameof(Retiros));
            }

            if (string.IsNullOrWhiteSpace(razon))
            {
                TempData["Error"] = "Debes especificar una razón para rechazar el retiro.";
                return RedirectToAction(nameof(Retiros));
            }

            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminName = User.Identity?.Name ?? "Admin";

            // Devolver el monto al saldo del usuario
            if (retiro.Usuario != null)
            {
                retiro.Usuario.Saldo += retiro.Monto;
            }

            retiro.EstadoPago = "Rechazado";
            retiro.EstadoTransaccion = EstadoTransaccion.Cancelada;
            retiro.Notas = $"Rechazado por {adminName}: {razon}";

            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Retiro #{id} RECHAZADO",
                CategoriaEvento.Pago,
                TipoLogEvento.Warning,
                adminId,
                adminName,
                $"Usuario: {retiro.Usuario?.UserName}, Monto devuelto: ${retiro.Monto:N2}, Razón: {razon}"
            );

            TempData["Success"] = $"Retiro #{id} rechazado. Se devolvió ${retiro.Monto:N2} al saldo de {retiro.Usuario?.NombreCompleto ?? "Usuario"}";
            return RedirectToAction(nameof(Retiros));
        }

        /// <summary>
        /// Obtener detalles de un retiro para el modal
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerRetiro(int id)
        {
            var retiro = await _context.Transacciones
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id && t.TipoTransaccion == TipoTransaccion.Retiro);

            if (retiro == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = retiro.Id,
                usuario = retiro.Usuario?.NombreCompleto ?? "Usuario",
                username = retiro.Usuario?.UserName,
                email = retiro.Usuario?.Email,
                foto = retiro.Usuario?.FotoPerfil,
                monto = retiro.Monto,
                comision = retiro.Comision,
                retencion = retiro.RetencionImpuestos,
                montoNeto = retiro.MontoNeto,
                metodoPago = retiro.MetodoPago,
                cuentaRetiro = retiro.Usuario?.CuentaRetiro,
                tipoCuenta = retiro.Usuario?.TipoCuentaRetiro,
                descripcion = retiro.Descripcion,
                fecha = retiro.FechaTransaccion.ToString("dd/MM/yyyy HH:mm"),
                estado = retiro.EstadoPago,
                notas = retiro.Notas
            });
        }

        // ========================================
        // TRANSACCIONES
        // ========================================

        /// <summary>
        /// Panel de todas las transacciones de la plataforma
        /// </summary>
        public async Task<IActionResult> Transacciones(
            string? buscar,
            string? tipo,
            string? estado,
            DateTime? desde,
            DateTime? hasta,
            int pagina = 1)
        {
            var query = _context.Transacciones
                .Include(t => t.Usuario)
                .AsQueryable();

            // Filtro por búsqueda (usuario)
            if (!string.IsNullOrWhiteSpace(buscar))
            {
                query = query.Where(t => t.Usuario != null &&
                    (t.Usuario.NombreCompleto.Contains(buscar) ||
                     t.Usuario.UserName.Contains(buscar) ||
                     t.Usuario.Email.Contains(buscar)));
            }

            // Filtro por tipo
            if (!string.IsNullOrWhiteSpace(tipo) && Enum.TryParse<TipoTransaccion>(tipo, out var tipoEnum))
            {
                query = query.Where(t => t.TipoTransaccion == tipoEnum);
            }

            // Filtro por estado
            if (!string.IsNullOrWhiteSpace(estado))
            {
                query = query.Where(t => t.EstadoPago == estado);
            }

            // Filtro por fechas
            if (desde.HasValue)
            {
                query = query.Where(t => t.FechaTransaccion >= desde.Value);
            }
            if (hasta.HasValue)
            {
                query = query.Where(t => t.FechaTransaccion <= hasta.Value.AddDays(1));
            }

            // Estadísticas generales
            ViewBag.TotalTransacciones = await _context.Transacciones.CountAsync();
            ViewBag.IngresosTotales = await _context.Transacciones
                .Where(t => t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.EstadoTransaccion == EstadoTransaccion.Completada)
                .SumAsync(t => (decimal?)t.Monto) ?? 0;
            ViewBag.RetirosTotales = await _context.Transacciones
                .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro &&
                            (t.EstadoPago == "Completado" || t.EstadoPago == "Aprobado"))
                .SumAsync(t => (decimal?)t.MontoNeto) ?? 0;
            ViewBag.ComisionesTotales = await _context.Transacciones
                .Where(t => t.Comision != null && t.EstadoTransaccion == EstadoTransaccion.Completada)
                .SumAsync(t => (decimal?)t.Comision) ?? 0;

            // Paginación
            var totalItems = await query.CountAsync();
            var itemsPorPagina = 50;
            var totalPaginas = (int)Math.Ceiling(totalItems / (double)itemsPorPagina);

            var transacciones = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .Skip((pagina - 1) * itemsPorPagina)
                .Take(itemsPorPagina)
                .ToListAsync();

            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalItems = totalItems;

            // Mantener filtros en la vista
            ViewBag.Buscar = buscar;
            ViewBag.Tipo = tipo;
            ViewBag.Estado = estado;
            ViewBag.Desde = desde?.ToString("yyyy-MM-dd");
            ViewBag.Hasta = hasta?.ToString("yyyy-MM-dd");

            // Lista de tipos de transacción para el filtro
            ViewBag.TiposTransaccion = Enum.GetValues<TipoTransaccion>()
                .Select(t => new { Valor = t.ToString(), Nombre = t.ToString() })
                .ToList();

            return View(transacciones);
        }

        /// <summary>
        /// Exportar transacciones a CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportarTransacciones(
            string? tipo,
            string? estado,
            DateTime? desde,
            DateTime? hasta)
        {
            var query = _context.Transacciones
                .Include(t => t.Usuario)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(tipo) && Enum.TryParse<TipoTransaccion>(tipo, out var tipoEnum))
            {
                query = query.Where(t => t.TipoTransaccion == tipoEnum);
            }
            if (!string.IsNullOrWhiteSpace(estado))
            {
                query = query.Where(t => t.EstadoPago == estado);
            }
            if (desde.HasValue)
            {
                query = query.Where(t => t.FechaTransaccion >= desde.Value);
            }
            if (hasta.HasValue)
            {
                query = query.Where(t => t.FechaTransaccion <= hasta.Value.AddDays(1));
            }

            var transacciones = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Usuario,Email,Tipo,Monto,Comision,Retencion,MontoNeto,Estado,MetodoPago,Fecha,Descripcion");

            foreach (var t in transacciones)
            {
                csv.AppendLine($"{t.Id},{t.Usuario?.NombreCompleto ?? "N/A"},{t.Usuario?.Email ?? "N/A"},{t.TipoTransaccion},{t.Monto:F2},{t.Comision:F2},{t.RetencionImpuestos:F2},{t.MontoNeto:F2},{t.EstadoPago},{t.MetodoPago},{t.FechaTransaccion:yyyy-MM-dd HH:mm},\"{t.Descripcion?.Replace("\"", "\"\"")}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"transacciones_{DateTime.Now:yyyyMMdd}.csv");
        }

        // ========================================
        // ALGORITMOS DE FEED
        // ========================================

        public async Task<IActionResult> Algoritmos()
        {
            var algoritmos = await _context.AlgoritmosFeed
                .OrderBy(a => a.Orden)
                .ToListAsync();

            // Estadísticas de uso por algoritmo (últimos 7 días)
            var hace7Dias = DateTime.Now.AddDays(-7);
            var usosPorAlgoritmo = await _context.PreferenciasAlgoritmoUsuario
                .Where(p => p.FechaSeleccion >= hace7Dias)
                .GroupBy(p => p.AlgoritmoFeedId)
                .Select(g => new { AlgoritmoId = g.Key, Usuarios = g.Count() })
                .ToDictionaryAsync(x => x.AlgoritmoId, x => x.Usuarios);

            ViewBag.UsosPorAlgoritmo = usosPorAlgoritmo;

            // Total de usuarios que han seleccionado un algoritmo
            ViewBag.TotalUsuariosConPreferencia = await _context.PreferenciasAlgoritmoUsuario.CountAsync();

            return View(algoritmos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAlgoritmo(int id)
        {
            var algoritmo = await _context.AlgoritmosFeed.FindAsync(id);
            if (algoritmo != null)
            {
                // No permitir desactivar el algoritmo por defecto
                if (algoritmo.EsPorDefecto && algoritmo.Activo)
                {
                    TempData["Error"] = "No se puede desactivar el algoritmo por defecto. Primero establece otro como predeterminado.";
                    return RedirectToAction("Algoritmos");
                }

                algoritmo.Activo = !algoritmo.Activo;
                algoritmo.FechaModificacion = DateTime.Now;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Algoritmo '{algoritmo.Nombre}' {(algoritmo.Activo ? "activado" : "desactivado")}.";
            }
            return RedirectToAction("Algoritmos");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAlgoritmoPorDefecto(int id)
        {
            var algoritmo = await _context.AlgoritmosFeed.FindAsync(id);
            if (algoritmo != null && algoritmo.Activo)
            {
                // Quitar el flag de todos los demás
                var todos = await _context.AlgoritmosFeed.ToListAsync();
                foreach (var alg in todos)
                {
                    alg.EsPorDefecto = (alg.Id == id);
                    alg.FechaModificacion = DateTime.Now;
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = $"'{algoritmo.Nombre}' es ahora el algoritmo por defecto.";
            }
            else
            {
                TempData["Error"] = "El algoritmo debe estar activo para ser el predeterminado.";
            }
            return RedirectToAction("Algoritmos");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarOrdenAlgoritmo(int id, int nuevoOrden)
        {
            var algoritmo = await _context.AlgoritmosFeed.FindAsync(id);
            if (algoritmo != null)
            {
                algoritmo.Orden = nuevoOrden;
                algoritmo.FechaModificacion = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarDescripcionAlgoritmo(int id, string descripcion)
        {
            var algoritmo = await _context.AlgoritmosFeed.FindAsync(id);
            if (algoritmo != null)
            {
                algoritmo.Descripcion = descripcion;
                algoritmo.FechaModificacion = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Descripción actualizada.";
            }
            return RedirectToAction("Algoritmos");
        }

        [HttpGet]
        public async Task<IActionResult> EstadisticasAlgoritmos()
        {
            var algoritmos = await _context.AlgoritmosFeed
                .Where(a => a.Activo)
                .OrderBy(a => a.Orden)
                .ToListAsync();

            var resultado = new List<object>();

            foreach (var alg in algoritmos)
            {
                var usuariosActivos = await _context.PreferenciasAlgoritmoUsuario
                    .CountAsync(p => p.AlgoritmoFeedId == alg.Id);

                resultado.Add(new
                {
                    alg.Id,
                    alg.Nombre,
                    alg.Codigo,
                    alg.TotalUsos,
                    UsuariosActivos = usuariosActivos
                });
            }

            return Json(resultado);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RepararAlgoritmos()
        {
            // Verificar si existe el algoritmo cronologico
            var cronologico = await _context.AlgoritmosFeed
                .FirstOrDefaultAsync(a => a.Codigo == "cronologico");

            if (cronologico == null)
            {
                // Crear los 4 algoritmos si no existen
                var algoritmos = new List<Lado.Models.AlgoritmoFeed>
                {
                    new Lado.Models.AlgoritmoFeed
                    {
                        Codigo = "cronologico",
                        Nombre = "Cronologico",
                        Descripcion = "Muestra los posts ordenados por fecha de publicacion, los mas recientes primero",
                        Icono = "clock",
                        Activo = true,
                        EsPorDefecto = true,
                        Orden = 1,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    },
                    new Lado.Models.AlgoritmoFeed
                    {
                        Codigo = "trending",
                        Nombre = "Trending",
                        Descripcion = "Prioriza contenido con alto engagement reciente (likes, comentarios, vistas)",
                        Icono = "trending-up",
                        Activo = true,
                        EsPorDefecto = false,
                        Orden = 2,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    },
                    new Lado.Models.AlgoritmoFeed
                    {
                        Codigo = "seguidos",
                        Nombre = "Seguidos Primero",
                        Descripcion = "70% contenido de creadores que sigues, 30% descubrimiento de nuevos",
                        Icono = "users",
                        Activo = true,
                        EsPorDefecto = false,
                        Orden = 3,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    },
                    new Lado.Models.AlgoritmoFeed
                    {
                        Codigo = "para_ti",
                        Nombre = "Para Ti",
                        Descripcion = "Personalizado basado en tu historial de interacciones y preferencias",
                        Icono = "heart",
                        Activo = true,
                        EsPorDefecto = false,
                        Orden = 4,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    }
                };

                _context.AlgoritmosFeed.AddRange(algoritmos);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Algoritmos creados correctamente.";
            }
            else
            {
                // Asegurar que cronologico sea el por defecto
                var todos = await _context.AlgoritmosFeed.ToListAsync();
                foreach (var alg in todos)
                {
                    alg.EsPorDefecto = (alg.Codigo == "cronologico");
                    alg.Activo = true;
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "Algoritmos reparados. 'Cronologico' es ahora el por defecto.";
            }

            return RedirectToAction("Algoritmos");
        }

        [HttpGet]
        public async Task<IActionResult> DiagnosticoAlgoritmos()
        {
            var algoritmos = await _context.AlgoritmosFeed.ToListAsync();
            var preferencias = await _context.PreferenciasAlgoritmoUsuario.CountAsync();

            return Json(new
            {
                TotalAlgoritmos = algoritmos.Count,
                Algoritmos = algoritmos.Select(a => new
                {
                    a.Id,
                    a.Codigo,
                    a.Nombre,
                    a.Activo,
                    a.EsPorDefecto,
                    a.Orden,
                    a.TotalUsos
                }),
                TotalPreferenciasUsuarios = preferencias
            });
        }

        // ========================================
        // SEED DE CATEGORIAS DE INTERES
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeedCategoriasInteres()
        {
            try
            {
                var categoriasExistentes = await _context.CategoriasIntereses.CountAsync();
                int categoriasCreadas = 0;

                if (categoriasExistentes == 0)
                {
                    var categorias = new List<CategoriaInteres>
                    {
                        new CategoriaInteres { Nombre = "Entretenimiento", Descripcion = "Contenido de entretenimiento general", Icono = "bi-film", Color = "#FF6B6B", Orden = 1, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Musica", Descripcion = "Artistas, covers, producciones musicales", Icono = "bi-music-note-beamed", Color = "#4ECDC4", Orden = 2, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Fitness", Descripcion = "Ejercicio, rutinas, vida saludable", Icono = "bi-heart-pulse", Color = "#45B7D1", Orden = 3, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Moda", Descripcion = "Estilo, tendencias, outfits", Icono = "bi-bag-heart", Color = "#F7DC6F", Orden = 4, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Belleza", Descripcion = "Maquillaje, skincare, cuidado personal", Icono = "bi-stars", Color = "#BB8FCE", Orden = 5, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Cocina", Descripcion = "Recetas, gastronomia, tips culinarios", Icono = "bi-egg-fried", Color = "#F39C12", Orden = 6, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Viajes", Descripcion = "Destinos, aventuras, experiencias", Icono = "bi-airplane", Color = "#1ABC9C", Orden = 7, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Gaming", Descripcion = "Videojuegos, streams, esports", Icono = "bi-controller", Color = "#9B59B6", Orden = 8, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Arte", Descripcion = "Dibujo, pintura, creatividad", Icono = "bi-palette", Color = "#E74C3C", Orden = 9, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Educacion", Descripcion = "Tutoriales, cursos, aprendizaje", Icono = "bi-book", Color = "#3498DB", Orden = 10, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Comedia", Descripcion = "Humor, sketches, entretenimiento", Icono = "bi-emoji-laughing", Color = "#F1C40F", Orden = 11, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Lifestyle", Descripcion = "Dia a dia, vlogs, estilo de vida", Icono = "bi-house-heart", Color = "#E91E63", Orden = 12, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Tecnologia", Descripcion = "Gadgets, apps, innovacion", Icono = "bi-cpu", Color = "#607D8B", Orden = 13, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Deportes", Descripcion = "Futbol, basquet, atletismo", Icono = "bi-trophy", Color = "#27AE60", Orden = 14, EstaActiva = true },
                        new CategoriaInteres { Nombre = "Mascotas", Descripcion = "Perros, gatos, animales", Icono = "bi-heart", Color = "#FF9800", Orden = 15, EstaActiva = true }
                    };

                    _context.CategoriasIntereses.AddRange(categorias);
                    await _context.SaveChangesAsync();
                    categoriasCreadas = categorias.Count;
                }

                // Verificar algoritmo por intereses
                var algoritmoExiste = await _context.AlgoritmosFeed.AnyAsync(a => a.Codigo == "por_intereses");
                bool algoritmoCreado = false;

                if (!algoritmoExiste)
                {
                    var algoritmo = new AlgoritmoFeed
                    {
                        Codigo = "por_intereses",
                        Nombre = "Por Intereses",
                        Descripcion = "Prioriza contenido basado en tus intereses seleccionados y aprendidos",
                        Icono = "star",
                        Activo = true,
                        EsPorDefecto = false,
                        Orden = 5,
                        TotalUsos = 0,
                        FechaCreacion = DateTime.Now
                    };

                    _context.AlgoritmosFeed.Add(algoritmo);
                    await _context.SaveChangesAsync();
                    algoritmoCreado = true;
                }

                var totalCategorias = await _context.CategoriasIntereses.CountAsync();
                var mensaje = categoriasCreadas > 0 || algoritmoCreado
                    ? "Seeds ejecutados correctamente"
                    : "Los datos ya existian, no se realizaron cambios";

                return Json(new
                {
                    success = true,
                    message = mensaje,
                    categoriasCreadas = categoriasCreadas,
                    totalCategorias = totalCategorias,
                    algoritmoCreado = algoritmoCreado
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimpiarCategorias()
        {
            try
            {
                // 1. Quitar referencias de contenidos a categorías
                var contenidosConCategoria = await _context.Contenidos
                    .Where(c => c.CategoriaInteresId != null)
                    .ToListAsync();

                foreach (var contenido in contenidosConCategoria)
                {
                    contenido.CategoriaInteresId = null;
                }

                // 2. Eliminar intereses de usuarios
                var intereses = await _context.InteresesUsuarios.ToListAsync();
                _context.InteresesUsuarios.RemoveRange(intereses);

                // 3. Eliminar subcategorías primero (por FK)
                var subcategorias = await _context.CategoriasIntereses
                    .Where(c => c.CategoriaPadreId != null)
                    .ToListAsync();
                _context.CategoriasIntereses.RemoveRange(subcategorias);

                // 4. Eliminar categorías principales
                var categorias = await _context.CategoriasIntereses.ToListAsync();
                _context.CategoriasIntereses.RemoveRange(categorias);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Categorías eliminadas. Se crearán automáticamente al subir nuevo contenido.",
                    contenidosActualizados = contenidosConCategoria.Count,
                    interesesEliminados = intereses.Count,
                    categoriasEliminadas = subcategorias.Count + categorias.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Endpoint para obtener estado de clasificación
        [HttpGet]
        public async Task<IActionResult> EstadoClasificacionIA()
        {
            var total = await _context.Contenidos
                .CountAsync(c => c.EstaActivo && !c.EsBorrador && !string.IsNullOrEmpty(c.RutaArchivo));

            var sinClasificar = await _context.Contenidos
                .CountAsync(c => c.EstaActivo && !c.EsBorrador && !string.IsNullOrEmpty(c.RutaArchivo) && c.CategoriaInteresId == null);

            var categorias = await _context.CategoriasIntereses.CountAsync();

            return Json(new {
                total,
                sinClasificar,
                clasificados = total - sinClasificar,
                categorias,
                porcentaje = total > 0 ? ((total - sinClasificar) * 100 / total) : 0
            });
        }

        // Endpoint para clasificar un lote pequeño (5 contenidos)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReclasificarLoteIA()
        {
            try
            {
                int clasificados = 0;
                int errores = 0;
                int categoriasCreadas = 0;
                int subcategoriasCreadas = 0;

                // Obtener IDs a excluir (que ya fallaron)
                List<int> idsExcluir;
                lock (_lockFallidos)
                {
                    idsExcluir = _contenidosFallidosClasificacion.ToList();
                }

                // Obtener solo 5 contenidos SIN clasificar (imágenes), excluyendo los que ya fallaron
                var contenidos = await _context.Contenidos
                    .Where(c => c.EstaActivo && !c.EsBorrador &&
                           !string.IsNullOrEmpty(c.RutaArchivo) &&
                           c.CategoriaInteresId == null &&
                           !idsExcluir.Contains(c.Id))
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(5)
                    .ToListAsync();

                // Contar pendientes reales (sin contar los fallidos)
                var pendientesReales = await _context.Contenidos
                    .CountAsync(c => c.EstaActivo && !c.EsBorrador &&
                               !string.IsNullOrEmpty(c.RutaArchivo) &&
                               c.CategoriaInteresId == null &&
                               !idsExcluir.Contains(c.Id));

                if (!contenidos.Any())
                {
                    // Limpiar lista de fallidos para próxima sesión
                    lock (_lockFallidos)
                    {
                        var cantidadFallidos = _contenidosFallidosClasificacion.Count;
                        _contenidosFallidosClasificacion.Clear();

                        return Json(new {
                            success = true,
                            terminado = true,
                            mensaje = cantidadFallidos > 0
                                ? $"No hay más contenidos por clasificar. {cantidadFallidos} contenidos no pudieron clasificarse (archivo no existe o formato no soportado)."
                                : "No hay más contenidos por clasificar"
                        });
                    }
                }

                var detallesErrores = new List<string>();

                foreach (var contenido in contenidos)
                {
                    try
                    {
                        byte[]? imagenBytes = null;
                        string? mimeType = null;

                        var rutaCompleta = Path.Combine(_hostEnvironment.WebRootPath, contenido.RutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                        if (!System.IO.File.Exists(rutaCompleta))
                        {
                            detallesErrores.Add($"ID {contenido.Id}: Archivo no existe: {rutaCompleta}");
                            errores++;
                            // Marcar como fallido para no reintentar
                            lock (_lockFallidos) { _contenidosFallidosClasificacion.Add(contenido.Id); }
                            continue;
                        }

                        var extension = Path.GetExtension(rutaCompleta).ToLower();
                        if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif" || extension == ".webp")
                        {
                            imagenBytes = await System.IO.File.ReadAllBytesAsync(rutaCompleta);
                            mimeType = extension switch
                            {
                                ".jpg" or ".jpeg" => "image/jpeg",
                                ".png" => "image/png",
                                ".gif" => "image/gif",
                                ".webp" => "image/webp",
                                _ => "image/jpeg"
                            };
                        }
                        else
                        {
                            // Es video u otro formato, saltar
                            detallesErrores.Add($"ID {contenido.Id}: Formato no soportado: {extension}");
                            errores++;
                            // Marcar como fallido para no reintentar
                            lock (_lockFallidos) { _contenidosFallidosClasificacion.Add(contenido.Id); }
                            continue;
                        }

                        // Usar método combinado para clasificar y detectar objetos
                        var resultado = await _claudeService.ClasificarYDetectarObjetosAsync(
                            imagenBytes,
                            contenido.Descripcion,
                            mimeType
                        );

                        if (resultado.Clasificacion.Exito && resultado.Clasificacion.CategoriaId.HasValue)
                        {
                            contenido.CategoriaInteresId = resultado.Clasificacion.CategoriaId.Value;
                            clasificados++;

                            if (resultado.Clasificacion.CategoriaCreada)
                            {
                                if (resultado.Clasificacion.CategoriaNombre?.Contains(">") == true)
                                    subcategoriasCreadas++;
                                else
                                    categoriasCreadas++;
                            }

                            // Guardar objetos detectados
                            if (resultado.ObjetosDetectados.Any())
                            {
                                // Eliminar objetos anteriores si existían
                                var objetosAnteriores = await _context.ObjetosContenido
                                    .Where(o => o.ContenidoId == contenido.Id)
                                    .ToListAsync();
                                if (objetosAnteriores.Any())
                                {
                                    _context.ObjetosContenido.RemoveRange(objetosAnteriores);
                                }

                                // Agregar nuevos objetos
                                foreach (var obj in resultado.ObjetosDetectados)
                                {
                                    _context.ObjetosContenido.Add(new Models.ObjetoContenido
                                    {
                                        ContenidoId = contenido.Id,
                                        NombreObjeto = obj.Nombre,
                                        Confianza = obj.Confianza,
                                        FechaDeteccion = DateTime.Now
                                    });
                                }
                            }
                        }
                        else
                        {
                            var errorMsg = resultado.Clasificacion.Error ?? "Sin clasificar";
                            detallesErrores.Add($"ID {contenido.Id}: {errorMsg}");
                            errores++;

                            // Marcar como fallido permanente si el servicio lo indica
                            if (resultado.Clasificacion.EsErrorPermanente)
                            {
                                lock (_lockFallidos) { _contenidosFallidosClasificacion.Add(contenido.Id); }
                            }
                        }

                        await Task.Delay(500); // Pausa entre llamadas para evitar rate limiting
                    }
                    catch (Exception ex)
                    {
                        detallesErrores.Add($"ID {contenido.Id}: Exception - {ex.Message}");
                        errores++;
                        // Marcar como fallido permanente
                        lock (_lockFallidos) { _contenidosFallidosClasificacion.Add(contenido.Id); }
                    }
                }

                await _context.SaveChangesAsync();

                // Obtener IDs fallidos actualizados
                List<int> idsFallidos;
                lock (_lockFallidos)
                {
                    idsFallidos = _contenidosFallidosClasificacion.ToList();
                }

                // Obtener estado actualizado (excluyendo fallidos para evitar loop infinito)
                var pendientes = await _context.Contenidos
                    .CountAsync(c => c.EstaActivo && !c.EsBorrador &&
                               !string.IsNullOrEmpty(c.RutaArchivo) &&
                               c.CategoriaInteresId == null &&
                               !idsFallidos.Contains(c.Id));

                return Json(new {
                    success = true,
                    terminado = pendientes == 0,
                    clasificados,
                    errores,
                    categoriasCreadas,
                    subcategoriasCreadas,
                    pendientes,
                    fallidosPermanentes = idsFallidos.Count,
                    detallesErrores = detallesErrores.Take(10) // Mostrar primeros 10 errores
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReclasificarContenidoIA()
        {
            // Este endpoint ahora solo limpia las categorías
            // La clasificación se hace con ReclasificarLoteIA
            try
            {
                // Limpiar lista de contenidos fallidos para nueva sesión
                lock (_lockFallidos)
                {
                    _contenidosFallidosClasificacion.Clear();
                }

                // 1. Limpiar categorías existentes
                var intereses = await _context.InteresesUsuarios.ToListAsync();
                _context.InteresesUsuarios.RemoveRange(intereses);

                var subcategorias = await _context.CategoriasIntereses
                    .Where(c => c.CategoriaPadreId != null)
                    .ToListAsync();
                _context.CategoriasIntereses.RemoveRange(subcategorias);

                var categorias = await _context.CategoriasIntereses.ToListAsync();
                _context.CategoriasIntereses.RemoveRange(categorias);

                // Limpiar referencias en contenidos
                await _context.Contenidos
                    .ExecuteUpdateAsync(c => c.SetProperty(x => x.CategoriaInteresId, (int?)null));

                await _context.SaveChangesAsync();

                // Contar contenidos pendientes
                var pendientes = await _context.Contenidos
                    .CountAsync(c => c.EstaActivo && !c.EsBorrador &&
                               !string.IsNullOrEmpty(c.RutaArchivo) &&
                               c.CategoriaInteresId == null);

                return Json(new
                {
                    success = true,
                    limpiado = true,
                    message = "Categorías limpiadas. Ahora inicia la clasificación.",
                    pendientes
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // PRUEBA DE CLASIFICACION CON CLAUDE
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ProbarClasificacion()
        {
            // Obtener categorias disponibles
            var categorias = await _context.CategoriasIntereses
                .Where(c => c.EstaActiva && c.CategoriaPadreId == null)
                .OrderBy(c => c.Orden)
                .Select(c => new { c.Id, c.Nombre, c.Icono, c.Color })
                .ToListAsync();

            ViewBag.Categorias = categorias;
            ViewBag.TieneCategorias = categorias.Any();
            ViewBag.ApiKeyConfigurada = !string.IsNullOrEmpty(_configuration["Claude:ApiKey"]);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProbarClasificacionTexto([FromBody] ProbarClasificacionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Texto))
                {
                    return Json(new { success = false, message = "El texto es requerido" });
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var categoriaId = await _claudeService.ClasificarPorTextoAsync(request.Texto);
                stopwatch.Stop();

                if (categoriaId == null)
                {
                    return Json(new {
                        success = false,
                        message = "No se pudo clasificar. Verifica que la API Key esté configurada y las categorías existan.",
                        tiempoMs = stopwatch.ElapsedMilliseconds
                    });
                }

                var categoria = await _context.CategoriasIntereses
                    .Where(c => c.Id == categoriaId)
                    .Select(c => new { c.Id, c.Nombre, c.Icono, c.Color })
                    .FirstOrDefaultAsync();

                return Json(new
                {
                    success = true,
                    categoriaId = categoriaId,
                    categoriaNombre = categoria?.Nombre,
                    categoriaIcono = categoria?.Icono,
                    categoriaColor = categoria?.Color,
                    tiempoMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProbarClasificacionImagen(IFormFile imagen, string? descripcion)
        {
            try
            {
                if (imagen == null || imagen.Length == 0)
                {
                    return Json(new { success = false, message = "La imagen es requerida" });
                }

                // Leer imagen
                byte[] imagenBytes;
                using (var ms = new MemoryStream())
                {
                    await imagen.CopyToAsync(ms);
                    imagenBytes = ms.ToArray();
                }

                // Verificar tamaño (max 5MB para Claude)
                if (imagenBytes.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "La imagen debe ser menor a 5MB" });
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var categoriaId = await _claudeService.ClasificarContenidoAsync(
                    imagenBytes,
                    descripcion,
                    imagen.ContentType
                );
                stopwatch.Stop();

                if (categoriaId == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se pudo clasificar. Verifica que la API Key esté configurada y las categorías existan.",
                        tiempoMs = stopwatch.ElapsedMilliseconds
                    });
                }

                var categoria = await _context.CategoriasIntereses
                    .Where(c => c.Id == categoriaId)
                    .Select(c => new { c.Id, c.Nombre, c.Icono, c.Color })
                    .FirstOrDefaultAsync();

                return Json(new
                {
                    success = true,
                    categoriaId = categoriaId,
                    categoriaNombre = categoria?.Nombre,
                    categoriaIcono = categoria?.Icono,
                    categoriaColor = categoria?.Color,
                    tiempoMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class ProbarClasificacionRequest
        {
            public string? Texto { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> DiagnosticoClasificacion()
        {
            var diagnostico = new Dictionary<string, object>();

            // 1. Verificar API Key
            var apiKey = _configuration["Claude:ApiKey"];
            diagnostico["apiKeyConfigurada"] = !string.IsNullOrEmpty(apiKey);
            diagnostico["apiKeyPrimeros10Chars"] = string.IsNullOrEmpty(apiKey) ? "NO CONFIGURADA" : apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...";

            // 2. Verificar categorías
            var categorias = await _context.CategoriasIntereses
                .Where(c => c.EstaActiva && c.CategoriaPadreId == null)
                .OrderBy(c => c.Orden)
                .Select(c => new { c.Id, c.Nombre })
                .ToListAsync();

            diagnostico["totalCategorias"] = categorias.Count;
            diagnostico["categorias"] = categorias;

            // 3. Probar conexión a Claude API (sin enviar contenido)
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    var testRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                    testRequest.Headers.Add("x-api-key", apiKey);
                    testRequest.Headers.Add("anthropic-version", "2023-06-01");
                    testRequest.Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            model = "claude-3-haiku-20240307",
                            max_tokens = 5,
                            messages = new[] { new { role = "user", content = "Di solo 'OK'" } }
                        }),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    var response = await httpClient.SendAsync(testRequest);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    diagnostico["claudeApiStatus"] = response.StatusCode.ToString();
                    diagnostico["claudeApiOk"] = response.IsSuccessStatusCode;

                    if (!response.IsSuccessStatusCode)
                    {
                        diagnostico["claudeApiError"] = responseContent;
                    }
                    else
                    {
                        diagnostico["claudeApiResponse"] = "Conexion exitosa";
                    }
                }
                catch (Exception ex)
                {
                    diagnostico["claudeApiStatus"] = "Error";
                    diagnostico["claudeApiError"] = ex.Message;
                }
            }

            return Json(diagnostico);
        }

        // ========================================
        // CONFIGURACION DE ALGORITMOS DE FEED
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ConfigurarAlgoritmos()
        {
            // Obtener configuraciones actuales o valores por defecto
            var configuraciones = await _context.ConfiguracionesPlataforma
                .Where(c => c.Categoria == "Algoritmos")
                .ToDictionaryAsync(c => c.Clave, c => c.Valor);

            var modelo = new ConfiguracionAlgoritmosViewModel
            {
                // Para Ti
                ParaTi_Engagement = ObtenerValorConfig(configuraciones, ConfiguracionPlataforma.PARATI_PESO_ENGAGEMENT, 30),
                ParaTi_Intereses = ObtenerValorConfig(configuraciones, ConfiguracionPlataforma.PARATI_PESO_INTERESES, 25),
                ParaTi_CreadorFavorito = ObtenerValorConfig(configuraciones, ConfiguracionPlataforma.PARATI_PESO_CREADOR_FAVORITO, 20),
                ParaTi_TipoContenido = ObtenerValorConfig(configuraciones, ConfiguracionPlataforma.PARATI_PESO_TIPO_CONTENIDO, 10),
                ParaTi_Recencia = ObtenerValorConfig(configuraciones, ConfiguracionPlataforma.PARATI_PESO_RECENCIA, 15),

                // Por Intereses
                Intereses_Categoria = ObtenerValorConfig(configuraciones, ConfiguracionPlataforma.INTERESES_PESO_CATEGORIA, 80),
                Intereses_Descubrimiento = ObtenerValorConfig(configuraciones, ConfiguracionPlataforma.INTERESES_PESO_DESCUBRIMIENTO, 20)
            };

            return View(modelo);
        }

        private int ObtenerValorConfig(Dictionary<string, string> config, string clave, int valorDefault)
        {
            if (config.TryGetValue(clave, out var valor) && int.TryParse(valor, out int resultado))
                return resultado;
            return valorDefault;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarConfiguracionAlgoritmos(ConfiguracionAlgoritmosViewModel modelo)
        {
            // Validar que Para Ti sume 100
            int sumaParaTi = modelo.ParaTi_Engagement + modelo.ParaTi_Intereses +
                            modelo.ParaTi_CreadorFavorito + modelo.ParaTi_TipoContenido + modelo.ParaTi_Recencia;

            if (sumaParaTi != 100)
            {
                TempData["Error"] = $"Los pesos de 'Para Ti' deben sumar 100. Actual: {sumaParaTi}";
                return RedirectToAction("ConfigurarAlgoritmos");
            }

            // Validar que Por Intereses sume 100
            int sumaIntereses = modelo.Intereses_Categoria + modelo.Intereses_Descubrimiento;
            if (sumaIntereses != 100)
            {
                TempData["Error"] = $"Los pesos de 'Por Intereses' deben sumar 100. Actual: {sumaIntereses}";
                return RedirectToAction("ConfigurarAlgoritmos");
            }

            // Guardar configuraciones
            var configuraciones = new Dictionary<string, int>
            {
                { ConfiguracionPlataforma.PARATI_PESO_ENGAGEMENT, modelo.ParaTi_Engagement },
                { ConfiguracionPlataforma.PARATI_PESO_INTERESES, modelo.ParaTi_Intereses },
                { ConfiguracionPlataforma.PARATI_PESO_CREADOR_FAVORITO, modelo.ParaTi_CreadorFavorito },
                { ConfiguracionPlataforma.PARATI_PESO_TIPO_CONTENIDO, modelo.ParaTi_TipoContenido },
                { ConfiguracionPlataforma.PARATI_PESO_RECENCIA, modelo.ParaTi_Recencia },
                { ConfiguracionPlataforma.INTERESES_PESO_CATEGORIA, modelo.Intereses_Categoria },
                { ConfiguracionPlataforma.INTERESES_PESO_DESCUBRIMIENTO, modelo.Intereses_Descubrimiento }
            };

            foreach (var config in configuraciones)
            {
                var existente = await _context.ConfiguracionesPlataforma
                    .FirstOrDefaultAsync(c => c.Clave == config.Key);

                if (existente != null)
                {
                    existente.Valor = config.Value.ToString();
                    existente.UltimaModificacion = DateTime.Now;
                }
                else
                {
                    _context.ConfiguracionesPlataforma.Add(new ConfiguracionPlataforma
                    {
                        Clave = config.Key,
                        Valor = config.Value.ToString(),
                        Categoria = "Algoritmos",
                        Descripcion = ObtenerDescripcionConfig(config.Key),
                        UltimaModificacion = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Configuracion de algoritmos guardada correctamente";
            return RedirectToAction("ConfigurarAlgoritmos");
        }

        private string ObtenerDescripcionConfig(string clave)
        {
            return clave switch
            {
                ConfiguracionPlataforma.PARATI_PESO_ENGAGEMENT => "Peso del engagement (likes, comentarios, vistas) en Para Ti",
                ConfiguracionPlataforma.PARATI_PESO_INTERESES => "Peso de los intereses del usuario en Para Ti",
                ConfiguracionPlataforma.PARATI_PESO_CREADOR_FAVORITO => "Peso de los creadores favoritos en Para Ti",
                ConfiguracionPlataforma.PARATI_PESO_TIPO_CONTENIDO => "Peso del tipo de contenido preferido en Para Ti",
                ConfiguracionPlataforma.PARATI_PESO_RECENCIA => "Peso de la recencia (contenido nuevo) en Para Ti",
                ConfiguracionPlataforma.INTERESES_PESO_CATEGORIA => "Porcentaje de contenido de categorias de interes",
                ConfiguracionPlataforma.INTERESES_PESO_DESCUBRIMIENTO => "Porcentaje de contenido de descubrimiento",
                _ => ""
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerAlgoritmos()
        {
            var clavesAlgoritmos = new[]
            {
                ConfiguracionPlataforma.PARATI_PESO_ENGAGEMENT,
                ConfiguracionPlataforma.PARATI_PESO_INTERESES,
                ConfiguracionPlataforma.PARATI_PESO_CREADOR_FAVORITO,
                ConfiguracionPlataforma.PARATI_PESO_TIPO_CONTENIDO,
                ConfiguracionPlataforma.PARATI_PESO_RECENCIA,
                ConfiguracionPlataforma.INTERESES_PESO_CATEGORIA,
                ConfiguracionPlataforma.INTERESES_PESO_DESCUBRIMIENTO
            };

            var configuraciones = await _context.ConfiguracionesPlataforma
                .Where(c => clavesAlgoritmos.Contains(c.Clave))
                .ToListAsync();

            _context.ConfiguracionesPlataforma.RemoveRange(configuraciones);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Configuracion restablecida a valores por defecto" });
        }

        // ========================================
        // GESTIÓN DE BLOQUEADOS
        // ========================================

        public async Task<IActionResult> Bloqueados()
        {
            // Bloqueos entre usuarios
            var bloqueosUsuarios = await _context.BloqueosUsuarios
                .Include(b => b.Bloqueador)
                .Include(b => b.Bloqueado)
                .OrderByDescending(b => b.FechaBloqueo)
                .Take(100)
                .ToListAsync();

            // IPs bloqueadas - separar por tipo
            var todasIpsBloqueadas = await _context.IpsBloqueadas
                .Where(ip => ip.EstaActivo)
                .OrderByDescending(ip => ip.FechaBloqueo)
                .ToListAsync();

            var ipsManuales = todasIpsBloqueadas.Where(ip => ip.TipoBloqueo == TipoBloqueoIp.Manual).ToList();
            var ipsAutomaticas = todasIpsBloqueadas.Where(ip => ip.TipoBloqueo == TipoBloqueoIp.Automatico).ToList();

            // Usuarios desactivados por admin
            var usuariosBloqueados = await _context.Users
                .Where(u => !u.EstaActivo)
                .OrderByDescending(u => u.FechaRegistro)
                .ToListAsync();

            // Intentos de ataque recientes
            var intentosAtaque = await _context.IntentosAtaque
                .OrderByDescending(i => i.Fecha)
                .Take(50)
                .ToListAsync();

            // Estadísticas de ataques
            var estadisticasAtaques = await _rateLimitService.GetEstadisticasAsync();

            ViewBag.BloqueosUsuarios = bloqueosUsuarios;
            ViewBag.IpsBloqueadas = todasIpsBloqueadas;
            ViewBag.IpsManuales = ipsManuales;
            ViewBag.IpsAutomaticas = ipsAutomaticas;
            ViewBag.UsuariosBloqueados = usuariosBloqueados;
            ViewBag.IntentosAtaque = intentosAtaque;
            ViewBag.EstadisticasAtaques = estadisticasAtaques;

            ViewBag.TotalBloqueosUsuarios = bloqueosUsuarios.Count;
            ViewBag.TotalIpsBloqueadas = todasIpsBloqueadas.Count;
            ViewBag.TotalIpsManuales = ipsManuales.Count;
            ViewBag.TotalIpsAutomaticas = ipsAutomaticas.Count;
            ViewBag.TotalUsuariosBloqueados = usuariosBloqueados.Count;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BloquearIp([FromBody] BloquearIpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DireccionIp))
            {
                return Json(new { success = false, message = "La dirección IP es requerida" });
            }

            // Validar formato IP
            if (!System.Net.IPAddress.TryParse(request.DireccionIp.Trim(), out _))
            {
                return Json(new { success = false, message = "Formato de IP inválido" });
            }

            // Verificar si ya existe
            var existente = await _context.IpsBloqueadas
                .FirstOrDefaultAsync(ip => ip.DireccionIp == request.DireccionIp.Trim());

            if (existente != null)
            {
                if (existente.EstaActivo)
                {
                    return Json(new { success = false, message = "Esta IP ya está bloqueada" });
                }
                // Reactivar bloqueo existente
                existente.EstaActivo = true;
                existente.FechaBloqueo = DateTime.Now;
                existente.Razon = request.Razon;
                existente.FechaExpiracion = request.Permanente ? null : request.FechaExpiracion;
                existente.AdminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }
            else
            {
                var nuevaIp = new IpBloqueada
                {
                    DireccionIp = request.DireccionIp.Trim(),
                    Razon = request.Razon,
                    FechaExpiracion = request.Permanente ? null : request.FechaExpiracion,
                    AdminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    EstaActivo = true,
                    TipoBloqueo = TipoBloqueoIp.Manual,
                    TipoAtaque = TipoAtaque.Ninguno
                };
                _context.IpsBloqueadas.Add(nuevaIp);
            }

            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"IP bloqueada: {request.DireccionIp}",
                CategoriaEvento.Admin,
                TipoLogEvento.Warning,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                User.Identity?.Name);

            return Json(new { success = true, message = $"IP {request.DireccionIp} bloqueada correctamente" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesbloquearIp([FromBody] DesbloquearIpRequest request)
        {
            var ip = await _context.IpsBloqueadas.FindAsync(request.Id);
            if (ip == null)
            {
                return Json(new { success = false, message = "IP no encontrada" });
            }

            ip.EstaActivo = false;
            await _context.SaveChangesAsync();

            // Remover IP de cache inmediatamente para que el desbloqueo sea efectivo al instante
            _rateLimitService.RemoveIpFromCache(ip.DireccionIp);

            await _logEventoService.RegistrarEventoAsync(
                $"IP desbloqueada: {ip.DireccionIp}",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                User.Identity?.Name);

            return Json(new { success = true, message = $"IP {ip.DireccionIp} desbloqueada" });
        }

        /// <summary>
        /// Vista dedicada para gestión de IPs bloqueadas
        /// </summary>
        public async Task<IActionResult> IpsBloqueadas(string? filtro = null, string? tipo = null)
        {
            var query = _context.IpsBloqueadas.AsQueryable();

            // Filtrar por estado activo/inactivo
            if (filtro == "activas")
                query = query.Where(ip => ip.EstaActivo);
            else if (filtro == "inactivas")
                query = query.Where(ip => !ip.EstaActivo);

            // Filtrar por tipo de bloqueo
            if (tipo == "manual")
                query = query.Where(ip => ip.TipoBloqueo == TipoBloqueoIp.Manual);
            else if (tipo == "automatico")
                query = query.Where(ip => ip.TipoBloqueo == TipoBloqueoIp.Automatico);

            var ipsBloqueadas = await query
                .OrderByDescending(ip => ip.FechaBloqueo)
                .ToListAsync();

            // Estadísticas
            var totalActivas = await _context.IpsBloqueadas.CountAsync(ip => ip.EstaActivo);
            var totalManuales = await _context.IpsBloqueadas.CountAsync(ip => ip.EstaActivo && ip.TipoBloqueo == TipoBloqueoIp.Manual);
            var totalAutomaticas = await _context.IpsBloqueadas.CountAsync(ip => ip.EstaActivo && ip.TipoBloqueo == TipoBloqueoIp.Automatico);
            var bloqueosHoy = await _context.IpsBloqueadas.CountAsync(ip => ip.FechaBloqueo.Date == DateTime.Today);

            // Intentos de ataque recientes
            var intentosRecientes = await _context.IntentosAtaque
                .OrderByDescending(i => i.Fecha)
                .Take(20)
                .ToListAsync();

            ViewBag.IpsBloqueadas = ipsBloqueadas;
            ViewBag.TotalActivas = totalActivas;
            ViewBag.TotalManuales = totalManuales;
            ViewBag.TotalAutomaticas = totalAutomaticas;
            ViewBag.BloqueosHoy = bloqueosHoy;
            ViewBag.IntentosRecientes = intentosRecientes;
            ViewBag.FiltroActual = filtro;
            ViewBag.TipoActual = tipo;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarBloqueoUsuario([FromBody] EliminarBloqueoRequest request)
        {
            var bloqueo = await _context.BloqueosUsuarios
                .Include(b => b.Bloqueador)
                .Include(b => b.Bloqueado)
                .FirstOrDefaultAsync(b => b.Id == request.Id);

            if (bloqueo == null)
            {
                return Json(new { success = false, message = "Bloqueo no encontrado" });
            }

            _context.BloqueosUsuarios.Remove(bloqueo);
            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Bloqueo eliminado por admin: {bloqueo.Bloqueador?.UserName} -> {bloqueo.Bloqueado?.UserName}",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                User.Identity?.Name);

            return Json(new { success = true, message = "Bloqueo eliminado correctamente" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivarUsuario([FromBody] ActivarUsuarioRequest request)
        {
            var usuario = await _context.Users.FindAsync(request.UsuarioId);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no encontrado" });
            }

            usuario.EstaActivo = true;
            await _context.SaveChangesAsync();

            await _logEventoService.RegistrarEventoAsync(
                $"Usuario reactivado por admin: {usuario.UserName}",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                User.Identity?.Name);

            return Json(new { success = true, message = $"Usuario {usuario.UserName} activado correctamente" });
        }

        // Request DTOs para Bloqueados
        public class BloquearIpRequest
        {
            public string DireccionIp { get; set; } = string.Empty;
            public string? Razon { get; set; }
            public bool Permanente { get; set; } = true;
            public DateTime? FechaExpiracion { get; set; }
        }

        public class DesbloquearIpRequest
        {
            public int Id { get; set; }
        }

        public class EliminarBloqueoRequest
        {
            public int Id { get; set; }
        }

        public class ActivarUsuarioRequest
        {
            public string UsuarioId { get; set; } = string.Empty;
        }

        // ========================================
        // LOGS DEL SISTEMA
        // ========================================

        public async Task<IActionResult> Logs(
            TipoLogEvento? tipo = null,
            CategoriaEvento? categoria = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? busqueda = null,
            int pagina = 1)
        {
            var filtro = new LogEventosFiltro
            {
                Tipo = tipo,
                Categoria = categoria,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                Busqueda = busqueda,
                Pagina = pagina,
                TamanoPagina = 50
            };

            var resultado = await _logEventoService.ObtenerLogsAsync(filtro);
            var estadisticas = await _logEventoService.ObtenerEstadisticasAsync();

            ViewBag.Filtro = filtro;
            ViewBag.Estadisticas = estadisticas;
            ViewBag.Resultado = resultado;

            return View(resultado.Logs);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerLogs(
            TipoLogEvento? tipo = null,
            CategoriaEvento? categoria = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? busqueda = null,
            int pagina = 1)
        {
            var filtro = new LogEventosFiltro
            {
                Tipo = tipo,
                Categoria = categoria,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                Busqueda = busqueda,
                Pagina = pagina,
                TamanoPagina = 50
            };

            var resultado = await _logEventoService.ObtenerLogsAsync(filtro);

            return Json(new
            {
                success = true,
                logs = resultado.Logs.Select(l => new
                {
                    l.Id,
                    fecha = l.Fecha.ToString("dd/MM/yyyy HH:mm:ss"),
                    tipo = l.Tipo.ToString(),
                    categoria = l.Categoria.ToString(),
                    l.Mensaje,
                    l.Detalle,
                    l.UsuarioNombre,
                    l.IpAddress,
                    l.Url,
                    l.TipoExcepcion
                }),
                total = resultado.Total,
                pagina = resultado.Pagina,
                totalPaginas = resultado.TotalPaginas
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimpiarLogs(int dias = 30)
        {
            var eliminados = await _logEventoService.LimpiarLogsAntiguosAsync(dias);

            await _logEventoService.RegistrarEventoAsync(
                $"Limpieza manual de logs: {eliminados} registros eliminados (>{dias} dias)",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                User.Identity?.Name);

            return Json(new { success = true, eliminados, message = $"Se eliminaron {eliminados} logs antiguos" });
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerEstadisticasLogs()
        {
            var stats = await _logEventoService.ObtenerEstadisticasAsync();
            return Json(stats);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDetalleLogs(int id)
        {
            var log = await _context.LogEventos.FindAsync(id);
            if (log == null)
                return Json(new { success = false, message = "Log no encontrado" });

            return Json(new
            {
                success = true,
                log = new
                {
                    log.Id,
                    fecha = log.Fecha.ToString("dd/MM/yyyy HH:mm:ss"),
                    tipo = log.Tipo.ToString(),
                    categoria = log.Categoria.ToString(),
                    log.Mensaje,
                    log.Detalle,
                    log.UsuarioId,
                    log.UsuarioNombre,
                    log.IpAddress,
                    log.UserAgent,
                    log.Url,
                    log.MetodoHttp,
                    log.TipoExcepcion
                }
            });
        }

        /// <summary>
        /// Endpoint para recibir logs desde el frontend (JavaScript)
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RegistrarLogFrontend([FromBody] LogFrontendRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Mensaje))
                {
                    return Json(new { success = false });
                }

                // Mapear tipo de log
                var tipoLog = request.Tipo?.ToLower() switch
                {
                    "error" => TipoLogEvento.Error,
                    "warning" or "warn" => TipoLogEvento.Warning,
                    "info" => TipoLogEvento.Info,
                    _ => TipoLogEvento.Evento
                };

                // Obtener usuario actual si está autenticado
                string? usuarioId = null;
                string? usuarioNombre = null;
                if (User.Identity?.IsAuthenticated == true)
                {
                    usuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    usuarioNombre = User.Identity.Name;
                }

                var log = new LogEvento
                {
                    Fecha = DateTime.UtcNow,
                    Tipo = tipoLog,
                    Categoria = CategoriaEvento.Frontend,
                    Mensaje = (request.Mensaje.Length > 500 ? request.Mensaje.Substring(0, 500) : request.Mensaje),
                    Detalle = request.Detalle,
                    UsuarioId = usuarioId,
                    UsuarioNombre = usuarioNombre,
                    IpAddress = GetClientIpAddress(),
                    UserAgent = Request.Headers["User-Agent"].ToString().Length > 500
                        ? Request.Headers["User-Agent"].ToString().Substring(0, 500)
                        : Request.Headers["User-Agent"].ToString(),
                    Url = request.Url ?? Request.Headers["Referer"].ToString(),
                    MetodoHttp = "JS"
                };

                _context.LogEventos.Add(log);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        private string? GetClientIpAddress()
        {
            var ip = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ip))
                ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            return ip?.Length > 45 ? ip.Substring(0, 45) : ip;
        }

        // ========================================
        // PRUEBA DE EMAIL
        // ========================================
        public async Task<IActionResult> ProbarEmail()
        {
            ViewData["Title"] = "Probar Emails";

            // Obtener configuracion de Mailjet para mostrar estado
            var apiKey = _configuration["Mailjet:ApiKey"];
            var secretKey = _configuration["Mailjet:SecretKey"];
            var fromEmail = _configuration["Mailjet:FromEmail"];
            var fromName = _configuration["Mailjet:FromName"];

            ViewBag.MailjetConfigurado = !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(secretKey);
            ViewBag.FromEmail = fromEmail ?? "No configurado";
            ViewBag.FromName = fromName ?? "No configurado";
            ViewBag.ApiKeyPresente = !string.IsNullOrEmpty(apiKey);
            ViewBag.SecretKeyPresente = !string.IsNullOrEmpty(secretKey);

            // Obtener ultimos logs de email
            var logsEmail = await _context.LogEventos
                .Where(l => l.Mensaje.Contains("email") || l.Mensaje.Contains("Email") || l.Mensaje.Contains("Mailjet"))
                .OrderByDescending(l => l.Fecha)
                .Take(50)
                .ToListAsync();

            return View(logsEmail);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarEmailPrueba(string destinatario, string tipoEmail)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinatario))
                {
                    TempData["Error"] = "Debe especificar un email destinatario.";
                    return RedirectToAction(nameof(ProbarEmail));
                }

                // Registrar inicio del envio
                await _logEventoService.RegistrarEventoAsync(
                    $"Iniciando envio de email de prueba tipo '{tipoEmail}' a {destinatario}",
                    CategoriaEvento.Sistema,
                    TipoLogEvento.Info,
                    null,
                    User.Identity?.Name,
                    $"Usuario admin: {User.Identity?.Name}"
                );

                // Usar el método con resultado detallado para obtener información del error
                Services.EmailResult? emailResult = null;
                bool resultado = false;
                string mensajeResultado = "";

                switch (tipoEmail)
                {
                    case "simple":
                        emailResult = await _emailService.SendEmailWithResultAsync(
                            destinatario,
                            "Email de Prueba - Lado",
                            @"<html>
                                <body style='font-family: Arial, sans-serif; padding: 20px;'>
                                    <h1 style='color: #4682B4;'>Email de Prueba</h1>
                                    <p>Este es un email de prueba enviado desde el panel de administracion de Lado.</p>
                                    <p>Fecha y hora: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + @"</p>
                                    <p>Si recibes este mensaje, la configuracion de Mailjet esta funcionando correctamente.</p>
                                </body>
                            </html>"
                        );
                        resultado = emailResult.Success;
                        mensajeResultado = "Email simple de prueba";
                        break;

                    case "confirmacion":
                        resultado = await _emailService.SendConfirmationEmailAsync(
                            destinatario,
                            "Usuario de Prueba",
                            "https://ladoapp.com/confirmar?token=test123"
                        );
                        mensajeResultado = "Email de confirmacion de cuenta";
                        break;

                    case "recuperar":
                        resultado = await _emailService.SendPasswordResetEmailAsync(
                            destinatario,
                            "Usuario de Prueba",
                            "https://ladoapp.com/recuperar?token=test123"
                        );
                        mensajeResultado = "Email de recuperacion de password";
                        break;

                    case "bienvenida":
                        resultado = await _emailService.SendWelcomeEmailAsync(
                            destinatario,
                            "Usuario de Prueba",
                            "usuario_test",
                            "Password123!"
                        );
                        mensajeResultado = "Email de bienvenida";
                        break;

                    case "suscriptor":
                        resultado = await _emailService.SendNewSubscriberNotificationAsync(
                            destinatario,
                            "Creador de Prueba",
                            "NuevoSuscriptor123"
                        );
                        mensajeResultado = "Email de nuevo suscriptor";
                        break;

                    case "pago":
                        resultado = await _emailService.SendPaymentReceivedNotificationAsync(
                            destinatario,
                            "Usuario de Prueba",
                            25.99m,
                            "Propina de prueba"
                        );
                        mensajeResultado = "Email de pago recibido";
                        break;

                    default:
                        TempData["Error"] = "Tipo de email no valido.";
                        return RedirectToAction(nameof(ProbarEmail));
                }

                // Registrar resultado
                if (resultado)
                {
                    await _logEventoService.RegistrarEventoAsync(
                        $"Email de prueba enviado exitosamente: {mensajeResultado} a {destinatario}",
                        CategoriaEvento.Sistema,
                        TipoLogEvento.Info,
                        null,
                        User.Identity?.Name
                    );
                    TempData["Success"] = $"Email enviado exitosamente a {destinatario}. Tipo: {mensajeResultado}";
                }
                else
                {
                    // Construir mensaje de error detallado
                    var errorMsg = emailResult != null
                        ? $"Error Mailjet: {emailResult.ErrorMessage}"
                        : "El servicio de email retorno false";

                    var errorDetails = emailResult?.ErrorDetails ?? "Sin detalles adicionales";

                    await _logEventoService.RegistrarEventoAsync(
                        $"Fallo envio de email de prueba: {mensajeResultado} a {destinatario}",
                        CategoriaEvento.Sistema,
                        TipoLogEvento.Error,
                        null,
                        User.Identity?.Name,
                        errorDetails
                    );

                    // Mostrar error detallado en la interfaz
                    if (emailResult != null && !string.IsNullOrEmpty(emailResult.ErrorMessage))
                    {
                        TempData["Error"] = $"Error al enviar email: {emailResult.ErrorMessage}";
                        TempData["ErrorDetails"] = emailResult.ErrorDetails;
                    }
                    else
                    {
                        TempData["Error"] = $"Error al enviar email a {destinatario}. Revisa la configuracion de Mailjet y los logs.";
                    }
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"Excepcion al enviar email de prueba a {destinatario}",
                    CategoriaEvento.Sistema,
                    TipoLogEvento.Error,
                    null,
                    User.Identity?.Name,
                    ex.ToString()
                );
                TempData["Error"] = $"Error: {ex.Message}";
            }

            return RedirectToAction(nameof(ProbarEmail));
        }

        // ========================================
        // CONFIGURACIÓN DE EMAIL
        // ========================================

        public async Task<IActionResult> ConfiguracionEmail()
        {
            ViewData["Title"] = "Configuracion de Email";

            // Cargar configuraciones de email desde BD
            var configuraciones = await _context.ConfiguracionesPlataforma
                .Where(c => c.Categoria == "Email")
                .ToDictionaryAsync(c => c.Clave, c => c.Valor);

            var modelo = new ConfiguracionEmailViewModel
            {
                ProveedorActivo = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_PROVEEDOR_ACTIVO, "Mailjet"),
                FromEmail = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_FROM_EMAIL, ""),
                FromName = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_FROM_NAME, ""),

                // Mailjet
                MailjetApiKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.MAILJET_API_KEY, ""),
                MailjetSecretKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.MAILJET_SECRET_KEY, ""),

                // Amazon SES
                AmazonSesAccessKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_ACCESS_KEY, ""),
                AmazonSesSecretKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_SECRET_KEY, ""),
                AmazonSesRegion = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_REGION, "us-east-1"),

                // Brevo
                BrevoApiKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.BREVO_API_KEY, "")
            };

            // Enmascarar claves secretas para mostrar (solo ultimos 4 caracteres)
            ViewBag.MailjetSecretKeyMasked = EnmascararClave(modelo.MailjetSecretKey);
            ViewBag.AmazonSesSecretKeyMasked = EnmascararClave(modelo.AmazonSesSecretKey);
            ViewBag.BrevoApiKeyMasked = EnmascararClave(modelo.BrevoApiKey);

            return View(modelo);
        }

        private string EnmascararClave(string clave)
        {
            if (string.IsNullOrEmpty(clave) || clave.Length < 4)
                return string.IsNullOrEmpty(clave) ? "" : "****";
            return new string('*', clave.Length - 4) + clave.Substring(clave.Length - 4);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarConfiguracionEmail(ConfiguracionEmailViewModel modelo)
        {
            try
            {
                var ahora = DateTime.Now;

                // Guardar proveedor activo
                await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.EMAIL_PROVEEDOR_ACTIVO, modelo.ProveedorActivo, ahora);
                await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.EMAIL_FROM_EMAIL, modelo.FromEmail, ahora);
                await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.EMAIL_FROM_NAME, modelo.FromName, ahora);

                // Guardar configuracion de Mailjet
                await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.MAILJET_API_KEY, modelo.MailjetApiKey, ahora);

                // Solo actualizar SecretKey si se proporciono un valor nuevo (no enmascarado)
                if (!string.IsNullOrEmpty(modelo.MailjetSecretKey) && !modelo.MailjetSecretKey.StartsWith("*"))
                {
                    await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.MAILJET_SECRET_KEY, modelo.MailjetSecretKey, ahora);
                }

                // Guardar configuracion de Amazon SES
                await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.AMAZONSES_ACCESS_KEY, modelo.AmazonSesAccessKey, ahora);
                await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.AMAZONSES_REGION, modelo.AmazonSesRegion, ahora);

                // Solo actualizar SecretKey si se proporciono un valor nuevo (no enmascarado)
                if (!string.IsNullOrEmpty(modelo.AmazonSesSecretKey) && !modelo.AmazonSesSecretKey.StartsWith("*"))
                {
                    await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.AMAZONSES_SECRET_KEY, modelo.AmazonSesSecretKey, ahora);
                }

                // Guardar configuracion de Brevo
                if (!string.IsNullOrEmpty(modelo.BrevoApiKey) && !modelo.BrevoApiKey.StartsWith("*"))
                {
                    await ActualizarConfiguracionEmailAsync(ConfiguracionPlataforma.BREVO_API_KEY, modelo.BrevoApiKey, ahora);
                }

                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Configuracion de email actualizada. Proveedor activo: {modelo.ProveedorActivo}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    User.Identity?.Name);

                TempData["Success"] = $"Configuracion de email actualizada correctamente. Proveedor activo: {modelo.ProveedorActivo}";
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"Error al actualizar configuracion de email: {ex.Message}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Error,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    User.Identity?.Name,
                    ex.ToString());

                TempData["Error"] = $"Error al actualizar la configuracion: {ex.Message}";
            }

            return RedirectToAction(nameof(ConfiguracionEmail));
        }

        private async Task ActualizarConfiguracionEmailAsync(string clave, string valor, DateTime fecha)
        {
            var config = await _context.ConfiguracionesPlataforma.FirstOrDefaultAsync(c => c.Clave == clave);
            if (config != null)
            {
                config.Valor = valor ?? "";
                config.UltimaModificacion = fecha;
            }
            else
            {
                _context.ConfiguracionesPlataforma.Add(new ConfiguracionPlataforma
                {
                    Clave = clave,
                    Valor = valor ?? "",
                    Categoria = "Email",
                    UltimaModificacion = fecha
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProbarProveedorEmail(string proveedor, string destinatario)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinatario))
                {
                    return Json(new { success = false, message = "Debe especificar un email destinatario" });
                }

                // Cargar configuraciones de email
                var configuraciones = await _context.ConfiguracionesPlataforma
                    .Where(c => c.Categoria == "Email")
                    .ToDictionaryAsync(c => c.Clave, c => c.Valor);

                var fromEmail = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_FROM_EMAIL, "noreply@ladoapp.com");
                var fromName = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_FROM_NAME, "Lado");

                IEmailProvider? emailProvider = null;

                if (proveedor == "Mailjet")
                {
                    var apiKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.MAILJET_API_KEY, "");
                    var secretKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.MAILJET_SECRET_KEY, "");

                    if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
                    {
                        return Json(new { success = false, message = "Credenciales de Mailjet no configuradas" });
                    }

                    emailProvider = new MailjetEmailProvider(apiKey, secretKey);
                }
                else if (proveedor == "AmazonSES")
                {
                    var accessKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_ACCESS_KEY, "");
                    var secretKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_SECRET_KEY, "");
                    var region = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_REGION, "us-east-1");

                    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                    {
                        return Json(new { success = false, message = "Credenciales de Amazon SES no configuradas" });
                    }

                    emailProvider = new AmazonSesEmailProvider(accessKey, secretKey, region);
                }
                else if (proveedor == "Brevo")
                {
                    var apiKey = configuraciones.GetValueOrDefault(ConfiguracionPlataforma.BREVO_API_KEY, "");

                    if (string.IsNullOrEmpty(apiKey))
                    {
                        return Json(new { success = false, message = "API Key de Brevo no configurada" });
                    }

                    emailProvider = new BrevoEmailProvider(apiKey);
                }
                else
                {
                    return Json(new { success = false, message = $"Proveedor no reconocido: {proveedor}" });
                }

                // Enviar email de prueba
                var resultado = await emailProvider.SendEmailAsync(
                    destinatario,
                    $"Prueba de {proveedor} - Lado",
                    $@"<html>
                        <body style='font-family: Arial, sans-serif; padding: 20px;'>
                            <h2>Prueba de conexion exitosa</h2>
                            <p>Este es un email de prueba enviado desde el panel de administracion de Lado.</p>
                            <p><strong>Proveedor:</strong> {proveedor}</p>
                            <p><strong>Fecha:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
                            <hr>
                            <p style='color: #666; font-size: 12px;'>Este mensaje fue generado automaticamente.</p>
                        </body>
                    </html>",
                    fromEmail,
                    fromName);

                if (resultado.Success)
                {
                    await _logEventoService.RegistrarEventoAsync(
                        $"Prueba de email exitosa con {proveedor} a {destinatario}",
                        CategoriaEvento.Admin,
                        TipoLogEvento.Info,
                        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                        User.Identity?.Name);

                    return Json(new { success = true, message = $"Email enviado correctamente via {proveedor}", messageId = resultado.MessageId });
                }
                else
                {
                    await _logEventoService.RegistrarEventoAsync(
                        $"Error en prueba de email con {proveedor}: {resultado.ErrorMessage}",
                        CategoriaEvento.Admin,
                        TipoLogEvento.Error,
                        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                        User.Identity?.Name,
                        resultado.ErrorDetails);

                    return Json(new { success = false, message = resultado.ErrorMessage, details = resultado.ErrorDetails });
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"Excepcion en prueba de email: {ex.Message}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Error,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    User.Identity?.Name,
                    ex.ToString());

                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // LIMPIEZA DE COMENTARIOS SPAM
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ComentariosSpam()
        {
            // Obtener posts con más de 50 comentarios (sospechosos)
            var postsConMuchosComentarios = await _context.Contenidos
                .Where(c => c.NumeroComentarios > 50)
                .OrderByDescending(c => c.NumeroComentarios)
                .Select(c => new
                {
                    c.Id,
                    c.Descripcion,
                    c.NumeroComentarios,
                    Usuario = c.Usuario.UserName,
                    c.FechaPublicacion
                })
                .Take(20)
                .ToListAsync();

            return Json(new { success = true, posts = postsConMuchosComentarios });
        }

        [HttpGet]
        public async Task<IActionResult> DetalleComentariosSpam(int contenidoId)
        {
            // Comentarios agrupados por usuario
            var comentariosPorUsuario = await _context.Comentarios
                .Where(c => c.ContenidoId == contenidoId && c.EstaActivo)
                .GroupBy(c => new { c.UsuarioId, c.Usuario.UserName })
                .Select(g => new
                {
                    UsuarioId = g.Key.UsuarioId,
                    UserName = g.Key.UserName,
                    Cantidad = g.Count(),
                    PrimerComentario = g.Min(c => c.FechaCreacion),
                    UltimoComentario = g.Max(c => c.FechaCreacion)
                })
                .OrderByDescending(x => x.Cantidad)
                .Take(50)
                .ToListAsync();

            var totalComentarios = await _context.Comentarios
                .CountAsync(c => c.ContenidoId == contenidoId && c.EstaActivo);

            return Json(new
            {
                success = true,
                contenidoId,
                totalComentarios,
                comentariosPorUsuario
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimpiarComentariosSpam(int contenidoId, string? usuarioId = null, int? mantenerPrimeros = 3)
        {
            try
            {
                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                int eliminados = 0;

                if (!string.IsNullOrEmpty(usuarioId))
                {
                    // Eliminar comentarios de un usuario específico (mantener los primeros N)
                    var comentariosUsuario = await _context.Comentarios
                        .Where(c => c.ContenidoId == contenidoId && c.UsuarioId == usuarioId && c.EstaActivo)
                        .OrderBy(c => c.FechaCreacion)
                        .ToListAsync();

                    var aEliminar = comentariosUsuario.Skip(mantenerPrimeros ?? 3).ToList();
                    foreach (var c in aEliminar)
                    {
                        c.EstaActivo = false;
                    }
                    eliminados = aEliminar.Count;
                }
                else
                {
                    // Eliminar comentarios excesivos de TODOS los usuarios (mantener primeros N de cada uno)
                    var comentarios = await _context.Comentarios
                        .Where(c => c.ContenidoId == contenidoId && c.EstaActivo)
                        .OrderBy(c => c.UsuarioId)
                        .ThenBy(c => c.FechaCreacion)
                        .ToListAsync();

                    var porUsuario = comentarios.GroupBy(c => c.UsuarioId);
                    foreach (var grupo in porUsuario)
                    {
                        var aEliminar = grupo.Skip(mantenerPrimeros ?? 3).ToList();
                        foreach (var c in aEliminar)
                        {
                            c.EstaActivo = false;
                        }
                        eliminados += aEliminar.Count;
                    }
                }

                // Actualizar contador del contenido
                var contenido = await _context.Contenidos.FindAsync(contenidoId);
                if (contenido != null)
                {
                    contenido.NumeroComentarios = await _context.Comentarios
                        .CountAsync(c => c.ContenidoId == contenidoId && c.EstaActivo);
                }

                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Limpieza de spam: {eliminados} comentarios desactivados en contenido {contenidoId}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Warning,
                    adminId,
                    User.Identity?.Name);

                return Json(new { success = true, eliminados, message = $"Se desactivaron {eliminados} comentarios spam" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BloquearSpammer(string usuarioId, string razon = "Spam de comentarios")
        {
            try
            {
                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario == null)
                    return Json(new { success = false, message = "Usuario no encontrado" });

                usuario.EstaActivo = false;
                await _context.SaveChangesAsync();

                var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                await _logEventoService.RegistrarEventoAsync(
                    $"Usuario bloqueado por spam: {usuario.UserName} ({usuarioId}). Razón: {razon}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Warning,
                    adminId,
                    User.Identity?.Name);

                return Json(new { success = true, message = $"Usuario {usuario.UserName} bloqueado" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // CONFIGURACIÓN DE NIVELES DE CONFIANZA
        // ========================================

        public async Task<IActionResult> ConfigurarConfianza()
        {
            var config = await _context.ConfiguracionesConfianza.FirstOrDefaultAsync();

            if (config == null)
            {
                config = new ConfiguracionConfianza();
                _context.ConfiguracionesConfianza.Add(config);
                await _context.SaveChangesAsync();
            }

            ViewData["Title"] = "Configurar Niveles de Confianza";
            return View(config);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarConfiguracionConfianza(ConfiguracionConfianza model)
        {
            try
            {
                var config = await _context.ConfiguracionesConfianza.FirstOrDefaultAsync();

                if (config == null)
                {
                    config = new ConfiguracionConfianza();
                    _context.ConfiguracionesConfianza.Add(config);
                }

                // Actualizar criterio 1: Verificación de identidad
                config.VerificacionIdentidadHabilitada = model.VerificacionIdentidadHabilitada;
                config.PuntosVerificacionIdentidad = model.PuntosVerificacionIdentidad;
                config.DescripcionVerificacionIdentidad = model.DescripcionVerificacionIdentidad;

                // Actualizar criterio 2: Verificación de edad
                config.VerificacionEdadHabilitada = model.VerificacionEdadHabilitada;
                config.PuntosVerificacionEdad = model.PuntosVerificacionEdad;
                config.DescripcionVerificacionEdad = model.DescripcionVerificacionEdad;

                // Actualizar criterio 3: Tasa de respuesta
                config.TasaRespuestaHabilitada = model.TasaRespuestaHabilitada;
                config.PuntosTasaRespuesta = model.PuntosTasaRespuesta;
                config.PorcentajeMinimoRespuesta = model.PorcentajeMinimoRespuesta;
                config.DescripcionTasaRespuesta = model.DescripcionTasaRespuesta;

                // Actualizar criterio 4: Actividad reciente
                config.ActividadRecienteHabilitada = model.ActividadRecienteHabilitada;
                config.PuntosActividadReciente = model.PuntosActividadReciente;
                config.HorasMaximasInactividad = model.HorasMaximasInactividad;
                config.DescripcionActividadReciente = model.DescripcionActividadReciente;

                // Actualizar criterio 5: Contenido publicado
                config.ContenidoPublicadoHabilitado = model.ContenidoPublicadoHabilitado;
                config.PuntosContenidoPublicado = model.PuntosContenidoPublicado;
                config.MinimoPublicaciones = model.MinimoPublicaciones;
                config.DescripcionContenidoPublicado = model.DescripcionContenidoPublicado;

                // Actualizar configuración general
                config.NivelMaximo = model.NivelMaximo;
                config.MostrarBadgesEnPerfil = model.MostrarBadgesEnPerfil;
                config.MostrarEstrellasEnPerfil = model.MostrarEstrellasEnPerfil;

                // Auditoría
                config.FechaModificacion = DateTime.Now;
                config.ModificadoPor = User.Identity?.Name;

                await _context.SaveChangesAsync();

                // Invalidar cache del TrustService
                Services.TrustService.InvalidarCache();

                await _logEventoService.RegistrarEventoAsync(
                    "Configuración de niveles de confianza actualizada",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    User.Identity?.Name);

                TempData["Success"] = "Configuración de confianza actualizada correctamente";
                return RedirectToAction("ConfigurarConfianza");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al guardar: {ex.Message}";
                return RedirectToAction("ConfigurarConfianza");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerConfiguracionConfianza()
        {
            var config = await _context.ConfiguracionesConfianza.FirstOrDefaultAsync();
            if (config == null)
            {
                return Json(new { success = false, message = "Configuración no encontrada" });
            }

            return Json(new
            {
                success = true,
                config = new
                {
                    // Criterio 1
                    config.VerificacionIdentidadHabilitada,
                    config.PuntosVerificacionIdentidad,
                    config.DescripcionVerificacionIdentidad,
                    // Criterio 2
                    config.VerificacionEdadHabilitada,
                    config.PuntosVerificacionEdad,
                    config.DescripcionVerificacionEdad,
                    // Criterio 3
                    config.TasaRespuestaHabilitada,
                    config.PuntosTasaRespuesta,
                    config.PorcentajeMinimoRespuesta,
                    config.DescripcionTasaRespuesta,
                    // Criterio 4
                    config.ActividadRecienteHabilitada,
                    config.PuntosActividadReciente,
                    config.HorasMaximasInactividad,
                    config.DescripcionActividadReciente,
                    // Criterio 5
                    config.ContenidoPublicadoHabilitado,
                    config.PuntosContenidoPublicado,
                    config.MinimoPublicaciones,
                    config.DescripcionContenidoPublicado,
                    // General
                    config.NivelMaximo,
                    config.MostrarBadgesEnPerfil,
                    config.MostrarEstrellasEnPerfil,
                    // Auditoría
                    config.FechaModificacion,
                    config.ModificadoPor
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetearConfiguracionConfianza()
        {
            try
            {
                var config = await _context.ConfiguracionesConfianza.FirstOrDefaultAsync();
                if (config != null)
                {
                    _context.ConfiguracionesConfianza.Remove(config);
                    await _context.SaveChangesAsync();
                }

                // Crear nueva configuración con valores por defecto
                var nuevaConfig = new ConfiguracionConfianza
                {
                    FechaModificacion = DateTime.Now,
                    ModificadoPor = User.Identity?.Name
                };
                _context.ConfiguracionesConfianza.Add(nuevaConfig);
                await _context.SaveChangesAsync();

                // Invalidar cache
                Services.TrustService.InvalidarCache();

                await _logEventoService.RegistrarEventoAsync(
                    "Configuración de niveles de confianza reseteada a valores por defecto",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Warning,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    User.Identity?.Name);

                TempData["Success"] = "Configuración reseteada a valores por defecto";
                return RedirectToAction("ConfigurarConfianza");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al resetear: {ex.Message}";
                return RedirectToAction("ConfigurarConfianza");
            }
        }

        // ========================================
        // EMAIL MASIVO
        // ========================================

        public async Task<IActionResult> EmailMasivo()
        {
            // Inicializar o actualizar plantillas predeterminadas
            await CrearPlantillasPredeterminadasAsync();

            var plantillas = await _context.PlantillasEmail
                .Where(p => p.EstaActiva)
                .OrderByDescending(p => p.FechaCreacion)
                .ToListAsync();

            var campanas = await _context.CampanasEmail
                .Include(c => c.Plantilla)
                .Include(c => c.CreadoPor)
                .OrderByDescending(c => c.FechaCreacion)
                .Take(50)
                .ToListAsync();

            // Estadísticas
            var totalEnviados = await _context.CampanasEmail
                .Where(c => c.Estado == EstadoCampanaEmail.Enviada)
                .SumAsync(c => c.Enviados);

            var totalCampanas = await _context.CampanasEmail.CountAsync();

            ViewBag.Plantillas = plantillas;
            ViewBag.Campanas = campanas;
            ViewBag.TotalEnviados = totalEnviados;
            ViewBag.TotalCampanas = totalCampanas;
            ViewBag.PlantillasCategorias = PlantillaEmail.CategoriasDisponibles;
            ViewBag.Placeholders = PlantillaEmail.PlaceholdersDisponibles;

            return View();
        }

        private async Task CrearPlantillasPredeterminadasAsync()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var logoUrl = $"{baseUrl}/images/logo.png";
            var year = DateTime.Now.Year;

            // Obtener nombres de plantillas existentes
            var plantillasExistentes = await _context.PlantillasEmail
                .Select(p => p.Nombre)
                .ToListAsync();

            var plantillasNuevas = new List<PlantillaEmail>();
            var todasLasPlantillas = new List<PlantillaEmail>
            {
                new PlantillaEmail
                {
                    Nombre = "Comunicado General",
                    Descripcion = "Plantilla para comunicados oficiales de la plataforma",
                    Categoria = "Comunicado",
                    Asunto = "Comunicado importante de Lado",
                    ContenidoHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
<div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
<div style='background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.1);'>
<div style='background:linear-gradient(135deg,#4682B4 0%,#36648B 100%);padding:30px;text-align:center;'>
<img src='{logoUrl}' alt='Lado' style='height:50px;margin-bottom:10px;' onerror=""this.style.display='none'"">
<h1 style='color:#ffffff;margin:0;font-size:24px;'>Lado</h1>
</div>
<div style='padding:40px 30px;'>
<h2 style='color:#333;margin:0 0 20px 0;font-size:22px;'>Hola {{{{nombre}}}},</h2>
<p style='color:#666;font-size:16px;line-height:1.8;margin:0 0 20px 0;'>
[Tu mensaje aqui]
</p>
<p style='color:#666;font-size:16px;line-height:1.8;'>
Gracias por ser parte de nuestra comunidad.
</p>
</div>
<div style='background:#f8f9fa;padding:20px 30px;text-align:center;border-top:1px solid #eee;'>
<p style='color:#999;font-size:12px;margin:0;'>&copy; {year} Lado. Todos los derechos reservados.</p>
<p style='margin:10px 0 0 0;'><a href='{baseUrl}' style='color:#4682B4;text-decoration:none;font-size:12px;'>www.ladoapp.com</a></p>
</div>
</div>
</div>
</body>
</html>"
                },
                new PlantillaEmail
                {
                    Nombre = "Promocion Especial",
                    Descripcion = "Plantilla para promociones y ofertas especiales",
                    Categoria = "Promocion",
                    Asunto = "{{nombre}}, tenemos algo especial para ti",
                    ContenidoHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
<div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
<div style='background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.1);'>
<div style='background:linear-gradient(135deg,#4682B4 0%,#36648B 100%);padding:40px;text-align:center;'>
<img src='{logoUrl}' alt='Lado' style='height:50px;margin-bottom:15px;' onerror=""this.style.display='none'"">
<h1 style='color:#ffffff;margin:0;font-size:28px;'>Oferta Especial</h1>
<p style='color:#B0C4DE;margin:10px 0 0 0;font-size:16px;'>Solo por tiempo limitado</p>
</div>
<div style='padding:40px 30px;text-align:center;'>
<h2 style='color:#333;margin:0 0 10px 0;font-size:24px;'>Hola {{{{nombre}}}},</h2>
<p style='color:#666;font-size:16px;line-height:1.8;margin:0 0 30px 0;'>
[Descripcion de la promocion]
</p>
<div style='background:linear-gradient(135deg,#4682B4 0%,#36648B 100%);border-radius:12px;padding:30px;margin:20px 0;'>
<p style='color:#B0C4DE;margin:0 0 5px 0;font-size:14px;'>OFERTA ESPECIAL</p>
<p style='color:#ffffff;margin:0;font-size:36px;font-weight:bold;'>[DESCUENTO]</p>
</div>
<a href='{baseUrl}' style='display:inline-block;background:#4682B4;color:#ffffff;padding:16px 40px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;margin-top:20px;'>Aprovechar Ahora</a>
</div>
<div style='background:#f8f9fa;padding:20px 30px;text-align:center;border-top:1px solid #eee;'>
<p style='color:#999;font-size:12px;margin:0;'>&copy; {year} Lado. Todos los derechos reservados.</p>
</div>
</div>
</div>
</body>
</html>"
                },
                new PlantillaEmail
                {
                    Nombre = "Bienvenida Marketing",
                    Descripcion = "Email de bienvenida para nuevos usuarios",
                    Categoria = "Bienvenida",
                    Asunto = "Bienvenido a Lado, {{nombre}}!",
                    ContenidoHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
<div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
<div style='background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.1);'>
<div style='background:linear-gradient(135deg,#4682B4 0%,#36648B 100%);padding:40px;text-align:center;'>
<img src='{logoUrl}' alt='Lado' style='height:60px;margin-bottom:15px;' onerror=""this.style.display='none'"">
<h1 style='color:#ffffff;margin:0;font-size:32px;'>Bienvenido!</h1>
</div>
<div style='padding:40px 30px;'>
<h2 style='color:#333;margin:0 0 20px 0;font-size:22px;'>Hola {{{{nombre}}}},</h2>
<p style='color:#666;font-size:16px;line-height:1.8;margin:0 0 20px 0;'>
Nos alegra tenerte en Lado. Ahora formas parte de una comunidad unica donde puedes conectar, compartir y descubrir contenido increible.
</p>
<div style='background:#f8f9fa;border-radius:12px;padding:25px;margin:25px 0;'>
<h3 style='color:#4682B4;margin:0 0 15px 0;font-size:18px;'>Que puedes hacer en Lado:</h3>
<ul style='color:#666;font-size:14px;line-height:2;margin:0;padding-left:20px;'>
<li>Descubre creadores y contenido exclusivo</li>
<li>Conecta con tu comunidad</li>
<li>Comparte tus momentos especiales</li>
<li>Monetiza tu contenido si eres creador</li>
</ul>
</div>
<div style='text-align:center;margin-top:30px;'>
<a href='{baseUrl}/Feed' style='display:inline-block;background:#4682B4;color:#ffffff;padding:16px 40px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;'>Explorar Lado</a>
</div>
</div>
<div style='background:#f8f9fa;padding:20px 30px;text-align:center;border-top:1px solid #eee;'>
<p style='color:#999;font-size:12px;margin:0;'>&copy; {year} Lado. Todos los derechos reservados.</p>
</div>
</div>
</div>
</body>
</html>"
                },
                new PlantillaEmail
                {
                    Nombre = "Newsletter Semanal",
                    Descripcion = "Plantilla para newsletter con novedades",
                    Categoria = "Marketing",
                    Asunto = "{{nombre}}, mira lo nuevo en Lado esta semana",
                    ContenidoHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
<div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
<div style='background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.1);'>
<div style='background:linear-gradient(135deg,#4682B4 0%,#36648B 100%);padding:30px;text-align:center;'>
<img src='{logoUrl}' alt='Lado' style='height:45px;margin-bottom:10px;' onerror=""this.style.display='none'"">
<p style='color:#B0C4DE;margin:0;font-size:14px;'>Newsletter Semanal</p>
</div>
<div style='padding:40px 30px;'>
<h2 style='color:#333;margin:0 0 10px 0;font-size:22px;'>Hola {{{{nombre}}}},</h2>
<p style='color:#999;font-size:14px;margin:0 0 25px 0;'>{{{{fecha}}}}</p>
<p style='color:#666;font-size:16px;line-height:1.8;margin:0 0 30px 0;'>
Aqui tienes las novedades mas destacadas de esta semana:
</p>
<div style='border-left:4px solid #4682B4;padding-left:20px;margin:25px 0;'>
<h3 style='color:#4682B4;margin:0 0 10px 0;font-size:18px;'>Novedad 1</h3>
<p style='color:#666;font-size:14px;line-height:1.6;margin:0;'>[Descripcion de la novedad]</p>
</div>
<div style='border-left:4px solid #36648B;padding-left:20px;margin:25px 0;'>
<h3 style='color:#36648B;margin:0 0 10px 0;font-size:18px;'>Novedad 2</h3>
<p style='color:#666;font-size:14px;line-height:1.6;margin:0;'>[Descripcion de la novedad]</p>
</div>
<div style='border-left:4px solid #B0C4DE;padding-left:20px;margin:25px 0;'>
<h3 style='color:#4682B4;margin:0 0 10px 0;font-size:18px;'>Novedad 3</h3>
<p style='color:#666;font-size:14px;line-height:1.6;margin:0;'>[Descripcion de la novedad]</p>
</div>
<div style='text-align:center;margin-top:30px;'>
<a href='{baseUrl}/Feed' style='display:inline-block;background:#4682B4;color:#ffffff;padding:14px 35px;border-radius:8px;text-decoration:none;font-weight:600;font-size:14px;'>Ver mas en Lado</a>
</div>
</div>
<div style='background:#f8f9fa;padding:20px 30px;text-align:center;border-top:1px solid #eee;'>
<p style='color:#999;font-size:11px;margin:0 0 10px 0;'>Recibes este email porque estas suscrito a nuestras novedades.</p>
<p style='color:#999;font-size:12px;margin:0;'>&copy; {year} Lado</p>
</div>
</div>
</div>
</body>
</html>"
                },
                new PlantillaEmail
                {
                    Nombre = "Alerta Simple",
                    Descripcion = "Plantilla minimalista para alertas y avisos",
                    Categoria = "Sistema",
                    Asunto = "Aviso importante - Lado",
                    ContenidoHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
<div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
<div style='background:#ffffff;border-radius:16px;padding:40px;box-shadow:0 4px 20px rgba(0,0,0,0.1);'>
<div style='text-align:center;margin-bottom:30px;'>
<img src='{logoUrl}' alt='Lado' style='height:40px;' onerror=""this.style.display='none'"">
</div>
<h2 style='color:#333;margin:0 0 20px 0;font-size:20px;'>Hola {{{{nombre}}}},</h2>
<div style='background:#fff3cd;border:1px solid #ffc107;border-radius:8px;padding:20px;margin:20px 0;'>
<p style='color:#856404;margin:0;font-size:15px;line-height:1.6;'>
[Tu mensaje de alerta aqui]
</p>
</div>
<p style='color:#666;font-size:14px;line-height:1.6;margin:20px 0 0 0;'>
Si tienes preguntas, no dudes en contactarnos.
</p>
<hr style='border:none;border-top:1px solid #eee;margin:30px 0;'>
<p style='color:#999;font-size:12px;text-align:center;margin:0;'>&copy; {year} Lado</p>
</div>
</div>
</body>
</html>"
                },
                new PlantillaEmail
                {
                    Nombre = "Invitacion Beta Exclusiva",
                    Descripcion = "Invitacion para probar la version Beta y ayudar con feedback",
                    Categoria = "Marketing",
                    Asunto = "{{nombre}}, te invitamos a probar Lado Beta - Tu opinion es importante",
                    ContenidoHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0;padding:0;background-color:#ffffff;font-family:Arial,Helvetica,sans-serif;'>
<div style='max-width:600px;margin:0 auto;padding:20px;'>

<!-- Header con logo -->
<div style='text-align:center;padding:30px 20px;border-bottom:3px solid #4682B4;'>
<img src='{logoUrl}' alt='Lado' style='height:60px;margin-bottom:15px;' onerror=""this.outerHTML='<div style=\\'font-size:42px;color:#4682B4;font-weight:bold;\\'>LADO</div>'"">
<div style='display:inline-block;background:#4682B4;padding:6px 18px;border-radius:20px;'>
<span style='color:#fff;font-size:12px;font-weight:600;letter-spacing:1px;'>BETA</span>
</div>
</div>

<!-- Contenido principal -->
<div style='padding:35px 25px;'>

<h1 style='color:#333;margin:0 0 25px 0;font-size:24px;font-weight:700;text-align:center;'>Te invitamos a probar Lado</h1>

<p style='color:#333;font-size:16px;margin:0 0 20px 0;line-height:1.7;'>Hola <strong style='color:#4682B4;'>{{{{nombre}}}}</strong>,</p>

<p style='color:#555;font-size:15px;line-height:1.8;margin:0 0 25px 0;'>
Estamos desarrollando una nueva version de <strong>Lado</strong> y nos encantaria que fueras parte del proceso. Tu experiencia y opiniones son fundamentales para crear algo mejor.
</p>

<!-- Seccion: Como ayudar -->
<div style='background:#f8f9fa;border-radius:12px;padding:25px;margin:25px 0;'>
<h3 style='color:#4682B4;margin:0 0 20px 0;font-size:16px;font-weight:700;text-align:center;'>Como puedes ayudarnos</h3>

<table style='width:100%;border-collapse:collapse;'>
<tr>
<td style='padding:10px 0;vertical-align:top;width:35px;'>
<div style='background:#4682B4;color:#fff;width:26px;height:26px;border-radius:50%;text-align:center;line-height:26px;font-weight:bold;font-size:13px;'>1</div>
</td>
<td style='padding:10px 0 10px 12px;'>
<p style='margin:0 0 3px 0;color:#333;font-weight:600;font-size:14px;'>Explora la plataforma</p>
<p style='margin:0;color:#666;font-size:13px;'>Navega por las secciones y prueba las funciones</p>
</td>
</tr>
<tr>
<td style='padding:10px 0;vertical-align:top;'>
<div style='background:#5a9bd4;color:#fff;width:26px;height:26px;border-radius:50%;text-align:center;line-height:26px;font-weight:bold;font-size:13px;'>2</div>
</td>
<td style='padding:10px 0 10px 12px;'>
<p style='margin:0 0 3px 0;color:#333;font-weight:600;font-size:14px;'>Reporta errores</p>
<p style='margin:0;color:#666;font-size:13px;'>Si encuentras algo que no funciona, cuentanos</p>
</td>
</tr>
<tr>
<td style='padding:10px 0;vertical-align:top;'>
<div style='background:#7ab8e8;color:#fff;width:26px;height:26px;border-radius:50%;text-align:center;line-height:26px;font-weight:bold;font-size:13px;'>3</div>
</td>
<td style='padding:10px 0 10px 12px;'>
<p style='margin:0 0 3px 0;color:#333;font-weight:600;font-size:14px;'>Sugiere mejoras</p>
<p style='margin:0;color:#666;font-size:13px;'>Tus ideas nos ayudan a mejorar</p>
</td>
</tr>
</table>
</div>

<p style='color:#555;font-size:15px;line-height:1.8;margin:0 0 30px 0;text-align:center;'>
No necesitas ser experto. Solo queremos saber tu opinion honesta sobre la plataforma.
</p>

<!-- CTA Button -->
<div style='text-align:center;margin:30px 0;'>
<a href='{baseUrl}' style='display:inline-block;background:#4682B4;color:#ffffff;padding:14px 40px;border-radius:8px;text-decoration:none;font-weight:600;font-size:15px;'>
Explorar Lado Beta
</a>
</div>

<div style='text-align:center;margin:25px 0;'>
<a href='{baseUrl}/Ayuda' style='color:#4682B4;text-decoration:none;font-size:13px;'>Ayuda</a>
<span style='color:#ddd;margin:0 12px;'>|</span>
<a href='{baseUrl}/Ayuda/FAQ' style='color:#4682B4;text-decoration:none;font-size:13px;'>FAQ</a>
</div>

<!-- Contacto -->
<div style='border-top:1px solid #eee;padding-top:20px;margin-top:25px;text-align:center;'>
<p style='color:#888;font-size:13px;margin:0 0 5px 0;'>Dudas o sugerencias:</p>
<p style='color:#4682B4;font-size:14px;font-weight:600;margin:0;'>soporte@ladoapp.com</p>
</div>

</div>

<!-- Footer -->
<div style='background:#f8f9fa;padding:20px;text-align:center;border-top:1px solid #eee;'>
<p style='color:#888;font-size:12px;margin:0 0 5px 0;'>Gracias por ser parte de este proceso</p>
<p style='color:#aaa;font-size:11px;margin:0;'>&copy; {year} Lado. Todos los derechos reservados.</p>
</div>

<!-- Nota al pie -->
<p style='text-align:center;color:#bbb;font-size:10px;margin:15px 0 0 0;'>
Este email fue enviado a {{{{email}}}}
</p>

</div>
</body>
</html>"
                }
            };

            // Filtrar solo las que no existen
            foreach (var plantilla in todasLasPlantillas)
            {
                if (!plantillasExistentes.Contains(plantilla.Nombre))
                {
                    plantillasNuevas.Add(plantilla);
                }
            }

            // Agregar solo las nuevas
            if (plantillasNuevas.Any())
            {
                _context.PlantillasEmail.AddRange(plantillasNuevas);
                await _context.SaveChangesAsync();
            }
        }

        // ========================================
        // PLANTILLAS
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerarPlantillasPredeterminadas()
        {
            try
            {
                // Eliminar plantillas predeterminadas existentes
                var nombresPredeterminados = new[] {
                    "Comunicado General", "Promocion Especial", "Bienvenida Marketing",
                    "Newsletter Semanal", "Alerta Simple", "Invitacion Beta Exclusiva"
                };

                var plantillasAEliminar = await _context.PlantillasEmail
                    .Where(p => nombresPredeterminados.Contains(p.Nombre))
                    .ToListAsync();

                _context.PlantillasEmail.RemoveRange(plantillasAEliminar);
                await _context.SaveChangesAsync();

                // Crear nuevas
                await CrearPlantillasPredeterminadasAsync();

                TempData["Success"] = $"Plantillas predeterminadas regeneradas ({nombresPredeterminados.Length} plantillas).";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al regenerar plantillas: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPlantilla(string nombre, string descripcion, string asunto,
            string contenidoHtml, string categoria)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(asunto) ||
                    string.IsNullOrWhiteSpace(contenidoHtml))
                {
                    TempData["Error"] = "Nombre, asunto y contenido son obligatorios.";
                    return RedirectToAction("EmailMasivo");
                }

                var plantilla = new PlantillaEmail
                {
                    Nombre = nombre,
                    Descripcion = descripcion,
                    Asunto = asunto,
                    ContenidoHtml = contenidoHtml,
                    Categoria = categoria ?? "Marketing",
                    EstaActiva = true,
                    FechaCreacion = DateTime.Now
                };

                _context.PlantillasEmail.Add(plantilla);
                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Plantilla de email '{nombre}' creada",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Evento,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    User.Identity?.Name);

                TempData["Success"] = $"Plantilla '{nombre}' creada exitosamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al crear plantilla: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPlantilla(int id, string nombre, string descripcion,
            string asunto, string contenidoHtml, string categoria)
        {
            try
            {
                var plantilla = await _context.PlantillasEmail.FindAsync(id);
                if (plantilla == null)
                {
                    TempData["Error"] = "Plantilla no encontrada.";
                    return RedirectToAction("EmailMasivo");
                }

                plantilla.Nombre = nombre;
                plantilla.Descripcion = descripcion;
                plantilla.Asunto = asunto;
                plantilla.ContenidoHtml = contenidoHtml;
                plantilla.Categoria = categoria ?? "Marketing";
                plantilla.UltimaModificacion = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Plantilla '{nombre}' actualizada.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al editar plantilla: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPlantilla(int id)
        {
            try
            {
                var plantilla = await _context.PlantillasEmail.FindAsync(id);
                if (plantilla != null)
                {
                    // Soft delete - solo desactivar
                    plantilla.EstaActiva = false;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Plantilla '{plantilla.Nombre}' eliminada.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar plantilla: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPlantilla(int id)
        {
            var plantilla = await _context.PlantillasEmail.FindAsync(id);
            if (plantilla == null)
                return NotFound();

            return Json(new
            {
                plantilla.Id,
                plantilla.Nombre,
                plantilla.Descripcion,
                plantilla.Asunto,
                plantilla.ContenidoHtml,
                plantilla.Categoria
            });
        }

        // ========================================
        // CAMPAÑAS
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearCampana(string nombre, int? plantillaId, string asunto,
            string contenidoHtml, TipoDestinatarioEmail tipoDestinatario, string? emailsEspecificos)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    TempData["Error"] = "El nombre de la campaña es obligatorio.";
                    return RedirectToAction("EmailMasivo");
                }

                // Si hay plantilla, cargar su contenido
                if (plantillaId.HasValue && plantillaId > 0)
                {
                    var plantilla = await _context.PlantillasEmail.FindAsync(plantillaId.Value);
                    if (plantilla != null)
                    {
                        asunto = plantilla.Asunto;
                        contenidoHtml = plantilla.ContenidoHtml;
                    }
                }

                if (string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(contenidoHtml))
                {
                    TempData["Error"] = "Asunto y contenido son obligatorios.";
                    return RedirectToAction("EmailMasivo");
                }

                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                var campana = new CampanaEmail
                {
                    Nombre = nombre,
                    PlantillaId = plantillaId > 0 ? plantillaId : null,
                    Asunto = asunto,
                    ContenidoHtml = contenidoHtml,
                    TipoDestinatario = tipoDestinatario,
                    EmailsEspecificos = tipoDestinatario == TipoDestinatarioEmail.EmailsEspecificos
                        ? emailsEspecificos : null,
                    Estado = EstadoCampanaEmail.Borrador,
                    FechaCreacion = DateTime.Now,
                    CreadoPorId = userId
                };

                // Contar destinatarios
                campana.TotalDestinatarios = await _bulkEmailService.ContarDestinatariosAsync(
                    tipoDestinatario, campana.EmailsEspecificos);

                _context.CampanasEmail.Add(campana);
                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Campaña de email '{nombre}' creada con {campana.TotalDestinatarios} destinatarios",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Evento,
                    userId,
                    User.Identity?.Name);

                TempData["Success"] = $"Campaña '{nombre}' creada. {campana.TotalDestinatarios} destinatarios.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al crear campaña: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarCampana(int id, string nombre, string asunto,
            string contenidoHtml, TipoDestinatarioEmail tipoDestinatario, string? emailsEspecificos)
        {
            try
            {
                var campana = await _context.CampanasEmail.FindAsync(id);
                if (campana == null)
                {
                    TempData["Error"] = "Campaña no encontrada.";
                    return RedirectToAction("EmailMasivo");
                }

                if (campana.Estado != EstadoCampanaEmail.Borrador)
                {
                    TempData["Error"] = "Solo se pueden editar campañas en estado borrador.";
                    return RedirectToAction("EmailMasivo");
                }

                if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(asunto) || string.IsNullOrWhiteSpace(contenidoHtml))
                {
                    TempData["Error"] = "Nombre, asunto y contenido son obligatorios.";
                    return RedirectToAction("EmailMasivo");
                }

                campana.Nombre = nombre;
                campana.Asunto = asunto;
                campana.ContenidoHtml = contenidoHtml;
                campana.TipoDestinatario = tipoDestinatario;
                campana.EmailsEspecificos = tipoDestinatario == TipoDestinatarioEmail.EmailsEspecificos
                    ? emailsEspecificos : null;

                // Recontear destinatarios
                campana.TotalDestinatarios = await _bulkEmailService.ContarDestinatariosAsync(
                    tipoDestinatario, campana.EmailsEspecificos);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Campaña '{nombre}' actualizada. {campana.TotalDestinatarios} destinatarios.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al editar campaña: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarCampana(int id)
        {
            try
            {
                var campana = await _context.CampanasEmail.FindAsync(id);
                if (campana == null)
                {
                    TempData["Error"] = "Campaña no encontrada.";
                    return RedirectToAction("EmailMasivo");
                }

                if (campana.Estado != EstadoCampanaEmail.Borrador)
                {
                    TempData["Error"] = "Solo se pueden enviar campañas en estado borrador.";
                    return RedirectToAction("EmailMasivo");
                }

                // Enviar la campaña (esto puede tomar tiempo para campañas grandes)
                var resultado = await _bulkEmailService.EnviarCampanaAsync(id);

                if (resultado.Success)
                {
                    TempData["Success"] = $"Campaña enviada: {resultado.TotalEnviados} emails enviados.";
                }
                else
                {
                    TempData["Warning"] = $"Campaña completada con errores: {resultado.TotalEnviados} enviados, {resultado.TotalFallidos} fallidos.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al enviar campaña: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarCampana(int id)
        {
            try
            {
                var campana = await _context.CampanasEmail.FindAsync(id);
                if (campana != null && campana.Estado == EstadoCampanaEmail.EnProgreso)
                {
                    campana.Estado = EstadoCampanaEmail.Cancelada;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Campaña marcada para cancelación.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cancelar campaña: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCampana(int id)
        {
            try
            {
                var campana = await _context.CampanasEmail.FindAsync(id);
                if (campana != null)
                {
                    if (campana.Estado == EstadoCampanaEmail.EnProgreso)
                    {
                        TempData["Error"] = "No se puede eliminar una campaña en progreso.";
                        return RedirectToAction("EmailMasivo");
                    }

                    _context.CampanasEmail.Remove(campana);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Campaña '{campana.Nombre}' eliminada.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar campaña: {ex.Message}";
            }

            return RedirectToAction("EmailMasivo");
        }

        [HttpGet]
        public async Task<IActionResult> ContarDestinatarios(TipoDestinatarioEmail tipo, string? emails = null)
        {
            var count = await _bulkEmailService.ContarDestinatariosAsync(tipo, emails);
            return Json(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerCampana(int id)
        {
            var campana = await _context.CampanasEmail
                .Include(c => c.Plantilla)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (campana == null)
                return NotFound();

            return Json(new
            {
                campana.Id,
                campana.Nombre,
                campana.Asunto,
                campana.ContenidoHtml,
                campana.TipoDestinatario,
                TipoDestinatarioNombre = campana.TipoDestinatarioDescripcion,
                campana.EmailsEspecificos,
                campana.Estado,
                EstadoNombre = campana.Estado.ToString(),
                campana.TotalDestinatarios,
                campana.Enviados,
                campana.Fallidos,
                campana.PorcentajeProgreso,
                campana.TasaExito,
                campana.DetalleErrores
            });
        }

        [HttpGet]
        public IActionResult PreviewEmail(string asunto, string contenido)
        {
            // Preview con datos de ejemplo
            var asuntoPreview = _bulkEmailService.ReemplazarPlaceholders(
                asunto, "Usuario Demo", "demo@ejemplo.com", "usuario_demo");
            var contenidoPreview = _bulkEmailService.ReemplazarPlaceholders(
                contenido, "Usuario Demo", "demo@ejemplo.com", "usuario_demo");

            return Json(new { asunto = asuntoPreview, contenido = contenidoPreview });
        }

        // ========================================
        // INTEGRIDAD DE ARCHIVOS MULTIMEDIA
        // ========================================

        /// <summary>
        /// Muestra estadísticas de integridad de archivos multimedia
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> IntegridadArchivos()
        {
            var stats = await _mediaIntegrity.ObtenerEstadisticasAsync();
            return View(stats);
        }

        /// <summary>
        /// API para obtener estadísticas de integridad
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerEstadisticasIntegridad()
        {
            var stats = await _mediaIntegrity.ObtenerEstadisticasAsync();
            return Json(new
            {
                success = true,
                totalContenidos = stats.TotalContenidos,
                contenidosConArchivo = stats.ContenidosConArchivo,
                contenidosSinArchivo = stats.ContenidosSinArchivo,
                archivosVerificados = stats.ArchivosVerificados,
                archivosFaltantes = stats.ArchivosFaltantes,
                porcentajeIntegridad = stats.ContenidosConArchivo > 0
                    ? Math.Round((1 - (double)stats.ArchivosFaltantes / stats.ContenidosConArchivo) * 100, 2)
                    : 100,
                rutasFaltantes = stats.RutasFaltantes.Take(20)
            });
        }

        /// <summary>
        /// Simula la limpieza de contenido con archivos faltantes
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SimularLimpiezaArchivos()
        {
            var afectados = await _mediaIntegrity.LimpiarContenidoSinArchivosAsync(soloSimular: true);
            return Json(new
            {
                success = true,
                mensaje = $"Simulación completada: {afectados} contenidos serían desactivados",
                afectados = afectados
            });
        }

        /// <summary>
        /// Ejecuta la limpieza de contenido con archivos faltantes
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EjecutarLimpiezaArchivos()
        {
            var afectados = await _mediaIntegrity.LimpiarContenidoSinArchivosAsync(soloSimular: false);
            return Json(new
            {
                success = true,
                mensaje = $"Limpieza completada: {afectados} contenidos desactivados",
                afectados = afectados
            });
        }

        /// <summary>
        /// Limpia la caché de verificación de archivos
        /// </summary>
        [HttpPost]
        public IActionResult LimpiarCacheIntegridad()
        {
            _mediaIntegrity.LimpiarCache();
            return Json(new { success = true, mensaje = "Caché limpiada correctamente" });
        }

        // ========================================
        // SISTEMA DE POPUPS - CRUD
        // ========================================

        /// <summary>
        /// Vista principal de administración de popups
        /// </summary>
        public async Task<IActionResult> Popups()
        {
            var popups = await _context.Popups
                .OrderByDescending(p => p.Prioridad)
                .ThenByDescending(p => p.FechaCreacion)
                .ToListAsync();

            return View(popups);
        }

        /// <summary>
        /// Guardar popup (crear o editar)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GuardarPopup(
            int? id,
            string nombre,
            TipoPopup tipo,
            string? titulo,
            string? contenido,
            string? imagenUrl,
            string? iconoClase,
            string? botonesJson,
            PosicionPopup posicion,
            string? colorFondo,
            string? colorTexto,
            string? colorBotonPrimario,
            string? cssPersonalizado,
            AnimacionPopup animacion,
            int anchoMaximo,
            bool mostrarBotonCerrar,
            bool cerrarAlClickFuera,
            TriggerPopup trigger,
            int? delaySegundos,
            int? scrollPorcentaje,
            int? visitasRequeridas,
            string? selectorClick,
            bool mostrarUsuariosLogueados,
            bool mostrarUsuariosAnonimos,
            bool mostrarEnMovil,
            bool mostrarEnDesktop,
            string? paginasIncluidas,
            string? paginasExcluidas,
            // PWA fields
            bool esPWA,
            bool soloSiInstalable,
            string? contenidoIOS,
            string? textoBotonInstalar,
            bool soloIOS,
            bool soloAndroid,
            // Frecuencia y estado
            FrecuenciaPopup frecuencia,
            int? diasFrecuencia,
            bool estaActivo,
            DateTime? fechaInicio,
            DateTime? fechaFin,
            int prioridad)
        {
            // DEBUG: Log de valores recibidos
            Console.WriteLine("=== GUARDANDO POPUP - VALORES RECIBIDOS ===");
            Console.WriteLine($"  id: {id}");
            Console.WriteLine($"  nombre: {nombre}");
            Console.WriteLine($"  contenido length: {contenido?.Length ?? 0}");
            Console.WriteLine($"  contenido (primeros 200): {(contenido?.Length > 200 ? contenido.Substring(0, 200) : contenido)}");
            Console.WriteLine($"  mostrarUsuariosLogueados: {mostrarUsuariosLogueados}");
            Console.WriteLine($"  mostrarUsuariosAnonimos: {mostrarUsuariosAnonimos}");
            Console.WriteLine($"  mostrarEnMovil: {mostrarEnMovil}");
            Console.WriteLine($"  mostrarEnDesktop: {mostrarEnDesktop}");
            Console.WriteLine($"  esPWA: {esPWA}");
            Console.WriteLine($"  soloSiInstalable: {soloSiInstalable}");

            try
            {
                Popup popup;

                if (id.HasValue && id.Value > 0)
                {
                    // Editar existente
                    popup = await _context.Popups.FindAsync(id.Value);
                    if (popup == null)
                    {
                        return Json(new { success = false, message = "Popup no encontrado" });
                    }
                    popup.UltimaModificacion = DateTime.Now;
                }
                else
                {
                    // Crear nuevo
                    popup = new Popup
                    {
                        FechaCreacion = DateTime.Now
                    };
                    _context.Popups.Add(popup);
                }

                // Asignar valores
                popup.Nombre = nombre;
                popup.Tipo = tipo;
                popup.Titulo = titulo;
                popup.Contenido = contenido;
                popup.ImagenUrl = imagenUrl;
                popup.IconoClase = iconoClase;
                popup.BotonesJson = botonesJson;
                popup.Posicion = posicion;
                popup.ColorFondo = colorFondo;
                popup.ColorTexto = colorTexto;
                popup.ColorBotonPrimario = colorBotonPrimario;
                popup.CssPersonalizado = cssPersonalizado;
                popup.Animacion = animacion;
                popup.AnchoMaximo = anchoMaximo > 0 ? anchoMaximo : 400;
                popup.MostrarBotonCerrar = mostrarBotonCerrar;
                popup.CerrarAlClickFuera = cerrarAlClickFuera;
                popup.Trigger = trigger;
                popup.DelaySegundos = delaySegundos;
                popup.ScrollPorcentaje = scrollPorcentaje;
                popup.VisitasRequeridas = visitasRequeridas;
                popup.SelectorClick = selectorClick;
                popup.MostrarUsuariosLogueados = mostrarUsuariosLogueados;
                popup.MostrarUsuariosAnonimos = mostrarUsuariosAnonimos;
                popup.MostrarEnMovil = mostrarEnMovil;
                popup.MostrarEnDesktop = mostrarEnDesktop;
                popup.PaginasIncluidas = paginasIncluidas;
                popup.PaginasExcluidas = paginasExcluidas;
                // PWA fields
                popup.EsPWA = esPWA;
                popup.SoloSiInstalable = soloSiInstalable;
                popup.ContenidoIOS = contenidoIOS;
                popup.TextoBotonInstalar = textoBotonInstalar;
                popup.SoloIOS = soloIOS;
                popup.SoloAndroid = soloAndroid;
                // Frecuencia
                popup.Frecuencia = frecuencia;
                popup.DiasFrecuencia = diasFrecuencia;
                popup.EstaActivo = estaActivo;
                popup.FechaInicio = fechaInicio;
                popup.FechaFin = fechaFin;
                popup.Prioridad = prioridad;

                await _context.SaveChangesAsync();

                Console.WriteLine($"=== POPUP GUARDADO ===");
                Console.WriteLine($"  Contenido guardado length: {popup.Contenido?.Length ?? 0}");

                return Json(new {
                    success = true,
                    message = id.HasValue ? "Popup actualizado" : "Popup creado",
                    popupId = popup.Id,
                    // DEBUG: Valores guardados
                    debug = new {
                        contenidoLength = popup.Contenido?.Length ?? 0,
                        contenidoPreview = popup.Contenido?.Length > 100 ? popup.Contenido.Substring(0, 100) + "..." : popup.Contenido,
                        mostrarUsuariosLogueados = popup.MostrarUsuariosLogueados,
                        mostrarUsuariosAnonimos = popup.MostrarUsuariosAnonimos,
                        mostrarEnMovil = popup.MostrarEnMovil,
                        mostrarEnDesktop = popup.MostrarEnDesktop,
                        esPWA = popup.EsPWA,
                        soloSiInstalable = popup.SoloSiInstalable
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Corregir popup PWA (temporal)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CorregirPopupPWA(int id = 1)
        {
            var popup = await _context.Popups.FindAsync(id);
            if (popup == null)
            {
                return Json(new { success = false, message = "Popup no encontrado" });
            }

            popup.EsPWA = true;
            popup.SoloSiInstalable = true;
            popup.MostrarUsuariosLogueados = true;
            popup.MostrarUsuariosAnonimos = true;
            popup.MostrarEnMovil = true;
            popup.MostrarEnDesktop = true;
            popup.Contenido = @"<div style=""text-align: center; padding: 8px;"">
    <div style=""font-size: 56px; margin-bottom: 12px;"">📲</div>
    <h3 style=""margin: 0 0 8px; font-size: 20px; font-weight: 700;"">¡Instala LADO!</h3>
    <p style=""margin: 0 0 16px; font-size: 14px; opacity: 0.85; line-height: 1.5;"">
        Accede más rápido, recibe notificaciones y disfruta la experiencia completa.
    </p>
    <div style=""display: flex; justify-content: center; gap: 24px; margin-top: 12px;"">
        <div style=""text-align: center;"">
            <div style=""font-size: 24px;"">⚡</div>
            <div style=""font-size: 11px; opacity: 0.7;"">Más rápido</div>
        </div>
        <div style=""text-align: center;"">
            <div style=""font-size: 24px;"">🔔</div>
            <div style=""font-size: 11px; opacity: 0.7;"">Notificaciones</div>
        </div>
        <div style=""text-align: center;"">
            <div style=""font-size: 24px;"">📱</div>
            <div style=""font-size: 11px; opacity: 0.7;"">Como app</div>
        </div>
    </div>
</div>";
            popup.ContenidoIOS = @"<div style=""text-align: center; padding: 8px;"">
    <div style=""font-size: 48px; margin-bottom: 12px;"">📱</div>
    <h3 style=""margin: 0 0 16px; font-size: 18px; font-weight: 700;"">Instala LADO en tu iPhone</h3>
    <div style=""text-align: left; background: rgba(255,255,255,0.1); border-radius: 12px; padding: 16px;"">
        <div style=""display: flex; align-items: center; margin-bottom: 12px;"">
            <span style=""font-size: 20px; margin-right: 12px;"">1️⃣</span>
            <span>Toca el botón <strong>Compartir</strong> <span style=""background: rgba(255,255,255,0.2); padding: 2px 8px; border-radius: 4px;"">⬆️</span></span>
        </div>
        <div style=""display: flex; align-items: center; margin-bottom: 12px;"">
            <span style=""font-size: 20px; margin-right: 12px;"">2️⃣</span>
            <span>Selecciona <strong>Agregar a Inicio</strong></span>
        </div>
        <div style=""display: flex; align-items: center;"">
            <span style=""font-size: 20px; margin-right: 12px;"">3️⃣</span>
            <span>Toca <strong>Agregar</strong> para confirmar</span>
        </div>
    </div>
</div>";
            popup.UltimaModificacion = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                message = "Popup PWA corregido",
                popup = new {
                    popup.Id,
                    popup.Nombre,
                    popup.EsPWA,
                    popup.SoloSiInstalable,
                    popup.MostrarUsuariosLogueados,
                    popup.MostrarUsuariosAnonimos,
                    popup.Contenido
                }
            });
        }

        /// <summary>
        /// Obtener popup por ID (para editar)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerPopup(int id)
        {
            var popup = await _context.Popups.FindAsync(id);
            if (popup == null)
            {
                return Json(new { success = false, message = "Popup no encontrado" });
            }

            // Forzar todos los nombres en camelCase explícitamente
            return Json(new
            {
                success = true,
                popup = new
                {
                    id = popup.Id,
                    nombre = popup.Nombre,
                    tipo = (int)popup.Tipo,
                    titulo = popup.Titulo,
                    contenido = popup.Contenido,
                    imagenUrl = popup.ImagenUrl,
                    iconoClase = popup.IconoClase,
                    botonesJson = popup.BotonesJson,
                    posicion = (int)popup.Posicion,
                    colorFondo = popup.ColorFondo,
                    colorTexto = popup.ColorTexto,
                    colorBotonPrimario = popup.ColorBotonPrimario,
                    cssPersonalizado = popup.CssPersonalizado,
                    animacion = (int)popup.Animacion,
                    anchoMaximo = popup.AnchoMaximo,
                    mostrarBotonCerrar = popup.MostrarBotonCerrar,
                    cerrarAlClickFuera = popup.CerrarAlClickFuera,
                    trigger = (int)popup.Trigger,
                    delaySegundos = popup.DelaySegundos,
                    scrollPorcentaje = popup.ScrollPorcentaje,
                    visitasRequeridas = popup.VisitasRequeridas,
                    selectorClick = popup.SelectorClick,
                    mostrarUsuariosLogueados = popup.MostrarUsuariosLogueados,
                    mostrarUsuariosAnonimos = popup.MostrarUsuariosAnonimos,
                    mostrarEnMovil = popup.MostrarEnMovil,
                    mostrarEnDesktop = popup.MostrarEnDesktop,
                    paginasIncluidas = popup.PaginasIncluidas,
                    paginasExcluidas = popup.PaginasExcluidas,
                    // PWA fields
                    esPWA = popup.EsPWA,
                    soloSiInstalable = popup.SoloSiInstalable,
                    contenidoIOS = popup.ContenidoIOS,
                    textoBotonInstalar = popup.TextoBotonInstalar,
                    soloIOS = popup.SoloIOS,
                    soloAndroid = popup.SoloAndroid,
                    // Frecuencia y estado
                    frecuencia = (int)popup.Frecuencia,
                    diasFrecuencia = popup.DiasFrecuencia,
                    estaActivo = popup.EstaActivo,
                    fechaInicio = popup.FechaInicio?.ToString("yyyy-MM-ddTHH:mm"),
                    fechaFin = popup.FechaFin?.ToString("yyyy-MM-ddTHH:mm"),
                    prioridad = popup.Prioridad,
                    impresiones = popup.Impresiones,
                    clics = popup.Clics,
                    cierres = popup.Cierres
                }
            });
        }

        /// <summary>
        /// Eliminar popup
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarPopup(int id)
        {
            var popup = await _context.Popups.FindAsync(id);
            if (popup == null)
            {
                return Json(new { success = false, message = "Popup no encontrado" });
            }

            _context.Popups.Remove(popup);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Popup eliminado" });
        }

        /// <summary>
        /// Toggle estado activo/inactivo
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePopup(int id)
        {
            var popup = await _context.Popups.FindAsync(id);
            if (popup == null)
            {
                return Json(new { success = false, message = "Popup no encontrado" });
            }

            popup.EstaActivo = !popup.EstaActivo;
            popup.UltimaModificacion = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, activo = popup.EstaActivo, message = popup.EstaActivo ? "Popup activado" : "Popup desactivado" });
        }

        /// <summary>
        /// Duplicar popup
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuplicarPopup(int id)
        {
            var original = await _context.Popups.FindAsync(id);
            if (original == null)
            {
                return Json(new { success = false, message = "Popup no encontrado" });
            }

            var copia = new Popup
            {
                Nombre = original.Nombre + " (copia)",
                Tipo = original.Tipo,
                Titulo = original.Titulo,
                Contenido = original.Contenido,
                ImagenUrl = original.ImagenUrl,
                IconoClase = original.IconoClase,
                BotonesJson = original.BotonesJson,
                Posicion = original.Posicion,
                ColorFondo = original.ColorFondo,
                ColorTexto = original.ColorTexto,
                ColorBotonPrimario = original.ColorBotonPrimario,
                CssPersonalizado = original.CssPersonalizado,
                Animacion = original.Animacion,
                AnchoMaximo = original.AnchoMaximo,
                MostrarBotonCerrar = original.MostrarBotonCerrar,
                CerrarAlClickFuera = original.CerrarAlClickFuera,
                Trigger = original.Trigger,
                DelaySegundos = original.DelaySegundos,
                ScrollPorcentaje = original.ScrollPorcentaje,
                VisitasRequeridas = original.VisitasRequeridas,
                SelectorClick = original.SelectorClick,
                MostrarUsuariosLogueados = original.MostrarUsuariosLogueados,
                MostrarUsuariosAnonimos = original.MostrarUsuariosAnonimos,
                MostrarEnMovil = original.MostrarEnMovil,
                MostrarEnDesktop = original.MostrarEnDesktop,
                PaginasIncluidas = original.PaginasIncluidas,
                PaginasExcluidas = original.PaginasExcluidas,
                // PWA fields
                EsPWA = original.EsPWA,
                SoloSiInstalable = original.SoloSiInstalable,
                ContenidoIOS = original.ContenidoIOS,
                TextoBotonInstalar = original.TextoBotonInstalar,
                SoloIOS = original.SoloIOS,
                SoloAndroid = original.SoloAndroid,
                // Frecuencia
                Frecuencia = original.Frecuencia,
                DiasFrecuencia = original.DiasFrecuencia,
                EstaActivo = false, // La copia empieza inactiva
                FechaInicio = original.FechaInicio,
                FechaFin = original.FechaFin,
                Prioridad = original.Prioridad,
                FechaCreacion = DateTime.Now
            };

            _context.Popups.Add(copia);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Popup duplicado", popupId = copia.Id });
        }

        /// <summary>
        /// Registrar impresión de popup (llamado desde frontend)
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PopupImpresion(int popupId)
        {
            var popup = await _context.Popups.FindAsync(popupId);
            if (popup != null)
            {
                popup.Impresiones++;
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        /// <summary>
        /// Registrar clic en popup (llamado desde frontend)
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PopupClic(int popupId)
        {
            var popup = await _context.Popups.FindAsync(popupId);
            if (popup != null)
            {
                popup.Clics++;
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        /// <summary>
        /// Registrar cierre de popup (llamado desde frontend)
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PopupCierre(int popupId)
        {
            var popup = await _context.Popups.FindAsync(popupId);
            if (popup != null)
            {
                popup.Cierres++;
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        /// <summary>
        /// Obtener popups activos para una página (llamado desde frontend)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerPopupsActivos(string pagina, bool esLogueado, bool esMovil)
        {
            var ahora = DateTime.Now;

            var popups = await _context.Popups
                .Where(p => p.EstaActivo)
                .Where(p => !p.FechaInicio.HasValue || p.FechaInicio <= ahora)
                .Where(p => !p.FechaFin.HasValue || p.FechaFin >= ahora)
                .Where(p => (esLogueado && p.MostrarUsuariosLogueados) || (!esLogueado && p.MostrarUsuariosAnonimos))
                .Where(p => (esMovil && p.MostrarEnMovil) || (!esMovil && p.MostrarEnDesktop))
                .OrderByDescending(p => p.Prioridad)
                .ToListAsync();

            // Filtrar por página
            var popupsFiltrados = popups.Where(p =>
            {
                // Si no hay páginas configuradas, mostrar en todas
                if (string.IsNullOrEmpty(p.PaginasIncluidas) || p.PaginasIncluidas == "*")
                {
                    // Verificar exclusiones
                    if (!string.IsNullOrEmpty(p.PaginasExcluidas))
                    {
                        var excluidas = p.PaginasExcluidas.Split(',').Select(x => x.Trim().ToLower());
                        if (excluidas.Any(e => pagina.ToLower().StartsWith(e)))
                            return false;
                    }
                    return true;
                }

                // Verificar inclusiones
                var incluidas = p.PaginasIncluidas.Split(',').Select(x => x.Trim().ToLower());
                return incluidas.Any(i => pagina.ToLower().StartsWith(i));
            }).ToList();

            return Json(popupsFiltrados.Select(p => new
            {
                p.Id,
                p.Nombre,
                tipo = (int)p.Tipo,
                tipoNombre = p.TipoDisplay,
                p.Titulo,
                p.Contenido,
                p.ImagenUrl,
                p.IconoClase,
                p.BotonesJson,
                posicion = (int)p.Posicion,
                posicionNombre = p.PosicionDisplay,
                p.ColorFondo,
                p.ColorTexto,
                p.ColorBotonPrimario,
                p.CssPersonalizado,
                animacion = (int)p.Animacion,
                p.AnchoMaximo,
                p.MostrarBotonCerrar,
                p.CerrarAlClickFuera,
                trigger = (int)p.Trigger,
                p.DelaySegundos,
                p.ScrollPorcentaje,
                p.VisitasRequeridas,
                p.SelectorClick,
                // PWA fields
                p.EsPWA,
                p.SoloSiInstalable,
                p.ContenidoIOS,
                p.TextoBotonInstalar,
                p.SoloIOS,
                p.SoloAndroid,
                // Frecuencia
                frecuencia = (int)p.Frecuencia,
                p.DiasFrecuencia,
                p.Prioridad
            }));
        }

        /// <summary>
        /// Resetear métricas de un popup
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ResetearMetricasPopup(int id)
        {
            var popup = await _context.Popups.FindAsync(id);
            if (popup == null)
            {
                return Json(new { success = false, message = "Popup no encontrado" });
            }

            popup.Impresiones = 0;
            popup.Clics = 0;
            popup.Cierres = 0;
            popup.UltimaModificacion = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Métricas reseteadas" });
        }

        /// <summary>
        /// Subir imagen para popup
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SubirImagenPopup(IFormFile archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                {
                    return Json(new { success = false, message = "No se recibió ningún archivo" });
                }

                // Validar extensión
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

                if (!extensionesPermitidas.Contains(extension))
                {
                    return Json(new { success = false, message = "Formato no permitido. Use: JPG, PNG, GIF, WebP o SVG" });
                }

                // Validar tamaño (5MB máximo)
                if (archivo.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "El archivo no puede superar los 5MB" });
                }

                // Crear directorio si no existe
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "popups");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Generar nombre único
                var nombreArchivo = $"popup_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{extension}";
                var rutaCompleta = Path.Combine(uploadsFolder, nombreArchivo);

                // Guardar archivo
                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                var urlArchivo = $"/uploads/popups/{nombreArchivo}";

                return Json(new {
                    success = true,
                    url = urlArchivo,
                    nombre = archivo.FileName,
                    tamaño = archivo.Length,
                    message = "Imagen subida correctamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir imagen de popup");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtener imágenes de popups disponibles
        /// </summary>
        [HttpGet]
        public IActionResult ObtenerImagenesPopup()
        {
            try
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "popups");

                if (!Directory.Exists(uploadsFolder))
                {
                    return Json(new { success = true, imagenes = new List<object>() });
                }

                var extensiones = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
                var archivos = Directory.GetFiles(uploadsFolder)
                    .Where(f => extensiones.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(f => new {
                        url = $"/uploads/popups/{Path.GetFileName(f)}",
                        nombre = Path.GetFileName(f),
                        fecha = System.IO.File.GetCreationTime(f)
                    })
                    .OrderByDescending(f => f.fecha)
                    .Take(50)
                    .ToList();

                return Json(new { success = true, imagenes = archivos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener imágenes de popup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar imagen de popup
        /// </summary>
        [HttpPost]
        public IActionResult EliminarImagenPopup(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/popups/"))
                {
                    return Json(new { success = false, message = "URL inválida" });
                }

                var nombreArchivo = Path.GetFileName(url);
                var rutaCompleta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "popups", nombreArchivo);

                if (System.IO.File.Exists(rutaCompleta))
                {
                    System.IO.File.Delete(rutaCompleta);
                    return Json(new { success = true, message = "Imagen eliminada" });
                }

                return Json(new { success = false, message = "Archivo no encontrado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar imagen de popup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // MIGRACIÓN DE MEDIOS A FORMATOS ESTÁNDAR
        // ========================================

        /// <summary>
        /// Vista para migrar medios existentes a formatos estándar (JPEG + MP4)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> MigracionMedias()
        {
            // Extensiones no estándar para imágenes (incluyendo RAW)
            var extensionesImagenNoEstandar = new[] { ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2", ".avif" };
            var extensionesVideoNoEstandar = new[] { ".mov", ".avi", ".mkv", ".webm", ".wmv", ".flv", ".m4v", ".3gp", ".mpeg", ".mpg" };

            // Contar contenidos con formatos no estándar
            var contenidosImagen = await _context.Contenidos
                .Where(c => c.EstaActivo && c.TipoContenido == TipoContenido.Foto && c.RutaArchivo != null)
                .ToListAsync();

            var imagenesNoEstandar = contenidosImagen
                .Where(c => extensionesImagenNoEstandar.Contains(Path.GetExtension(c.RutaArchivo ?? "").ToLower()))
                .Count();

            var contenidosVideo = await _context.Contenidos
                .Where(c => c.EstaActivo && c.TipoContenido == TipoContenido.Video && c.RutaArchivo != null)
                .ToListAsync();

            var videosNoEstandar = contenidosVideo
                .Where(c => extensionesVideoNoEstandar.Contains(Path.GetExtension(c.RutaArchivo ?? "").ToLower()))
                .Count();

            // Contar archivos de carrusel no estándar
            var archivosCarrusel = await _context.ArchivosContenido.ToListAsync();

            var archivosImagenNoEstandar = archivosCarrusel
                .Where(a => a.TipoArchivo == TipoArchivo.Foto &&
                           extensionesImagenNoEstandar.Contains(Path.GetExtension(a.RutaArchivo ?? "").ToLower()))
                .Count();

            var archivosVideoNoEstandar = archivosCarrusel
                .Where(a => a.TipoArchivo == TipoArchivo.Video &&
                           extensionesVideoNoEstandar.Contains(Path.GetExtension(a.RutaArchivo ?? "").ToLower()))
                .Count();

            // Contar stories no estándar
            var stories = await _context.Stories.Where(s => s.EstaActivo).ToListAsync();

            var storiesImagenNoEstandar = stories
                .Where(s => s.TipoContenido == TipoContenido.Imagen &&
                           extensionesImagenNoEstandar.Contains(Path.GetExtension(s.RutaArchivo ?? "").ToLower()))
                .Count();

            var storiesVideoNoEstandar = stories
                .Where(s => s.TipoContenido == TipoContenido.Video &&
                           extensionesVideoNoEstandar.Contains(Path.GetExtension(s.RutaArchivo ?? "").ToLower()))
                .Count();

            ViewBag.ImagenesNoEstandar = imagenesNoEstandar;
            ViewBag.VideosNoEstandar = videosNoEstandar;
            ViewBag.ArchivosImagenNoEstandar = archivosImagenNoEstandar;
            ViewBag.ArchivosVideoNoEstandar = archivosVideoNoEstandar;
            ViewBag.StoriesImagenNoEstandar = storiesImagenNoEstandar;
            ViewBag.StoriesVideoNoEstandar = storiesVideoNoEstandar;
            ViewBag.TotalNoEstandar = imagenesNoEstandar + videosNoEstandar + archivosImagenNoEstandar +
                                      archivosVideoNoEstandar + storiesImagenNoEstandar + storiesVideoNoEstandar;

            // Contar videos MP4 existentes (para opción de reconvertir)
            var videosMp4Contenidos = contenidosVideo
                .Where(c => Path.GetExtension(c.RutaArchivo ?? "").ToLower() == ".mp4")
                .Count();
            var videosMp4Archivos = archivosCarrusel
                .Where(a => a.TipoArchivo == TipoArchivo.Video &&
                           Path.GetExtension(a.RutaArchivo ?? "").ToLower() == ".mp4")
                .Count();
            var videosMp4Stories = stories
                .Where(s => s.TipoContenido == TipoContenido.Video &&
                           Path.GetExtension(s.RutaArchivo ?? "").ToLower() == ".mp4")
                .Count();
            ViewBag.VideosMp4Total = videosMp4Contenidos + videosMp4Archivos + videosMp4Stories;

            ViewBag.MigracionEnProgreso = _migracionEnProgreso;
            ViewBag.MigracionTotal = _migracionTotal;
            ViewBag.MigracionProcesados = _migracionProcesados;
            ViewBag.MigracionExitosos = _migracionExitosos;
            ViewBag.MigracionFallidos = _migracionFallidos;
            ViewBag.MigracionMensaje = _migracionMensaje;

            return View();
        }

        /// <summary>
        /// Iniciar migración de medios en segundo plano
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarMigracionMedias(bool soloImagenes = false, bool soloVideos = false, bool reconvertirMp4 = false)
        {
            if (_migracionEnProgreso)
            {
                return Json(new { success = false, message = "Ya hay una migración en progreso" });
            }

            _migracionEnProgreso = true;
            _migracionTotal = 0;
            _migracionProcesados = 0;
            _migracionExitosos = 0;
            _migracionFallidos = 0;
            _migracionMensaje = "Iniciando migración...";

            // Ejecutar en segundo plano con scope independiente para logging
            _ = Task.Run(async () =>
            {
                using var scopeLog = _serviceScopeFactory.CreateScope();
                var logServiceTask = scopeLog.ServiceProvider.GetRequiredService<ILogEventoService>();

                await logServiceTask.RegistrarEventoAsync(
                    $"[MigracionMedias] Task.Run iniciado. SoloImagenes={soloImagenes}, SoloVideos={soloVideos}, ReconvertirMp4={reconvertirMp4}",
                    CategoriaEvento.Sistema, TipoLogEvento.Evento);

                try
                {
                    await EjecutarMigracionAsync(soloImagenes, soloVideos, reconvertirMp4);
                    await logServiceTask.RegistrarEventoAsync(
                        "[MigracionMedias] EjecutarMigracionAsync completado",
                        CategoriaEvento.Sistema, TipoLogEvento.Evento);
                }
                catch (Exception ex)
                {
                    _migracionMensaje = $"Error: {ex.Message}";
                    await logServiceTask.RegistrarEventoAsync(
                        $"[MigracionMedias] Error durante migración: {ex.Message}",
                        CategoriaEvento.Sistema, TipoLogEvento.Error,
                        detalle: ex.StackTrace);
                }
                finally
                {
                    _migracionEnProgreso = false;
                    await logServiceTask.RegistrarEventoAsync(
                        "[MigracionMedias] Task.Run finalizado. EnProgreso=false",
                        CategoriaEvento.Sistema, TipoLogEvento.Evento);
                }
            });

            return Json(new { success = true, message = "Migración iniciada en segundo plano" });
        }

        /// <summary>
        /// Obtener estado de la migración
        /// </summary>
        [HttpGet]
        public IActionResult EstadoMigracion()
        {
            return Json(new
            {
                enProgreso = _migracionEnProgreso,
                total = _migracionTotal,
                procesados = _migracionProcesados,
                exitosos = _migracionExitosos,
                fallidos = _migracionFallidos,
                mensaje = _migracionMensaje,
                porcentaje = _migracionTotal > 0 ? (_migracionProcesados * 100 / _migracionTotal) : 0
            });
        }

        /// <summary>
        /// Cancelar migración en progreso
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelarMigracion()
        {
            if (_migracionEnProgreso)
            {
                _migracionEnProgreso = false;
                _migracionMensaje = "Migración cancelada por el usuario";
                return Json(new { success = true, message = "Migración cancelada" });
            }

            return Json(new { success = false, message = "No hay migración en progreso" });
        }

        private async Task EjecutarMigracionAsync(bool soloImagenes, bool soloVideos, bool reconvertirMp4 = false)
        {
            // Incluir TODOS los formatos de imagen no estándar (incluyendo RAW)
            var extensionesImagenNoEstandar = new[] { ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2", ".avif" };
            var extensionesVideoNoEstandar = new[] { ".mov", ".avi", ".mkv", ".webm", ".wmv", ".flv", ".m4v", ".3gp", ".mpeg", ".mpg" };

            // Crear scope independiente para operaciones de BD
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mediaConversion = scope.ServiceProvider.GetRequiredService<IMediaConversionService>();
            var logService = scope.ServiceProvider.GetRequiredService<ILogEventoService>();

            await logService.RegistrarEventoAsync(
                $"[MigracionMedias] Iniciando migración. SoloImagenes={soloImagenes}, SoloVideos={soloVideos}, ReconvertirMp4={reconvertirMp4}",
                CategoriaEvento.Sistema, TipoLogEvento.Evento);

            // Verificar disponibilidad de herramientas
            var ffmpegDisponible = await VerificarHerramientaAsync("ffmpeg", "-version");
            var ffprobeDisponible = await VerificarHerramientaAsync("ffprobe", "-version");

            await logService.RegistrarEventoAsync(
                $"[MigracionMedias] Herramientas: FFmpeg={ffmpegDisponible}, FFprobe={ffprobeDisponible}",
                CategoriaEvento.Sistema, ffmpegDisponible ? TipoLogEvento.Evento : TipoLogEvento.Warning);

            await logService.RegistrarEventoAsync(
                $"[MigracionMedias] WebRootPath: {_hostEnvironment.WebRootPath}",
                CategoriaEvento.Sistema, TipoLogEvento.Evento);

            // Obtener archivos a migrar
            var archivosAMigrar = new List<(string tipo, int id, string rutaActual, string carpeta)>();

            _migracionMensaje = "Recopilando archivos a migrar...";

            // Contenidos principales
            var contenidos = await context.Contenidos
                .Where(c => c.EstaActivo && c.RutaArchivo != null)
                .Select(c => new { c.Id, c.RutaArchivo, c.TipoContenido })
                .ToListAsync();

            await logService.RegistrarEventoAsync(
                $"[MigracionMedias] Encontrados {contenidos.Count} contenidos",
                CategoriaEvento.Sistema, TipoLogEvento.Evento);

            foreach (var c in contenidos)
            {
                var ext = Path.GetExtension(c.RutaArchivo ?? "").ToLower();
                if (!soloVideos && c.TipoContenido == TipoContenido.Foto && extensionesImagenNoEstandar.Contains(ext))
                {
                    archivosAMigrar.Add(("contenido_img", c.Id, c.RutaArchivo!, "uploads"));
                }
                else if (!soloImagenes && c.TipoContenido == TipoContenido.Video && extensionesVideoNoEstandar.Contains(ext))
                {
                    archivosAMigrar.Add(("contenido_vid", c.Id, c.RutaArchivo!, "uploads"));
                }
                // Incluir videos MP4 existentes si reconvertirMp4 está activo
                else if (!soloImagenes && reconvertirMp4 && c.TipoContenido == TipoContenido.Video && ext == ".mp4")
                {
                    archivosAMigrar.Add(("contenido_vid_mp4", c.Id, c.RutaArchivo!, "uploads"));
                }
            }

            // Archivos de carrusel
            var archivos = await context.ArchivosContenido
                .Select(a => new { a.Id, a.RutaArchivo, a.TipoArchivo })
                .ToListAsync();

            foreach (var a in archivos)
            {
                var ext = Path.GetExtension(a.RutaArchivo ?? "").ToLower();
                if (!soloVideos && a.TipoArchivo == TipoArchivo.Foto && extensionesImagenNoEstandar.Contains(ext))
                {
                    archivosAMigrar.Add(("archivo_img", a.Id, a.RutaArchivo!, "uploads"));
                }
                else if (!soloImagenes && a.TipoArchivo == TipoArchivo.Video && extensionesVideoNoEstandar.Contains(ext))
                {
                    archivosAMigrar.Add(("archivo_vid", a.Id, a.RutaArchivo!, "uploads"));
                }
                // Incluir videos MP4 existentes si reconvertirMp4 está activo
                else if (!soloImagenes && reconvertirMp4 && a.TipoArchivo == TipoArchivo.Video && ext == ".mp4")
                {
                    archivosAMigrar.Add(("archivo_vid_mp4", a.Id, a.RutaArchivo!, "uploads"));
                }
            }

            // Stories
            var stories = await context.Stories
                .Where(s => s.EstaActivo)
                .Select(s => new { s.Id, s.RutaArchivo, s.TipoContenido })
                .ToListAsync();

            foreach (var s in stories)
            {
                var ext = Path.GetExtension(s.RutaArchivo ?? "").ToLower();
                if (!soloVideos && s.TipoContenido == TipoContenido.Imagen && extensionesImagenNoEstandar.Contains(ext))
                {
                    archivosAMigrar.Add(("story_img", s.Id, s.RutaArchivo!, "stories"));
                }
                else if (!soloImagenes && s.TipoContenido == TipoContenido.Video && extensionesVideoNoEstandar.Contains(ext))
                {
                    archivosAMigrar.Add(("story_vid", s.Id, s.RutaArchivo!, "stories"));
                }
                // Incluir videos MP4 existentes si reconvertirMp4 está activo
                else if (!soloImagenes && reconvertirMp4 && s.TipoContenido == TipoContenido.Video && ext == ".mp4")
                {
                    archivosAMigrar.Add(("story_vid_mp4", s.Id, s.RutaArchivo!, "stories"));
                }
            }

            _migracionTotal = archivosAMigrar.Count;
            _migracionMensaje = $"Migrando {_migracionTotal} archivos...";

            await logService.RegistrarEventoAsync(
                $"[MigracionMedias] Total archivos a migrar: {_migracionTotal}",
                CategoriaEvento.Sistema, TipoLogEvento.Evento);

            if (_migracionTotal == 0)
            {
                _migracionMensaje = "No hay archivos para migrar";
                await logService.RegistrarEventoAsync(
                    "[MigracionMedias] No hay archivos para migrar, terminando",
                    CategoriaEvento.Sistema, TipoLogEvento.Warning);
                return;
            }

            // Procesar cada archivo
            foreach (var (tipo, id, rutaActual, carpetaBase) in archivosAMigrar)
            {
                if (!_migracionEnProgreso)
                {
                    await logService.RegistrarEventoAsync(
                        "[MigracionMedias] Migración cancelada por usuario",
                        CategoriaEvento.Sistema, TipoLogEvento.Warning);
                    break;
                }

                try
                {
                    var rutaCompleta = Path.Combine(_hostEnvironment.WebRootPath, rutaActual.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                    await logService.RegistrarEventoAsync(
                        $"[MigracionMedias] Procesando [{_migracionProcesados + 1}/{_migracionTotal}]: {tipo} ID={id}",
                        CategoriaEvento.Sistema, TipoLogEvento.Evento,
                        detalle: $"Ruta BD: {rutaActual}\nRuta Completa: {rutaCompleta}");

                    if (!System.IO.File.Exists(rutaCompleta))
                    {
                        // Eliminar registro huérfano de la BD
                        var eliminado = false;
                        var tablaAfectada = "";

                        if (tipo.StartsWith("archivo"))
                        {
                            var archivo = await context.ArchivosContenido.FindAsync(id);
                            if (archivo != null)
                            {
                                context.ArchivosContenido.Remove(archivo);
                                await context.SaveChangesAsync();
                                eliminado = true;
                                tablaAfectada = "ArchivosContenido";
                            }
                        }
                        else if (tipo.StartsWith("contenido"))
                        {
                            // Para contenidos principales, solo marcar como inactivo
                            var contenido = await context.Contenidos.FindAsync(id);
                            if (contenido != null)
                            {
                                contenido.EstaActivo = false;
                                await context.SaveChangesAsync();
                                eliminado = true;
                                tablaAfectada = "Contenidos (desactivado)";
                            }
                        }
                        else if (tipo.StartsWith("story"))
                        {
                            var story = await context.Stories.FindAsync(id);
                            if (story != null)
                            {
                                story.EstaActivo = false;
                                await context.SaveChangesAsync();
                                eliminado = true;
                                tablaAfectada = "Stories (desactivado)";
                            }
                        }

                        await logService.RegistrarEventoAsync(
                            $"[MigracionMedias] Archivo no existe - Registro {(eliminado ? "LIMPIADO" : "no eliminado")}",
                            CategoriaEvento.Sistema, TipoLogEvento.Warning,
                            detalle: $"Tipo: {tipo}\nID: {id}\nRuta BD: {rutaActual}\nRuta esperada: {rutaCompleta}\nTabla: {tablaAfectada}");

                        _migracionFallidos++;
                        _migracionProcesados++;
                        continue;
                    }

                    var carpetaDestino = Path.GetDirectoryName(rutaCompleta)!;
                    var nombreBase = Path.GetFileNameWithoutExtension(rutaCompleta);
                    var extension = Path.GetExtension(rutaCompleta).ToLower();
                    string? nuevaRuta = null;
                    string tipoConversion = "";

                    // Convertir según tipo
                    if (tipo.EndsWith("_img"))
                    {
                        tipoConversion = "imagen";
                        await logService.RegistrarEventoAsync(
                            $"[MigracionMedias] Convirtiendo imagen {extension} a JPEG",
                            CategoriaEvento.Sistema, TipoLogEvento.Evento,
                            detalle: $"Origen: {rutaCompleta}\nDestino: {carpetaDestino}\nNombre: {nombreBase}");

                        nuevaRuta = await mediaConversion.ConvertirImagenAsync(rutaCompleta, carpetaDestino, nombreBase, 2048, 90);
                    }
                    else if (tipo.EndsWith("_vid") || tipo.EndsWith("_vid_mp4"))
                    {
                        tipoConversion = tipo.EndsWith("_vid_mp4") ? "video (reconversión MP4)" : "video";
                        await logService.RegistrarEventoAsync(
                            $"[MigracionMedias] Convirtiendo {tipoConversion} {extension} a MP4 H.264",
                            CategoriaEvento.Sistema, TipoLogEvento.Evento,
                            detalle: $"Origen: {rutaCompleta}\nDestino: {carpetaDestino}\nNombre: {nombreBase}");

                        nuevaRuta = await mediaConversion.ConvertirVideoAsync(rutaCompleta, carpetaDestino, nombreBase, 20, 1920);
                    }

                    if (!string.IsNullOrEmpty(nuevaRuta) && System.IO.File.Exists(nuevaRuta))
                    {
                        // Actualizar BD con nueva ruta
                        var webRootLength = _hostEnvironment.WebRootPath.TrimEnd('\\', '/').Length;
                        var nuevaRutaRelativa = "/" + nuevaRuta.Substring(webRootLength).Replace('\\', '/').TrimStart('/');
                        var rutaAnterior = rutaActual;
                        var actualizado = false;

                        await logService.RegistrarEventoAsync(
                            $"[MigracionMedias] Actualizando BD: {tipo} ID={id}",
                            CategoriaEvento.Sistema, TipoLogEvento.Evento,
                            detalle: $"Ruta anterior: {rutaAnterior}\nRuta nueva: {nuevaRutaRelativa}\nWebRootPath: {_hostEnvironment.WebRootPath}\nNuevaRuta absoluta: {nuevaRuta}");

                        if (tipo.StartsWith("contenido"))
                        {
                            var contenido = await context.Contenidos.FindAsync(id);
                            if (contenido != null)
                            {
                                contenido.RutaArchivo = nuevaRutaRelativa;
                                await context.SaveChangesAsync();
                                actualizado = true;

                                // Verificar que se guardó correctamente
                                context.Entry(contenido).Reload();
                                if (contenido.RutaArchivo != nuevaRutaRelativa)
                                {
                                    await logService.RegistrarEventoAsync(
                                        $"[MigracionMedias] ADVERTENCIA: La BD no se actualizó correctamente",
                                        CategoriaEvento.Sistema, TipoLogEvento.Warning,
                                        detalle: $"Esperado: {nuevaRutaRelativa}\nActual: {contenido.RutaArchivo}");
                                    actualizado = false;
                                }
                            }
                        }
                        else if (tipo.StartsWith("archivo"))
                        {
                            var archivo = await context.ArchivosContenido.FindAsync(id);
                            if (archivo != null)
                            {
                                archivo.RutaArchivo = nuevaRutaRelativa;
                                await context.SaveChangesAsync();
                                actualizado = true;

                                // Verificar que se guardó correctamente
                                context.Entry(archivo).Reload();
                                if (archivo.RutaArchivo != nuevaRutaRelativa)
                                {
                                    await logService.RegistrarEventoAsync(
                                        $"[MigracionMedias] ADVERTENCIA: La BD no se actualizó correctamente para archivo",
                                        CategoriaEvento.Sistema, TipoLogEvento.Warning,
                                        detalle: $"Esperado: {nuevaRutaRelativa}\nActual: {archivo.RutaArchivo}");
                                    actualizado = false;
                                }
                            }
                        }
                        else if (tipo.StartsWith("story"))
                        {
                            var story = await context.Stories.FindAsync(id);
                            if (story != null)
                            {
                                story.RutaArchivo = nuevaRutaRelativa;
                                await context.SaveChangesAsync();
                                actualizado = true;

                                // Verificar que se guardó correctamente
                                context.Entry(story).Reload();
                                if (story.RutaArchivo != nuevaRutaRelativa)
                                {
                                    await logService.RegistrarEventoAsync(
                                        $"[MigracionMedias] ADVERTENCIA: La BD no se actualizó correctamente para story",
                                        CategoriaEvento.Sistema, TipoLogEvento.Warning,
                                        detalle: $"Esperado: {nuevaRutaRelativa}\nActual: {story.RutaArchivo}");
                                    actualizado = false;
                                }
                            }
                        }

                        // Eliminar archivo original si es diferente (comparación case-insensitive para Windows)
                        if (!nuevaRuta.Equals(rutaCompleta, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(rutaCompleta))
                        {
                            try
                            {
                                System.IO.File.Delete(rutaCompleta);
                                await logService.RegistrarEventoAsync(
                                    $"[MigracionMedias] Archivo original eliminado: {rutaCompleta}",
                                    CategoriaEvento.Sistema, TipoLogEvento.Evento);
                            }
                            catch (Exception delEx)
                            {
                                await logService.RegistrarEventoAsync(
                                    $"[MigracionMedias] No se pudo eliminar archivo original: {delEx.Message}",
                                    CategoriaEvento.Sistema, TipoLogEvento.Warning,
                                    detalle: $"Ruta: {rutaCompleta}");
                            }
                        }

                        if (actualizado)
                        {
                            _migracionExitosos++;
                            await logService.RegistrarEventoAsync(
                                $"[MigracionMedias] ÉXITO: Convertido {tipoConversion}",
                                CategoriaEvento.Sistema, TipoLogEvento.Evento,
                                detalle: $"Original: {rutaAnterior}\nConvertido: {nuevaRutaRelativa}");
                        }
                        else
                        {
                            _migracionFallidos++;
                            await logService.RegistrarEventoAsync(
                                $"[MigracionMedias] ERROR: No se pudo actualizar la BD para {tipo} ID={id}",
                                CategoriaEvento.Sistema, TipoLogEvento.Error);
                        }
                    }
                    else
                    {
                        _migracionFallidos++;
                        await logService.RegistrarEventoAsync(
                            $"[MigracionMedias] ERROR: Conversión de {tipoConversion} retornó null o archivo no existe",
                            CategoriaEvento.Sistema, TipoLogEvento.Error,
                            detalle: $"Archivo original: {rutaCompleta}\nExtensión: {extension}\nNuevaRuta retornada: {nuevaRuta ?? "NULL"}\nArchivo existe: {(nuevaRuta != null ? System.IO.File.Exists(nuevaRuta).ToString() : "N/A")}");
                    }
                }
                catch (Exception ex)
                {
                    _migracionFallidos++;
                    await logService.RegistrarEventoAsync(
                        $"[MigracionMedias] Error procesando {tipo} ID={id}: {ex.Message}",
                        CategoriaEvento.Sistema, TipoLogEvento.Error,
                        detalle: $"Ruta: {rutaActual}\nException: {ex.GetType().Name}\nStack:\n{ex.StackTrace}");
                }

                _migracionProcesados++;
                _migracionMensaje = $"Procesando {_migracionProcesados}/{_migracionTotal} ({_migracionExitosos} exitosos, {_migracionFallidos} fallidos)";

                // Pequeña pausa para no saturar el servidor
                await Task.Delay(100);
            }

            _migracionMensaje = $"Migración completada: {_migracionExitosos} exitosos, {_migracionFallidos} fallidos de {_migracionTotal} total";

            await logService.RegistrarEventoAsync(
                $"[MigracionMedias] {_migracionMensaje}",
                CategoriaEvento.Sistema,
                _migracionFallidos > 0 ? TipoLogEvento.Warning : TipoLogEvento.Evento);
        }

        private async Task<bool> VerificarHerramientaAsync(string comando, string argumentos)
        {
            // Intentar primero con el comando directo (en PATH)
            var resultado = await IntentarEjecutarAsync(comando, argumentos);
            if (resultado) return true;

            // Intentar con rutas comunes de FFmpeg en Windows
            var rutasComunes = new[]
            {
                @"C:\ffmpeg\bin\" + comando + ".exe",
                @"C:\Program Files\ffmpeg\bin\" + comando + ".exe",
                @"C:\tools\ffmpeg\bin\" + comando + ".exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", comando + ".exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", comando + ".exe")
            };

            foreach (var ruta in rutasComunes)
            {
                if (System.IO.File.Exists(ruta))
                {
                    resultado = await IntentarEjecutarAsync(ruta, argumentos);
                    if (resultado) return true;
                }
            }

            return false;
        }

        private async Task<bool> IntentarEjecutarAsync(string comando, string argumentos)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = comando,
                    Arguments = argumentos,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null) return false;

                var timeoutTask = Task.Delay(5000);
                var processTask = process.WaitForExitAsync();

                if (await Task.WhenAny(processTask, timeoutTask) == timeoutTask)
                {
                    try { process.Kill(); } catch { }
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        #region Sincronizar Rutas de Medios

        /// <summary>
        /// Vista para sincronizar rutas de medios (cuando archivos fueron convertidos pero BD no se actualizó)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SincronizarRutasMedias()
        {
            var extensionesConvertibles = new[] { ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2", ".avif" };
            var extensionesVideoConvertibles = new[] { ".mov", ".avi", ".mkv", ".webm", ".wmv", ".flv", ".m4v", ".3gp", ".mpeg", ".mpg" };

            var problemas = new List<(string tipo, int id, string rutaBD, string? rutaEncontrada, string estado)>();

            // Revisar contenidos
            var contenidos = await _context.Contenidos
                .Where(c => c.EstaActivo && c.RutaArchivo != null)
                .Select(c => new { c.Id, c.RutaArchivo, c.TipoContenido })
                .ToListAsync();

            foreach (var c in contenidos)
            {
                var resultado = AnalizarRutaArchivo(c.RutaArchivo!, extensionesConvertibles, extensionesVideoConvertibles);
                if (resultado.estado != "ok")
                {
                    problemas.Add(("Contenido", c.Id, c.RutaArchivo!, resultado.rutaEncontrada, resultado.estado));
                }
            }

            // Revisar archivos de carrusel
            var archivos = await _context.ArchivosContenido
                .Select(a => new { a.Id, a.RutaArchivo })
                .ToListAsync();

            foreach (var a in archivos)
            {
                if (string.IsNullOrEmpty(a.RutaArchivo)) continue;
                var resultado = AnalizarRutaArchivo(a.RutaArchivo, extensionesConvertibles, extensionesVideoConvertibles);
                if (resultado.estado != "ok")
                {
                    problemas.Add(("Archivo", a.Id, a.RutaArchivo, resultado.rutaEncontrada, resultado.estado));
                }
            }

            // Revisar stories
            var stories = await _context.Stories
                .Where(s => s.EstaActivo)
                .Select(s => new { s.Id, s.RutaArchivo })
                .ToListAsync();

            foreach (var s in stories)
            {
                if (string.IsNullOrEmpty(s.RutaArchivo)) continue;
                var resultado = AnalizarRutaArchivo(s.RutaArchivo, extensionesConvertibles, extensionesVideoConvertibles);
                if (resultado.estado != "ok")
                {
                    problemas.Add(("Story", s.Id, s.RutaArchivo, resultado.rutaEncontrada, resultado.estado));
                }
            }

            ViewBag.Problemas = problemas;
            ViewBag.TotalContenidos = contenidos.Count;
            ViewBag.TotalArchivos = archivos.Count;
            ViewBag.TotalStories = stories.Count;
            ViewBag.ConProblemas = problemas.Count;
            ViewBag.Reparables = problemas.Count(p => p.estado == "convertido_existe");
            ViewBag.Huerfanos = problemas.Count(p => p.estado == "no_existe");

            return View();
        }

        private (string? rutaEncontrada, string estado) AnalizarRutaArchivo(string rutaBD, string[] extImagenes, string[] extVideos)
        {
            var rutaFisica = Path.Combine(_hostEnvironment.WebRootPath, rutaBD.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            var extension = Path.GetExtension(rutaBD).ToLower();

            // Si el archivo existe, todo OK
            if (System.IO.File.Exists(rutaFisica))
            {
                return (null, "ok");
            }

            // Si no existe, buscar versión convertida
            var carpeta = Path.GetDirectoryName(rutaFisica);
            var nombreBase = Path.GetFileNameWithoutExtension(rutaFisica);

            if (carpeta == null) return (null, "no_existe");

            // Buscar .jpg si era imagen
            if (extImagenes.Contains(extension))
            {
                var rutaJpg = Path.Combine(carpeta, nombreBase + ".jpg");
                if (System.IO.File.Exists(rutaJpg))
                {
                    var rutaRelativa = "/" + rutaJpg.Substring(_hostEnvironment.WebRootPath.TrimEnd('\\', '/').Length).Replace('\\', '/').TrimStart('/');
                    return (rutaRelativa, "convertido_existe");
                }
            }

            // Buscar .mp4 si era video
            if (extVideos.Contains(extension))
            {
                var rutaMp4 = Path.Combine(carpeta, nombreBase + ".mp4");
                if (System.IO.File.Exists(rutaMp4))
                {
                    var rutaRelativa = "/" + rutaMp4.Substring(_hostEnvironment.WebRootPath.TrimEnd('\\', '/').Length).Replace('\\', '/').TrimStart('/');
                    return (rutaRelativa, "convertido_existe");
                }
            }

            return (null, "no_existe");
        }

        /// <summary>
        /// Ejecutar sincronización de rutas
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EjecutarSincronizacionRutas()
        {
            var extensionesConvertibles = new[] { ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2", ".avif" };
            var extensionesVideoConvertibles = new[] { ".mov", ".avi", ".mkv", ".webm", ".wmv", ".flv", ".m4v", ".3gp", ".mpeg", ".mpg" };

            var reparados = 0;
            var huerfanosDesactivados = 0;
            var errores = 0;

            // Reparar contenidos
            var contenidos = await _context.Contenidos
                .Where(c => c.EstaActivo && c.RutaArchivo != null)
                .ToListAsync();

            foreach (var c in contenidos)
            {
                var resultado = AnalizarRutaArchivo(c.RutaArchivo!, extensionesConvertibles, extensionesVideoConvertibles);

                if (resultado.estado == "convertido_existe" && resultado.rutaEncontrada != null)
                {
                    c.RutaArchivo = resultado.rutaEncontrada;
                    reparados++;
                }
                else if (resultado.estado == "no_existe")
                {
                    c.EstaActivo = false;
                    huerfanosDesactivados++;
                }
            }

            // Reparar archivos de carrusel
            var archivos = await _context.ArchivosContenido.ToListAsync();

            foreach (var a in archivos)
            {
                if (string.IsNullOrEmpty(a.RutaArchivo)) continue;

                var resultado = AnalizarRutaArchivo(a.RutaArchivo, extensionesConvertibles, extensionesVideoConvertibles);

                if (resultado.estado == "convertido_existe" && resultado.rutaEncontrada != null)
                {
                    a.RutaArchivo = resultado.rutaEncontrada;
                    reparados++;
                }
                else if (resultado.estado == "no_existe")
                {
                    _context.ArchivosContenido.Remove(a);
                    huerfanosDesactivados++;
                }
            }

            // Reparar stories
            var stories = await _context.Stories
                .Where(s => s.EstaActivo)
                .ToListAsync();

            foreach (var s in stories)
            {
                if (string.IsNullOrEmpty(s.RutaArchivo)) continue;

                var resultado = AnalizarRutaArchivo(s.RutaArchivo, extensionesConvertibles, extensionesVideoConvertibles);

                if (resultado.estado == "convertido_existe" && resultado.rutaEncontrada != null)
                {
                    s.RutaArchivo = resultado.rutaEncontrada;
                    reparados++;
                }
                else if (resultado.estado == "no_existe")
                {
                    s.EstaActivo = false;
                    huerfanosDesactivados++;
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error guardando: {ex.Message}" });
            }

            await _logEventoService.RegistrarEventoAsync(
                $"[SincronizarRutas] Completado: {reparados} reparados, {huerfanosDesactivados} huérfanos desactivados",
                CategoriaEvento.Sistema, TipoLogEvento.Evento);

            return Json(new
            {
                success = true,
                reparados,
                huerfanosDesactivados,
                errores,
                message = $"Sincronización completada: {reparados} rutas reparadas, {huerfanosDesactivados} huérfanos desactivados"
            });
        }

        #endregion

        #region Analizar y Reconvertir Videos

        public class VideoAnalisis
        {
            public int Id { get; set; }
            public string Tipo { get; set; } = ""; // Contenido, Archivo, Story
            public string RutaArchivo { get; set; } = "";
            public string? Usuario { get; set; }
            public string? CodecReal { get; set; }
            public string? Formato { get; set; }
            public int Ancho { get; set; }
            public int Alto { get; set; }
            public double DuracionSegundos { get; set; }
            public long PesoBytes { get; set; }
            public string Estado { get; set; } = ""; // ok, necesita_conversion, muy_pesado, error
            public string? Problema { get; set; }
        }

        public class ImagenAnalisis
        {
            public int Id { get; set; }
            public string Tipo { get; set; } = ""; // Contenido, Archivo, Story
            public string RutaArchivo { get; set; } = "";
            public string? Usuario { get; set; }
            public string? FormatoReal { get; set; } // PNG, WEBP, HEIC, etc.
            public string Extension { get; set; } = "";
            public int Ancho { get; set; }
            public int Alto { get; set; }
            public long PesoBytes { get; set; }
            public string Estado { get; set; } = ""; // ok, necesita_conversion, error
            public string? Problema { get; set; }
        }

        /// <summary>
        /// Vista para analizar videos e imágenes y detectar cuáles necesitan reconversión
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AnalizarVideos()
        {
            var videos = new List<VideoAnalisis>();
            var imagenes = new List<ImagenAnalisis>();

            // ========================================
            // ANALIZAR VIDEOS
            // ========================================

            // Obtener videos de contenidos
            var contenidosVideo = await _context.Contenidos
                .Include(c => c.Usuario)
                .Where(c => c.EstaActivo && c.TipoContenido == TipoContenido.Video && c.RutaArchivo != null)
                .Select(c => new { c.Id, c.RutaArchivo, Usuario = c.Usuario != null ? c.Usuario.UserName : null })
                .ToListAsync();

            foreach (var c in contenidosVideo)
            {
                var analisis = await AnalizarVideoAsync(c.Id, "Contenido", c.RutaArchivo!, c.Usuario);
                if (analisis != null)
                    videos.Add(analisis);
            }

            // Obtener videos de archivos de carrusel
            var archivosVideo = await _context.ArchivosContenido
                .Include(a => a.Contenido)
                    .ThenInclude(c => c!.Usuario)
                .Where(a => a.TipoArchivo == TipoArchivo.Video && a.RutaArchivo != null)
                .Select(a => new { a.Id, a.RutaArchivo, Usuario = a.Contenido != null && a.Contenido.Usuario != null ? a.Contenido.Usuario.UserName : null })
                .ToListAsync();

            foreach (var a in archivosVideo)
            {
                var analisis = await AnalizarVideoAsync(a.Id, "Archivo", a.RutaArchivo!, a.Usuario);
                if (analisis != null)
                    videos.Add(analisis);
            }

            // Obtener videos de stories
            var storiesVideo = await _context.Stories
                .Include(s => s.Creador)
                .Where(s => s.EstaActivo && s.TipoContenido == TipoContenido.Video && s.RutaArchivo != null)
                .Select(s => new { s.Id, s.RutaArchivo, Usuario = s.Creador != null ? s.Creador.UserName : null })
                .ToListAsync();

            foreach (var s in storiesVideo)
            {
                var analisis = await AnalizarVideoAsync(s.Id, "Story", s.RutaArchivo!, s.Usuario);
                if (analisis != null)
                    videos.Add(analisis);
            }

            // ========================================
            // ANALIZAR IMÁGENES
            // ========================================

            // Obtener imágenes de contenidos
            var contenidosImagen = await _context.Contenidos
                .Include(c => c.Usuario)
                .Where(c => c.EstaActivo && c.TipoContenido == TipoContenido.Foto && c.RutaArchivo != null)
                .Select(c => new { c.Id, c.RutaArchivo, Usuario = c.Usuario != null ? c.Usuario.UserName : null })
                .ToListAsync();

            foreach (var c in contenidosImagen)
            {
                var analisis = AnalizarImagen(c.Id, "Contenido", c.RutaArchivo!, c.Usuario);
                imagenes.Add(analisis);
            }

            // Obtener imágenes de archivos de carrusel
            var archivosImagen = await _context.ArchivosContenido
                .Include(a => a.Contenido)
                    .ThenInclude(c => c!.Usuario)
                .Where(a => a.TipoArchivo == TipoArchivo.Foto && a.RutaArchivo != null)
                .Select(a => new { a.Id, a.RutaArchivo, Usuario = a.Contenido != null && a.Contenido.Usuario != null ? a.Contenido.Usuario.UserName : null })
                .ToListAsync();

            foreach (var a in archivosImagen)
            {
                var analisis = AnalizarImagen(a.Id, "Archivo", a.RutaArchivo!, a.Usuario);
                imagenes.Add(analisis);
            }

            // Obtener imágenes de stories
            var storiesImagen = await _context.Stories
                .Include(s => s.Creador)
                .Where(s => s.EstaActivo && s.TipoContenido == TipoContenido.Foto && s.RutaArchivo != null)
                .Select(s => new { s.Id, s.RutaArchivo, Usuario = s.Creador != null ? s.Creador.UserName : null })
                .ToListAsync();

            foreach (var s in storiesImagen)
            {
                var analisis = AnalizarImagen(s.Id, "Story", s.RutaArchivo!, s.Usuario);
                imagenes.Add(analisis);
            }

            // Ordenar videos: primero los problemáticos, luego por peso
            videos = videos
                .OrderByDescending(v => v.Estado == "necesita_conversion" ? 2 : (v.Estado == "muy_pesado" ? 1 : 0))
                .ThenByDescending(v => v.PesoBytes)
                .ToList();

            // Ordenar imágenes: primero las que necesitan conversión
            imagenes = imagenes
                .OrderByDescending(i => i.Estado == "necesita_conversion" ? 1 : 0)
                .ThenByDescending(i => i.PesoBytes)
                .ToList();

            // ViewBag para Videos
            ViewBag.Videos = videos;
            ViewBag.TotalVideos = videos.Count;
            ViewBag.VideosNecesitanConversion = videos.Count(v => v.Estado == "necesita_conversion");
            ViewBag.VideosMuyPesados = videos.Count(v => v.Estado == "muy_pesado");
            ViewBag.VideosErrores = videos.Count(v => v.Estado == "error");
            ViewBag.VideosOk = videos.Count(v => v.Estado == "ok");

            // ViewBag para Imágenes
            ViewBag.Imagenes = imagenes;
            ViewBag.TotalImagenes = imagenes.Count;
            ViewBag.ImagenesNecesitanConversion = imagenes.Count(i => i.Estado == "necesita_conversion");
            ViewBag.ImagenesErrores = imagenes.Count(i => i.Estado == "error");
            ViewBag.ImagenesOk = imagenes.Count(i => i.Estado == "ok");

            // Mantener compatibilidad con vista anterior
            ViewBag.NecesitanConversion = videos.Count(v => v.Estado == "necesita_conversion");
            ViewBag.MuyPesados = videos.Count(v => v.Estado == "muy_pesado");
            ViewBag.Errores = videos.Count(v => v.Estado == "error");
            ViewBag.Ok = videos.Count(v => v.Estado == "ok");

            // Info de diagnóstico
            ViewBag.FFprobePath = BuscarFFprobe() ?? "No encontrado";
            ViewBag.FFmpegPath = BuscarFFmpeg() ?? "No encontrado";

            return View();
        }

        private async Task<VideoAnalisis?> AnalizarVideoAsync(int id, string tipo, string rutaArchivo, string? usuario)
        {
            var rutaFisica = Path.Combine(_hostEnvironment.WebRootPath, rutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(rutaFisica))
            {
                return new VideoAnalisis
                {
                    Id = id,
                    Tipo = tipo,
                    RutaArchivo = rutaArchivo,
                    Usuario = usuario,
                    Estado = "error",
                    Problema = "Archivo no existe"
                };
            }

            var fileInfo = new FileInfo(rutaFisica);
            var analisis = new VideoAnalisis
            {
                Id = id,
                Tipo = tipo,
                RutaArchivo = rutaArchivo,
                Usuario = usuario,
                PesoBytes = fileInfo.Length
            };

            // Usar ffprobe para obtener info real
            try
            {
                // Buscar ffprobe en rutas comunes (IIS no siempre tiene el PATH del sistema)
                var ffprobePath = BuscarFFprobe();
                if (ffprobePath == null)
                {
                    analisis.Estado = "error";
                    analisis.Problema = "ffprobe no encontrado en PATH ni rutas comunes";
                    return analisis;
                }

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v quiet -print_format json -show_format -show_streams \"{rutaFisica}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    analisis.Estado = "error";
                    analisis.Problema = "No se pudo iniciar ffprobe";
                    return analisis;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var completed = await Task.Run(() => process.WaitForExit(10000));

                if (!completed || string.IsNullOrEmpty(output))
                {
                    analisis.Estado = "error";
                    analisis.Problema = "ffprobe timeout o sin respuesta";
                    return analisis;
                }

                // Parsear JSON para obtener codec
                var codecMatch = System.Text.RegularExpressions.Regex.Match(output, "\"codec_name\"\\s*:\\s*\"([^\"]+)\"");
                if (codecMatch.Success)
                    analisis.CodecReal = codecMatch.Groups[1].Value;

                var widthMatch = System.Text.RegularExpressions.Regex.Match(output, "\"width\"\\s*:\\s*(\\d+)");
                var heightMatch = System.Text.RegularExpressions.Regex.Match(output, "\"height\"\\s*:\\s*(\\d+)");
                if (widthMatch.Success) analisis.Ancho = int.Parse(widthMatch.Groups[1].Value);
                if (heightMatch.Success) analisis.Alto = int.Parse(heightMatch.Groups[1].Value);

                var durationMatch = System.Text.RegularExpressions.Regex.Match(output, "\"duration\"\\s*:\\s*\"([\\d.]+)\"");
                if (durationMatch.Success)
                    analisis.DuracionSegundos = double.Parse(durationMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

                var formatMatch = System.Text.RegularExpressions.Regex.Match(output, "\"format_name\"\\s*:\\s*\"([^\"]+)\"");
                if (formatMatch.Success)
                    analisis.Formato = formatMatch.Groups[1].Value;

                // Determinar estado
                var codecLower = analisis.CodecReal?.ToLower() ?? "";
                var pesoMB = analisis.PesoBytes / (1024.0 * 1024.0);

                if (codecLower != "h264" && codecLower != "avc")
                {
                    analisis.Estado = "necesita_conversion";
                    analisis.Problema = $"Codec {analisis.CodecReal} no es H.264";
                }
                else if (pesoMB > 50) // Más de 50MB
                {
                    analisis.Estado = "muy_pesado";
                    analisis.Problema = $"Peso excesivo ({pesoMB:F1} MB)";
                }
                else
                {
                    analisis.Estado = "ok";
                }
            }
            catch (Exception ex)
            {
                analisis.Estado = "error";
                analisis.Problema = ex.Message;
            }

            return analisis;
        }

        private ImagenAnalisis AnalizarImagen(int id, string tipo, string rutaArchivo, string? usuario)
        {
            var rutaFisica = Path.Combine(_hostEnvironment.WebRootPath, rutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            var extension = Path.GetExtension(rutaArchivo).ToLower();

            if (!System.IO.File.Exists(rutaFisica))
            {
                return new ImagenAnalisis
                {
                    Id = id,
                    Tipo = tipo,
                    RutaArchivo = rutaArchivo,
                    Usuario = usuario,
                    Extension = extension,
                    Estado = "error",
                    Problema = "Archivo no existe"
                };
            }

            var fileInfo = new FileInfo(rutaFisica);
            var analisis = new ImagenAnalisis
            {
                Id = id,
                Tipo = tipo,
                RutaArchivo = rutaArchivo,
                Usuario = usuario,
                Extension = extension,
                PesoBytes = fileInfo.Length
            };

            try
            {
                // Usar ImageMagick para obtener info real
                using var image = new ImageMagick.MagickImage(rutaFisica);
                analisis.Ancho = (int)image.Width;
                analisis.Alto = (int)image.Height;
                analisis.FormatoReal = image.Format.ToString().ToUpper();

                // Extensiones que NO son JPEG estándar
                var extensionesNoEstandar = new[] { ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif",
                    ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2", ".avif" };

                if (extensionesNoEstandar.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    analisis.Estado = "necesita_conversion";
                    analisis.Problema = $"Formato {analisis.FormatoReal} no es JPEG estándar";
                }
                else
                {
                    analisis.Estado = "ok";
                }
            }
            catch (Exception ex)
            {
                analisis.Estado = "error";
                analisis.Problema = ex.Message;
            }

            return analisis;
        }

        /// <summary>
        /// Buscar ffprobe en rutas comunes (IIS no siempre tiene el PATH del sistema)
        /// </summary>
        private string? BuscarFFprobe()
        {
            // Primero intentar en PATH
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                        return "ffprobe";
                }
            }
            catch { }

            // Buscar en rutas comunes de Windows
            var rutasComunes = new[]
            {
                // Chocolatey (muy común en servidores Windows)
                @"C:\ProgramData\chocolatey\bin\ffprobe.exe",
                @"C:\ffmpeg\bin\ffprobe.exe",
                @"C:\Program Files\ffmpeg\bin\ffprobe.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffprobe.exe",
                @"C:\tools\ffmpeg\bin\ffprobe.exe",
                @"C:\Users\Administrator\ffmpeg\bin\ffprobe.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffprobe.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffprobe.exe"),
                // Rutas comunes en servidores de hosting
                @"C:\inetpub\ffmpeg\bin\ffprobe.exe",
                @"D:\ffmpeg\bin\ffprobe.exe",
            };

            foreach (var ruta in rutasComunes)
            {
                if (System.IO.File.Exists(ruta))
                    return ruta;
            }

            return null;
        }

        /// <summary>
        /// Buscar ffmpeg en rutas comunes
        /// </summary>
        private string? BuscarFFmpeg()
        {
            // Primero intentar en PATH
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                        return "ffmpeg";
                }
            }
            catch { }

            // Buscar en rutas comunes de Windows
            var rutasComunes = new[]
            {
                // Chocolatey (muy común en servidores Windows)
                @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                @"C:\tools\ffmpeg\bin\ffmpeg.exe",
                @"C:\Users\Administrator\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin", "ffmpeg.exe"),
                // Rutas comunes en servidores de hosting
                @"C:\inetpub\ffmpeg\bin\ffmpeg.exe",
                @"D:\ffmpeg\bin\ffmpeg.exe",
            };

            foreach (var ruta in rutasComunes)
            {
                if (System.IO.File.Exists(ruta))
                    return ruta;
            }

            return null;
        }

        /// <summary>
        /// Reconvertir un video específico a H.264
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReconvertirVideo(string tipo, int id)
        {
            try
            {
                string? rutaArchivo = null;

                // Obtener ruta según tipo
                if (tipo == "Contenido")
                {
                    var contenido = await _context.Contenidos.FindAsync(id);
                    rutaArchivo = contenido?.RutaArchivo;
                }
                else if (tipo == "Archivo")
                {
                    var archivo = await _context.ArchivosContenido.FindAsync(id);
                    rutaArchivo = archivo?.RutaArchivo;
                }
                else if (tipo == "Story")
                {
                    var story = await _context.Stories.FindAsync(id);
                    rutaArchivo = story?.RutaArchivo;
                }

                if (string.IsNullOrEmpty(rutaArchivo))
                {
                    return Json(new { success = false, message = "No se encontró el archivo" });
                }

                var rutaFisica = Path.Combine(_hostEnvironment.WebRootPath, rutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (!System.IO.File.Exists(rutaFisica))
                {
                    return Json(new { success = false, message = "El archivo no existe en disco" });
                }

                // Crear ruta temporal para el archivo convertido
                var carpeta = Path.GetDirectoryName(rutaFisica)!;
                var nombreBase = Path.GetFileNameWithoutExtension(rutaFisica);
                var rutaTemporal = Path.Combine(carpeta, $"{nombreBase}_temp.mp4");
                var rutaFinal = Path.Combine(carpeta, $"{nombreBase}.mp4");

                // Buscar ffmpeg
                var ffmpegPath = BuscarFFmpeg();
                if (ffmpegPath == null)
                {
                    return Json(new { success = false, message = "ffmpeg no encontrado en PATH ni rutas comunes" });
                }

                // Convertir con ffmpeg
                // Parámetros optimizados estilo TikTok: H.264, 30fps, AAC 192kbps
                var arguments = $"-y -i \"{rutaFisica}\" -c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p -r 30 -c:a aac -b:a 192k -movflags +faststart -vf \"scale='min(1920,iw)':-2\" \"{rutaTemporal}\"";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    return Json(new { success = false, message = "No se pudo iniciar ffmpeg" });
                }

                var errorOutput = await process.StandardError.ReadToEndAsync();
                var completed = await Task.Run(() => process.WaitForExit(600000)); // 10 minutos máximo

                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    return Json(new { success = false, message = "Timeout - el video es muy largo" });
                }

                if (process.ExitCode != 0 || !System.IO.File.Exists(rutaTemporal))
                {
                    return Json(new { success = false, message = $"Error en ffmpeg: {errorOutput.Substring(0, Math.Min(500, errorOutput.Length))}" });
                }

                // Verificar que el archivo temporal es válido
                var tempInfo = new FileInfo(rutaTemporal);
                if (tempInfo.Length < 1000)
                {
                    System.IO.File.Delete(rutaTemporal);
                    return Json(new { success = false, message = "El archivo convertido es muy pequeño, posible error" });
                }

                // Reemplazar archivo original
                var pesoOriginal = new FileInfo(rutaFisica).Length;
                System.IO.File.Delete(rutaFisica);

                // Si la ruta original no era .mp4, renombrar
                if (!rutaFisica.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.File.Move(rutaTemporal, rutaFinal);

                    // Actualizar BD con nueva extensión
                    var nuevaRutaRelativa = "/" + rutaFinal.Substring(_hostEnvironment.WebRootPath.TrimEnd('\\', '/').Length).Replace('\\', '/').TrimStart('/');

                    if (tipo == "Contenido")
                    {
                        var contenido = await _context.Contenidos.FindAsync(id);
                        if (contenido != null) contenido.RutaArchivo = nuevaRutaRelativa;
                    }
                    else if (tipo == "Archivo")
                    {
                        var archivo = await _context.ArchivosContenido.FindAsync(id);
                        if (archivo != null) archivo.RutaArchivo = nuevaRutaRelativa;
                    }
                    else if (tipo == "Story")
                    {
                        var story = await _context.Stories.FindAsync(id);
                        if (story != null) story.RutaArchivo = nuevaRutaRelativa;
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    System.IO.File.Move(rutaTemporal, rutaFisica);
                }

                var pesoNuevo = new FileInfo(rutaFinal.EndsWith(".mp4") ? rutaFinal : rutaFisica).Length;
                var reduccion = ((pesoOriginal - pesoNuevo) / (double)pesoOriginal * 100);

                await _logEventoService.RegistrarEventoAsync(
                    $"[ReconvertirVideo] {tipo} ID={id} convertido. {pesoOriginal / 1024 / 1024:F1}MB -> {pesoNuevo / 1024 / 1024:F1}MB ({reduccion:F0}% reducción)",
                    CategoriaEvento.Sistema, TipoLogEvento.Evento);

                return Json(new
                {
                    success = true,
                    message = $"Convertido exitosamente. {pesoOriginal / 1024 / 1024:F1}MB → {pesoNuevo / 1024 / 1024:F1}MB ({reduccion:F0}% reducción)"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Reconvertir una imagen específica a JPEG
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReconvertirImagen(string tipo, int id)
        {
            try
            {
                string? rutaArchivo = null;

                // Obtener ruta según tipo
                if (tipo == "Contenido")
                {
                    var contenido = await _context.Contenidos.FindAsync(id);
                    rutaArchivo = contenido?.RutaArchivo;
                }
                else if (tipo == "Archivo")
                {
                    var archivo = await _context.ArchivosContenido.FindAsync(id);
                    rutaArchivo = archivo?.RutaArchivo;
                }
                else if (tipo == "Story")
                {
                    var story = await _context.Stories.FindAsync(id);
                    rutaArchivo = story?.RutaArchivo;
                }

                if (string.IsNullOrEmpty(rutaArchivo))
                {
                    return Json(new { success = false, message = "No se encontró el archivo" });
                }

                var rutaFisica = Path.Combine(_hostEnvironment.WebRootPath, rutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (!System.IO.File.Exists(rutaFisica))
                {
                    return Json(new { success = false, message = "El archivo no existe en disco" });
                }

                var carpeta = Path.GetDirectoryName(rutaFisica)!;
                var nombreBase = Path.GetFileNameWithoutExtension(rutaFisica);
                var rutaFinal = Path.Combine(carpeta, $"{nombreBase}.jpg");

                // Convertir imagen usando ImageMagick
                var pesoOriginal = new FileInfo(rutaFisica).Length;

                using (var image = new ImageMagick.MagickImage(rutaFisica))
                {
                    image.AutoOrient();

                    // Redimensionar si es muy grande
                    if (image.Width > 2048 || image.Height > 2048)
                    {
                        var geometry = new ImageMagick.MagickGeometry(2048, 2048)
                        {
                            IgnoreAspectRatio = false
                        };
                        image.Resize(geometry);
                    }

                    image.Format = ImageMagick.MagickFormat.Jpeg;
                    image.Quality = 90;
                    await image.WriteAsync(rutaFinal);
                }

                // Si la conversión fue exitosa, eliminar el original si es diferente
                if (rutaFisica != rutaFinal && System.IO.File.Exists(rutaFinal))
                {
                    System.IO.File.Delete(rutaFisica);

                    // Actualizar BD con nueva extensión
                    var nuevaRutaRelativa = "/" + rutaFinal.Substring(_hostEnvironment.WebRootPath.TrimEnd('\\', '/').Length).Replace('\\', '/').TrimStart('/');

                    if (tipo == "Contenido")
                    {
                        var contenido = await _context.Contenidos.FindAsync(id);
                        if (contenido != null) contenido.RutaArchivo = nuevaRutaRelativa;
                    }
                    else if (tipo == "Archivo")
                    {
                        var archivo = await _context.ArchivosContenido.FindAsync(id);
                        if (archivo != null) archivo.RutaArchivo = nuevaRutaRelativa;
                    }
                    else if (tipo == "Story")
                    {
                        var story = await _context.Stories.FindAsync(id);
                        if (story != null) story.RutaArchivo = nuevaRutaRelativa;
                    }

                    await _context.SaveChangesAsync();
                }

                var pesoNuevo = new FileInfo(rutaFinal).Length;
                var reduccion = ((pesoOriginal - pesoNuevo) / (double)pesoOriginal * 100);

                await _logEventoService.RegistrarEventoAsync(
                    $"[ReconvertirImagen] {tipo} ID={id} convertido a JPEG. {pesoOriginal / 1024:F0}KB -> {pesoNuevo / 1024:F0}KB ({reduccion:F0}% reducción)",
                    CategoriaEvento.Sistema, TipoLogEvento.Evento);

                return Json(new
                {
                    success = true,
                    message = $"Convertido a JPEG. {pesoOriginal / 1024:F0}KB → {pesoNuevo / 1024:F0}KB ({reduccion:F0}% reducción)"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Reconvertir todos los videos problemáticos
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReconvertirTodosVideos()
        {
            // Esta operación se hace de forma síncrona para mostrar progreso
            // En producción debería ser un job en segundo plano

            var convertidos = 0;
            var errores = 0;
            var mensajes = new List<string>();

            // Obtener videos de contenidos que necesitan conversión
            var contenidos = await _context.Contenidos
                .Where(c => c.EstaActivo && c.TipoContenido == TipoContenido.Video && c.RutaArchivo != null)
                .Select(c => new { c.Id, c.RutaArchivo })
                .ToListAsync();

            foreach (var c in contenidos)
            {
                var analisis = await AnalizarVideoAsync(c.Id, "Contenido", c.RutaArchivo!, null);
                if (analisis != null && (analisis.Estado == "necesita_conversion" || analisis.Estado == "muy_pesado"))
                {
                    var resultado = await ReconvertirVideo("Contenido", c.Id) as JsonResult;
                    var data = resultado?.Value as dynamic;
                    if (data?.success == true)
                        convertidos++;
                    else
                        errores++;
                }
            }

            return Json(new
            {
                success = true,
                convertidos,
                errores,
                message = $"Proceso completado: {convertidos} convertidos, {errores} errores"
            });
        }

        /// <summary>
        /// Diagnóstico rápido: Verificar estado de videos en BD vs archivos en disco
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DiagnosticoVideos()
        {
            var resultados = new List<object>();
            var totalVideos = 0;
            var archivosExisten = 0;
            var archivosNoExisten = 0;
            var rutasIncorrectas = 0;

            // Contenidos
            var contenidos = await _context.Contenidos
                .Where(c => c.EstaActivo && c.TipoContenido == TipoContenido.Video && c.RutaArchivo != null)
                .Select(c => new { c.Id, c.RutaArchivo })
                .Take(100)
                .ToListAsync();

            foreach (var c in contenidos)
            {
                totalVideos++;
                var rutaFisica = Path.Combine(_hostEnvironment.WebRootPath, c.RutaArchivo!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                var existe = System.IO.File.Exists(rutaFisica);
                var extension = Path.GetExtension(c.RutaArchivo).ToLower();
                var esMp4 = extension == ".mp4";

                if (existe) archivosExisten++;
                else archivosNoExisten++;

                if (!esMp4) rutasIncorrectas++;

                if (!existe || !esMp4)
                {
                    // Buscar si existe versión convertida
                    var carpeta = Path.GetDirectoryName(rutaFisica);
                    var nombreBase = Path.GetFileNameWithoutExtension(rutaFisica);
                    var rutaMp4 = Path.Combine(carpeta!, $"{nombreBase}.mp4");
                    var existeMp4 = System.IO.File.Exists(rutaMp4);

                    resultados.Add(new
                    {
                        tipo = "Contenido",
                        id = c.Id,
                        rutaBD = c.RutaArchivo,
                        extension = extension,
                        archivoExiste = existe,
                        esMp4 = esMp4,
                        versionMp4Existe = existeMp4,
                        problema = !existe ? "Archivo no existe" : (!esMp4 ? "Extensión no es .mp4" : "OK")
                    });
                }
            }

            // Archivos de carrusel
            var archivos = await _context.ArchivosContenido
                .Where(a => a.TipoArchivo == TipoArchivo.Video && a.RutaArchivo != null)
                .Select(a => new { a.Id, a.RutaArchivo })
                .Take(50)
                .ToListAsync();

            foreach (var a in archivos)
            {
                totalVideos++;
                var rutaFisica = Path.Combine(_hostEnvironment.WebRootPath, a.RutaArchivo!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                var existe = System.IO.File.Exists(rutaFisica);
                var extension = Path.GetExtension(a.RutaArchivo).ToLower();
                var esMp4 = extension == ".mp4";

                if (existe) archivosExisten++;
                else archivosNoExisten++;

                if (!esMp4) rutasIncorrectas++;

                if (!existe || !esMp4)
                {
                    var carpeta = Path.GetDirectoryName(rutaFisica);
                    var nombreBase = Path.GetFileNameWithoutExtension(rutaFisica);
                    var rutaMp4 = Path.Combine(carpeta!, $"{nombreBase}.mp4");
                    var existeMp4 = System.IO.File.Exists(rutaMp4);

                    resultados.Add(new
                    {
                        tipo = "Archivo",
                        id = a.Id,
                        rutaBD = a.RutaArchivo,
                        extension = extension,
                        archivoExiste = existe,
                        esMp4 = esMp4,
                        versionMp4Existe = existeMp4,
                        problema = !existe ? "Archivo no existe" : (!esMp4 ? "Extensión no es .mp4" : "OK")
                    });
                }
            }

            return Json(new
            {
                resumen = new
                {
                    totalVideos,
                    archivosExisten,
                    archivosNoExisten,
                    rutasNoMp4 = rutasIncorrectas,
                    conProblemas = resultados.Count
                },
                problemas = resultados
            });
        }

        #endregion

        #region Actualizar Metadatos de Archivos

        private static bool _metadatosEnProgreso = false;
        private static int _metadatosTotal = 0;
        private static int _metadatosProcesados = 0;
        private static int _metadatosActualizados = 0;
        private static int _metadatosFallidos = 0;
        private static string _metadatosMensaje = "";

        [HttpGet]
        public async Task<IActionResult> ActualizarMetadatos()
        {
            // Contar archivos sin metadatos
            var sinDimensiones = await _context.ArchivosContenido
                .CountAsync(a => a.Ancho == null || a.Alto == null);
            var sinTamano = await _context.ArchivosContenido
                .CountAsync(a => a.TamanoBytes == null || a.TamanoBytes == 0);
            var videosSinDuracion = await _context.ArchivosContenido
                .CountAsync(a => a.TipoArchivo == TipoArchivo.Video && (a.DuracionSegundos == null || a.DuracionSegundos == 0));

            var totalArchivos = await _context.ArchivosContenido.CountAsync();

            // También contar contenidos que usan RutaArchivo directo (sin ArchivosContenido)
            var contenidosSinArchivos = await _context.Contenidos
                .CountAsync(c => !string.IsNullOrEmpty(c.RutaArchivo) && !c.Archivos.Any());

            ViewBag.SinDimensiones = sinDimensiones;
            ViewBag.SinTamano = sinTamano;
            ViewBag.VideosSinDuracion = videosSinDuracion;
            ViewBag.TotalArchivos = totalArchivos;
            ViewBag.ContenidosSinArchivos = contenidosSinArchivos;
            ViewBag.MetadatosEnProgreso = _metadatosEnProgreso;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult IniciarActualizacionMetadatos()
        {
            if (_metadatosEnProgreso)
            {
                return Json(new { success = false, message = "Ya hay una actualización en progreso" });
            }

            _metadatosEnProgreso = true;
            _metadatosTotal = 0;
            _metadatosProcesados = 0;
            _metadatosActualizados = 0;
            _metadatosFallidos = 0;
            _metadatosMensaje = "Iniciando...";

            _ = Task.Run(async () =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var logService = scope.ServiceProvider.GetRequiredService<ILogEventoService>();

                await logService.RegistrarEventoAsync(
                    "[ActualizarMetadatos] Iniciando actualización de metadatos",
                    CategoriaEvento.Sistema, TipoLogEvento.Evento);

                try
                {
                    await EjecutarActualizacionMetadatosAsync();
                    await logService.RegistrarEventoAsync(
                        $"[ActualizarMetadatos] Completado: {_metadatosActualizados} actualizados, {_metadatosFallidos} fallidos",
                        CategoriaEvento.Sistema, TipoLogEvento.Evento);
                }
                catch (Exception ex)
                {
                    _metadatosMensaje = $"Error: {ex.Message}";
                    await logService.RegistrarEventoAsync(
                        $"[ActualizarMetadatos] Error: {ex.Message}",
                        CategoriaEvento.Sistema, TipoLogEvento.Error,
                        detalle: ex.StackTrace);
                }
                finally
                {
                    _metadatosEnProgreso = false;
                }
            });

            return Json(new { success = true, message = "Actualización iniciada" });
        }

        [HttpGet]
        public IActionResult EstadoActualizacionMetadatos()
        {
            return Json(new
            {
                enProgreso = _metadatosEnProgreso,
                total = _metadatosTotal,
                procesados = _metadatosProcesados,
                actualizados = _metadatosActualizados,
                fallidos = _metadatosFallidos,
                mensaje = _metadatosMensaje,
                porcentaje = _metadatosTotal > 0 ? (int)((_metadatosProcesados / (double)_metadatosTotal) * 100) : 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelarActualizacionMetadatos()
        {
            _metadatosEnProgreso = false;
            _metadatosMensaje = "Cancelado por usuario";
            return Json(new { success = true, message = "Actualización cancelada" });
        }

        private async Task EjecutarActualizacionMetadatosAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logService = scope.ServiceProvider.GetRequiredService<ILogEventoService>();

            var webRoot = _hostEnvironment.WebRootPath;

            // PASO 1: Crear ArchivosContenido para Contenidos que no tienen
            var contenidosSinArchivos = await context.Contenidos
                .Include(c => c.Archivos)
                .Where(c => !string.IsNullOrEmpty(c.RutaArchivo) && !c.Archivos.Any())
                .ToListAsync();

            await logService.RegistrarEventoAsync(
                $"[ActualizarMetadatos] Creando {contenidosSinArchivos.Count} registros ArchivoContenido faltantes",
                CategoriaEvento.Sistema, TipoLogEvento.Evento);

            foreach (var contenido in contenidosSinArchivos)
            {
                var nuevoArchivo = new ArchivoContenido
                {
                    ContenidoId = contenido.Id,
                    RutaArchivo = contenido.RutaArchivo!,
                    TipoArchivo = contenido.TipoContenido == TipoContenido.Video ? TipoArchivo.Video : TipoArchivo.Foto,
                    Thumbnail = contenido.Thumbnail,
                    Orden = 0
                };
                context.ArchivosContenido.Add(nuevoArchivo);
            }

            if (contenidosSinArchivos.Any())
            {
                await context.SaveChangesAsync();
            }

            // PASO 2: Obtener TODOS los archivos (sin filtro de NULL para asegurar actualizacion completa)
            var archivos = await context.ArchivosContenido.ToListAsync();

            _metadatosTotal = archivos.Count;
            _metadatosMensaje = $"Procesando {_metadatosTotal} archivos...";

            await logService.RegistrarEventoAsync(
                $"[ActualizarMetadatos] Procesando {_metadatosTotal} archivos totales",
                CategoriaEvento.Sistema, TipoLogEvento.Evento);

            foreach (var archivo in archivos)
            {
                if (!_metadatosEnProgreso) break;

                _metadatosProcesados++;

                try
                {
                    var rutaRelativa = archivo.RutaArchivo?.TrimStart('/') ?? "";
                    var rutaCompleta = Path.Combine(webRoot, rutaRelativa.Replace('/', Path.DirectorySeparatorChar));

                    _metadatosMensaje = $"[{_metadatosProcesados}/{_metadatosTotal}] {Path.GetFileName(rutaCompleta)}";

                    if (!System.IO.File.Exists(rutaCompleta))
                    {
                        _metadatosFallidos++;
                        await logService.RegistrarEventoAsync(
                            $"[ActualizarMetadatos] Archivo no existe: {rutaCompleta}",
                            CategoriaEvento.Sistema, TipoLogEvento.Warning);
                        continue;
                    }

                    // Siempre obtener tamano del archivo
                    var fileInfo = new FileInfo(rutaCompleta);
                    archivo.TamanoBytes = fileInfo.Length;

                    var extension = Path.GetExtension(rutaCompleta).ToLower();
                    var esVideo = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm", ".wmv", ".flv", ".m4v", ".3gp" }.Contains(extension);

                    if (esVideo)
                    {
                        // Usar FFprobe para obtener info del video
                        var videoInfo = await ObtenerInfoVideoAsync(rutaCompleta);
                        if (videoInfo != null)
                        {
                            archivo.Ancho = videoInfo.Ancho;
                            archivo.Alto = videoInfo.Alto;
                            archivo.DuracionSegundos = videoInfo.DuracionSegundos;
                            archivo.TipoArchivo = TipoArchivo.Video;
                        }
                        else
                        {
                            await logService.RegistrarEventoAsync(
                                $"[ActualizarMetadatos] FFprobe fallo para: {Path.GetFileName(rutaCompleta)}",
                                CategoriaEvento.Sistema, TipoLogEvento.Warning);
                        }
                    }
                    else
                    {
                        // Usar ImageSharp para obtener dimensiones de imagen
                        try
                        {
                            using var image = await SixLabors.ImageSharp.Image.LoadAsync(rutaCompleta);
                            archivo.Ancho = image.Width;
                            archivo.Alto = image.Height;
                            archivo.TipoArchivo = TipoArchivo.Foto;
                        }
                        catch (Exception imgEx)
                        {
                            await logService.RegistrarEventoAsync(
                                $"[ActualizarMetadatos] ImageSharp fallo para {Path.GetFileName(rutaCompleta)}: {imgEx.Message}",
                                CategoriaEvento.Sistema, TipoLogEvento.Warning);
                        }
                    }

                    // Guardar despues de cada archivo
                    context.ArchivosContenido.Update(archivo);
                    await context.SaveChangesAsync();
                    _metadatosActualizados++;
                }
                catch (Exception ex)
                {
                    _metadatosFallidos++;
                    await logService.RegistrarEventoAsync(
                        $"[ActualizarMetadatos] Error en archivo ID={archivo.Id}: {ex.Message}",
                        CategoriaEvento.Sistema, TipoLogEvento.Error,
                        detalle: ex.StackTrace);
                }
            }

            _metadatosMensaje = $"Completado: {_metadatosActualizados} actualizados, {_metadatosFallidos} fallidos de {_metadatosTotal}";

            await logService.RegistrarEventoAsync(
                $"[ActualizarMetadatos] {_metadatosMensaje}",
                CategoriaEvento.Sistema, TipoLogEvento.Evento);
        }

        private async Task<VideoInfoResult?> ObtenerInfoVideoAsync(string rutaVideo)
        {
            try
            {
                var ffprobePath = "ffprobe";
                var args = $"-v quiet -print_format json -show_format -show_streams \"{rutaVideo}\"";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null) return null;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0 || string.IsNullOrEmpty(output)) return null;

                // Parsear JSON de ffprobe
                using var doc = System.Text.Json.JsonDocument.Parse(output);
                var root = doc.RootElement;

                int? ancho = null, alto = null, duracion = null;

                // Buscar stream de video
                if (root.TryGetProperty("streams", out var streams))
                {
                    foreach (var stream in streams.EnumerateArray())
                    {
                        if (stream.TryGetProperty("codec_type", out var codecType) &&
                            codecType.GetString() == "video")
                        {
                            if (stream.TryGetProperty("width", out var w)) ancho = w.GetInt32();
                            if (stream.TryGetProperty("height", out var h)) alto = h.GetInt32();
                            break;
                        }
                    }
                }

                // Obtener duración del formato
                if (root.TryGetProperty("format", out var format))
                {
                    if (format.TryGetProperty("duration", out var dur))
                    {
                        if (double.TryParse(dur.GetString(), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var durSecs))
                        {
                            duracion = (int)durSecs;
                        }
                    }
                }

                return new VideoInfoResult { Ancho = ancho, Alto = alto, DuracionSegundos = duracion };
            }
            catch
            {
                return null;
            }
        }

        private class VideoInfoResult
        {
            public int? Ancho { get; set; }
            public int? Alto { get; set; }
            public int? DuracionSegundos { get; set; }
        }

        #endregion

        // ========================================
        // LADOCOINS - PANEL DE ADMINISTRACIÓN
        // ========================================

        #region LadoCoins

        /// <summary>
        /// Dashboard principal de LadoCoins
        /// </summary>
        public async Task<IActionResult> LadoCoins()
        {
            // Estadísticas globales
            var totalEnCirculacion = await _context.LadoCoins.SumAsync(l => l.SaldoDisponible);
            var totalGanado = await _context.LadoCoins.SumAsync(l => l.TotalGanado);
            var totalGastado = await _context.LadoCoins.SumAsync(l => l.TotalGastado);
            var totalQuemado = await _context.LadoCoins.SumAsync(l => l.TotalQuemado);

            // Contar vencidos
            var totalVencido = await _context.TransaccionesLadoCoins
                .Where(t => t.Tipo == TipoTransaccionLadoCoin.Vencimiento)
                .SumAsync(t => Math.Abs(t.Monto));

            // Usuarios con LadoCoins
            var usuariosConSaldo = await _context.LadoCoins.CountAsync(l => l.SaldoDisponible > 0);
            var totalUsuarios = await _context.LadoCoins.CountAsync();

            // Top 10 usuarios con más saldo
            var topUsuarios = await _context.LadoCoins
                .Include(l => l.Usuario)
                .Where(l => l.SaldoDisponible > 0)
                .OrderByDescending(l => l.SaldoDisponible)
                .Take(10)
                .Select(l => new LadoCoinsUsuarioViewModel
                {
                    UsuarioId = l.UsuarioId,
                    Username = l.Usuario != null ? l.Usuario.UserName : "?",
                    Email = l.Usuario != null ? l.Usuario.Email : "",
                    SaldoDisponible = l.SaldoDisponible,
                    TotalGanado = l.TotalGanado,
                    TotalGastado = l.TotalGastado
                })
                .ToListAsync();

            // Transacciones recientes (últimas 20)
            var transaccionesRecientes = await _context.TransaccionesLadoCoins
                .Include(t => t.Usuario)
                .OrderByDescending(t => t.FechaTransaccion)
                .Take(20)
                .ToListAsync();

            // Estadísticas por tipo de transacción
            var statsPorTipo = await _context.TransaccionesLadoCoins
                .Where(t => t.Monto > 0)
                .GroupBy(t => t.Tipo)
                .Select(g => new { Tipo = g.Key, Total = g.Sum(t => t.Monto), Count = g.Count() })
                .ToListAsync();

            var viewModel = new LadoCoinsDashboardAdminViewModel
            {
                TotalEnCirculacion = totalEnCirculacion,
                TotalGanado = totalGanado,
                TotalGastado = totalGastado,
                TotalQuemado = totalQuemado,
                TotalVencido = totalVencido,
                UsuariosConSaldo = usuariosConSaldo,
                TotalUsuarios = totalUsuarios,
                TopUsuarios = topUsuarios,
                TransaccionesRecientes = transaccionesRecientes,
                EstadisticasPorTipo = statsPorTipo.ToDictionary(
                    x => x.Tipo.ToString(),
                    x => new LadoCoinsStatTipo { Total = x.Total, Count = x.Count }
                )
            };

            return View(viewModel);
        }

        /// <summary>
        /// Configuración de LadoCoins (bonos, quema, vencimiento, etc.)
        /// </summary>
        public async Task<IActionResult> LadoCoinsConfiguracion()
        {
            var configuraciones = await _context.ConfiguracionesLadoCoins
                .OrderBy(c => c.Categoria)
                .ThenBy(c => c.Clave)
                .ToListAsync();

            // Si no hay configuraciones, insertar los valores por defecto
            if (!configuraciones.Any())
            {
                var valoresDefault = ConfiguracionLadoCoin.ObtenerValoresDefault();
                _context.ConfiguracionesLadoCoins.AddRange(valoresDefault);
                await _context.SaveChangesAsync();

                configuraciones = await _context.ConfiguracionesLadoCoins
                    .OrderBy(c => c.Categoria)
                    .ThenBy(c => c.Clave)
                    .ToListAsync();
            }

            return View(configuraciones);
        }

        /// <summary>
        /// Guardar configuración de LadoCoins
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarConfiguracionLadoCoins(int id, decimal valor)
        {
            try
            {
                var config = await _context.ConfiguracionesLadoCoins.FindAsync(id);
                if (config == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada" });
                }

                var valorAnterior = config.Valor;
                config.Valor = valor;
                config.FechaModificacion = DateTime.Now;
                config.ModificadoPor = User.Identity?.Name;

                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Configuración LadoCoins modificada: {config.Clave}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Evento,
                    null,
                    User.Identity?.Name,
                    $"Valor anterior: {valorAnterior}, Nuevo valor: {valor}"
                );

                return Json(new { success = true, message = "Configuración actualizada", modificadoPor = config.ModificadoPor });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Toggle activo/inactivo de una configuración LadoCoins
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleConfiguracionLadoCoins(int id, bool activo)
        {
            try
            {
                var config = await _context.ConfiguracionesLadoCoins.FindAsync(id);
                if (config == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada" });
                }

                config.Activo = activo;
                config.FechaModificacion = DateTime.Now;
                config.ModificadoPor = User.Identity?.Name;

                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Configuración LadoCoins {(activo ? "activada" : "desactivada")}: {config.Clave}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Evento,
                    null,
                    User.Identity?.Name,
                    null
                );

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Buscar usuario para gestionar sus LadoCoins
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarUsuarioLadoCoins(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new { success = false, message = "Ingresa al menos 2 caracteres" });
            }

            var usuarios = await _context.Users
                .Where(u => u.UserName!.Contains(query) || u.Email!.Contains(query) || u.NombreCompleto!.Contains(query))
                .Take(10)
                .Select(u => new
                {
                    usuarioId = u.Id,
                    username = u.UserName,
                    email = u.Email,
                    nombreCompleto = u.NombreCompleto,
                    fotoPerfil = u.FotoPerfil
                })
                .ToListAsync();

            // Obtener saldos de LadoCoins
            var userIds = usuarios.Select(u => u.usuarioId).ToList();
            var saldos = await _context.LadoCoins
                .Where(l => userIds.Contains(l.UsuarioId))
                .ToDictionaryAsync(l => l.UsuarioId, l => l.SaldoDisponible);

            var resultado = usuarios.Select(u => new
            {
                u.usuarioId,
                u.username,
                u.email,
                u.nombreCompleto,
                u.fotoPerfil,
                saldoDisponible = saldos.TryGetValue(u.usuarioId, out var s) ? s : 0m
            });

            return Json(new { success = true, usuarios = resultado });
        }

        /// <summary>
        /// Ver detalle de LadoCoins de un usuario
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DetalleUsuarioLadoCoins(string usuarioId)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no encontrado" });
            }

            var saldo = await _context.LadoCoins.FirstOrDefaultAsync(l => l.UsuarioId == usuarioId);

            return Json(new
            {
                success = true,
                usuario = new
                {
                    usuarioId = usuario.Id,
                    username = usuario.UserName,
                    email = usuario.Email,
                    nombreCompleto = usuario.NombreCompleto,
                    fotoPerfil = usuario.FotoPerfil,
                    saldoDisponible = saldo?.SaldoDisponible ?? 0,
                    totalGanado = saldo?.TotalGanado ?? 0,
                    totalGastado = saldo?.TotalGastado ?? 0,
                    totalQuemado = saldo?.TotalQuemado ?? 0
                }
            });
        }

        /// <summary>
        /// Acreditar LadoCoins manualmente a un usuario
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcreditarLadoCoins(string usuarioId, decimal monto, string razon)
        {
            try
            {
                if (monto <= 0)
                {
                    return Json(new { success = false, message = "El monto debe ser mayor a 0" });
                }

                if (string.IsNullOrWhiteSpace(razon))
                {
                    return Json(new { success = false, message = "Debes especificar una razón" });
                }

                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                var descripcion = $"[ADMIN] {razon} - Por: {User.Identity?.Name}";

                var exito = await _ladoCoinsService.AcreditarMontoAsync(
                    usuarioId,
                    monto,
                    TipoTransaccionLadoCoin.BonoAdmin,
                    descripcion,
                    null,
                    "Admin"
                );

                if (!exito)
                {
                    return Json(new { success = false, message = "Error al acreditar LadoCoins" });
                }

                await _logEventoService.RegistrarEventoAsync(
                    $"LadoCoins acreditados manualmente: ${monto:F2} a @{usuario.UserName}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Evento,
                    usuarioId,
                    User.Identity?.Name,
                    razon
                );

                var nuevoSaldo = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuarioId);

                return Json(new { success = true, message = $"${monto:F2} acreditados a @{usuario.UserName}", nuevoSaldo });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Debitar LadoCoins manualmente de un usuario
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DebitarLadoCoins(string usuarioId, decimal monto, string razon)
        {
            try
            {
                if (monto <= 0)
                {
                    return Json(new { success = false, message = "El monto debe ser mayor a 0" });
                }

                if (string.IsNullOrWhiteSpace(razon))
                {
                    return Json(new { success = false, message = "Debes especificar una razón" });
                }

                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                var saldoActual = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuarioId);
                if (saldoActual < monto)
                {
                    return Json(new { success = false, message = $"El usuario solo tiene ${saldoActual:F2} disponibles" });
                }

                var descripcion = $"[ADMIN] {razon} - Por: {User.Identity?.Name}";

                var (exito, _) = await _ladoCoinsService.DebitarAsync(
                    usuarioId,
                    monto,
                    TipoTransaccionLadoCoin.DebitoAdmin,
                    descripcion,
                    null,
                    "Admin"
                );

                if (!exito)
                {
                    return Json(new { success = false, message = "Error al debitar LadoCoins" });
                }

                await _logEventoService.RegistrarEventoAsync(
                    $"LadoCoins debitados manualmente: ${monto:F2} de @{usuario.UserName}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Warning,
                    usuarioId,
                    User.Identity?.Name,
                    razon
                );

                var nuevoSaldo = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuarioId);

                return Json(new { success = true, message = $"${monto:F2} debitados de @{usuario.UserName}", nuevoSaldo });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Historial global de transacciones LadoCoins
        /// </summary>
        public async Task<IActionResult> HistorialLadoCoins(int pagina = 1, string? tipo = null, string? usuario = null)
        {
            var query = _context.TransaccionesLadoCoins
                .Include(t => t.Usuario)
                .AsQueryable();

            if (!string.IsNullOrEmpty(tipo) && Enum.TryParse<TipoTransaccionLadoCoin>(tipo, out var tipoEnum))
            {
                query = query.Where(t => t.Tipo == tipoEnum);
            }

            if (!string.IsNullOrEmpty(usuario))
            {
                query = query.Where(t => t.Usuario != null &&
                    (t.Usuario.UserName!.Contains(usuario) || t.Usuario.Email!.Contains(usuario)));
            }

            var total = await query.CountAsync();
            var porPagina = 50;
            var totalPaginas = (int)Math.Ceiling(total / (double)porPagina);

            var transacciones = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            ViewBag.Pagina = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.Total = total;
            ViewBag.TipoFiltro = tipo;
            ViewBag.UsuarioFiltro = usuario;
            ViewBag.TiposTransaccion = Enum.GetNames(typeof(TipoTransaccionLadoCoin));

            return View(transacciones);
        }

        #endregion

        #region SEO

        /// <summary>
        /// Vista principal de configuración SEO
        /// </summary>
        public async Task<IActionResult> ConfiguracionSeo()
        {
            var config = await _seoConfigService.ObtenerConfiguracionAsync();
            var redirecciones = await _seoConfigService.ObtenerTodasRedireccionesAsync();
            var rutasRobots = await _seoConfigService.ObtenerTodasRutasRobotsAsync();
            var botsRobots = await _seoConfigService.ObtenerTodosBotsRobotsAsync();

            ViewBag.Redirecciones = redirecciones;
            ViewBag.RutasRobots = rutasRobots;
            ViewBag.BotsRobots = botsRobots;

            return View(config);
        }

        /// <summary>
        /// Actualizar configuración general de SEO
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarConfiguracionSeo(ConfiguracionSeo config)
        {
            try
            {
                var resultado = await _seoConfigService.ActualizarConfiguracionAsync(
                    config, User.Identity?.Name ?? "Admin");

                if (resultado)
                {
                    await _logEventoService.RegistrarEventoAsync(
                        "Configuración SEO actualizada",
                        CategoriaEvento.Admin,
                        TipoLogEvento.Info,
                        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                        User.Identity?.Name,
                        "Se actualizó la configuración SEO del sitio"
                    );

                    TempData["Success"] = "Configuración SEO actualizada correctamente";
                }
                else
                {
                    TempData["Error"] = "Error al actualizar la configuración SEO";
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    User.Identity?.Name);
                TempData["Error"] = "Error al actualizar la configuración SEO";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Crear nueva redirección 301/302
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearRedireccion(Redireccion301 redireccion)
        {
            try
            {
                // Normalizar URL origen
                if (!redireccion.UrlOrigen.StartsWith("/"))
                    redireccion.UrlOrigen = "/" + redireccion.UrlOrigen;

                redireccion.CreadoPor = User.Identity?.Name;

                await _seoConfigService.CrearRedireccionAsync(redireccion);

                await _logEventoService.RegistrarEventoAsync(
                    $"Redirección creada: {redireccion.UrlOrigen} → {redireccion.UrlDestino}",
                    CategoriaEvento.Admin,
                    TipoLogEvento.Info,
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    User.Identity?.Name
                );

                TempData["Success"] = "Redirección creada correctamente";
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al crear la redirección. Puede que la URL de origen ya exista.";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Eliminar redirección
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarRedireccion(int id)
        {
            try
            {
                var resultado = await _seoConfigService.EliminarRedireccionAsync(id);
                if (resultado)
                {
                    TempData["Success"] = "Redirección eliminada correctamente";
                }
                else
                {
                    TempData["Error"] = "No se encontró la redirección";
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al eliminar la redirección";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Toggle estado de redirección
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRedireccion(int id)
        {
            try
            {
                var redireccion = await _context.Redirecciones301.FindAsync(id);
                if (redireccion != null)
                {
                    redireccion.Activa = !redireccion.Activa;
                    await _context.SaveChangesAsync();
                    _seoConfigService.LimpiarCache();

                    TempData["Success"] = $"Redirección {(redireccion.Activa ? "activada" : "desactivada")}";
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al cambiar estado de la redirección";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Crear nueva ruta de robots.txt
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearRutaRobots(RutaRobotsTxt ruta)
        {
            try
            {
                await _seoConfigService.CrearRutaRobotsAsync(ruta);
                TempData["Success"] = "Ruta agregada correctamente";
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al crear la ruta";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Eliminar ruta de robots.txt
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarRutaRobots(int id)
        {
            try
            {
                var resultado = await _seoConfigService.EliminarRutaRobotsAsync(id);
                if (resultado)
                {
                    TempData["Success"] = "Ruta eliminada correctamente";
                }
                else
                {
                    TempData["Error"] = "No se encontró la ruta";
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al eliminar la ruta";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Toggle estado de ruta robots
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRutaRobots(int id)
        {
            try
            {
                var ruta = await _context.RutasRobotsTxt.FindAsync(id);
                if (ruta != null)
                {
                    ruta.Activa = !ruta.Activa;
                    await _context.SaveChangesAsync();
                    _seoConfigService.LimpiarCache();

                    TempData["Success"] = $"Ruta {(ruta.Activa ? "activada" : "desactivada")}";
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al cambiar estado de la ruta";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Crear nuevo bot de robots.txt
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearBotRobots(BotRobotsTxt bot)
        {
            try
            {
                await _seoConfigService.CrearBotRobotsAsync(bot);
                TempData["Success"] = "Bot agregado correctamente";
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al crear el bot. Puede que ya exista.";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Eliminar bot de robots.txt
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarBotRobots(int id)
        {
            try
            {
                var resultado = await _seoConfigService.EliminarBotRobotsAsync(id);
                if (resultado)
                {
                    TempData["Success"] = "Bot eliminado correctamente";
                }
                else
                {
                    TempData["Error"] = "No se encontró el bot";
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al eliminar el bot";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Toggle estado de bot (activar/desactivar)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBotRobots(int id)
        {
            try
            {
                var bot = await _context.BotsRobotsTxt.FindAsync(id);
                if (bot != null)
                {
                    bot.Activo = !bot.Activo;
                    await _context.SaveChangesAsync();
                    _seoConfigService.LimpiarCache();

                    TempData["Success"] = $"Bot {(bot.Activo ? "activado" : "desactivado")}";
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al cambiar estado del bot";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Toggle bloqueo de bot
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBloqueoBot(int id)
        {
            try
            {
                var bot = await _context.BotsRobotsTxt.FindAsync(id);
                if (bot != null)
                {
                    bot.Bloqueado = !bot.Bloqueado;
                    await _context.SaveChangesAsync();
                    _seoConfigService.LimpiarCache();

                    TempData["Success"] = $"Bot {(bot.Bloqueado ? "bloqueado" : "permitido")}";
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Admin);
                TempData["Error"] = "Error al cambiar bloqueo del bot";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        /// <summary>
        /// Limpiar caché de SEO y sitemaps
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LimpiarCacheSeo()
        {
            try
            {
                _seoConfigService.LimpiarCache();
                _cache.Remove("sitemap_index");
                _cache.Remove("sitemap_paginas");
                _cache.Remove("sitemap_perfiles");
                _cache.Remove("sitemap_contenido");

                _logger.LogInformation("Caché SEO limpiado por {User}", User.Identity?.Name);
                TempData["Success"] = "Caché de SEO y sitemaps limpiado correctamente";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar caché SEO");
                TempData["Error"] = "Error al limpiar la caché";
            }

            return RedirectToAction(nameof(ConfiguracionSeo));
        }

        #endregion
    }

    // ViewModels para LadoCoins Admin
    public class LadoCoinsDashboardAdminViewModel
    {
        public decimal TotalEnCirculacion { get; set; }
        public decimal TotalGanado { get; set; }
        public decimal TotalGastado { get; set; }
        public decimal TotalQuemado { get; set; }
        public decimal TotalVencido { get; set; }
        public int UsuariosConSaldo { get; set; }
        public int TotalUsuarios { get; set; }
        public List<LadoCoinsUsuarioViewModel> TopUsuarios { get; set; } = new();
        public List<TransaccionLadoCoin> TransaccionesRecientes { get; set; } = new();
        public Dictionary<string, LadoCoinsStatTipo> EstadisticasPorTipo { get; set; } = new();
    }

    public class LadoCoinsUsuarioViewModel
    {
        public string UsuarioId { get; set; } = "";
        public string? Username { get; set; }
        public string? Email { get; set; }
        public decimal SaldoDisponible { get; set; }
        public decimal TotalGanado { get; set; }
        public decimal TotalGastado { get; set; }
    }

    public class LadoCoinsStatTipo
    {
        public decimal Total { get; set; }
        public int Count { get; set; }
    }

    public class ConfiguracionEmailViewModel
    {
        public string ProveedorActivo { get; set; } = "Mailjet";
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "";

        // Mailjet
        public string MailjetApiKey { get; set; } = "";
        public string MailjetSecretKey { get; set; } = "";

        // Amazon SES
        public string AmazonSesAccessKey { get; set; } = "";
        public string AmazonSesSecretKey { get; set; } = "";
        public string AmazonSesRegion { get; set; } = "us-east-1";

        // Brevo (antes Sendinblue)
        public string BrevoApiKey { get; set; } = "";
    }

    public class ConfiguracionAlgoritmosViewModel
    {
        // Para Ti (deben sumar 100)
        public int ParaTi_Engagement { get; set; }
        public int ParaTi_Intereses { get; set; }
        public int ParaTi_CreadorFavorito { get; set; }
        public int ParaTi_TipoContenido { get; set; }
        public int ParaTi_Recencia { get; set; }

        // Por Intereses (deben sumar 100)
        public int Intereses_Categoria { get; set; }
        public int Intereses_Descubrimiento { get; set; }
    }

    /// <summary>
    /// Request para registrar logs desde el frontend
    /// </summary>
    public class LogFrontendRequest
    {
        public string? Tipo { get; set; }  // error, warning, info, event
        public string Mensaje { get; set; } = "";
        public string? Detalle { get; set; }
        public string? Url { get; set; }
    }
}