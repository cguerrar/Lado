using Mailjet.Client;
using Mailjet.Client.Resources;
using Mailjet.Client.TransactionalEmails;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Mail;

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

        public static EmailResult Ok() => new() { Success = true, StatusCode = 200 };
        public static EmailResult Fail(string message, int statusCode = 0, string? details = null)
            => new() { Success = false, ErrorMessage = message, StatusCode = statusCode, ErrorDetails = details };
    }

    public interface IEmailService
    {
        /// <summary>
        /// Envia un email generico
        /// </summary>
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);

        /// <summary>
        /// Envia un email generico con resultado detallado
        /// </summary>
        Task<EmailResult> SendEmailWithResultAsync(string toEmail, string subject, string htmlBody);

        /// <summary>
        /// Envia email de confirmacion de cuenta
        /// </summary>
        Task<bool> SendConfirmationEmailAsync(string toEmail, string userName, string confirmationLink);

        /// <summary>
        /// Envia email de recuperacion de contraseña
        /// </summary>
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);

        /// <summary>
        /// Envia email de bienvenida con credenciales (para usuarios creados por admin)
        /// </summary>
        Task<bool> SendWelcomeEmailAsync(string toEmail, string nombre, string username, string temporaryPassword);

        /// <summary>
        /// Envia notificacion de nueva suscripcion
        /// </summary>
        Task<bool> SendNewSubscriberNotificationAsync(string creatorEmail, string creatorName, string subscriberName);

        /// <summary>
        /// Envia notificacion de pago recibido
        /// </summary>
        Task<bool> SendPaymentReceivedNotificationAsync(string email, string nombre, decimal monto, string concepto);
    }

    public class MailjetEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MailjetEmailService> _logger;
        private readonly string _apiKey;
        private readonly string _secretKey;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public MailjetEmailService(IConfiguration configuration, ILogger<MailjetEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _apiKey = configuration["Mailjet:ApiKey"]?.Trim() ?? "";
            _secretKey = configuration["Mailjet:SecretKey"]?.Trim() ?? "";
            _fromEmail = configuration["Mailjet:FromEmail"]?.Trim() ?? "noreply@ladoapp.com";
            _fromName = configuration["Mailjet:FromName"]?.Trim() ?? "Lado";

            // Log para diagnóstico
            _logger.LogInformation("Mailjet Config - ApiKey: {ApiKey}***, SecretKey length: {SecretLen}",
                _apiKey.Length > 8 ? _apiKey.Substring(0, 8) : "EMPTY",
                _secretKey.Length);
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_secretKey))
                {
                    _logger.LogWarning("Mailjet credentials not configured");
                    return false;
                }

                var client = new MailjetClient(_apiKey, _secretKey);

                // Usar API v3.1 con la sintaxis correcta
                var request = new MailjetRequest
                {
                    Resource = SendV31.Resource
                }
                .Property("Messages", new JArray
                {
                    new JObject
                    {
                        { "From", new JObject
                            {
                                { "Email", _fromEmail },
                                { "Name", _fromName }
                            }
                        },
                        { "To", new JArray
                            {
                                new JObject
                                {
                                    { "Email", toEmail },
                                    { "Name", toEmail.Split('@')[0] }
                                }
                            }
                        },
                        { "Subject", subject },
                        { "HTMLPart", htmlBody },
                        { "TextPart", StripHtml(htmlBody) }
                    }
                });

                _logger.LogInformation("Mailjet: Enviando email a {Email} desde {From}", toEmail, _fromEmail);

                var response = await client.PostAsync(request);

                _logger.LogInformation("Mailjet Response: StatusCode={StatusCode}, IsSuccess={IsSuccess}",
                    response.StatusCode, response.IsSuccessStatusCode);

                if (response.IsSuccessStatusCode)
                {
                    // Log de la respuesta completa para diagnóstico
                    var responseData = response.GetData();
                    _logger.LogInformation("Email enviado exitosamente a {Email}. Response: {Response}",
                        toEmail, responseData?.ToString() ?? "null");

                    // Verificar si realmente se envió
                    if (responseData != null)
                    {
                        try
                        {
                            var messages = responseData["Messages"];
                            if (messages != null)
                            {
                                foreach (var msg in messages)
                                {
                                    var status = msg["Status"]?.ToString();
                                    var msgId = msg["MessageID"]?.ToString() ?? msg["MessageUUID"]?.ToString();
                                    _logger.LogInformation("Mailjet Message Status: {Status}, MessageID: {MessageId}",
                                        status, msgId);
                                }
                            }
                        }
                        catch { /* Ignorar errores de parsing */ }
                    }

                    return true;
                }
                else
                {
                    var errorInfo = response.GetErrorInfo();
                    var errorMessage = response.GetErrorMessage();
                    var rawData = response.GetData();
                    var data = rawData?.ToString() ?? "null";

                    // Log más detallado del error
                    _logger.LogError("Error Mailjet enviando email a {To}: StatusCode={StatusCode}, ErrorInfo={ErrorInfo}, ErrorMessage={ErrorMessage}, RawResponse={Data}",
                        toEmail, (int)response.StatusCode, errorInfo, errorMessage, data);

                    // Si hay mensajes de error en la respuesta, extraerlos
                    if (rawData != null)
                    {
                        try
                        {
                            var messages = rawData["Messages"];
                            if (messages != null)
                            {
                                foreach (var msg in messages)
                                {
                                    var status = msg["Status"]?.ToString();
                                    var errors = msg["Errors"];
                                    if (errors != null)
                                    {
                                        foreach (var err in errors)
                                        {
                                            _logger.LogError("Mailjet Error Detail: Status={Status}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMsg}",
                                                status, err["ErrorCode"]?.ToString(), err["ErrorMessage"]?.ToString());
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Ignorar errores de parsing */ }
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepcion al enviar email a {Email}", toEmail);
                return false;
            }
        }

        public async Task<EmailResult> SendEmailWithResultAsync(string toEmail, string subject, string htmlBody)
        {
            // Usar método Legacy que da más información de errores
            return await SendEmailWithResultAsyncLegacy(toEmail, subject, htmlBody);
        }

        /// <summary>
        /// Envía email usando SMTP de Mailjet
        /// </summary>
        private async Task<EmailResult> SendEmailViaSMTPAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_secretKey))
                {
                    return EmailResult.Fail("Credenciales de Mailjet no configuradas.", 0);
                }

                _logger.LogInformation("Mailjet SMTP: Enviando email a {Email} desde {From}", toEmail, _fromEmail);

                using var smtpClient = new SmtpClient("in-v3.mailjet.com", 587)
                {
                    Credentials = new NetworkCredential(_apiKey, _secretKey),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(new MailAddress(toEmail));

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Email enviado exitosamente via SMTP a {Email}", toEmail);
                return EmailResult.Ok();
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, "Error SMTP al enviar email a {Email}: {StatusCode} - {Message}",
                    toEmail, smtpEx.StatusCode, smtpEx.Message);
                return EmailResult.Fail($"Error SMTP: {smtpEx.Message}", (int)smtpEx.StatusCode, smtpEx.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepcion al enviar email via SMTP a {Email}", toEmail);
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        /// <summary>
        /// Envía email usando la API de Mailjet v3.1
        /// </summary>
        private async Task<EmailResult> SendEmailViaAPIAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                _logger.LogInformation("========== MAILJET API v3.1 - INICIO ==========");
                _logger.LogInformation("Config: ApiKey={ApiKeyPrefix}***, FromEmail={From}, FromName={FromName}",
                    _apiKey.Length > 8 ? _apiKey.Substring(0, 8) : "???", _fromEmail, _fromName);

                if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_secretKey))
                {
                    _logger.LogError("ERROR: Credenciales de Mailjet no configuradas");
                    return EmailResult.Fail("Credenciales de Mailjet no configuradas. Verifica ApiKey y SecretKey.", 0);
                }

                var client = new MailjetClient(_apiKey, _secretKey);

                // Usar TransactionalEmailBuilder (forma recomendada para API v3.1)
                _logger.LogInformation("Construyendo email transaccional...");
                _logger.LogInformation("  From: {FromEmail} ({FromName})", _fromEmail, _fromName);
                _logger.LogInformation("  To: {ToEmail}", toEmail);
                _logger.LogInformation("  Subject: {Subject}", subject);
                _logger.LogInformation("  HTML Length: {Length} chars", htmlBody?.Length ?? 0);

                var email = new TransactionalEmailBuilder()
                    .WithFrom(new SendContact(_fromEmail, _fromName))
                    .WithSubject(subject)
                    .WithHtmlPart(htmlBody)
                    .WithTextPart(StripHtml(htmlBody))
                    .WithTo(new SendContact(toEmail, toEmail.Split('@')[0]))
                    .Build();

                _logger.LogInformation("Email construido. Enviando a Mailjet API...");

                try
                {
                    var response = await client.SendTransactionalEmailAsync(email);

                    _logger.LogInformation("Respuesta recibida de Mailjet");

                    if (response == null)
                    {
                        _logger.LogError("ERROR: Response es NULL");
                        return EmailResult.Fail("Mailjet devolvió respuesta nula", 0);
                    }

                    _logger.LogInformation("  Messages Count: {Count}", response.Messages?.Length ?? 0);

                    // Log detallado de la respuesta
                    if (response.Messages != null && response.Messages.Length > 0)
                    {
                        for (int i = 0; i < response.Messages.Length; i++)
                        {
                            var msg = response.Messages[i];
                            _logger.LogInformation("  Message[{Index}]:", i);
                            _logger.LogInformation("    Status: {Status}", msg.Status);

                            if (msg.Errors != null && msg.Errors.Count > 0)
                            {
                                var errorDetails = new System.Text.StringBuilder();
                                foreach (var err in msg.Errors)
                                {
                                    _logger.LogError("    Error: {Code} - {Message}", err.ErrorCode, err.ErrorMessage);
                                    errorDetails.AppendLine($"{err.ErrorCode}: {err.ErrorMessage}");
                                }
                                return EmailResult.Fail(msg.Errors[0].ErrorMessage, 0, errorDetails.ToString());
                            }

                            if (msg.Status == "success")
                            {
                                _logger.LogInformation("========== EMAIL ENVIADO EXITOSAMENTE ==========");
                                return EmailResult.Ok();
                            }
                        }
                    }

                    _logger.LogWarning("No se recibieron mensajes en la respuesta");
                    return EmailResult.Fail("No se recibió respuesta de Mailjet", 0);
                }
                catch (Exception apiEx)
                {
                    _logger.LogError(apiEx, "ERROR al llamar SendTransactionalEmailAsync");
                    return EmailResult.Fail($"Error API Mailjet: {apiEx.Message}", 0, apiEx.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EXCEPCION al enviar email via API a {Email}", toEmail);
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

        // Método antiguo mantenido para compatibilidad - usa MailjetRequest directamente
        public async Task<EmailResult> SendEmailWithResultAsyncLegacy(string toEmail, string subject, string htmlBody)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_secretKey))
                {
                    return EmailResult.Fail("Credenciales de Mailjet no configuradas. Verifica ApiKey y SecretKey.", 0);
                }

                var client = new MailjetClient(_apiKey, _secretKey);

                var request = new MailjetRequest
                {
                    Resource = SendV31.Resource
                }
                .Property("Messages", new JArray
                {
                    new JObject
                    {
                        { "From", new JObject
                            {
                                { "Email", _fromEmail },
                                { "Name", _fromName }
                            }
                        },
                        { "To", new JArray
                            {
                                new JObject
                                {
                                    { "Email", toEmail },
                                    { "Name", toEmail.Split('@')[0] }
                                }
                            }
                        },
                        { "Subject", subject },
                        { "HTMLPart", htmlBody },
                        { "TextPart", StripHtml(htmlBody) }
                    }
                });

                _logger.LogInformation("Mailjet Legacy: Enviando email a {Email} desde {From}", toEmail, _fromEmail);

                var response = await client.PostAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = response.GetData();
                    _logger.LogInformation("Email enviado exitosamente a {Email}. Response: {Response}",
                        toEmail, responseData?.ToString() ?? "null");
                    return EmailResult.Ok();
                }
                else
                {
                    var errorInfo = response.GetErrorInfo();
                    var errorMessage = response.GetErrorMessage();
                    var rawData = response.GetData();
                    var statusCode = (int)response.StatusCode;

                    // Construir mensaje de error detallado
                    var errorDetails = new System.Text.StringBuilder();
                    errorDetails.AppendLine($"StatusCode: {statusCode}");
                    errorDetails.AppendLine($"ErrorInfo: {errorInfo}");
                    errorDetails.AppendLine($"ErrorMessage: {errorMessage}");

                    string mainError = $"Error HTTP {statusCode}";

                    // Extraer errores específicos de Mailjet
                    if (rawData != null)
                    {
                        try
                        {
                            var messages = rawData["Messages"];
                            if (messages != null)
                            {
                                foreach (var msg in messages)
                                {
                                    var status = msg["Status"]?.ToString();
                                    var errors = msg["Errors"];
                                    if (errors != null)
                                    {
                                        foreach (var err in errors)
                                        {
                                            var errCode = err["ErrorCode"]?.ToString();
                                            var errMsg = err["ErrorMessage"]?.ToString();
                                            var errRelated = err["ErrorRelatedTo"]?.ToString();

                                            mainError = $"[{errCode}] {errMsg}";
                                            errorDetails.AppendLine($"ErrorCode: {errCode}");
                                            errorDetails.AppendLine($"ErrorMessage: {errMsg}");
                                            if (!string.IsNullOrEmpty(errRelated))
                                                errorDetails.AppendLine($"RelatedTo: {errRelated}");
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Ignorar errores de parsing */ }

                        errorDetails.AppendLine($"RawResponse: {rawData}");
                    }

                    _logger.LogError("Error Mailjet: {Error}", mainError);

                    return EmailResult.Fail(mainError, statusCode, errorDetails.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepcion al enviar email a {Email}", toEmail);
                return EmailResult.Fail($"Excepción: {ex.Message}", 0, ex.ToString());
            }
        }

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
            <p style='color:#999;font-size:14px;margin-top:20px;'>
                Si el boton no funciona, copia y pega este enlace en tu navegador:<br>
                <span style='color:#4682B4;word-break:break-all;'>{{{{link}}}}</span>
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
            <p style='color:#999;font-size:14px;margin-top:20px;'>
                Si el boton no funciona, copia y pega este enlace en tu navegador:<br>
                <span style='color:#4682B4;word-break:break-all;'>{{{{link}}}}</span>
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

                _ => "<p>{{content}}</p>"
            };

            // Replace variables
            foreach (var variable in variables)
            {
                template = template.Replace($"{{{{{variable.Key}}}}}", variable.Value);
            }

            return template;
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ")
                .Replace("  ", " ")
                .Trim();
        }
    }
}
