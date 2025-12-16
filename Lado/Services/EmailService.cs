using System.Net;
using System.Net.Mail;

namespace Lado.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "465");
                var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
                var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
                var fromEmail = _configuration["EmailSettings:FromEmail"];
                var fromName = _configuration["EmailSettings:FromName"] ?? "Lado";
                var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail!, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email enviado exitosamente a {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email a {Email}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            var subject = "Restablecer tu contraseña - Lado";
            var htmlBody = $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; background-color: #f5f5f5;'>
    <table role='presentation' style='width: 100%; border-collapse: collapse;'>
        <tr>
            <td align='center' style='padding: 40px 0;'>
                <table role='presentation' style='width: 100%; max-width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='padding: 40px 40px 30px; text-align: center; background: linear-gradient(135deg, #4682B4 0%, #2563EB 100%); border-radius: 12px 12px 0 0;'>
                            <h1 style='margin: 0; color: #ffffff; font-size: 32px; font-weight: 700;'>LADO</h1>
                            <p style='margin: 10px 0 0; color: rgba(255,255,255,0.9); font-size: 14px;'>Tu plataforma de contenido</p>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            <h2 style='margin: 0 0 20px; color: #1e293b; font-size: 24px; font-weight: 600;'>Restablecer Contraseña</h2>

                            <p style='margin: 0 0 20px; color: #64748b; font-size: 16px; line-height: 1.6;'>
                                Hola <strong style='color: #1e293b;'>{userName}</strong>,
                            </p>

                            <p style='margin: 0 0 30px; color: #64748b; font-size: 16px; line-height: 1.6;'>
                                Recibimos una solicitud para restablecer la contraseña de tu cuenta en Lado. Si no realizaste esta solicitud, puedes ignorar este correo.
                            </p>

                            <table role='presentation' style='width: 100%; border-collapse: collapse;'>
                                <tr>
                                    <td align='center'>
                                        <a href='{resetLink}' style='display: inline-block; padding: 16px 40px; background: linear-gradient(135deg, #4682B4 0%, #2563EB 100%); color: #ffffff; text-decoration: none; font-size: 16px; font-weight: 600; border-radius: 8px; box-shadow: 0 4px 14px rgba(37, 99, 235, 0.4);'>
                                            Restablecer Contraseña
                                        </a>
                                    </td>
                                </tr>
                            </table>

                            <p style='margin: 30px 0 0; color: #94a3b8; font-size: 14px; line-height: 1.6;'>
                                Si el boton no funciona, copia y pega el siguiente enlace en tu navegador:
                            </p>
                            <p style='margin: 10px 0 0; color: #2563EB; font-size: 14px; word-break: break-all;'>
                                {resetLink}
                            </p>

                            <hr style='margin: 30px 0; border: none; border-top: 1px solid #e2e8f0;'>

                            <p style='margin: 0; color: #94a3b8; font-size: 13px; line-height: 1.6;'>
                                <strong>Importante:</strong> Este enlace expirara en 24 horas por razones de seguridad.
                            </p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style='padding: 30px 40px; background-color: #f8fafc; border-radius: 0 0 12px 12px; text-align: center;'>
                            <p style='margin: 0 0 10px; color: #64748b; font-size: 14px;'>
                                ¿Necesitas ayuda? Contactanos en <a href='mailto:soporte@ladoapp.com' style='color: #2563EB; text-decoration: none;'>soporte@ladoapp.com</a>
                            </p>
                            <p style='margin: 0; color: #94a3b8; font-size: 12px;'>
                                &copy; {DateTime.Now.Year} Lado. Todos los derechos reservados.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }
    }
}
