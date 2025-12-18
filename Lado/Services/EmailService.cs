using Mailjet.Client;
using Mailjet.Client.Resources;
using Newtonsoft.Json.Linq;

namespace Lado.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// Envia un email generico
        /// </summary>
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);

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
            _apiKey = configuration["Mailjet:ApiKey"] ?? "";
            _secretKey = configuration["Mailjet:SecretKey"] ?? "";
            _fromEmail = configuration["Mailjet:FromEmail"] ?? "noreply@ladoapp.com";
            _fromName = configuration["Mailjet:FromName"] ?? "Lado";
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

                var request = new MailjetRequest
                {
                    Resource = Send.Resource
                }
                .Property(Send.FromEmail, _fromEmail)
                .Property(Send.FromName, _fromName)
                .Property(Send.Subject, subject)
                .Property(Send.HtmlPart, htmlBody)
                .Property(Send.TextPart, StripHtml(htmlBody))
                .Property(Send.Recipients, new JArray
                {
                    new JObject
                    {
                        { "Email", toEmail },
                        { "Name", toEmail.Split('@')[0] }
                    }
                });

                var response = await client.PostAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email enviado exitosamente a {Email}", toEmail);
                    return true;
                }
                else
                {
                    _logger.LogError("Error enviando email: {StatusCode} - {ErrorInfo}",
                        response.StatusCode, response.GetErrorInfo());
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepcion al enviar email a {Email}", toEmail);
                return false;
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
