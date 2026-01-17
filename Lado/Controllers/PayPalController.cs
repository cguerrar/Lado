using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.Services;
using System.Text.Json;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para manejar pagos con PayPal.
    /// Incluye creación de órdenes, captura de pagos y webhooks.
    /// </summary>
    public class PayPalController : Controller
    {
        private readonly IPayPalService _payPalService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayPalController> _logger;
        private readonly ILogEventoService _logEventoService;
        private readonly IRateLimitService _rateLimitService;

        public PayPalController(
            IPayPalService payPalService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger<PayPalController> logger,
            ILogEventoService logEventoService,
            IRateLimitService rateLimitService)
        {
            _payPalService = payPalService;
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
            _logEventoService = logEventoService;
            _rateLimitService = rateLimitService;
        }

        /// <summary>
        /// Vista de recarga de saldo con PayPal
        /// </summary>
        [Authorize]
        public async Task<IActionResult> Recargar()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.SaldoActual = user.Saldo;
            ViewBag.PayPalClientId = _configuration["PayPal:ClientId"];
            ViewBag.IsSandbox = _configuration.GetValue<bool>("PayPal:Sandbox", true);

            return View();
        }

        /// <summary>
        /// Crea una orden de pago en PayPal (llamado por AJAX)
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearOrden([FromBody] CrearOrdenRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Json(new { success = false, error = "Usuario no autenticado" });

                // Rate limiting: máximo 5 órdenes por hora por usuario
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = Request.Headers.UserAgent.ToString();
                if (!await _rateLimitService.IsAllowedAsync(
                    clientIp,
                    $"paypal_crear_orden_{user.Id}",
                    5,
                    TimeSpan.FromHours(1),
                    TipoAtaque.Fraude,
                    "/PayPal/CrearOrden",
                    user.Id,
                    userAgent))
                {
                    _logger.LogWarning("Rate limit excedido para crear orden PayPal: Usuario {UserId}, IP {Ip}", user.Id, clientIp);
                    return Json(new { success = false, error = "Demasiadas solicitudes. Por favor espera unos minutos." });
                }

                // Validar monto
                if (request.Monto < 5)
                    return Json(new { success = false, error = "El monto mínimo es $5 USD" });

                if (request.Monto > 1000)
                    return Json(new { success = false, error = "El monto máximo es $1,000 USD" });

                var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:7001";
                var returnUrl = $"{baseUrl}/PayPal/Completado";
                var cancelUrl = $"{baseUrl}/PayPal/Cancelado";

                var result = await _payPalService.CrearOrdenAsync(
                    request.Monto,
                    $"Recarga de saldo LADO - {user.UserName}",
                    returnUrl,
                    cancelUrl
                );

                if (!result.Success)
                {
                    await _logEventoService.RegistrarEventoAsync(
                        $"Error al crear orden PayPal: {result.Error}",
                        CategoriaEvento.Pago,
                        TipoLogEvento.Error,
                        user.Id,
                        user.UserName,
                        $"Monto: ${request.Monto}"
                    );

                    return Json(new { success = false, error = result.Error });
                }

                // Guardar la orden pendiente en la base de datos
                var ordenPendiente = new OrdenPayPalPendiente
                {
                    OrderId = result.OrderId!,
                    UsuarioId = user.Id,
                    Monto = request.Monto,
                    Estado = "CREATED",
                    FechaCreacion = DateTime.UtcNow
                };

                _context.OrdenesPayPalPendientes.Add(ordenPendiente);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Orden PayPal creada: {OrderId} para usuario {UserId}, Monto: ${Monto}",
                    result.OrderId, user.Id, request.Monto);

                return Json(new
                {
                    success = true,
                    orderId = result.OrderId,
                    approvalUrl = result.ApprovalUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear orden PayPal");
                return Json(new { success = false, error = "Error interno al procesar el pago" });
            }
        }

        /// <summary>
        /// Captura (confirma) una orden de PayPal después de la aprobación del usuario
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapturarOrden([FromBody] CapturarOrdenRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Json(new { success = false, error = "Usuario no autenticado" });

                // Rate limiting: máximo 10 capturas por hora por usuario
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = Request.Headers.UserAgent.ToString();
                if (!await _rateLimitService.IsAllowedAsync(
                    clientIp,
                    $"paypal_capturar_orden_{user.Id}",
                    10,
                    TimeSpan.FromHours(1),
                    TipoAtaque.Fraude,
                    "/PayPal/CapturarOrden",
                    user.Id,
                    userAgent))
                {
                    _logger.LogWarning("Rate limit excedido para capturar orden PayPal: Usuario {UserId}, IP {Ip}", user.Id, clientIp);
                    return Json(new { success = false, error = "Demasiadas solicitudes. Por favor espera unos minutos." });
                }

                // Verificar que la orden existe y pertenece al usuario
                var ordenPendiente = await _context.OrdenesPayPalPendientes
                    .FirstOrDefaultAsync(o => o.OrderId == request.OrderId && o.UsuarioId == user.Id);

                if (ordenPendiente == null)
                    return Json(new { success = false, error = "Orden no encontrada" });

                if (ordenPendiente.Estado == "COMPLETED")
                    return Json(new { success = false, error = "Esta orden ya fue procesada" });

                // Capturar el pago en PayPal
                var result = await _payPalService.CapturarOrdenAsync(request.OrderId);

                if (!result.Success)
                {
                    await _logEventoService.RegistrarEventoAsync(
                        $"Error al capturar orden PayPal: {result.Error}",
                        CategoriaEvento.Pago,
                        TipoLogEvento.Error,
                        user.Id,
                        user.UserName,
                        $"OrderId: {request.OrderId}"
                    );

                    return Json(new { success = false, error = result.Error });
                }

                // Actualizar el saldo del usuario
                user.Saldo += ordenPendiente.Monto;
                await _userManager.UpdateAsync(user);

                // Crear transacción de recarga
                var transaccion = new Transaccion
                {
                    UsuarioId = user.Id,
                    TipoTransaccion = TipoTransaccion.Recarga,
                    Monto = ordenPendiente.Monto,
                    MontoNeto = ordenPendiente.Monto,
                    Comision = 0,
                    EstadoPago = "Completado",
                    MetodoPago = "PayPal",
                    FechaTransaccion = DateTime.UtcNow,
                    Descripcion = $"Recarga de saldo via PayPal - Order: {request.OrderId}",
                    Notas = $"CaptureId: {result.CaptureId}"
                };

                _context.Transacciones.Add(transaccion);

                // Actualizar estado de la orden
                ordenPendiente.Estado = "COMPLETED";
                ordenPendiente.FechaCompletado = DateTime.UtcNow;
                ordenPendiente.CaptureId = result.CaptureId;
                ordenPendiente.PayerEmail = result.PayerEmail;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Recarga PayPal exitosa: ${ordenPendiente.Monto}",
                    CategoriaEvento.Pago,
                    TipoLogEvento.Info,
                    user.Id,
                    user.UserName,
                    $"OrderId: {request.OrderId}, CaptureId: {result.CaptureId}"
                );

                _logger.LogInformation("Pago PayPal capturado: {OrderId}, Usuario: {UserId}, Monto: ${Monto}",
                    request.OrderId, user.Id, ordenPendiente.Monto);

                return Json(new
                {
                    success = true,
                    nuevoSaldo = user.Saldo,
                    mensaje = $"Se han agregado ${ordenPendiente.Monto:F2} a tu saldo"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al capturar orden PayPal {OrderId}", request.OrderId);
                return Json(new { success = false, error = "Error interno al confirmar el pago" });
            }
        }

        /// <summary>
        /// Página de éxito después de completar el pago
        /// </summary>
        [Authorize]
        public async Task<IActionResult> Completado(string token)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.OrderId = token;
            ViewBag.SaldoActual = user.Saldo;

            return View();
        }

        /// <summary>
        /// Página cuando el usuario cancela el pago
        /// </summary>
        [Authorize]
        public IActionResult Cancelado()
        {
            return View();
        }

        /// <summary>
        /// Webhook de PayPal para recibir notificaciones de eventos
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            // Obtener IP del cliente
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Rate limiting por IP (máximo 100/min)
            var ipAllowed = await _rateLimitService.IsAllowedAsync(
                clientIp,
                $"paypal_webhook_ip:{clientIp}",
                RateLimits.PayPalWebhook_MaxRequests,
                RateLimits.PayPalWebhook_Window,
                TipoAtaque.WebhookAbuse,
                "/PayPal/Webhook",
                null,
                Request.Headers.UserAgent.ToString()
            );

            if (!ipAllowed)
            {
                _logger.LogWarning("Rate limit excedido para webhook PayPal desde IP: {Ip}", clientIp);
                return StatusCode(429, "Too Many Requests");
            }

            // Rate limiting global (máximo 500/hora)
            var globalAllowed = _rateLimitService.IsAllowed(
                "paypal_webhook_global",
                RateLimits.PayPalWebhook_Global_MaxRequests,
                RateLimits.PayPalWebhook_Global_Window
            );

            if (!globalAllowed)
            {
                _logger.LogError("Rate limit GLOBAL excedido para webhooks PayPal");
                return StatusCode(429, "Too Many Requests");
            }

            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                // Verificar el webhook (en producción, implementar verificación completa)
                var webhookId = _configuration["PayPal:WebhookId"];
                var transmissionId = Request.Headers["PAYPAL-TRANSMISSION-ID"].FirstOrDefault();
                var transmissionTime = Request.Headers["PAYPAL-TRANSMISSION-TIME"].FirstOrDefault();
                var certUrl = Request.Headers["PAYPAL-CERT-URL"].FirstOrDefault();
                var authAlgo = Request.Headers["PAYPAL-AUTH-ALGO"].FirstOrDefault();
                var transmissionSig = Request.Headers["PAYPAL-TRANSMISSION-SIG"].FirstOrDefault();

                var isValid = await _payPalService.VerificarWebhookAsync(
                    webhookId ?? "",
                    transmissionId ?? "",
                    transmissionTime ?? "",
                    certUrl ?? "",
                    authAlgo ?? "",
                    transmissionSig ?? "",
                    body
                );

                if (!isValid)
                {
                    _logger.LogWarning("Webhook PayPal inválido recibido");
                    return BadRequest();
                }

                // Parsear el evento
                var webhookEvent = JsonDocument.Parse(body);
                var eventType = webhookEvent.RootElement.GetProperty("event_type").GetString();

                _logger.LogInformation("Webhook PayPal recibido: {EventType}", eventType);

                // Procesar según el tipo de evento
                switch (eventType)
                {
                    case "CHECKOUT.ORDER.APPROVED":
                        // El usuario aprobó el pago (no es necesario hacer nada, el frontend captura)
                        break;

                    case "PAYMENT.CAPTURE.COMPLETED":
                        // Pago capturado exitosamente (confirmación adicional)
                        await ProcesarPagoCaptured(webhookEvent.RootElement);
                        break;

                    case "PAYMENT.CAPTURE.DENIED":
                        // Pago denegado
                        await ProcesarPagoDenegado(webhookEvent.RootElement);
                        break;

                    case "PAYMENT.CAPTURE.REFUNDED":
                        // Reembolso procesado
                        await ProcesarReembolso(webhookEvent.RootElement);
                        break;

                    default:
                        _logger.LogInformation("Evento PayPal no manejado: {EventType}", eventType);
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando webhook de PayPal");
                return StatusCode(500);
            }
        }

        #region Métodos privados para procesar webhooks

        private async Task ProcesarPagoCaptured(JsonElement resource)
        {
            try
            {
                var captureId = resource.GetProperty("resource").GetProperty("id").GetString();
                _logger.LogInformation("Confirmación de pago capturado via webhook: {CaptureId}", captureId);

                // Aquí podrías actualizar el estado si necesitas confirmación adicional
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando PAYMENT.CAPTURE.COMPLETED");
            }
        }

        private async Task ProcesarPagoDenegado(JsonElement resource)
        {
            try
            {
                var captureId = resource.GetProperty("resource").GetProperty("id").GetString();
                _logger.LogWarning("Pago denegado: {CaptureId}", captureId);

                await _logEventoService.RegistrarEventoAsync(
                    $"Pago PayPal denegado: {captureId}",
                    CategoriaEvento.Pago,
                    TipoLogEvento.Warning,
                    null,
                    null,
                    "Webhook: PAYMENT.CAPTURE.DENIED"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando PAYMENT.CAPTURE.DENIED");
            }
        }

        private async Task ProcesarReembolso(JsonElement resource)
        {
            try
            {
                var refundId = resource.GetProperty("resource").GetProperty("id").GetString();
                _logger.LogInformation("Reembolso procesado: {RefundId}", refundId);

                await _logEventoService.RegistrarEventoAsync(
                    $"Reembolso PayPal procesado: {refundId}",
                    CategoriaEvento.Pago,
                    TipoLogEvento.Info,
                    null,
                    null,
                    "Webhook: PAYMENT.CAPTURE.REFUNDED"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando PAYMENT.CAPTURE.REFUNDED");
            }
        }

        #endregion
    }

    #region Request DTOs

    public class CrearOrdenRequest
    {
        public decimal Monto { get; set; }
    }

    public class CapturarOrdenRequest
    {
        public string OrderId { get; set; } = string.Empty;
    }

    #endregion
}
