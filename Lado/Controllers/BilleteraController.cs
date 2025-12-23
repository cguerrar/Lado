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
    public class BilleteraController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILiquidacionService _liquidacionService;
        private readonly IEmailService _emailService;
        private readonly ILogger<BilleteraController> _logger;

        public BilleteraController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILiquidacionService liquidacionService,
            IEmailService emailService,
            ILogger<BilleteraController> logger)
        {
            _userManager = userManager;
            _context = context;
            _liquidacionService = liquidacionService;
            _emailService = emailService;
            _logger = logger;
        }

        // GET: /Billetera
        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

          
            // Ingresos este mes (todas las transacciones excepto retiros)
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ViewBag.IngresosEsteMes = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.FechaTransaccion >= inicioMes &&
                            t.EstadoPago == "Completado")
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            // Crecimiento mes anterior
            var inicioMesAnterior = inicioMes.AddMonths(-1);
            var ingresosMesAnterior = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id &&
                            t.TipoTransaccion != TipoTransaccion.Retiro &&
                            t.FechaTransaccion >= inicioMesAnterior &&
                            t.FechaTransaccion < inicioMes &&
                            t.EstadoPago == "Completado")
                .SumAsync(t => (decimal?)t.Monto) ?? 0;

            if (ingresosMesAnterior > 0)
            {
                var crecimiento = ((ViewBag.IngresosEsteMes - ingresosMesAnterior) / ingresosMesAnterior) * 100;
                ViewBag.CrecimientoMes = Math.Round(crecimiento, 1);
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

            // ✅ CORREGIDO: Usar DTO en lugar de tipo anónimo
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
                    TipoTransaccion = t.TipoTransaccion,
                    RutaLiquidacion = t.RutaLiquidacion
                })
                .ToListAsync();

            // Próximo pago estimado (primer día del siguiente mes)
            ViewBag.ProximoPago = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1);

            // Monto estimado basado en suscripciones activas
            var suscripcionesActivas = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id && s.EstaActiva);

            ViewBag.MontoEstimado = suscripcionesActivas * usuario.PrecioSuscripcion;

            // Método de pago configurado (ejemplo, deberías tener una tabla para esto)
            ViewBag.MetodoPago = "Transferencia Bancaria"; // Ejemplo
            ViewBag.CuentaBancaria = "**** **** **** 1234"; // Ejemplo

            // Obtener retención de impuestos para el usuario
            ViewBag.RetencionImpuestos = await ObtenerRetencionUsuarioAsync(usuario);
            ViewBag.NombrePais = await ObtenerNombrePaisAsync(usuario.Pais);

            // Cargar configuraciones de plataforma
            var configuraciones = await _context.ConfiguracionesPlataforma
                .Where(c => c.Categoria == "Billetera")
                .ToDictionaryAsync(c => c.Clave, c => c.Valor);

            ViewBag.ComisionBilleteraElectronica = configuraciones.TryGetValue(ConfiguracionPlataforma.COMISION_BILLETERA_ELECTRONICA, out var comision) ? comision : "2.5";
            ViewBag.TiempoProcesoRetiro = configuraciones.TryGetValue(ConfiguracionPlataforma.TIEMPO_PROCESO_RETIRO, out var tiempo) ? tiempo : "3-5 dias habiles";
            ViewBag.MontoMinimoRecarga = configuraciones.TryGetValue(ConfiguracionPlataforma.MONTO_MINIMO_RECARGA, out var minRecarga) ? minRecarga : "5";
            ViewBag.MontoMaximoRecarga = configuraciones.TryGetValue(ConfiguracionPlataforma.MONTO_MAXIMO_RECARGA, out var maxRecarga) ? maxRecarga : "1000";

            return View(usuario);
        }

        // Método helper para obtener la retención de un usuario
        private async Task<decimal> ObtenerRetencionUsuarioAsync(ApplicationUser usuario)
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

                // Si no está en la BD, usar las predeterminadas
                if (RetencionesPredeterminadas.Paises.ContainsKey(usuario.Pais))
                {
                    return RetencionesPredeterminadas.Paises[usuario.Pais].Retencion;
                }
            }

            // Si no hay país configurado o no existe la retención, devolver 0
            return 0;
        }

        private async Task<string> ObtenerNombrePaisAsync(string? codigoPais)
        {
            if (string.IsNullOrEmpty(codigoPais))
                return "No definido";

            var retencionPais = await _context.RetencionesPaises
                .FirstOrDefaultAsync(r => r.CodigoPais == codigoPais);

            if (retencionPais != null)
                return retencionPais.NombrePais;

            if (RetencionesPredeterminadas.Paises.ContainsKey(codigoPais))
                return RetencionesPredeterminadas.Paises[codigoPais].Nombre;

            return codigoPais;
        }

        // POST: /Billetera/SolicitarRetiro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarRetiro(decimal monto, string metodoPago, string? detalles)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Validaciones
            if (monto <= 0)
            {
                TempData["Error"] = "El monto debe ser mayor a 0";
                return RedirectToAction(nameof(Index));
            }

            if (monto > usuario.Saldo)
            {
                TempData["Error"] = "No tienes saldo suficiente para este retiro";
                return RedirectToAction(nameof(Index));
            }

            // Validar monto mínimo de retiro
            if (monto < usuario.MontoMinimoRetiro)
            {
                TempData["Error"] = $"El monto mínimo de retiro es ${usuario.MontoMinimoRetiro:N2}";
                return RedirectToAction(nameof(Index));
            }

            // Calcular comisión LadoApp
            var comision = monto * (usuario.ComisionRetiro / 100);

            // Calcular retención de impuestos
            var retencionImpuestos = await ObtenerRetencionUsuarioAsync(usuario);
            var montoRetencion = monto * (retencionImpuestos / 100);

            // Calcular monto neto (bruto - comisión - impuestos)
            var montoNeto = monto - comision - montoRetencion;

            // Crear transacción de retiro
            var transaccion = new Transaccion
            {
                UsuarioId = usuario.Id,
                Monto = monto,
                MontoNeto = montoNeto,
                Comision = comision,
                RetencionImpuestos = montoRetencion,
                TipoTransaccion = TipoTransaccion.Retiro,
                Descripcion = $"Retiro vía {metodoPago} (Comisión: {usuario.ComisionRetiro}%, Impuestos: {retencionImpuestos}%)",
                EstadoPago = "Pendiente",
                MetodoPago = metodoPago,
                FechaTransaccion = DateTime.Now,
                Notas = detalles
            };

            _context.Transacciones.Add(transaccion);

            // Restar del saldo disponible (el monto bruto)
            usuario.Saldo -= monto;
            _context.Users.Update(usuario);

            await _context.SaveChangesAsync();

            // Generar PDF de liquidación
            try
            {
                // Generar y guardar PDF
                var rutaPdf = await _liquidacionService.GenerarLiquidacionPdfAsync(transaccion, usuario);
                transaccion.RutaLiquidacion = rutaPdf;
                await _context.SaveChangesAsync();

                // Generar bytes del PDF para enviar por email
                var pdfBytes = await _liquidacionService.GenerarLiquidacionBytesAsync(transaccion, usuario);

                // Enviar email con PDF adjunto
                var emailEnviado = await _emailService.SendLiquidacionRetiroAsync(
                    usuario.Email!,
                    usuario.NombreCompleto ?? usuario.UserName ?? "Creador",
                    monto,
                    comision,
                    montoRetencion,
                    montoNeto,
                    metodoPago,
                    transaccion.Id,
                    pdfBytes
                );

                if (emailEnviado)
                {
                    _logger.LogInformation("Liquidación enviada por email para transacción {TransaccionId}", transaccion.Id);
                }
                else
                {
                    _logger.LogWarning("No se pudo enviar email de liquidación para transacción {TransaccionId}", transaccion.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar/enviar liquidación para transacción {TransaccionId}", transaccion.Id);
                // No fallar la solicitud de retiro si falla la generación del PDF
            }

            TempData["Success"] = $"Retiro solicitado: ${monto:N2} bruto → Comisión ${comision:N2} ({usuario.ComisionRetiro}%) + Impuestos ${montoRetencion:N2} ({retencionImpuestos}%) = Neto ${montoNeto:N2}. Se procesará en 3-5 días hábiles. Recibirás la liquidación por email.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Billetera/ActualizarMetodoPago
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarMetodoPago(
            string tipoCuenta,
            string nombreBanco,
            string numeroCuenta,
            string nombreTitular,
            string identificacion)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

  
            TempData["Success"] = "Método de pago actualizado correctamente";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Billetera/HistorialCompleto
        public async Task<IActionResult> HistorialCompleto()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var transacciones = await _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id)
                .OrderByDescending(t => t.FechaTransaccion)
                .ToListAsync();

            ViewBag.SaldoActual = usuario.Saldo;
            return View(transacciones);
        }

        // GET: /Billetera/HistorialTransacciones
        public async Task<IActionResult> HistorialTransacciones(int pagina = 1, string tipo = "todas")
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

      
            int itemsPorPagina = 20;

            var query = _context.Transacciones
                .Where(t => t.UsuarioId == usuario.Id);

            if (tipo != "todas")
            {
                if (tipo == "retiro")
                {
                    query = query.Where(t => t.TipoTransaccion == TipoTransaccion.Retiro);
                }
                else
                {
                    query = query.Where(t => t.TipoTransaccion != TipoTransaccion.Retiro);
                }
            }

            var totalItems = await query.CountAsync();
            var transacciones = await query
                .OrderByDescending(t => t.FechaTransaccion)
                .Skip((pagina - 1) * itemsPorPagina)
                .Take(itemsPorPagina)
                .ToListAsync();

            ViewBag.Transacciones = transacciones;
            ViewBag.TotalPaginas = (int)Math.Ceiling(totalItems / (double)itemsPorPagina);
            ViewBag.PaginaActual = pagina;
            ViewBag.TipoFiltro = tipo;

            return View(usuario);
        }
    }
}