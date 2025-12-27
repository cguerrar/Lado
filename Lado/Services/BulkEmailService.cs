using System.Text.Json;
using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Resultado de una operación de envío masivo
    /// </summary>
    public class BulkEmailResult
    {
        public bool Success { get; set; }
        public int TotalEnviados { get; set; }
        public int TotalFallidos { get; set; }
        public List<string> Errores { get; set; } = new();
        public string? Mensaje { get; set; }
    }

    /// <summary>
    /// Interfaz para el servicio de email masivo
    /// </summary>
    public interface IBulkEmailService
    {
        Task<int> ContarDestinatariosAsync(TipoDestinatarioEmail tipo, string? emailsEspecificos = null);
        Task<List<(string Email, string Nombre, string Usuario)>> ObtenerDestinatariosAsync(TipoDestinatarioEmail tipo, string? emailsEspecificos = null);
        Task<BulkEmailResult> EnviarCampanaAsync(int campanaId, CancellationToken cancellationToken = default);
        string ReemplazarPlaceholders(string contenido, string nombre, string email, string usuario);
    }

    /// <summary>
    /// Servicio para envío de emails masivos
    /// </summary>
    public class BulkEmailService : IBulkEmailService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<BulkEmailService> _logger;
        private readonly ILogEventoService _logService;

        // Configuración de batches
        private const int BATCH_SIZE = 50;
        private const int DELAY_BETWEEN_BATCHES_MS = 1000;
        private const int DELAY_BETWEEN_EMAILS_MS = 100;

        public BulkEmailService(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<BulkEmailService> logger,
            ILogEventoService logService)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _logService = logService;
        }

        /// <summary>
        /// Cuenta los destinatarios según el tipo seleccionado
        /// </summary>
        public async Task<int> ContarDestinatariosAsync(TipoDestinatarioEmail tipo, string? emailsEspecificos = null)
        {
            // Para emails específicos, contar los emails ingresados directamente
            if (tipo == TipoDestinatarioEmail.EmailsEspecificos && !string.IsNullOrEmpty(emailsEspecificos))
            {
                var emails = ParsearListaEmails(emailsEspecificos);
                return emails.Count;
            }

            var query = ObtenerQueryDestinatarios(tipo, emailsEspecificos);
            return await query.CountAsync();
        }

        /// <summary>
        /// Obtiene la lista de destinatarios según el tipo seleccionado
        /// </summary>
        public async Task<List<(string Email, string Nombre, string Usuario)>> ObtenerDestinatariosAsync(
            TipoDestinatarioEmail tipo, string? emailsEspecificos = null)
        {
            // Para emails específicos, usar los emails ingresados directamente
            if (tipo == TipoDestinatarioEmail.EmailsEspecificos && !string.IsNullOrEmpty(emailsEspecificos))
            {
                var emailsList = ParsearListaEmails(emailsEspecificos);
                var resultado = new List<(string Email, string Nombre, string Usuario)>();

                // Buscar datos de usuarios que existan en BD
                var usuariosExistentes = await _context.Users
                    .Where(u => emailsList.Contains(u.Email!.ToLower()))
                    .Select(u => new { Email = u.Email!.ToLower(), u.NombreCompleto, u.UserName })
                    .ToDictionaryAsync(u => u.Email, u => (u.NombreCompleto, u.UserName ?? ""));

                // Construir lista con datos de BD o genéricos
                foreach (var email in emailsList)
                {
                    if (usuariosExistentes.TryGetValue(email, out var userData))
                    {
                        resultado.Add((email, userData.NombreCompleto, userData.Item2));
                    }
                    else
                    {
                        // Email externo: usar parte antes del @ como nombre
                        var nombreGenerico = email.Split('@')[0];
                        nombreGenerico = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                            .ToTitleCase(nombreGenerico.Replace(".", " ").Replace("_", " "));
                        resultado.Add((email, nombreGenerico, ""));
                    }
                }

                return resultado;
            }

            var query = ObtenerQueryDestinatarios(tipo, emailsEspecificos);

            return await query
                .Select(u => new { u.Email, u.NombreCompleto, u.UserName })
                .ToListAsync()
                .ContinueWith(t => t.Result
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .Select(u => (u.Email!, u.NombreCompleto, u.UserName ?? ""))
                    .ToList());
        }

        /// <summary>
        /// Parsea una lista de emails desde texto
        /// </summary>
        private List<string> ParsearListaEmails(string emailsTexto)
        {
            try
            {
                var listaEmails = JsonSerializer.Deserialize<List<string>>(emailsTexto);
                if (listaEmails != null && listaEmails.Any())
                {
                    return listaEmails.Select(e => e.Trim().ToLowerInvariant()).Where(e => e.Contains('@')).ToList();
                }
            }
            catch { }

            // Si no es JSON, tratar como lista separada por comas/líneas
            return emailsTexto
                .Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLowerInvariant())
                .Where(e => e.Contains('@') && e.Length > 3)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Construye la query base de destinatarios según el tipo
        /// </summary>
        private IQueryable<ApplicationUser> ObtenerQueryDestinatarios(
            TipoDestinatarioEmail tipo, string? emailsEspecificos)
        {
            // Query base: usuarios con email válido
            // Nota: El filtro RecibirEmailsMarketing se puede agregar después cuando los usuarios lo configuren
            var query = _context.Users
                .Where(u => u.Email != null && u.Email != "");

            switch (tipo)
            {
                case TipoDestinatarioEmail.Creadores:
                    query = query.Where(u => u.EsCreador);
                    break;

                case TipoDestinatarioEmail.Fans:
                    query = query.Where(u => !u.EsCreador);
                    break;

                case TipoDestinatarioEmail.Activos:
                    var hace30Dias = DateTime.Now.AddDays(-30);
                    query = query.Where(u => u.UltimaActividad != null && u.UltimaActividad > hace30Dias);
                    break;

                case TipoDestinatarioEmail.EmailsEspecificos:
                    if (!string.IsNullOrEmpty(emailsEspecificos))
                    {
                        try
                        {
                            var listaEmails = JsonSerializer.Deserialize<List<string>>(emailsEspecificos);
                            if (listaEmails != null && listaEmails.Any())
                            {
                                query = query.Where(u => listaEmails.Contains(u.Email!));
                            }
                        }
                        catch
                        {
                            // Si no es JSON, tratar como lista separada por comas/líneas
                            var emails = emailsEspecificos
                                .Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(e => e.Trim().ToLowerInvariant())
                                .Where(e => e.Contains('@'))
                                .ToList();

                            if (emails.Any())
                            {
                                query = query.Where(u => emails.Contains(u.Email!.ToLower()));
                            }
                        }
                    }
                    break;

                case TipoDestinatarioEmail.Todos:
                default:
                    // Sin filtro adicional
                    break;
            }

            return query;
        }

        /// <summary>
        /// Ejecuta el envío de una campaña de email
        /// </summary>
        public async Task<BulkEmailResult> EnviarCampanaAsync(int campanaId, CancellationToken cancellationToken = default)
        {
            var result = new BulkEmailResult();

            var campana = await _context.CampanasEmail
                .Include(c => c.CreadoPor)
                .FirstOrDefaultAsync(c => c.Id == campanaId);

            if (campana == null)
            {
                result.Mensaje = "Campaña no encontrada";
                return result;
            }

            if (campana.Estado == EstadoCampanaEmail.Enviada)
            {
                result.Mensaje = "Esta campaña ya fue enviada";
                return result;
            }

            try
            {
                // Marcar como en progreso
                campana.Estado = EstadoCampanaEmail.EnProgreso;
                campana.FechaInicioEnvio = DateTime.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Iniciando envío de campaña {CampanaId}: {Nombre}",
                    campanaId, campana.Nombre);

                // Obtener destinatarios
                var destinatarios = await ObtenerDestinatariosAsync(
                    campana.TipoDestinatario,
                    campana.EmailsEspecificos);

                campana.TotalDestinatarios = destinatarios.Count;
                await _context.SaveChangesAsync();

                if (destinatarios.Count == 0)
                {
                    campana.Estado = EstadoCampanaEmail.Enviada;
                    campana.FechaFinEnvio = DateTime.Now;
                    await _context.SaveChangesAsync();

                    result.Mensaje = "No hay destinatarios para esta campaña";
                    return result;
                }

                _logger.LogInformation("Campaña {CampanaId}: {Count} destinatarios encontrados",
                    campanaId, destinatarios.Count);

                var errores = new List<string>();
                var enviados = 0;
                var fallidos = 0;

                // Procesar en batches
                var batches = destinatarios
                    .Select((dest, index) => new { dest, index })
                    .GroupBy(x => x.index / BATCH_SIZE)
                    .Select(g => g.Select(x => x.dest).ToList())
                    .ToList();

                for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    // Verificar cancelación
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Campaña {CampanaId}: Envío cancelado por el usuario", campanaId);
                        campana.Estado = EstadoCampanaEmail.Cancelada;
                        break;
                    }

                    // Recargar campaña para verificar si fue cancelada externamente
                    var estadoActual = await _context.CampanasEmail
                        .Where(c => c.Id == campanaId)
                        .Select(c => c.Estado)
                        .FirstOrDefaultAsync();

                    if (estadoActual == EstadoCampanaEmail.Cancelada)
                    {
                        _logger.LogWarning("Campaña {CampanaId}: Envío cancelado externamente", campanaId);
                        break;
                    }

                    var batch = batches[batchIndex];

                    _logger.LogDebug("Campaña {CampanaId}: Procesando batch {BatchIndex}/{TotalBatches} ({Count} emails)",
                        campanaId, batchIndex + 1, batches.Count, batch.Count);

                    foreach (var (email, nombre, usuario) in batch)
                    {
                        try
                        {
                            // Personalizar contenido
                            var asuntoPersonalizado = ReemplazarPlaceholders(campana.Asunto, nombre, email, usuario);
                            var contenidoPersonalizado = ReemplazarPlaceholders(campana.ContenidoHtml, nombre, email, usuario);

                            // Enviar email
                            var emailResult = await _emailService.SendEmailWithResultAsync(
                                email,
                                asuntoPersonalizado,
                                contenidoPersonalizado);

                            if (emailResult.Success)
                            {
                                enviados++;
                            }
                            else
                            {
                                fallidos++;
                                errores.Add($"{email}: {emailResult.ErrorMessage}");

                                // Limitar errores guardados
                                if (errores.Count > 100)
                                {
                                    errores.RemoveAt(0);
                                }
                            }

                            // Pequeña pausa entre emails para no saturar
                            await Task.Delay(DELAY_BETWEEN_EMAILS_MS, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            fallidos++;
                            errores.Add($"{email}: {ex.Message}");
                            _logger.LogError(ex, "Error al enviar email a {Email}", email);
                        }
                    }

                    // Actualizar progreso en BD
                    campana.Enviados = enviados;
                    campana.Fallidos = fallidos;
                    await _context.SaveChangesAsync();

                    // Pausa entre batches
                    if (batchIndex < batches.Count - 1)
                    {
                        await Task.Delay(DELAY_BETWEEN_BATCHES_MS, cancellationToken);
                    }
                }

                // Finalizar campaña
                campana.Enviados = enviados;
                campana.Fallidos = fallidos;
                campana.FechaFinEnvio = DateTime.Now;
                campana.DetalleErrores = errores.Any() ? JsonSerializer.Serialize(errores.Take(50)) : null;

                if (campana.Estado != EstadoCampanaEmail.Cancelada)
                {
                    campana.Estado = EstadoCampanaEmail.Enviada;
                }

                await _context.SaveChangesAsync();

                // Log del evento
                await _logService.RegistrarEventoAsync(
                    $"Campaña '{campana.Nombre}' completada: {enviados} enviados, {fallidos} fallidos",
                    Lado.Models.CategoriaEvento.Admin,
                    Lado.Models.TipoLogEvento.Evento,
                    campana.CreadoPorId);

                result.Success = fallidos == 0;
                result.TotalEnviados = enviados;
                result.TotalFallidos = fallidos;
                result.Errores = errores;
                result.Mensaje = $"Envío completado: {enviados} enviados, {fallidos} fallidos";

                _logger.LogInformation(
                    "Campaña {CampanaId} completada: {Enviados} enviados, {Fallidos} fallidos",
                    campanaId, enviados, fallidos);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en campaña {CampanaId}", campanaId);

                campana.Estado = EstadoCampanaEmail.Cancelada;
                campana.FechaFinEnvio = DateTime.Now;
                campana.DetalleErrores = JsonSerializer.Serialize(new[] { $"Error crítico: {ex.Message}" });
                await _context.SaveChangesAsync();

                result.Mensaje = $"Error crítico: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Reemplaza los placeholders en el contenido con los datos del usuario
        /// </summary>
        public string ReemplazarPlaceholders(string contenido, string nombre, string email, string usuario)
        {
            if (string.IsNullOrEmpty(contenido))
                return contenido;

            return contenido
                .Replace("{{nombre}}", nombre)
                .Replace("{{email}}", email)
                .Replace("{{usuario}}", $"@{usuario}")
                .Replace("{{fecha}}", DateTime.Now.ToString("dd/MM/yyyy"))
                .Replace("{{año}}", DateTime.Now.Year.ToString());
        }
    }
}
