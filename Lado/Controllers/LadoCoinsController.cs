using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    [Authorize]
    public class LadoCoinsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly IReferidosService _referidosService;
        private readonly IRachasService _rachasService;
        private readonly ILogger<LadoCoinsController> _logger;
        private readonly ILogEventoService _logEventoService;
        private readonly IDateTimeService _dateTimeService;

        public LadoCoinsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILadoCoinsService ladoCoinsService,
            IReferidosService referidosService,
            IRachasService rachasService,
            ILogger<LadoCoinsController> logger,
            ILogEventoService logEventoService,
            IDateTimeService dateTimeService)
        {
            _context = context;
            _userManager = userManager;
            _ladoCoinsService = ladoCoinsService;
            _referidosService = referidosService;
            _rachasService = rachasService;
            _logger = logger;
            _logEventoService = logEventoService;
            _dateTimeService = dateTimeService;
        }

        /// <summary>
        /// Dashboard principal de LadoCoins
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var saldo = await _ladoCoinsService.ObtenerOCrearSaldoAsync(usuario.Id);
            var racha = await _rachasService.ObtenerOCrearRachaAsync(usuario.Id);
            var montoPorVencer = await _ladoCoinsService.ObtenerMontoPorVencerAsync(usuario.Id);
            var totalReferidos = await _referidosService.ContarReferidosAsync(usuario.Id);
            var totalComisiones = await _referidosService.TotalComisionesGanadasAsync(usuario.Id);

            // Obtener multiplicadores
            var multiplicadorPublicidad = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.MULTIPLICADOR_PUBLICIDAD);
            var multiplicadorBoost = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.MULTIPLICADOR_BOOST);

            // Obtener código de referido (o generar si no tiene)
            var codigoReferido = usuario.CodigoReferido;
            if (string.IsNullOrEmpty(codigoReferido))
            {
                codigoReferido = await _referidosService.GenerarCodigoReferidoAsync(usuario.Id);
            }

            // Obtener bonos disponibles con progreso
            var contadores = await _rachasService.ObtenerContadoresHoyAsync(usuario.Id);

            var model = new LadoCoinsDashboardViewModel
            {
                SaldoDisponible = saldo.SaldoDisponible,
                SaldoPorVencer = saldo.SaldoPorVencer,
                MontoPorVencer7Dias = montoPorVencer,
                TotalGanado = saldo.TotalGanado,
                TotalGastado = saldo.TotalGastado,
                TotalQuemado = saldo.TotalQuemado,

                RachaActual = racha.RachaActiva() ? racha.RachaActual : 0,
                RachaMaxima = racha.RachaMaxima,

                CodigoReferido = codigoReferido,
                TotalReferidos = totalReferidos,
                TotalComisiones = totalComisiones,

                LikesHoy = contadores.likes,
                ComentariosHoy = contadores.comentarios,
                ContenidosHoy = contadores.contenidos,

                Premio5LikesHoy = racha.Premio5LikesHoy,
                Premio3ComentariosHoy = racha.Premio3ComentariosHoy,
                PremioContenidoHoy = racha.PremioContenidoHoy,
                PremioLoginHoy = racha.PremioLoginHoy,

                MultiplicadorPublicidad = multiplicadorPublicidad > 0 ? multiplicadorPublicidad : 1.5m,
                MultiplicadorBoost = multiplicadorBoost > 0 ? multiplicadorBoost : 2m,

                // Solo creadores verificados pueden usar publicidad y boost
                EsCreadorVerificado = usuario.EsCreador && usuario.CreadorVerificado,

                BonoBienvenidaEntregado = usuario.BonoBienvenidaEntregado,
                BonoPrimerContenidoEntregado = usuario.BonoPrimerContenidoEntregado,
                BonoEmailVerificadoEntregado = usuario.BonoEmailVerificadoEntregado,
                BonoPerfilCompletoEntregado = usuario.BonoPerfilCompletoEntregado,
                EmailConfirmado = usuario.EmailConfirmed,
                PerfilCompleto = usuario.PerfilCompletoParaBono(),

                // Campos individuales del perfil
                TieneFotoPerfil = !string.IsNullOrEmpty(usuario.FotoPerfil),
                TieneBiografia = !string.IsNullOrEmpty(usuario.Biografia),
                TieneNombreCompleto = !string.IsNullOrEmpty(usuario.NombreCompleto),
                TienePais = !string.IsNullOrEmpty(usuario.Pais),
                TieneFechaNacimiento = usuario.FechaNacimiento.HasValue,
                TieneGenero = usuario.Genero != Lado.Models.GeneroUsuario.NoEspecificado
            };

            return View(model);
        }

        /// <summary>
        /// Historial de transacciones
        /// </summary>
        public async Task<IActionResult> Historial(int pagina = 1)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            var porPagina = 20;
            var transacciones = await _ladoCoinsService.ObtenerHistorialAsync(usuario.Id, pagina, porPagina);
            var total = await _context.TransaccionesLadoCoins.CountAsync(t => t.UsuarioId == usuario.Id);

            var model = new LadoCoinsHistorialViewModel
            {
                Transacciones = transacciones,
                PaginaActual = pagina,
                TotalPaginas = (int)Math.Ceiling((double)total / porPagina)
            };

            return View(model);
        }

        /// <summary>
        /// Panel de referidos
        /// </summary>
        public async Task<IActionResult> Referidos()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return RedirectToAction("Login", "Account");

            // Obtener código de referido (o generar si no tiene)
            var codigoReferido = usuario.CodigoReferido;
            if (string.IsNullOrEmpty(codigoReferido))
            {
                codigoReferido = await _referidosService.GenerarCodigoReferidoAsync(usuario.Id);
            }

            var misReferidos = await _referidosService.ObtenerMisReferidosAsync(usuario.Id);
            var miReferidor = await _referidosService.ObtenerMiReferidorAsync(usuario.Id);
            var totalComisiones = await _referidosService.TotalComisionesGanadasAsync(usuario.Id);

            // Obtener bonos de configuración
            var bonoReferidor = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_REFERIDOR);
            var bonoReferido = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_REFERIDO);
            var bonoCreadorLadoB = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_REFERIDO_CREADOR);
            var porcentajeComision = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.COMISION_REFERIDO_PORCENTAJE);
            var mesesComision = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.COMISION_REFERIDO_MESES);

            var model = new LadoCoinsReferidosViewModel
            {
                CodigoReferido = codigoReferido,
                LinkReferido = $"{Request.Scheme}://{Request.Host}/Account/Register?ref={codigoReferido}",
                MisReferidos = misReferidos,
                MiReferidor = miReferidor,
                TotalComisiones = totalComisiones,
                BonoReferidor = bonoReferidor > 0 ? bonoReferidor : 10,
                BonoReferido = bonoReferido > 0 ? bonoReferido : 15,
                BonoCreadorLadoB = bonoCreadorLadoB > 0 ? bonoCreadorLadoB : 50,
                PorcentajeComision = porcentajeComision > 0 ? porcentajeComision : 10,
                MesesComision = (int)(mesesComision > 0 ? mesesComision : 3)
            };

            return View(model);
        }

        /// <summary>
        /// Canjear LadoCoins por crédito publicitario
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CanjearPublicidad(decimal monto)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Json(new { success = false, error = "No autenticado" });

            if (monto <= 0)
            {
                return Json(new { success = false, error = "El monto debe ser mayor a 0" });
            }

            // Verificar saldo
            var puedeUsar = await _ladoCoinsService.PuedeUsarLadoCoinsAsync(usuario.Id, monto);
            if (!puedeUsar)
            {
                return Json(new { success = false, error = "Saldo insuficiente" });
            }

            // Obtener multiplicador
            var multiplicador = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.MULTIPLICADOR_PUBLICIDAD);
            if (multiplicador <= 0) multiplicador = 1.5m;

            var creditoPublicitario = Math.Round(monto * multiplicador, 2);

            // Debitar LadoCoins
            var (exito, montoQuemado) = await _ladoCoinsService.DebitarAsync(
                usuario.Id,
                monto,
                TipoTransaccionLadoCoin.CompraPublicidad,
                $"Canje por ${creditoPublicitario:F2} en crédito publicitario"
            );

            if (!exito)
            {
                return Json(new { success = false, error = "Error al procesar el canje" });
            }

            // Acreditar al saldo publicitario
            // Primero verificar si tiene agencia activa
            var agencia = await _context.Agencias
                .FirstOrDefaultAsync(a => a.UsuarioId == usuario.Id && a.Estado == EstadoAgencia.Activa);

            if (agencia != null)
            {
                // Acreditar a la agencia
                agencia.SaldoPublicitario += creditoPublicitario;
                _context.Agencias.Update(agencia);
            }
            else
            {
                // Acreditar al usuario directamente
                usuario.SaldoPublicitario += creditoPublicitario;
                _context.Users.Update(usuario);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario {UsuarioId} canjeó ${Monto} LadoCoins por ${Credito} en publicidad (destino: {Destino})",
                usuario.Id, monto, creditoPublicitario, agencia != null ? $"Agencia {agencia.Id}" : "Usuario");

            return Json(new
            {
                success = true,
                mensaje = agencia != null
                    ? $"¡Canjeaste ${monto:F2} LadoCoins por ${creditoPublicitario:F2} en crédito publicitario para tu agencia!"
                    : $"¡Canjeaste ${monto:F2} LadoCoins por ${creditoPublicitario:F2} en crédito publicitario!",
                creditoPublicitario,
                montoQuemado,
                destinoAgencia = agencia != null
            });
        }

        /// <summary>
        /// Canjear LadoCoins por boost de algoritmo
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CanjearBoost(decimal monto)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Json(new { success = false, error = "No autenticado" });

            if (monto <= 0)
            {
                return Json(new { success = false, error = "El monto debe ser mayor a 0" });
            }

            // Verificar saldo
            var puedeUsar = await _ladoCoinsService.PuedeUsarLadoCoinsAsync(usuario.Id, monto);
            if (!puedeUsar)
            {
                return Json(new { success = false, error = "Saldo insuficiente" });
            }

            // Obtener multiplicador
            var multiplicador = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.MULTIPLICADOR_BOOST);
            if (multiplicador <= 0) multiplicador = 2m;

            var creditoBoost = Math.Round(monto * multiplicador, 2);

            // Debitar LadoCoins
            var (exito, montoQuemado) = await _ladoCoinsService.DebitarAsync(
                usuario.Id,
                monto,
                TipoTransaccionLadoCoin.BoostAlgoritmo,
                $"Canje por ${creditoBoost:F2} en boost de algoritmo"
            );

            if (!exito)
            {
                return Json(new { success = false, error = "Error al procesar el canje" });
            }

            // Activar boost de algoritmo para el usuario
            // El boost dura 7 días y aumenta la visibilidad del contenido
            var duracionBoostDias = 7;
            var multiplicadorVisibilidad = 1.5m; // 50% más visibilidad

            // Si ya tiene boost activo, sumar el crédito
            if (usuario.BoostActivo && usuario.BoostFechaFin > DateTime.Now)
            {
                usuario.BoostCredito += creditoBoost;
                // Extender la fecha de fin si el nuevo crédito lo justifica
                var diasExtra = (int)Math.Ceiling(creditoBoost / 10); // 1 día extra por cada $10
                usuario.BoostFechaFin = usuario.BoostFechaFin.Value.AddDays(diasExtra);
            }
            else
            {
                // Nuevo boost
                usuario.BoostActivo = true;
                usuario.BoostCredito = creditoBoost;
                usuario.BoostFechaFin = DateTime.Now.AddDays(duracionBoostDias);
                usuario.BoostMultiplicador = multiplicadorVisibilidad;
            }

            _context.Users.Update(usuario);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario {UsuarioId} activó boost de algoritmo: ${Credito}, válido hasta {FechaFin}",
                usuario.Id, creditoBoost, usuario.BoostFechaFin);

            return Json(new
            {
                success = true,
                mensaje = $"¡Boost activado! Tu contenido tendrá {(multiplicadorVisibilidad - 1) * 100}% más visibilidad hasta {usuario.BoostFechaFin:dd/MM/yyyy}",
                creditoBoost,
                montoQuemado,
                boostFechaFin = usuario.BoostFechaFin?.ToString("yyyy-MM-dd"),
                boostMultiplicador = usuario.BoostMultiplicador
            });
        }

        /// <summary>
        /// Obtener saldo actual (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerSaldo()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Json(new { success = false });

            var saldo = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuario.Id);
            var montoPorVencer = await _ladoCoinsService.ObtenerMontoPorVencerAsync(usuario.Id);

            return Json(new
            {
                success = true,
                saldo,
                montoPorVencer
            });
        }

        /// <summary>
        /// Generar nuevo código de referido
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerarCodigo()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Json(new { success = false, error = "No autenticado" });

            // Solo generar si no tiene código
            if (!string.IsNullOrEmpty(usuario.CodigoReferido))
            {
                return Json(new { success = true, codigo = usuario.CodigoReferido });
            }

            var codigo = await _referidosService.GenerarCodigoReferidoAsync(usuario.Id);

            return Json(new
            {
                success = !string.IsNullOrEmpty(codigo),
                codigo,
                link = $"{Request.Scheme}://{Request.Host}/Account/Register?ref={codigo}"
            });
        }

        /// <summary>
        /// Reclamar bonos pendientes (para usuarios que existían antes del sistema)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReclamarBonosPendientes()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Json(new { success = false, error = "No autenticado" });

            var bonosEntregados = new List<string>();
            decimal totalEntregado = 0;

            try
            {
                // 1. Bono de bienvenida (si no lo tiene y ya está registrado)
                if (!usuario.BonoBienvenidaEntregado)
                {
                    var entregado = await _ladoCoinsService.AcreditarBonoAsync(
                        usuario.Id,
                        TipoTransaccionLadoCoin.BonoBienvenida,
                        "Bono de bienvenida (reclamado)"
                    );
                    if (entregado)
                    {
                        usuario.BonoBienvenidaEntregado = true;
                        var monto = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_BIENVENIDA);
                        totalEntregado += monto > 0 ? monto : 20;
                        bonosEntregados.Add("Bienvenida");
                    }
                }

                // 2. Bono de email verificado (si email confirmado pero no tiene bono)
                if (usuario.EmailConfirmed && !usuario.BonoEmailVerificadoEntregado)
                {
                    var entregado = await _ladoCoinsService.AcreditarBonoAsync(
                        usuario.Id,
                        TipoTransaccionLadoCoin.BonoVerificarEmail,
                        "Bono verificar email (reclamado)"
                    );
                    if (entregado)
                    {
                        usuario.BonoEmailVerificadoEntregado = true;
                        var monto = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_VERIFICAR_EMAIL);
                        totalEntregado += monto > 0 ? monto : 2;
                        bonosEntregados.Add("Email Verificado");
                    }
                }

                // 3. Bono de perfil completo (si perfil completo pero no tiene bono)
                if (usuario.PerfilCompletoParaBono() && !usuario.BonoPerfilCompletoEntregado)
                {
                    var entregado = await _ladoCoinsService.AcreditarBonoAsync(
                        usuario.Id,
                        TipoTransaccionLadoCoin.BonoCompletarPerfil,
                        "Bono perfil completo (reclamado)"
                    );
                    if (entregado)
                    {
                        usuario.BonoPerfilCompletoEntregado = true;
                        var monto = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_COMPLETAR_PERFIL);
                        totalEntregado += monto > 0 ? monto : 3;
                        bonosEntregados.Add("Perfil Completo");
                    }
                }

                // 4. Bono de primer contenido (si tiene contenido pero no tiene bono)
                if (!usuario.BonoPrimerContenidoEntregado)
                {
                    var tieneContenido = await _context.Contenidos
                        .AnyAsync(c => c.UsuarioId == usuario.Id && !c.EsBorrador);

                    if (tieneContenido)
                    {
                        var entregado = await _ladoCoinsService.AcreditarBonoAsync(
                            usuario.Id,
                            TipoTransaccionLadoCoin.BonoPrimerContenido,
                            "Bono primer contenido (reclamado)"
                        );
                        if (entregado)
                        {
                            usuario.BonoPrimerContenidoEntregado = true;
                            var monto = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_PRIMER_CONTENIDO);
                            totalEntregado += monto > 0 ? monto : 5;
                            bonosEntregados.Add("Primer Contenido");
                        }
                    }
                }

                // Guardar cambios en usuario
                if (bonosEntregados.Any())
                {
                    await _userManager.UpdateAsync(usuario);
                }

                return Json(new
                {
                    success = true,
                    bonosEntregados = bonosEntregados,
                    totalEntregado = totalEntregado,
                    mensaje = bonosEntregados.Any()
                        ? $"¡Reclamaste ${totalEntregado:N2} en bonos: {string.Join(", ", bonosEntregados)}!"
                        : "No tienes bonos pendientes por reclamar"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reclamar bonos pendientes para {UserId}", usuario.Id);
                return Json(new { success = false, error = "Error al procesar bonos" });
            }
        }

        /// <summary>
        /// Diagnóstico del sistema LadoCoins (solo para depuración)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Diagnostico()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null) return Json(new { error = "No autenticado" });

            try
            {
                // Verificar racha
                var racha = await _rachasService.ObtenerOCrearRachaAsync(usuario.Id);
                var contadores = await _rachasService.ObtenerContadoresHoyAsync(usuario.Id);

                // Verificar saldo
                var saldo = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuario.Id);

                // Contar transacciones
                var totalTransacciones = await _context.TransaccionesLadoCoins
                    .CountAsync(t => t.UsuarioId == usuario.Id);

                // Leer directamente de la base de datos (sin cache)
                var rachaDB = await _context.RachasUsuarios
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.UsuarioId == usuario.Id);

                return Json(new
                {
                    success = true,
                    usuario = new
                    {
                        id = usuario.Id,
                        email = usuario.Email,
                        emailConfirmado = usuario.EmailConfirmed,
                        bonoBienvenida = usuario.BonoBienvenidaEntregado,
                        bonoPrimerContenido = usuario.BonoPrimerContenidoEntregado,
                        bonoEmailVerificado = usuario.BonoEmailVerificadoEntregado,
                        bonoPerfilCompleto = usuario.BonoPerfilCompletoEntregado
                    },
                    rachaServicio = new
                    {
                        id = racha.Id,
                        rachaActual = racha.RachaActual,
                        likesHoy = racha.LikesHoy,
                        comentariosHoy = racha.ComentariosHoy,
                        contenidosHoy = racha.ContenidosHoy,
                        premioLoginHoy = racha.PremioLoginHoy,
                        premio5LikesHoy = racha.Premio5LikesHoy,
                        premio3ComentariosHoy = racha.Premio3ComentariosHoy,
                        premioContenidoHoy = racha.PremioContenidoHoy,
                        fechaReset = racha.FechaReset.ToString("yyyy-MM-dd HH:mm:ss"),
                        necesitaReset = racha.NecesitaReset()
                    },
                    contadoresServicio = new
                    {
                        likes = contadores.likes,
                        comentarios = contadores.comentarios,
                        contenidos = contadores.contenidos
                    },
                    rachaBaseDatos = rachaDB != null ? new
                    {
                        id = rachaDB.Id,
                        likesHoy = rachaDB.LikesHoy,
                        comentariosHoy = rachaDB.ComentariosHoy,
                        contenidosHoy = rachaDB.ContenidosHoy,
                        fechaReset = rachaDB.FechaReset.ToString("yyyy-MM-dd HH:mm:ss"),
                        necesitaReset = rachaDB.NecesitaReset()
                    } : null,
                    saldoLadoCoins = saldo,
                    totalTransacciones = totalTransacciones,
                    horaPlataforma = new
                    {
                        zonaHoraria = _dateTimeService.GetTimeZoneId(),
                        fechaLocal = _dateTimeService.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss"),
                        fechaLocalDate = _dateTimeService.GetLocalNow().Date.ToString("yyyy-MM-dd"),
                        fechaServidor = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        fechaUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en diagnóstico LadoCoins");
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }

    #region ViewModels

    public class LadoCoinsDashboardViewModel
    {
        // Saldo
        public decimal SaldoDisponible { get; set; }
        public decimal SaldoPorVencer { get; set; }
        public decimal MontoPorVencer7Dias { get; set; }
        public decimal TotalGanado { get; set; }
        public decimal TotalGastado { get; set; }
        public decimal TotalQuemado { get; set; }

        // Racha
        public int RachaActual { get; set; }
        public int RachaMaxima { get; set; }

        // Referidos
        public string CodigoReferido { get; set; } = string.Empty;
        public int TotalReferidos { get; set; }
        public decimal TotalComisiones { get; set; }

        // Contadores del día
        public int LikesHoy { get; set; }
        public int ComentariosHoy { get; set; }
        public int ContenidosHoy { get; set; }

        // Premios del día
        public bool Premio5LikesHoy { get; set; }
        public bool Premio3ComentariosHoy { get; set; }
        public bool PremioContenidoHoy { get; set; }
        public bool PremioLoginHoy { get; set; }

        // Multiplicadores
        public decimal MultiplicadorPublicidad { get; set; }
        public decimal MultiplicadorBoost { get; set; }

        // Verificación de creador (para mostrar opciones de publicidad/boost)
        public bool EsCreadorVerificado { get; set; }

        // Bonos de perfil
        public bool BonoBienvenidaEntregado { get; set; }
        public bool BonoPrimerContenidoEntregado { get; set; }
        public bool BonoEmailVerificadoEntregado { get; set; }
        public bool BonoPerfilCompletoEntregado { get; set; }
        public bool EmailConfirmado { get; set; }
        public bool PerfilCompleto { get; set; }

        // Campos individuales para mostrar qué falta en el perfil
        public bool TieneFotoPerfil { get; set; }
        public bool TieneBiografia { get; set; }
        public bool TieneNombreCompleto { get; set; }
        public bool TienePais { get; set; }
        public bool TieneFechaNacimiento { get; set; }
        public bool TieneGenero { get; set; }
    }

    public class LadoCoinsHistorialViewModel
    {
        public List<TransaccionLadoCoin> Transacciones { get; set; } = new();
        public int PaginaActual { get; set; }
        public int TotalPaginas { get; set; }
    }

    public class LadoCoinsReferidosViewModel
    {
        public string CodigoReferido { get; set; } = string.Empty;
        public string LinkReferido { get; set; } = string.Empty;
        public List<Referido> MisReferidos { get; set; } = new();
        public Referido? MiReferidor { get; set; }
        public decimal TotalComisiones { get; set; }

        // Bonos configurados
        public decimal BonoReferidor { get; set; }
        public decimal BonoReferido { get; set; }
        public decimal BonoCreadorLadoB { get; set; }
        public decimal PorcentajeComision { get; set; }
        public int MesesComision { get; set; }
    }

    #endregion
}
