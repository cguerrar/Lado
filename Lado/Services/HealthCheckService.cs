using Lado.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net.Http;

namespace Lado.Services;

public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckAllAsync();
    Task<ComponentHealth> CheckDatabaseAsync();
    Task<ComponentHealth> CheckStorageAsync();
    Task<ComponentHealth> CheckPayPalAsync();
    Task<ComponentHealth> CheckEmailAsync();
    Task<ComponentHealth> CheckGiphyAsync();
}

public class HealthCheckResult
{
    public bool IsHealthy => Components.All(c => c.Value.Status != HealthStatus.Down);
    public bool IsDegraded => Components.Any(c => c.Value.Status == HealthStatus.Degraded);
    public HealthStatus OverallStatus => IsHealthy ? (IsDegraded ? HealthStatus.Degraded : HealthStatus.Healthy) : HealthStatus.Down;
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Uptime { get; set; }
    public string Version { get; set; } = "1.0.0";
}

public class ComponentHealth
{
    public string Name { get; set; } = "";
    public HealthStatus Status { get; set; }
    public string? Message { get; set; }
    public long? ResponseTimeMs { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Down,
    Unknown
}

public class HealthCheckService : IHealthCheckService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HealthCheckService> _logger;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public HealthCheckService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment,
        ILogger<HealthCheckService> logger)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckAllAsync()
    {
        var result = new HealthCheckResult
        {
            Uptime = DateTime.UtcNow - _startTime,
            Version = typeof(HealthCheckService).Assembly.GetName().Version?.ToString() ?? "1.0.0"
        };

        // Ejecutar todas las verificaciones en paralelo
        var tasks = new List<Task<(string Key, ComponentHealth Health)>>
        {
            CheckComponentAsync("database", CheckDatabaseAsync),
            CheckComponentAsync("storage", CheckStorageAsync),
            CheckComponentAsync("paypal", CheckPayPalAsync),
            CheckComponentAsync("email", CheckEmailAsync),
            CheckComponentAsync("giphy", CheckGiphyAsync)
        };

        var results = await Task.WhenAll(tasks);

        foreach (var (key, health) in results)
        {
            result.Components[key] = health;
        }

        return result;
    }

    private async Task<(string Key, ComponentHealth Health)> CheckComponentAsync(
        string key, Func<Task<ComponentHealth>> checkFunc)
    {
        try
        {
            var health = await checkFunc();
            return (key, health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health of {Component}", key);
            return (key, new ComponentHealth
            {
                Name = key,
                Status = HealthStatus.Down,
                Message = "Error al verificar"
            });
        }
    }

    public async Task<ComponentHealth> CheckDatabaseAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Verificar conexión a la base de datos
            var canConnect = await _context.Database.CanConnectAsync();
            sw.Stop();

            if (canConnect)
            {
                // Verificar que podemos hacer una query simple
                var userCount = await _context.Users.CountAsync();

                return new ComponentHealth
                {
                    Name = "Base de Datos",
                    Status = sw.ElapsedMilliseconds < 1000 ? HealthStatus.Healthy : HealthStatus.Degraded,
                    Message = sw.ElapsedMilliseconds < 1000 ? "Operacional" : "Respuesta lenta",
                    ResponseTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new ComponentHealth
            {
                Name = "Base de Datos",
                Status = HealthStatus.Down,
                Message = "No se puede conectar",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Database health check failed");
            return new ComponentHealth
            {
                Name = "Base de Datos",
                Status = HealthStatus.Down,
                Message = "Error de conexión",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<ComponentHealth> CheckStorageAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads");

            // Verificar que el directorio existe
            if (!Directory.Exists(uploadsPath))
            {
                return new ComponentHealth
                {
                    Name = "Almacenamiento",
                    Status = HealthStatus.Down,
                    Message = "Directorio no existe"
                };
            }

            // Verificar permisos de escritura
            var testFile = Path.Combine(uploadsPath, $"health_check_{Guid.NewGuid()}.tmp");
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);
            sw.Stop();

            // Verificar espacio disponible
            var driveInfo = new DriveInfo(Path.GetPathRoot(uploadsPath) ?? "C:");
            var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

            var status = freeSpaceGB > 10 ? HealthStatus.Healthy :
                         freeSpaceGB > 2 ? HealthStatus.Degraded : HealthStatus.Down;

            return new ComponentHealth
            {
                Name = "Almacenamiento",
                Status = status,
                Message = $"{freeSpaceGB:F1} GB disponibles",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Storage health check failed");
            return new ComponentHealth
            {
                Name = "Almacenamiento",
                Status = HealthStatus.Down,
                Message = "Sin permisos de escritura",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<ComponentHealth> CheckPayPalAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var clientId = _configuration["PayPal:ClientId"];
            var clientSecret = _configuration["PayPal:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return new ComponentHealth
                {
                    Name = "PayPal",
                    Status = HealthStatus.Unknown,
                    Message = "No configurado"
                };
            }

            // Solo verificar que podemos alcanzar PayPal (no autenticar)
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync("https://www.paypal.com/");
            sw.Stop();

            return new ComponentHealth
            {
                Name = "PayPal",
                Status = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Degraded,
                Message = response.IsSuccessStatusCode ? "Operacional" : "Problemas de conectividad",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "PayPal health check failed");
            return new ComponentHealth
            {
                Name = "PayPal",
                Status = HealthStatus.Degraded,
                Message = "No se puede verificar",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<ComponentHealth> CheckEmailAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var mailjetApiKey = _configuration["Mailjet:ApiKey"];
            var sesAccessKey = _configuration["AWS:SES:AccessKey"];

            if (string.IsNullOrEmpty(mailjetApiKey) && string.IsNullOrEmpty(sesAccessKey))
            {
                return new ComponentHealth
                {
                    Name = "Email",
                    Status = HealthStatus.Unknown,
                    Message = "No configurado"
                };
            }

            // Verificar conectividad a Mailjet
            if (!string.IsNullOrEmpty(mailjetApiKey))
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.GetAsync("https://api.mailjet.com/");
                sw.Stop();

                return new ComponentHealth
                {
                    Name = "Email (Mailjet)",
                    Status = HealthStatus.Healthy,
                    Message = "Operacional",
                    ResponseTimeMs = sw.ElapsedMilliseconds
                };
            }

            sw.Stop();
            return new ComponentHealth
            {
                Name = "Email (AWS SES)",
                Status = HealthStatus.Healthy,
                Message = "Configurado",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Email health check failed");
            return new ComponentHealth
            {
                Name = "Email",
                Status = HealthStatus.Degraded,
                Message = "No se puede verificar",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<ComponentHealth> CheckGiphyAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var apiKey = _configuration["Giphy:ApiKey"];

            if (string.IsNullOrEmpty(apiKey))
            {
                return new ComponentHealth
                {
                    Name = "Giphy",
                    Status = HealthStatus.Unknown,
                    Message = "No configurado"
                };
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"https://api.giphy.com/v1/gifs/trending?api_key={apiKey}&limit=1");
            sw.Stop();

            return new ComponentHealth
            {
                Name = "Giphy",
                Status = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Degraded,
                Message = response.IsSuccessStatusCode ? "Operacional" : "Problemas de API",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Giphy health check failed");
            return new ComponentHealth
            {
                Name = "Giphy",
                Status = HealthStatus.Degraded,
                Message = "No se puede verificar",
                ResponseTimeMs = sw.ElapsedMilliseconds
            };
        }
    }
}
