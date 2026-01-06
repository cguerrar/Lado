using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;
using PayPalHttp;
using System.Net;

namespace Lado.Services
{
    /// <summary>
    /// Servicio de integración con PayPal para procesar pagos.
    /// Soporta modo Sandbox (pruebas) y Live (producción).
    /// </summary>
    public interface IPayPalService
    {
        /// <summary>
        /// Crea una orden de pago en PayPal
        /// </summary>
        Task<PayPalOrderResult> CrearOrdenAsync(decimal monto, string descripcion, string returnUrl, string cancelUrl);

        /// <summary>
        /// Captura (confirma) un pago después de que el usuario aprobó en PayPal
        /// </summary>
        Task<PayPalCaptureResult> CapturarOrdenAsync(string orderId);

        /// <summary>
        /// Obtiene los detalles de una orden
        /// </summary>
        Task<PayPalOrderDetails?> ObtenerOrdenAsync(string orderId);

        /// <summary>
        /// Verifica si el webhook es válido (firma)
        /// </summary>
        Task<bool> VerificarWebhookAsync(string webhookId, string transmissionId, string transmissionTime,
            string certUrl, string authAlgo, string transmissionSig, string webhookEvent);
    }

    public class PayPalService : IPayPalService
    {
        private readonly PayPalHttpClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayPalService> _logger;
        private readonly bool _isSandbox;

        public PayPalService(IConfiguration configuration, ILogger<PayPalService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var clientId = _configuration["PayPal:ClientId"]
                ?? throw new InvalidOperationException("PayPal:ClientId no está configurado");
            var clientSecret = _configuration["PayPal:ClientSecret"]
                ?? throw new InvalidOperationException("PayPal:ClientSecret no está configurado");

            _isSandbox = _configuration.GetValue<bool>("PayPal:Sandbox", true);

            // Crear el entorno (Sandbox o Live)
            PayPalEnvironment environment = _isSandbox
                ? new SandboxEnvironment(clientId, clientSecret)
                : new LiveEnvironment(clientId, clientSecret);

            _client = new PayPalHttpClient(environment);

            _logger.LogInformation("PayPalService inicializado en modo {Mode}", _isSandbox ? "SANDBOX" : "LIVE");
        }

        public async Task<PayPalOrderResult> CrearOrdenAsync(decimal monto, string descripcion, string returnUrl, string cancelUrl)
        {
            try
            {
                var orderRequest = new OrderRequest()
                {
                    CheckoutPaymentIntent = "CAPTURE",
                    PurchaseUnits = new List<PurchaseUnitRequest>
                    {
                        new PurchaseUnitRequest
                        {
                            AmountWithBreakdown = new AmountWithBreakdown
                            {
                                CurrencyCode = "USD",
                                Value = monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                            },
                            Description = descripcion
                        }
                    },
                    ApplicationContext = new ApplicationContext
                    {
                        ReturnUrl = returnUrl,
                        CancelUrl = cancelUrl,
                        BrandName = "LADO",
                        LandingPage = "BILLING",
                        UserAction = "PAY_NOW",
                        ShippingPreference = "NO_SHIPPING" // No necesitamos dirección de envío
                    }
                };

                var request = new OrdersCreateRequest();
                request.Prefer("return=representation");
                request.RequestBody(orderRequest);

                var response = await _client.Execute(request);
                var order = response.Result<Order>();

                // Buscar el link de aprobación
                var approvalLink = order.Links.FirstOrDefault(l => l.Rel == "approve")?.Href;

                _logger.LogInformation("Orden PayPal creada: {OrderId}, Monto: ${Monto}", order.Id, monto);

                return new PayPalOrderResult
                {
                    Success = true,
                    OrderId = order.Id,
                    ApprovalUrl = approvalLink,
                    Status = order.Status
                };
            }
            catch (HttpException ex)
            {
                _logger.LogError(ex, "Error al crear orden PayPal: {StatusCode}", ex.StatusCode);
                return new PayPalOrderResult
                {
                    Success = false,
                    Error = $"Error de PayPal: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al crear orden PayPal");
                return new PayPalOrderResult
                {
                    Success = false,
                    Error = "Error inesperado al procesar el pago"
                };
            }
        }

