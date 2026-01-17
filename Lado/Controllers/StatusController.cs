using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lado.Controllers;

/// <summary>
/// Controlador público para mostrar el estado del sistema
/// </summary>
[AllowAnonymous]
public class StatusController : Controller
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<StatusController> _logger;

    public StatusController(
        IHealthCheckService healthCheckService,
        ILogger<StatusController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Página pública de estado del sistema
    /// </summary>
    [HttpGet]
    [Route("status")]
    [ResponseCache(Duration = 30)] // Cache de 30 segundos
    public async Task<IActionResult> Index()
    {
        try
        {
            var result = await _healthCheckService.CheckAllAsync();
            return View(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estado del sistema");
            return View(new HealthCheckResult
            {
                Components = new Dictionary<string, ComponentHealth>
                {
                    ["error"] = new ComponentHealth
                    {
                        Name = "Sistema",
                        Status = HealthStatus.Down,
                        Message = "Error al verificar estado"
                    }
                }
            });
        }
    }

    /// <summary>
    /// API JSON para integraciones externas
    /// </summary>
    [HttpGet]
    [Route("api/status")]
    [Route("status/json")]
    [Produces("application/json")]
    [ResponseCache(Duration = 30)]
    public async Task<IActionResult> Json()
    {
        try
        {
            var result = await _healthCheckService.CheckAllAsync();

            // Devolver código HTTP apropiado según el estado
            var statusCode = result.OverallStatus switch
            {
                HealthStatus.Healthy => 200,
                HealthStatus.Degraded => 200,
                HealthStatus.Down => 503,
                _ => 200
            };

            return StatusCode(statusCode, new
            {
                status = result.OverallStatus.ToString().ToLower(),
                version = result.Version,
                uptime = FormatUptime(result.Uptime),
                uptimeSeconds = (int)result.Uptime.TotalSeconds,
                checkedAt = result.CheckedAt,
                components = result.Components.Select(c => new
                {
                    name = c.Value.Name,
                    key = c.Key,
                    status = c.Value.Status.ToString().ToLower(),
                    message = c.Value.Message,
                    responseTimeMs = c.Value.ResponseTimeMs
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en API de estado");
            return StatusCode(503, new
            {
                status = "down",
                error = "Error al verificar estado del sistema"
            });
        }
    }

    /// <summary>
    /// Endpoint simple para load balancers y monitoring
    /// </summary>
    [HttpGet]
    [Route("health")]
    [Route("healthz")]
    [Produces("text/plain")]
    public async Task<IActionResult> Health()
    {
        try
        {
            var dbHealth = await _healthCheckService.CheckDatabaseAsync();

            if (dbHealth.Status == HealthStatus.Healthy)
            {
                return Ok("OK");
            }

            return StatusCode(503, "UNHEALTHY");
        }
        catch
        {
            return StatusCode(503, "UNHEALTHY");
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }
        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    /// <summary>
    /// Diagnóstico de cookies y antiforgery token
    /// </summary>
    [HttpGet]
    [Route("status/diagnostico-cookies")]
    [Authorize]
    public IActionResult DiagnosticoCookies()
    {
        var cookies = Request.Cookies.Select(c => new {
            nombre = c.Key,
            tieneValor = !string.IsNullOrEmpty(c.Value),
            longitud = c.Value?.Length ?? 0
        }).ToList();

        var antiforgeryPresente = Request.Cookies.ContainsKey(".Lado.Antiforgery");
        var authPresente = Request.Cookies.ContainsKey(".Lado.Auth");
        var sessionPresente = Request.Cookies.ContainsKey(".Lado.Session");

        return Json(new {
            usuario = User.Identity?.Name,
            autenticado = User.Identity?.IsAuthenticated,
            cookies = cookies,
            antiforgeryPresente,
            authPresente,
            sessionPresente,
            headers = new {
                contentType = Request.ContentType,
                host = Request.Host.Value,
                scheme = Request.Scheme,
                isHttps = Request.IsHttps,
                forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault(),
                forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault()
            }
        });
    }

    /// <summary>
    /// Test de upload SIN antiforgery para diagnóstico
    /// </summary>
    [HttpPost]
    [Route("status/test-upload")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public IActionResult TestUpload(IFormFile? archivo)
    {
        try
        {
            return Json(new {
                success = true,
                archivoRecibido = archivo != null,
                nombre = archivo?.FileName,
                tamano = archivo?.Length,
                contentType = archivo?.ContentType,
                mensaje = archivo != null
                    ? $"Archivo '{archivo.FileName}' recibido correctamente ({archivo.Length / 1024.0:F1} KB)"
                    : "No se recibió ningún archivo"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en test de upload");
            return Json(new { success = false, error = ex.Message });
        }
    }
}
