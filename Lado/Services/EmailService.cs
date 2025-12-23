using Mailjet.Client;
using Mailjet.Client.Resources;
using Mailjet.Client.TransactionalEmails;
using Newtonsoft.Json.Linq;
using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Resultado del envío de email con información detallada
    /// </summary>
    public class EmailResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int StatusCode { get; set; }
        public string? ErrorDetails { get; set; }
        public string? MessageId { get; set; }

        public static EmailResult Ok(string? messageId = null) => new() { Success = true, StatusCode = 200, MessageId = messageId };
        public static EmailResult Fail(string message, int statusCode = 0, string? details = null)
            => new() { Success = false, ErrorMessage = message, StatusCode = statusCode, ErrorDetails = details };
    }

    /// <summary>
    /// Interfaz para proveedores de email
    /// </summary>
    public interface IEmailProvider
    {
        string ProviderName { get; }
        Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlBody, string fromEmail, string fromName);
        Task<EmailResult> TestConnectionAsync(string testEmail);
    }

    /// <summary>
    /// Interfaz del servicio de email
    /// </summary>
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<EmailResult> SendEmailWithResultAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendConfirmationEmailAsync(string toEmail, string userName, string confirmationLink);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
        Task<bool> SendWelcomeEmailAsync(string toEmail, string nombre, string username, string temporaryPassword);
        Task<bool> SendNewSubscriberNotificationAsync(string creatorEmail, string creatorName, string subscriberName);
        Task<bool> SendPaymentReceivedNotificationAsync(string email, string nombre, decimal monto, string concepto);
        Task<bool> SendLiquidacionRetiroAsync(string email, string nombre, decimal montoBruto, decimal comision, decimal impuestos, decimal montoNeto, string metodoPago, int transaccionId, byte[] pdfBytes);
        Task<EmailResult> TestProviderAsync(string testEmail);
        Task<string> GetActiveProviderNameAsync();
    }

    #region Proveedores de Email

    /// <summary>
    /// Proveedor de email usando Mailjet
    /// </summary>
    public class MailjetEmailProvider : IEmailProvider
    {
        private readonly string _apiKey;
        private readonly string _secretKey;
        private readonly ILogger? _logger;

        public string ProviderName => "Mailjet";

        public MailjetEmailProvider(string apiKey, string secretKey, ILogger? logger = null)
        {
            _apiKey = apiKey?.Trim() ?? "";
            _secretKey = secretKey?.Trim() ?? "";
            _logger = logger;
        }

        public async Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlBody, string fromEmail, string fromName)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_secretKey))
                {
                    return EmailResult.Fail("Credenciales de Mailjet no configuradas.", 0);
                }

                var client = new MailjetClient(_apiKey, _secretKey);

                var request = new MailjetRequest { Resource = SendV31.Resource }
                    .Property("Messages", new JArray
                    {
                        new JObject
                        {
                            { "From", new JObject { { "Email", fromEmail }, { "Name", fromName } } },
                            { "To", new JArray { new JObject { { "Email", toEmail }, { "Name", toEmail.Split('@')[0] } } } },
                            { "Subject", subject },
                            { "HTMLPart", htmlBody },
                            { "TextPart", StripHtml(htmlBody) }
                        }
                    });

                _logger?.LogInformation("Mailjet: Enviando email a {Email} desde {From}", toEmail, fromEmail);

                var response = await client.PostAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = response.GetData();
                    _logger?.LogInformation("Email enviado exitosamente a {Email}. Response: {Response}",
                        toEmail, responseData?.ToString() ?? "null");
                    return EmailResult.Ok();
                }
                else
                {
                    var statusCode = (int)response.StatusCode;
                    var rawData = response.GetData();
                    string mainError = $"Error HTTP {statusCode}";

                    if (rawData != null)
                    {
                        try
                        {
                            var messages = rawData["Messages"];
                            if (messages != null)
                            {
                                foreach (var msg in messages)
                                {
                                    var errors = msg["Errors"];
                                    if (errors != null)
                                    {
                                        foreach (var err in errors)
                                        {
                                            mainError = $"[{err["ErrorCode"]}] {err["ErrorMessage"]}";
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception parseEx)
                        {
                            _logger?.LogDebug(parseEx, "Error al parsear respuesta de error de Mailjet");
                        }
                    }

                    _logger?.LogError("Error Mailjet: {Error}", mainError);
                    return EmailResult.Fail(mainError, statusCode, rawData?.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Excepcion Mailjet al enviar email a {Email}", toEmail);
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        public async Task<EmailResult> TestConnectionAsync(string testEmail)
        {
            return await SendEmailAsync(
                testEmail,
                "Prueba de conexión - Mailjet",
                "<h1>Prueba exitosa</h1><p>La conexión con Mailjet está funcionando correctamente.</p>",
                testEmail.Contains("@") ? $"test@{testEmail.Split('@')[1]}" : "test@example.com",
                "Lado Test"
            );
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ")
                .Replace("  ", " ")
                .Trim();
        }
    }

    /// <summary>
    /// Proveedor de email usando Amazon SES
    /// </summary>
    public class AmazonSesEmailProvider : IEmailProvider
    {
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _region;
        private readonly ILogger? _logger;

        public string ProviderName => "Amazon SES";

        public AmazonSesEmailProvider(string accessKey, string secretKey, string region = "us-east-1", ILogger? logger = null)
        {
            _accessKey = accessKey?.Trim() ?? "";
            _secretKey = secretKey?.Trim() ?? "";
            _region = region?.Trim() ?? "us-east-1";
            _logger = logger;
        }

        public async Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlBody, string fromEmail, string fromName)
        {
            try
            {
                if (string.IsNullOrEmpty(_accessKey) || string.IsNullOrEmpty(_secretKey))
                {
                    return EmailResult.Fail("Credenciales de Amazon SES no configuradas.", 0);
                }

                var regionEndpoint = RegionEndpoint.GetBySystemName(_region);
                using var client = new AmazonSimpleEmailServiceClient(_accessKey, _secretKey, regionEndpoint);

                var sendRequest = new Amazon.SimpleEmail.Model.SendEmailRequest
                {
                    Source = string.IsNullOrEmpty(fromName) ? fromEmail : $"{fromName} <{fromEmail}>",
                    Destination = new Destination
                    {
                        ToAddresses = new List<string> { toEmail }
                    },
                    Message = new Amazon.SimpleEmail.Model.Message
                    {
                        Subject = new Amazon.SimpleEmail.Model.Content(subject),
                        Body = new Amazon.SimpleEmail.Model.Body
                        {
                            Html = new Amazon.SimpleEmail.Model.Content
                            {
                                Charset = "UTF-8",
                                Data = htmlBody
                            },
                            Text = new Amazon.SimpleEmail.Model.Content
                            {
                                Charset = "UTF-8",
                                Data = StripHtml(htmlBody)
                            }
                        }
                    }
                };

                _logger?.LogInformation("Amazon SES: Enviando email a {Email} desde {From}", toEmail, fromEmail);

                var response = await client.SendEmailAsync(sendRequest);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger?.LogInformation("Email enviado exitosamente via Amazon SES a {Email}. MessageId: {MessageId}",
                        toEmail, response.MessageId);
                    return EmailResult.Ok();
                }
                else
                {
                    var error = $"Error HTTP {(int)response.HttpStatusCode}";
                    _logger?.LogError("Error Amazon SES: {Error}", error);
                    return EmailResult.Fail(error, (int)response.HttpStatusCode);
                }
            }
            catch (AmazonSimpleEmailServiceException sesEx)
            {
                _logger?.LogError(sesEx, "Error Amazon SES al enviar email a {Email}: {ErrorCode} - {Message}",
                    toEmail, sesEx.ErrorCode, sesEx.Message);
                return EmailResult.Fail($"[{sesEx.ErrorCode}] {sesEx.Message}", (int)sesEx.StatusCode, sesEx.ToString());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Excepcion Amazon SES al enviar email a {Email}", toEmail);
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        public async Task<EmailResult> TestConnectionAsync(string testEmail)
        {
            try
            {
                if (string.IsNullOrEmpty(_accessKey) || string.IsNullOrEmpty(_secretKey))
                {
                    return EmailResult.Fail("Credenciales de Amazon SES no configuradas.", 0);
                }

                var regionEndpoint = RegionEndpoint.GetBySystemName(_region);
                using var client = new AmazonSimpleEmailServiceClient(_accessKey, _secretKey, regionEndpoint);

                // Verificar la cuenta
                var accountResponse = await client.GetAccountSendingEnabledAsync(new GetAccountSendingEnabledRequest());

                if (accountResponse.Enabled)
                {
                    _logger?.LogInformation("Amazon SES: Cuenta verificada y habilitada para envío");
                    return EmailResult.Ok();
                }
                else
                {
                    return EmailResult.Fail("La cuenta de Amazon SES no está habilitada para envío.", 0);
                }
            }
            catch (AmazonSimpleEmailServiceException sesEx)
            {
                return EmailResult.Fail($"[{sesEx.ErrorCode}] {sesEx.Message}", (int)sesEx.StatusCode);
            }
            catch (Exception ex)
            {
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ")
                .Replace("  ", " ")
                .Trim();
        }
    }

    /// <summary>
    /// Proveedor de email usando Brevo (antes Sendinblue)
    /// </summary>
    public class BrevoEmailProvider : IEmailProvider
    {
        private readonly string _apiKey;
        private readonly ILogger? _logger;
        private readonly HttpClient _httpClient;

        public string ProviderName => "Brevo";

        public BrevoEmailProvider(string apiKey, ILogger? logger = null)
        {
            _apiKey = apiKey?.Trim() ?? "";
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
        }

        public async Task<EmailResult> SendEmailAsync(string toEmail, string subject, string htmlBody, string fromEmail, string fromName)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return EmailResult.Fail("API Key de Brevo no configurada.", 0);
                }

                var payload = new
                {
                    sender = new { email = fromEmail, name = fromName },
                    to = new[] { new { email = toEmail, name = toEmail.Split('@')[0] } },
                    subject = subject,
                    htmlContent = htmlBody,
                    textContent = StripHtml(htmlBody)
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _logger?.LogInformation("Brevo: Enviando email a {Email} desde {From}", toEmail, fromEmail);

                var response = await _httpClient.PostAsync("https://api.brevo.com/v3/smtp/email", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("Email enviado exitosamente via Brevo a {Email}. Response: {Response}",
                        toEmail, responseBody);

                    // Extraer messageId si existe
                    string? messageId = null;
                    try
                    {
                        var responseJson = System.Text.Json.JsonDocument.Parse(responseBody);
                        if (responseJson.RootElement.TryGetProperty("messageId", out var msgId))
                        {
                            messageId = msgId.GetString();
                        }
                    }
                    catch { }

                    return EmailResult.Ok(messageId);
                }
                else
                {
                    var statusCode = (int)response.StatusCode;
                    string errorMessage = $"Error HTTP {statusCode}";

                    try
                    {
                        var errorJson = System.Text.Json.JsonDocument.Parse(responseBody);
                        if (errorJson.RootElement.TryGetProperty("message", out var msg))
                        {
                            errorMessage = msg.GetString() ?? errorMessage;
                        }
                    }
                    catch { }

                    _logger?.LogError("Error Brevo: {Error}", errorMessage);
                    return EmailResult.Fail(errorMessage, statusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Excepcion Brevo al enviar email a {Email}", toEmail);
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        public async Task<EmailResult> TestConnectionAsync(string testEmail)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return EmailResult.Fail("API Key de Brevo no configurada.", 0);
                }

                // Verificar la cuenta obteniendo información
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.brevo.com/v3/account");
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("Brevo: Cuenta verificada correctamente");
                    return EmailResult.Ok();
                }
                else
                {
                    var statusCode = (int)response.StatusCode;
                    string errorMessage = $"Error HTTP {statusCode}";

                    try
                    {
                        var errorJson = System.Text.Json.JsonDocument.Parse(responseBody);
                        if (errorJson.RootElement.TryGetProperty("message", out var msg))
                        {
                            errorMessage = msg.GetString() ?? errorMessage;
                        }
                    }
                    catch { }

                    return EmailResult.Fail(errorMessage, statusCode);
                }
            }
            catch (Exception ex)
            {
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ")
                .Replace("  ", " ")
                .Trim();
        }
    }

    #endregion

    /// <summary>
    /// Servicio de email que gestiona múltiples proveedores
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(ApplicationDbContext context, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la configuración de email desde la base de datos
        /// </summary>
        private async Task<Dictionary<string, string>> GetEmailConfigAsync()
        {
            var configs = await _context.ConfiguracionesPlataforma
                .Where(c => c.Categoria == "Email")
                .ToDictionaryAsync(c => c.Clave, c => c.Valor);

            // Si no hay configuración en BD, usar valores del appsettings (compatibilidad)
            if (!configs.ContainsKey(ConfiguracionPlataforma.EMAIL_PROVEEDOR_ACTIVO))
            {
                configs[ConfiguracionPlataforma.EMAIL_PROVEEDOR_ACTIVO] = "Mailjet";
            }
            if (!configs.ContainsKey(ConfiguracionPlataforma.EMAIL_FROM_EMAIL))
            {
                configs[ConfiguracionPlataforma.EMAIL_FROM_EMAIL] = _configuration["Mailjet:FromEmail"] ?? "noreply@ladoapp.com";
            }
            if (!configs.ContainsKey(ConfiguracionPlataforma.EMAIL_FROM_NAME))
            {
                configs[ConfiguracionPlataforma.EMAIL_FROM_NAME] = _configuration["Mailjet:FromName"] ?? "Lado";
            }
            if (!configs.ContainsKey(ConfiguracionPlataforma.MAILJET_API_KEY))
            {
                configs[ConfiguracionPlataforma.MAILJET_API_KEY] = _configuration["Mailjet:ApiKey"] ?? "";
            }
            if (!configs.ContainsKey(ConfiguracionPlataforma.MAILJET_SECRET_KEY))
            {
                configs[ConfiguracionPlataforma.MAILJET_SECRET_KEY] = _configuration["Mailjet:SecretKey"] ?? "";
            }

            return configs;
        }

        /// <summary>
        /// Obtiene el proveedor de email activo
        /// </summary>
        private async Task<(IEmailProvider provider, string fromEmail, string fromName)> GetActiveProviderAsync()
        {
            var configs = await GetEmailConfigAsync();

            var proveedorActivo = configs.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_PROVEEDOR_ACTIVO, "Mailjet");
            var fromEmail = configs.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_FROM_EMAIL, "noreply@ladoapp.com");
            var fromName = configs.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_FROM_NAME, "Lado");

            IEmailProvider provider;

            switch (proveedorActivo)
            {
                case "AmazonSES":
                    var accessKey = configs.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_ACCESS_KEY, "");
                    var secretKey = configs.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_SECRET_KEY, "");
                    var region = configs.GetValueOrDefault(ConfiguracionPlataforma.AMAZONSES_REGION, "us-east-1");
                    provider = new AmazonSesEmailProvider(accessKey, secretKey, region, _logger);
                    break;

                case "Brevo":
                    var brevoApiKey = configs.GetValueOrDefault(ConfiguracionPlataforma.BREVO_API_KEY, "");
                    provider = new BrevoEmailProvider(brevoApiKey, _logger);
                    break;

                default: // Mailjet
                    var mailjetApiKey = configs.GetValueOrDefault(ConfiguracionPlataforma.MAILJET_API_KEY, "");
                    var mailjetSecretKey = configs.GetValueOrDefault(ConfiguracionPlataforma.MAILJET_SECRET_KEY, "");
                    provider = new MailjetEmailProvider(mailjetApiKey, mailjetSecretKey, _logger);
                    break;
            }

            _logger.LogInformation("Proveedor de email activo: {Provider}, From: {From}",
                provider.ProviderName, fromEmail);

            return (provider, fromEmail, fromName);
        }

        public async Task<string> GetActiveProviderNameAsync()
        {
            var configs = await GetEmailConfigAsync();
            return configs.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_PROVEEDOR_ACTIVO, "Mailjet");
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var result = await SendEmailWithResultAsync(toEmail, subject, htmlBody);
            return result.Success;
        }

        public async Task<EmailResult> SendEmailWithResultAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var (provider, fromEmail, fromName) = await GetActiveProviderAsync();
                return await provider.SendEmailAsync(toEmail, subject, htmlBody, fromEmail, fromName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email a {Email}", toEmail);
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        public async Task<EmailResult> TestProviderAsync(string testEmail)
        {
            try
            {
                var (provider, fromEmail, fromName) = await GetActiveProviderAsync();

                var html = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h1 style='color: #4682B4;'>Prueba de Email - Lado</h1>
                        <p>Este es un email de prueba enviado desde el panel de administración.</p>
                        <p><strong>Proveedor:</strong> {provider.ProviderName}</p>
                        <p><strong>Fecha:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
                        <hr style='border: 1px solid #eee;'>
                        <p style='color: #999; font-size: 12px;'>Este email fue enviado como prueba de configuración.</p>
                    </div>";

                return await provider.SendEmailAsync(testEmail, "Prueba de Email - Lado", html, fromEmail, fromName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al probar proveedor de email");
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        #region Métodos específicos de email

        public async Task<bool> SendConfirmationEmailAsync(string toEmail, string userName, string confirmationLink)
        {
            var subject = "Confirma tu cuenta en Lado";
            var html = GetEmailTemplate("confirmation", new Dictionary<string, string>
            {
                { "nombre", userName },
                { "link", confirmationLink }
            });
            return await SendEmailAsync(toEmail, subject, html);
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            var subject = "Recupera tu contraseña - Lado";
            var html = GetEmailTemplate("password_reset", new Dictionary<string, string>
            {
                { "nombre", userName },
                { "link", resetLink }
            });
            return await SendEmailAsync(toEmail, subject, html);
        }

        public async Task<bool> SendWelcomeEmailAsync(string toEmail, string nombre, string username, string temporaryPassword)
        {
            var subject = "Bienvenido a Lado - Tus credenciales de acceso";
            var html = GetEmailTemplate("welcome", new Dictionary<string, string>
            {
                { "nombre", nombre },
                { "username", username },
                { "password", temporaryPassword },
                { "loginUrl", _configuration["App:BaseUrl"] ?? "https://ladoapp.com" }
            });
            return await SendEmailAsync(toEmail, subject, html);
        }

        public async Task<bool> SendNewSubscriberNotificationAsync(string creatorEmail, string creatorName, string subscriberName)
        {
            var subject = "Tienes un nuevo suscriptor en Lado";
            var html = GetEmailTemplate("new_subscriber", new Dictionary<string, string>
            {
                { "creatorName", creatorName },
                { "subscriberName", subscriberName }
            });
            return await SendEmailAsync(creatorEmail, subject, html);
        }

        public async Task<bool> SendPaymentReceivedNotificationAsync(string email, string nombre, decimal monto, string concepto)
        {
            var subject = "Has recibido un pago en Lado";
            var html = GetEmailTemplate("payment_received", new Dictionary<string, string>
            {
                { "nombre", nombre },
                { "monto", monto.ToString("C2") },
                { "concepto", concepto }
            });
            return await SendEmailAsync(email, subject, html);
        }

        public async Task<bool> SendLiquidacionRetiroAsync(string email, string nombre, decimal montoBruto, decimal comision, decimal impuestos, decimal montoNeto, string metodoPago, int transaccionId, byte[] pdfBytes)
        {
            try
            {
                var configs = await GetEmailConfigAsync();
                var apiKey = configs.GetValueOrDefault(ConfiguracionPlataforma.MAILJET_API_KEY, "");
                var secretKey = configs.GetValueOrDefault(ConfiguracionPlataforma.MAILJET_SECRET_KEY, "");
                var fromEmail = configs.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_FROM_EMAIL, "noreply@ladoapp.com");
                var fromName = configs.GetValueOrDefault(ConfiguracionPlataforma.EMAIL_FROM_NAME, "Lado");

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
                {
                    _logger.LogWarning("No hay credenciales de Mailjet configuradas para enviar liquidación");
                    return false;
                }

                var client = new MailjetClient(apiKey, secretKey);

                var subject = $"Liquidación de Retiro #{transaccionId:D8} - Lado";
                var html = GetEmailTemplate("liquidacion_retiro", new Dictionary<string, string>
                {
                    { "nombre", nombre },
                    { "montoBruto", montoBruto.ToString("N2") },
                    { "comision", comision.ToString("N2") },
                    { "impuestos", impuestos.ToString("N2") },
                    { "montoNeto", montoNeto.ToString("N2") },
                    { "metodoPago", metodoPago },
                    { "transaccionId", transaccionId.ToString("D8") },
                    { "fecha", DateTime.Now.ToString("dd/MM/yyyy HH:mm") }
                });

                // Convertir PDF a Base64
                var pdfBase64 = Convert.ToBase64String(pdfBytes);

                var request = new MailjetRequest { Resource = SendV31.Resource }
                    .Property("Messages", new JArray
                    {
                        new JObject
                        {
                            { "From", new JObject { { "Email", fromEmail }, { "Name", fromName } } },
                            { "To", new JArray { new JObject { { "Email", email }, { "Name", nombre } } } },
                            { "Subject", subject },
                            { "HTMLPart", html },
                            { "TextPart", $"Hola {nombre}, adjuntamos la liquidación de tu retiro #{transaccionId:D8}. Monto bruto: ${montoBruto:N2}, Comisión: ${comision:N2}, Impuestos: ${impuestos:N2}, Neto a recibir: ${montoNeto:N2}" },
                            { "Attachments", new JArray
                                {
                                    new JObject
                                    {
                                        { "ContentType", "application/pdf" },
                                        { "Filename", $"liquidacion_{transaccionId}.pdf" },
                                        { "Base64Content", pdfBase64 }
                                    }
                                }
                            }
                        }
                    });

                _logger.LogInformation("Enviando liquidación de retiro a {Email}", email);

                var response = await client.PostAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Liquidación enviada exitosamente a {Email}", email);
                    return true;
                }
                else
                {
                    _logger.LogError("Error al enviar liquidación: {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar liquidación de retiro a {Email}", email);
                return false;
            }
        }

        #endregion

        #region Templates

        private string GetEmailTemplate(string templateName, Dictionary<string, string> variables)
        {
            var year = DateTime.Now.Year.ToString();

            var template = templateName switch
            {
                "confirmation" => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
    <div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
        <div style='background:#ffffff;border-radius:16px;padding:40px;box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
            <div style='text-align:center;margin-bottom:30px;'>
                <h1 style='color:#4682B4;margin:0;font-size:28px;'>Lado</h1>
            </div>
            <h2 style='color:#333;margin-bottom:20px;'>Hola {{{{nombre}}}},</h2>
            <p style='color:#666;font-size:16px;line-height:1.6;'>
                Gracias por registrarte en Lado. Para completar tu registro y activar tu cuenta,
                haz clic en el siguiente boton:
            </p>
            <div style='text-align:center;margin:30px 0;'>
                <a href='{{{{link}}}}' style='display:inline-block;background:#4682B4;color:#ffffff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;'>
                    Confirmar mi cuenta
                </a>
            </div>
            <p style='color:#999;font-size:14px;'>
                Si no creaste esta cuenta, puedes ignorar este mensaje.
            </p>
            <hr style='border:none;border-top:1px solid #eee;margin:30px 0;'>
            <p style='color:#999;font-size:12px;text-align:center;'>
                &copy; {year} Lado. Todos los derechos reservados.
            </p>
        </div>
    </div>
</body>
</html>",

                "password_reset" => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
    <div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
        <div style='background:#ffffff;border-radius:16px;padding:40px;box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
            <div style='text-align:center;margin-bottom:30px;'>
                <h1 style='color:#4682B4;margin:0;font-size:28px;'>Lado</h1>
            </div>
            <h2 style='color:#333;margin-bottom:20px;'>Hola {{{{nombre}}}},</h2>
            <p style='color:#666;font-size:16px;line-height:1.6;'>
                Recibimos una solicitud para restablecer tu contrasena.
                Haz clic en el siguiente boton para crear una nueva:
            </p>
            <div style='text-align:center;margin:30px 0;'>
                <a href='{{{{link}}}}' style='display:inline-block;background:#4682B4;color:#ffffff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;'>
                    Restablecer contrasena
                </a>
            </div>
            <p style='color:#999;font-size:14px;'>
                Si no solicitaste esto, puedes ignorar este mensaje. Tu contrasena no cambiara.
            </p>
            <p style='color:#e74c3c;font-size:14px;font-weight:600;'>
                Este enlace expirara en 24 horas.
            </p>
            <hr style='border:none;border-top:1px solid #eee;margin:30px 0;'>
            <p style='color:#999;font-size:12px;text-align:center;'>
                &copy; {year} Lado. Todos los derechos reservados.
            </p>
        </div>
    </div>
</body>
</html>",

                "welcome" => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
    <div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
        <div style='background:#ffffff;border-radius:16px;padding:40px;box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
            <div style='text-align:center;margin-bottom:30px;'>
                <h1 style='color:#4682B4;margin:0;font-size:28px;'>Lado</h1>
            </div>
            <h2 style='color:#333;margin-bottom:20px;'>Bienvenido a Lado, {{{{nombre}}}}!</h2>
            <p style='color:#666;font-size:16px;line-height:1.6;'>
                Tu cuenta ha sido creada exitosamente. Aqui estan tus credenciales de acceso:
            </p>
            <div style='background:#f8f9fa;border-radius:8px;padding:20px;margin:20px 0;border-left:4px solid #4682B4;'>
                <p style='margin:8px 0;color:#333;font-size:16px;'><strong>Usuario:</strong> {{{{username}}}}</p>
                <p style='margin:8px 0;color:#333;font-size:16px;'><strong>Contrasena temporal:</strong> {{{{password}}}}</p>
            </div>
            <p style='color:#e74c3c;font-size:14px;font-weight:600;'>
                Por seguridad, te recomendamos cambiar tu contrasena despues de iniciar sesion.
            </p>
            <div style='text-align:center;margin:30px 0;'>
                <a href='{{{{loginUrl}}}}/Account/Login' style='display:inline-block;background:#4682B4;color:#ffffff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:600;font-size:16px;'>
                    Iniciar sesion
                </a>
            </div>
            <hr style='border:none;border-top:1px solid #eee;margin:30px 0;'>
            <p style='color:#999;font-size:12px;text-align:center;'>
                &copy; {year} Lado. Todos los derechos reservados.
            </p>
        </div>
    </div>
</body>
</html>",

                "new_subscriber" => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
    <div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
        <div style='background:#ffffff;border-radius:16px;padding:40px;box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
            <div style='text-align:center;margin-bottom:30px;'>
                <h1 style='color:#4682B4;margin:0;font-size:28px;'>Lado</h1>
            </div>
            <div style='text-align:center;margin-bottom:20px;'>
                <span style='font-size:48px;'>🎉</span>
            </div>
            <h2 style='color:#333;margin-bottom:20px;text-align:center;'>Felicidades {{{{creatorName}}}}!</h2>
            <p style='color:#666;font-size:16px;line-height:1.6;text-align:center;'>
                <strong style='color:#4682B4;'>{{{{subscriberName}}}}</strong> se ha suscrito a tu contenido premium.
            </p>
            <p style='color:#666;font-size:16px;line-height:1.6;text-align:center;'>
                Sigue creando contenido increible para tu comunidad!
            </p>
            <hr style='border:none;border-top:1px solid #eee;margin:30px 0;'>
            <p style='color:#999;font-size:12px;text-align:center;'>
                &copy; {year} Lado. Todos los derechos reservados.
            </p>
        </div>
    </div>
</body>
</html>",

                "payment_received" => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
    <div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
        <div style='background:#ffffff;border-radius:16px;padding:40px;box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
            <div style='text-align:center;margin-bottom:30px;'>
                <h1 style='color:#4682B4;margin:0;font-size:28px;'>Lado</h1>
            </div>
            <h2 style='color:#333;margin-bottom:20px;'>Hola {{{{nombre}}}},</h2>
            <p style='color:#666;font-size:16px;line-height:1.6;'>
                Has recibido un nuevo pago en tu cuenta:
            </p>
            <div style='background:#d4edda;border:1px solid #c3e6cb;border-radius:8px;padding:20px;margin:20px 0;text-align:center;'>
                <p style='margin:0;color:#155724;font-size:28px;font-weight:700;'>{{{{monto}}}}</p>
                <p style='margin:8px 0 0;color:#155724;font-size:14px;'>{{{{concepto}}}}</p>
            </div>
            <p style='color:#666;font-size:14px;'>
                Puedes ver el detalle de tus ganancias en tu panel de creador.
            </p>
            <hr style='border:none;border-top:1px solid #eee;margin:30px 0;'>
            <p style='color:#999;font-size:12px;text-align:center;'>
                &copy; {year} Lado. Todos los derechos reservados.
            </p>
        </div>
    </div>
</body>
</html>",

                "liquidacion_retiro" => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,sans-serif;'>
    <div style='max-width:600px;margin:0 auto;padding:40px 20px;'>
        <div style='background:#ffffff;border-radius:16px;padding:40px;box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
            <div style='text-align:center;margin-bottom:30px;'>
                <h1 style='color:#4682B4;margin:0;font-size:28px;'>Lado</h1>
                <p style='color:#666;margin:8px 0 0;font-size:14px;'>Liquidacion de Retiro</p>
            </div>
            <h2 style='color:#333;margin-bottom:20px;'>Hola {{{{nombre}}}},</h2>
            <p style='color:#666;font-size:16px;line-height:1.6;'>
                Tu solicitud de retiro <strong>#{{{{transaccionId}}}}</strong> ha sido registrada exitosamente.
                Adjuntamos el documento de liquidacion en formato PDF para tus registros.
            </p>
            <div style='background:#f8f9fa;border:1px solid #e9ecef;border-radius:12px;padding:24px;margin:24px 0;'>
                <h3 style='color:#4682B4;margin:0 0 16px 0;font-size:16px;border-bottom:2px solid #4682B4;padding-bottom:8px;'>Resumen del Retiro</h3>
                <table style='width:100%;border-collapse:collapse;'>
                    <tr>
                        <td style='padding:8px 0;color:#666;'>Monto Bruto:</td>
                        <td style='padding:8px 0;text-align:right;font-weight:600;color:#333;'>${{{{montoBruto}}}}</td>
                    </tr>
                    <tr>
                        <td style='padding:8px 0;color:#dc3545;'>Comision LadoApp:</td>
                        <td style='padding:8px 0;text-align:right;font-weight:600;color:#dc3545;'>-${{{{comision}}}}</td>
                    </tr>
                    <tr>
                        <td style='padding:8px 0;color:#fd7e14;'>Retencion Impuestos:</td>
                        <td style='padding:8px 0;text-align:right;font-weight:600;color:#fd7e14;'>-${{{{impuestos}}}}</td>
                    </tr>
                    <tr style='border-top:2px solid #dee2e6;'>
                        <td style='padding:12px 0 0;color:#333;font-weight:700;font-size:18px;'>Neto a Recibir:</td>
                        <td style='padding:12px 0 0;text-align:right;font-weight:700;color:#28a745;font-size:20px;'>${{{{montoNeto}}}}</td>
                    </tr>
                </table>
            </div>
            <div style='background:#e7f5ff;border:1px solid #74c0fc;border-radius:8px;padding:16px;margin:20px 0;'>
                <p style='margin:0;color:#1971c2;font-size:14px;'>
                    <strong>Metodo de pago:</strong> {{{{metodoPago}}}}<br>
                    <strong>Fecha:</strong> {{{{fecha}}}}<br>
                    <strong>Tiempo estimado:</strong> 3-5 dias habiles
                </p>
            </div>
            <p style='color:#666;font-size:14px;'>
                Recibiras una notificacion cuando el pago haya sido procesado.
                Si tienes alguna pregunta, no dudes en contactarnos.
            </p>
            <hr style='border:none;border-top:1px solid #eee;margin:30px 0;'>
            <p style='color:#999;font-size:12px;text-align:center;'>
                &copy; {year} Lado. Todos los derechos reservados.<br>
                <a href='https://ladoapp.com' style='color:#4682B4;'>www.ladoapp.com</a>
            </p>
        </div>
    </div>
</body>
</html>",

                _ => "<p>{{content}}</p>"
            };

            // Replace variables
            foreach (var variable in variables)
            {
                template = template.Replace($"{{{{{variable.Key}}}}}", variable.Value);
            }

            return template;
        }

        #endregion
    }
}
