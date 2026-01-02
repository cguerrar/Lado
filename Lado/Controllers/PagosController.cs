using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Lado.Controllers
{
    [Authorize]
    public class PagosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PagosController> _logger;
        private readonly IRateLimitService _rateLimitService;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly IReferidosService _referidosService;

        public PagosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<PagosController> logger,
            IRateLimitService rateLimitService,
            ILadoCoinsService ladoCoinsService,
            IReferidosService referidosService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _rateLimitService = rateLimitService;
            _ladoCoinsService = ladoCoinsService;
            _referidosService = referidosService;
        }

        // ============================================
        // SUSCRIPCIONES
        // ============================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Suscribirse(string creadorId, int? tipoLado = null, int? duracion = null, decimal? montoLadoCoins = null)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Rate limiting: máximo 10 suscripciones por 5 minutos por usuario (evita fraude)
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                if (!await _rateLimitService.IsAllowedAsync(
                    clientIp,
                    $"suscribir_user_{usuarioId}",
                    10,
                    TimeSpan.FromMinutes(5),
                    TipoAtaque.Otro,
                    "/Pagos/Suscribirse",
                    usuarioId))
                {
                    TempData["Error"] = "Demasiadas solicitudes. Espera unos minutos.";
                    return RedirectToAction("Index", "Feed");
                }

                var usuario = await _userManager.FindByIdAsync(usuarioId);
                var creador = await _userManager.FindByIdAsync(creadorId);

                if (creador == null)
                {
                    TempData["Error"] = "Creador no encontrado";
                    return RedirectToAction("Index", "Feed");
                }

                // Determinar tipo de suscripción
                var tipo = tipoLado.HasValue ? (TipoLado)tipoLado.Value : TipoLado.LadoA;

                // Determinar duración (default: mensual)
                var tipoDuracion = duracion.HasValue
                    ? (DuracionSuscripcion)duracion.Value
                    : DuracionSuscripcion.Mes;

                // IMPORTANTE: Solo LadoB cobra, LadoA es GRATIS (seguir)
                var precioMensual = tipo == TipoLado.LadoB
                    ? (creador.PrecioSuscripcionLadoB ?? creador.PrecioSuscripcion)
                    : 0m; // LadoA es gratis

                // Calcular precio según duración
                var precio = tipo == TipoLado.LadoB
                    ? Suscripcion.CalcularPrecio(precioMensual, tipoDuracion)
                    : 0m;

                // Verificar si ya está suscrito a este TipoLado específico
                var suscripcionExistente = await _context.Suscripciones
                    .FirstOrDefaultAsync(s => s.FanId == usuarioId && s.CreadorId == creadorId && s.TipoLado == tipo && s.EstaActiva);

                if (suscripcionExistente != null)
                {
                    TempData["Info"] = "Ya estás suscrito a este creador";
                    return RedirectToAction("Perfil", "Feed", new { id = creadorId });
                }

                // ⭐ LADOCOINS: Calcular pago mixto
                decimal usarLadoCoins = 0;
                decimal montoReal = precio;
                decimal montoQuemado = 0;

                if (precio > 0 && montoLadoCoins.HasValue && montoLadoCoins.Value > 0)
                {
                    // Usar el porcentaje máximo configurado por el creador (default 30%)
                    var maxPorcentaje = creador.PorcentajeMaxLadoCoinsSuscripcion / 100m;
                    var maxLadoCoins = precio * maxPorcentaje;
                    usarLadoCoins = Math.Min(montoLadoCoins.Value, maxLadoCoins);

                    // Verificar si tiene suficientes LadoCoins
                    var saldoLadoCoins = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuarioId);
                    if (saldoLadoCoins < usarLadoCoins)
                    {
                        usarLadoCoins = saldoLadoCoins;
                    }

                    // Recalcular monto real necesario
                    montoReal = precio - usarLadoCoins;
                }

                // Verificar saldo real necesario
                if (montoReal > 0 && usuario.Saldo < montoReal)
                {
                    TempData["Error"] = $"Saldo insuficiente. Necesitas ${(montoReal - usuario.Saldo):N2} más";
                    return RedirectToAction("Perfil", "Feed", new { id = creadorId });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Variables para transacciones
                    decimal comisionReal = 0;
                    decimal comisionLC = 0;
                    decimal gananciaCreadorReal = 0;
                    decimal gananciaCreadorLC = 0;

                    // Solo procesar cobro si es LadoB (precio > 0)
                    if (precio > 0)
                    {
                        // ⭐ 1. Descontar LadoCoins si aplica
                        if (usarLadoCoins > 0)
                        {
                            var (exitoLC, quemado) = await _ladoCoinsService.DebitarAsync(
                                usuarioId,
                                usarLadoCoins,
                                TipoTransaccionLadoCoin.PagoSuscripcion,
                                $"Pago parcial suscripción a {creador.Seudonimo ?? creador.NombreCompleto}"
                            );

                            if (!exitoLC)
                            {
                                await transaction.RollbackAsync();
                                TempData["Error"] = "Error al procesar LadoCoins";
                                return RedirectToAction("Perfil", "Feed", new { id = creadorId });
                            }

                            montoQuemado = quemado;
                            comisionLC = usarLadoCoins * 0.20m;
                            gananciaCreadorLC = usarLadoCoins - comisionLC;

                            // Creador recibe LadoCoins (NO dinero real)
                            await _ladoCoinsService.AcreditarMontoAsync(
                                creadorId,
                                gananciaCreadorLC,
                                TipoTransaccionLadoCoin.RecibirPago,
                                $"Pago suscripción de @{usuario.UserName} (LadoCoins)"
                            );

                            // ⭐ Procesar comisión de referido si aplica
                            await _referidosService.ProcesarComisionAsync(usuarioId, usarLadoCoins);
                        }

                        // 2. Descontar saldo real del fan
                        if (montoReal > 0)
                        {
                            usuario.Saldo -= montoReal;
                            comisionReal = montoReal * 0.20m;
                            gananciaCreadorReal = montoReal - comisionReal;
                            creador.Saldo += gananciaCreadorReal;
                            creador.TotalGanancias += gananciaCreadorReal;
                        }
                    }

                    // Incrementar seguidores siempre
                    creador.NumeroSeguidores++;

                    // 3. Crear suscripción con duración
                    var fechaInicio = DateTime.Now;
                    var fechaFin = Suscripcion.CalcularFechaFin(fechaInicio, tipoDuracion);

                    var suscripcion = new Suscripcion
                    {
                        FanId = usuarioId,
                        CreadorId = creadorId,
                        FechaInicio = fechaInicio,
                        FechaFin = fechaFin,
                        ProximaRenovacion = fechaFin,
                        PrecioMensual = precioMensual,
                        Precio = precio,
                        EstaActiva = true,
                        TipoLado = tipo,
                        Duracion = tipoDuracion,
                        RenovacionAutomatica = tipoDuracion == DuracionSuscripcion.Mes
                    };

                    _context.Suscripciones.Add(suscripcion);

                    // 4. Registrar transacciones solo si hubo cobro real
                    if (montoReal > 0)
                    {
                        var textoDuracion = Suscripcion.ObtenerTextoDuracion(tipoDuracion);
                        var descripcionPago = usarLadoCoins > 0
                            ? $"Suscripción Premium ({textoDuracion}) a {creador.Seudonimo ?? creador.NombreCompleto} (${montoReal:N2} + ${usarLadoCoins:N2} LC)"
                            : $"Suscripción Premium ({textoDuracion}) a {creador.Seudonimo ?? creador.NombreCompleto}";

                        var transaccionFan = new Transaccion
                        {
                            UsuarioId = usuarioId,
                            TipoTransaccion = TipoTransaccion.Suscripcion,
                            Monto = -montoReal,
                            FechaTransaccion = DateTime.Now,
                            Descripcion = descripcionPago
                        };

                        var transaccionCreador = new Transaccion
                        {
                            UsuarioId = creadorId,
                            TipoTransaccion = TipoTransaccion.IngresoSuscripcion,
                            Monto = gananciaCreadorReal,
                            Comision = comisionReal,
                            MontoNeto = gananciaCreadorReal,
                            FechaTransaccion = DateTime.Now,
                            Descripcion = $"Nueva suscripción premium ({textoDuracion}) de @{usuario.UserName}"
                        };

                        _context.Transacciones.AddRange(transaccionFan, transaccionCreador);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var textoDuracionMsg = Suscripcion.ObtenerTextoDuracion(tipoDuracion);
                    var mensaje = tipo == TipoLado.LadoB
                        ? $"¡Te has suscrito por {textoDuracionMsg} al contenido premium de {creador.Seudonimo ?? creador.NombreCompleto}!"
                        : $"¡Ahora sigues a {creador.NombreCompleto}!";

                    if (usarLadoCoins > 0)
                    {
                        mensaje += $" (Usaste ${usarLadoCoins:N2} en LadoCoins)";
                    }

                    _logger.LogInformation("Suscripción creada: Fan {FanId} -> Creador {CreadorId}, Tipo: {Tipo}, Duración: {Duracion}, Precio: {Precio}, LadoCoins: {LC}, Real: {Real}",
                        usuarioId, creadorId, tipo, tipoDuracion, precio, usarLadoCoins, montoReal);

                    TempData["Success"] = mensaje;
                    return RedirectToAction("Perfil", "Feed", new { id = creadorId });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error en transacción de suscripción");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar suscripción");
                TempData["Error"] = "Error al procesar la suscripción";
                return RedirectToAction("Perfil", "Feed", new { id = creadorId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarDesdeCreador(string creadorId, int? tipoLado = null)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Determinar TipoLado (default LadoA)
                var tipo = tipoLado.HasValue ? (TipoLado)tipoLado.Value : TipoLado.LadoA;

                var suscripcion = await _context.Suscripciones
                    .Include(s => s.Creador)
                    .FirstOrDefaultAsync(s => s.FanId == usuarioId && s.CreadorId == creadorId && s.TipoLado == tipo && s.EstaActiva);

                if (suscripcion == null)
                {
                    TempData["Error"] = "No se encontró la suscripción";
                    return RedirectToAction("Perfil", "Feed", new { id = creadorId });
                }

                suscripcion.EstaActiva = false;
                suscripcion.FechaCancelacion = DateTime.Now;

                // Disminuir contador de seguidores
                var creador = await _userManager.FindByIdAsync(creadorId);
                if (creador != null && creador.NumeroSeguidores > 0)
                {
                    creador.NumeroSeguidores--;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Suscripción cancelada: Fan {usuarioId} -> Creador {creadorId}");

                TempData["Success"] = "Suscripción cancelada exitosamente";
                return RedirectToAction("Perfil", "Feed", new { id = creadorId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar suscripción");
                TempData["Error"] = "Error al cancelar la suscripción";
                return RedirectToAction("Perfil", "Feed", new { id = creadorId });
            }
        }

        // ============================================
        // DESBLOQUEO DE CONTENIDO
        // ============================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesbloquearContenido([FromBody] DesbloquearContenidoRequest request)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var usuario = await _userManager.FindByIdAsync(usuarioId);

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                // Obtener el contenido
                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .FirstOrDefaultAsync(c => c.Id == request.ContenidoId && c.EstaActivo);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                // SEGURIDAD: No puedes desbloquear tu propio contenido
                if (contenido.UsuarioId == usuarioId)
                {
                    return Json(new { success = false, message = "No puedes comprar tu propio contenido" });
                }

                // Verificar que el contenido es premium
                if (!contenido.EsPremium)
                {
                    return Json(new { success = false, message = "Este contenido no es premium" });
                }

                // Verificar saldo primero (optimización para evitar transacción innecesaria)
                if (usuario.Saldo < contenido.PrecioDesbloqueo)
                {
                    var faltante = contenido.PrecioDesbloqueo - usuario.Saldo;
                    return Json(new
                    {
                        success = false,
                        requiereRecarga = true,
                        message = $"Saldo insuficiente. Necesitas ${faltante.Value:N2} más."
                    });
                }

                // Procesar el desbloqueo con transacción serializable para evitar race condition
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                try
                {
                    // Verificar DENTRO de la transacción si ya tiene acceso (evita race condition)
                    var yaDesbloqueado = await _context.ComprasContenido
                        .AnyAsync(cc => cc.UsuarioId == usuarioId && cc.ContenidoId == request.ContenidoId);

                    if (yaDesbloqueado)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = true, message = "Ya tienes acceso a este contenido" });
                    }

                    // CRÍTICO: Forzar lectura fresca de BD (evita cache de EF Core)
                    // Detach la entidad cacheada para forzar re-lectura
                    _context.Entry(usuario).State = EntityState.Detached;

                    // Re-obtener usuario con lectura fresca de la BD dentro de la transacción
                    var usuarioActualizado = await _context.Users
                        .FirstOrDefaultAsync(u => u.Id == usuarioId);

                    if (usuarioActualizado == null || usuarioActualizado.Saldo < contenido.PrecioDesbloqueo)
                    {
                        await transaction.RollbackAsync();
                        var faltante = contenido.PrecioDesbloqueo - (usuarioActualizado?.Saldo ?? 0);
                        return Json(new {
                            success = false,
                            requiereRecarga = true,
                            message = $"Saldo insuficiente. Necesitas ${faltante:N2} más."
                        });
                    }

                    // 1. Descontar saldo del fan
                    usuarioActualizado.Saldo -= contenido.PrecioDesbloqueo.Value;
                    usuario = usuarioActualizado; // Actualizar referencia para retorno

                    // 2. Agregar ganancia al creador
                    // CRÍTICO: Re-obtener creador con lectura fresca (evita race condition con compras simultáneas)
                    _context.Entry(contenido.Usuario).State = EntityState.Detached;
                    var creador = await _context.Users.FirstOrDefaultAsync(u => u.Id == contenido.UsuarioId);

                    if (creador == null)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = "Creador no encontrado" });
                    }

                    var comision = contenido.PrecioDesbloqueo.Value * 0.20m; // 20% comisión
                    var gananciaCreador = contenido.PrecioDesbloqueo.Value - comision;

                    creador.Saldo += gananciaCreador;
                    creador.TotalGanancias += gananciaCreador;

                    // 3. Registrar la compra
                    var compra = new CompraContenido
                    {
                        ContenidoId = request.ContenidoId,
                        UsuarioId = usuarioId,
                        Monto = contenido.PrecioDesbloqueo.Value,
                        FechaCompra = DateTime.Now
                    };

                    _context.ComprasContenido.Add(compra);

                    // 4. Registrar transacciones
                    var transaccionFan = new Transaccion
                    {
                        UsuarioId = usuarioId,
                        TipoTransaccion = TipoTransaccion.CompraContenido,
                        Monto = -contenido.PrecioDesbloqueo.Value,
                        FechaTransaccion = DateTime.Now,
                        Descripcion = $"Desbloqueo de contenido de {creador.NombreCompleto}"
                    };

                    var transaccionCreador = new Transaccion
                    {
                        UsuarioId = creador.Id,
                        TipoTransaccion = TipoTransaccion.VentaContenido,
                        Monto = gananciaCreador,
                        Comision = comision,
                        MontoNeto = gananciaCreador,
                        FechaTransaccion = DateTime.Now,
                        Descripcion = $"Venta de contenido a @{usuario.UserName}"
                    };

                    _context.Transacciones.AddRange(transaccionFan, transaccionCreador);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Contenido {request.ContenidoId} desbloqueado por usuario {usuarioId}");

                    return Json(new
                    {
                        success = true,
                        message = "¡Contenido desbloqueado exitosamente!",
                        nuevoSaldo = usuario.Saldo
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error en transacción de desbloqueo");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desbloquear contenido");
                return Json(new { success = false, message = "Error al procesar la solicitud" });
            }
        }

        // ============================================
        // TIPS
        // ============================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarTip([FromBody] EnviarTipRequest request)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var usuario = await _userManager.FindByIdAsync(usuarioId);

                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado. Inicia sesión de nuevo." });
                }

                if (string.IsNullOrEmpty(request?.CreadorId))
                {
                    return Json(new { success = false, message = "ID del creador no especificado." });
                }

                var creador = await _userManager.FindByIdAsync(request.CreadorId);

                if (creador == null)
                {
                    return Json(new { success = false, message = "El creador no existe." });
                }

                if (creador.Id == usuarioId)
                {
                    return Json(new { success = false, message = "No puedes enviarte una propina a ti mismo." });
                }

                // Validar monto mínimo y máximo
                if (request.Monto < 1)
                {
                    return Json(new { success = false, message = "El monto mínimo es $1.00" });
                }

                const decimal MONTO_MAXIMO_TIP = 10000m;
                if (request.Monto > MONTO_MAXIMO_TIP)
                {
                    return Json(new { success = false, message = $"El monto máximo es ${MONTO_MAXIMO_TIP:N0}" });
                }

                // ⭐ LADOCOINS: Calcular pago mixto (100% LC permitido para propinas)
                decimal usarLadoCoins = request.MontoLadoCoins ?? 0;
                decimal montoReal = request.Monto - usarLadoCoins;

                // Validar LadoCoins
                if (usarLadoCoins > 0)
                {
                    var saldoLC = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuarioId);
                    if (saldoLC < usarLadoCoins)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"No tienes suficientes LadoCoins. Disponible: ${saldoLC:N2}"
                        });
                    }

                    // Ajustar si el usuario quiere usar más LC del monto total
                    if (usarLadoCoins > request.Monto)
                    {
                        usarLadoCoins = request.Monto;
                        montoReal = 0;
                    }
                }

                // Verificar saldo real
                if (montoReal > 0 && usuario.Saldo < montoReal)
                {
                    var faltante = montoReal - usuario.Saldo;
                    return Json(new
                    {
                        success = false,
                        requiereRecarga = true,
                        message = $"Saldo insuficiente. Necesitas ${faltante:N2} más."
                    });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    decimal comisionReal = 0;
                    decimal comisionLC = 0;
                    decimal gananciaCreadorReal = 0;
                    decimal gananciaCreadorLC = 0;
                    decimal montoQuemado = 0;

                    // ⭐ 1. Procesar LadoCoins si aplica
                    if (usarLadoCoins > 0)
                    {
                        var (exitoLC, quemado) = await _ladoCoinsService.DebitarAsync(
                            usuarioId,
                            usarLadoCoins,
                            TipoTransaccionLadoCoin.PagoPropina,
                            $"Propina a {creador.Seudonimo ?? creador.NombreCompleto}"
                        );

                        if (!exitoLC)
                        {
                            await transaction.RollbackAsync();
                            return Json(new { success = false, message = "Error al procesar LadoCoins" });
                        }

                        montoQuemado = quemado;
                        comisionLC = usarLadoCoins * 0.10m; // 10% comisión para tips
                        gananciaCreadorLC = usarLadoCoins - comisionLC;

                        // Creador recibe LadoCoins (NO dinero real)
                        await _ladoCoinsService.AcreditarMontoAsync(
                            request.CreadorId,
                            gananciaCreadorLC,
                            TipoTransaccionLadoCoin.RecibirPago,
                            $"Propina de @{usuario.UserName} (LadoCoins)"
                        );

                        // Procesar comisión de referido
                        await _referidosService.ProcesarComisionAsync(usuarioId, usarLadoCoins);
                    }

                    // 2. Descontar saldo real del fan
                    if (montoReal > 0)
                    {
                        usuario.Saldo -= montoReal;
                        comisionReal = montoReal * 0.10m;
                        gananciaCreadorReal = montoReal - comisionReal;
                        creador.Saldo += gananciaCreadorReal;
                        creador.TotalGanancias += gananciaCreadorReal;
                    }

                    // 3. Registrar tip
                    var tip = new Tip
                    {
                        FanId = usuarioId,
                        CreadorId = request.CreadorId,
                        Monto = request.Monto,
                        Mensaje = request.Mensaje,
                        FechaEnvio = DateTime.Now
                    };

                    _context.Tips.Add(tip);

                    // 4. Registrar transacciones solo si hubo dinero real
                    if (montoReal > 0)
                    {
                        var descripcion = usarLadoCoins > 0
                            ? $"Tip enviado a {creador.NombreCompleto} (${montoReal:N2} + ${usarLadoCoins:N2} LC)"
                            : $"Tip enviado a {creador.NombreCompleto}";

                        var transaccionFan = new Transaccion
                        {
                            UsuarioId = usuarioId,
                            TipoTransaccion = TipoTransaccion.Tip,
                            Monto = -montoReal,
                            FechaTransaccion = DateTime.Now,
                            Descripcion = descripcion
                        };

                        var transaccionCreador = new Transaccion
                        {
                            UsuarioId = request.CreadorId,
                            TipoTransaccion = TipoTransaccion.IngresoPropina,
                            Monto = gananciaCreadorReal,
                            Comision = comisionReal,
                            MontoNeto = gananciaCreadorReal,
                            FechaTransaccion = DateTime.Now,
                            Descripcion = $"Tip recibido de @{usuario.UserName}"
                        };

                        _context.Transacciones.AddRange(transaccionFan, transaccionCreador);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var mensajeExito = usarLadoCoins > 0 && montoReal > 0
                        ? $"¡Tip de ${request.Monto:N2} enviado! (${montoReal:N2} + ${usarLadoCoins:N2} LC)"
                        : usarLadoCoins > 0
                            ? $"¡Tip de ${usarLadoCoins:N2} LC enviado exitosamente!"
                            : $"¡Tip de ${request.Monto:N2} enviado exitosamente!";

                    _logger.LogInformation("Tip enviado: ${Monto} de {FanId} a {CreadorId} (Real: {Real}, LC: {LC})",
                        request.Monto, usuarioId, request.CreadorId, montoReal, usarLadoCoins);

                    return Json(new
                    {
                        success = true,
                        message = mensajeExito,
                        nuevoSaldo = usuario.Saldo,
                        nuevoSaldoLC = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuarioId)
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error en transacción de tip");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar tip");
                return Json(new { success = false, message = "Error al procesar el tip" });
            }
        }

        /// <summary>
        /// Obtener info de LadoCoins del usuario actual (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerInfoLadoCoins(string? creadorId = null)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
            {
                return Json(new { success = false });
            }

            var saldo = await _ladoCoinsService.ObtenerSaldoDisponibleAsync(usuarioId);

            // Si se especifica creadorId, obtener su configuración de LadoCoins
            int porcentajeMaximo = 30; // Default
            bool aceptaLadoCoins = true;

            if (!string.IsNullOrEmpty(creadorId))
            {
                var creador = await _userManager.FindByIdAsync(creadorId);
                if (creador != null)
                {
                    aceptaLadoCoins = creador.AceptaLadoCoins;
                    porcentajeMaximo = creador.PorcentajeMaxLadoCoinsSuscripcion;
                }
            }

            return Json(new
            {
                success = true,
                saldoLadoCoins = saldo,
                aceptaLadoCoins,
                porcentajeMaximo
            });
        }
    }

    // ============================================
    // CLASES DE REQUEST
    // ============================================

    public class DesbloquearContenidoRequest
    {
        public int ContenidoId { get; set; }
        public decimal Precio { get; set; }
    }

    public class EnviarTipRequest
    {
        public string CreadorId { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string? Mensaje { get; set; }
        public decimal? MontoLadoCoins { get; set; }
    }
}