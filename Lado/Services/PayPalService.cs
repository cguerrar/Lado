using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;
using PayPalHttp;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
            // Validar que los headers requeridos existan
            if (string.IsNullOrEmpty(transmissionId) ||
                string.IsNullOrEmpty(transmissionTime) ||
                string.IsNullOrEmpty(transmissionSig) ||
                string.IsNullOrEmpty(certUrl) ||
                string.IsNullOrEmpty(authAlgo))
            {
                _logger.LogWarning("Webhook PayPal con headers incompletos");
                return false;
            }

            // Validar que certUrl sea de PayPal (seguridad básica)
            if (!certUrl.StartsWith("https://api.paypal.com/") &&
                !certUrl.StartsWith("https://api.sandbox.paypal.com/"))
            {
                _logger.LogWarning("Webhook PayPal con certUrl sospechoso: {CertUrl}", certUrl);
                return false;
            }

            try
            {
                // Verificar firma usando la API de PayPal
                var clientId = _configuration["PayPal:ClientId"];
                var clientSecret = _configuration["PayPal:ClientSecret"];
                var baseUrl = _isSandbox
                    ? "https://api-m.sandbox.paypal.com"
                    : "https://api-m.paypal.com";

                using var httpClient = new System.Net.Http.HttpClient();

                // Obtener access token
                var authBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

                var tokenResponse = await httpClient.PostAsync(
                    $"{baseUrl}/v1/oauth2/token",
                    new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded")
                );

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Error al obtener token de PayPal para verificar webhook");
                    return false;
                }

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                var tokenDoc = JsonDocument.Parse(tokenJson);
                var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

                // Verificar la firma del webhook
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var verifyRequest = new
                {
                    auth_algo = authAlgo,
                    cert_url = certUrl,
                    transmission_id = transmissionId,
                    transmission_sig = transmissionSig,
                    transmission_time = transmissionTime,
                    webhook_id = webhookId,
                    webhook_event = JsonDocument.Parse(webhookEvent).RootElement
                };

                var verifyContent = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(verifyRequest),
                    Encoding.UTF8,
                    "application/json"
                );

                var verifyResponse = await httpClient.PostAsync(
                    $"{baseUrl}/v1/notifications/verify-webhook-signature",
                    verifyContent
                );

                if (!verifyResponse.IsSuccessStatusCode)
                {
                    var errorBody = await verifyResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("PayPal rechazó verificación de webhook: {Status} - {Error}",
                        verifyResponse.StatusCode, errorBody);
                    return false;
                }

                var verifyJson = await verifyResponse.Content.ReadAsStringAsync();
                var verifyDoc = JsonDocument.Parse(verifyJson);
                var verificationStatus = verifyDoc.RootElement.GetProperty("verification_status").GetString();

                if (verificationStatus == "SUCCESS")
                {
                    _logger.LogInformation("Webhook PayPal verificado exitosamente: {TransmissionId}", transmissionId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Webhook PayPal falló verificación: {Status}", verificationStatus);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar webhook de PayPal");
                // En caso de error de verificación, rechazar el webhook por seguridad
                return false;
            }
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
