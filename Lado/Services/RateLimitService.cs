using System.Collections.Concurrent;

namespace Lado.Services
{
    /// <summary>
    /// Servicio de Rate Limiting en memoria para prevenir abuso
    /// </summary>
    public interface IRateLimitService
    {
        bool IsAllowed(string key, int maxRequests, TimeSpan window);
        int GetRemainingRequests(string key, int maxRequests, TimeSpan window);
        void Reset(string key);
    }

    public class RateLimitService : IRateLimitService
    {
        private readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new();
        private readonly ILogger<RateLimitService> _logger;

        public RateLimitService(ILogger<RateLimitService> logger)
        {
            _logger = logger;
        }

        public bool IsAllowed(string key, int maxRequests, TimeSpan window)
        {
            var now = DateTime.UtcNow;
            var entry = _entries.AddOrUpdate(
                key,
                _ => new RateLimitEntry { Count = 1, WindowStart = now },
                (_, existing) =>
                {
                    // Si la ventana expiró, reiniciar
                    if (now - existing.WindowStart > window)
                    {
                        return new RateLimitEntry { Count = 1, WindowStart = now };
                    }
                    // Incrementar contador
                    existing.Count++;
                    return existing;
                });

            var allowed = entry.Count <= maxRequests;

            if (!allowed)
            {
                _logger.LogWarning("🚫 RATE LIMIT EXCEDIDO - Key: {Key}, Count: {Count}, Max: {Max}, Window: {Window}min",
                    key, entry.Count, maxRequests, window.TotalMinutes);
            }

            return allowed;
        }

        public int GetRemainingRequests(string key, int maxRequests, TimeSpan window)
        {
            if (!_entries.TryGetValue(key, out var entry))
                return maxRequests;

            if (DateTime.UtcNow - entry.WindowStart > window)
                return maxRequests;

            return Math.Max(0, maxRequests - entry.Count);
        }

        public void Reset(string key)
        {
            _entries.TryRemove(key, out _);
        }

        private class RateLimitEntry
        {
            public int Count { get; set; }
            public DateTime WindowStart { get; set; }
        }
    }

    /// <summary>
    /// Límites predefinidos para diferentes acciones
    /// </summary>
    public static class RateLimits
    {
        // Crear contenido: máximo 10 por cada 5 minutos
        public const int ContentCreation_MaxRequests = 10;
        public static readonly TimeSpan ContentCreation_Window = TimeSpan.FromMinutes(5);

        // Crear contenido: máximo 50 por hora
        public const int ContentCreation_Hourly_MaxRequests = 50;
        public static readonly TimeSpan ContentCreation_Hourly_Window = TimeSpan.FromHours(1);

        // Crear contenido: máximo 100 por día
        public const int ContentCreation_Daily_MaxRequests = 100;
        public static readonly TimeSpan ContentCreation_Daily_Window = TimeSpan.FromHours(24);

        // Mensajes: máximo 30 por minuto
        public const int Messaging_MaxRequests = 30;
        public static readonly TimeSpan Messaging_Window = TimeSpan.FromMinutes(1);

        // Login: máximo 5 intentos por 15 minutos
        public const int Login_MaxRequests = 5;
        public static readonly TimeSpan Login_Window = TimeSpan.FromMinutes(15);

        // Rate limit por IP (para detectar ataques multi-cuenta)
        // Una IP no debería crear más de 20 contenidos por 5 minutos
        public const int ContentCreation_IP_MaxRequests = 20;
        public static readonly TimeSpan ContentCreation_IP_Window = TimeSpan.FromMinutes(5);

        // Una IP no debería crear más de 100 contenidos por hora
        public const int ContentCreation_IP_Hourly_MaxRequests = 100;
        public static readonly TimeSpan ContentCreation_IP_Hourly_Window = TimeSpan.FromHours(1);
    }
}