        public async Task<PayPalCaptureResult> CapturarOrdenAsync(string orderId)
        {
            try
            {
                var request = new OrdersCaptureRequest(orderId);
                request.Prefer("return=representation");
                request.RequestBody(new OrderActionRequest());

                var response = await _client.Execute(request);
                var order = response.Result<Order>();

                // Obtener información del pago capturado
                var capture = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?.FirstOrDefault();

                _logger.LogInformation("Orden PayPal capturada: {OrderId}, Status: {Status}", order.Id, order.Status);

                return new PayPalCaptureResult
                {
                    Success = order.Status == "COMPLETED",
                    OrderId = order.Id,
                    CaptureId = capture?.Id,
                    Status = order.Status,
                    PayerEmail = order.Payer?.Email,
                    PayerId = order.Payer?.PayerId,
                    Amount = capture?.Amount?.Value != null
                        ? decimal.Parse(capture.Amount.Value, System.Globalization.CultureInfo.InvariantCulture)
                        : 0
                };
            }
            catch (HttpException ex)
            {
                _logger.LogError(ex, "Error al capturar orden PayPal {OrderId}: {StatusCode}", orderId, ex.StatusCode);

                // Si el pago ya fue capturado, podría ser un intento duplicado
                if (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    return new PayPalCaptureResult
                    {
                        Success = false,
                        OrderId = orderId,
                        Error = "El pago ya fue procesado anteriormente"
                    };
                }

                return new PayPalCaptureResult
                {
                    Success = false,
                    OrderId = orderId,
                    Error = $"Error de PayPal: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al capturar orden PayPal {OrderId}", orderId);
                return new PayPalCaptureResult
                {
                    Success = false,
                    OrderId = orderId,
                    Error = "Error inesperado al confirmar el pago"
                };
            }
        }

        public async Task<PayPalOrderDetails?> ObtenerOrdenAsync(string orderId)
        {
            try
            {
                var request = new OrdersGetRequest(orderId);
                var response = await _client.Execute(request);
                var order = response.Result<Order>();

                return new PayPalOrderDetails
                {
                    OrderId = order.Id,
                    Status = order.Status,
                    CreateTime = order.CreateTime,
                    Amount = order.PurchaseUnits?.FirstOrDefault()?.AmountWithBreakdown?.Value != null
                        ? decimal.Parse(order.PurchaseUnits.First().AmountWithBreakdown.Value,
                            System.Globalization.CultureInfo.InvariantCulture)
                        : 0,
                    PayerEmail = order.Payer?.Email
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener orden PayPal {OrderId}", orderId);
                return null;
            }
        }

        public async Task<bool> VerificarWebhookAsync(string webhookId, string transmissionId, string transmissionTime,
            string certUrl, string authAlgo, string transmissionSig, string webhookEvent)
        {
            // Para una implementación completa de verificación de webhooks,
            // PayPal recomienda usar su API de verificación.
            // Por simplicidad, aquí validamos que los headers existan.
            // En producción, deberías implementar la verificación completa.

            if (string.IsNullOrEmpty(transmissionId) ||
                string.IsNullOrEmpty(transmissionTime) ||
                string.IsNullOrEmpty(transmissionSig))
            {
                _logger.LogWarning("Webhook PayPal con headers inválidos");
                return false;
            }

            // En un entorno de producción, aquí deberías:
            // 1. Verificar la firma del webhook usando la API de PayPal
            // 2. Validar que el webhook viene de PayPal
            // Por ahora, en sandbox, aceptamos todos los webhooks con headers válidos

            _logger.LogInformation("Webhook PayPal recibido: {TransmissionId}", transmissionId);
            return true;
        }
    }

    #region DTOs de PayPal

    public class PayPalOrderResult
    {
        public bool Success { get; set; }
        public string? OrderId { get; set; }
        public string? ApprovalUrl { get; set; }
        public string? Status { get; set; }
        public string? Error { get; set; }
    }

    public class PayPalCaptureResult
    {
        public bool Success { get; set; }
        public string? OrderId { get; set; }
        public string? CaptureId { get; set; }
        public string? Status { get; set; }
        public string? PayerEmail { get; set; }
        public string? PayerId { get; set; }
        public decimal Amount { get; set; }
        public string? Error { get; set; }
    }

    public class PayPalOrderDetails
    {
        public string? OrderId { get; set; }
        public string? Status { get; set; }
        public string? CreateTime { get; set; }
        public decimal Amount { get; set; }
        public string? PayerEmail { get; set; }
    }

    #endregion
}
