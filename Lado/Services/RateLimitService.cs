using System.Collections.Concurrent;
using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

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

        /// <summary>
        /// Verifica rate limit y registra ataque si se excede
        /// </summary>
        Task<bool> IsAllowedAsync(string ip, string key, int maxRequests, TimeSpan window,
            TipoAtaque tipoAtaque, string? endpoint = null, string? usuarioId = null, string? userAgent = null);

        /// <summary>
        /// Verifica si una IP está bloqueada
        /// </summary>
        Task<bool> IsIpBlockedAsync(string ip);

        /// <summary>
        /// Obtiene estadísticas de ataques
        /// </summary>
        Task<AtaqueEstadisticas> GetEstadisticasAsync();
    }

    public class AtaqueEstadisticas
    {
        public int AtaquesHoy { get; set; }
        public int AtaquesSemana { get; set; }
        public int IpsBloqueadasAuto { get; set; }
        public int IpsBloqueadasManual { get; set; }
        public Dictionary<TipoAtaque, int> AtaquesPorTipo { get; set; } = new();
        public List<(string Ip, int Intentos)> TopIpsAtacantes { get; set; } = new();
    }

    public class RateLimitService : IRateLimitService
    {
        private readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new();
        private readonly ConcurrentDictionary<string, int> _violaciones = new(); // Contador de violaciones por IP
        private readonly ILogger<RateLimitService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // Cache de IPs bloqueadas (se refresca cada 5 minutos)
        private HashSet<string> _ipsBloqueadasCache = new();
        private DateTime _ultimoRefrescoCache = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        // Umbral para auto-bloqueo: 5 violaciones en la sesión
        private const int UMBRAL_AUTOBLOQUEO = 5;

        public RateLimitService(ILogger<RateLimitService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public bool IsAllowed(string key, int maxRequests, TimeSpan window)
        {
            var now = DateTime.UtcNow;
            var entry = _entries.AddOrUpdate(
                key,
                _ => new RateLimitEntry { Count = 1, WindowStart = now },
                (_, existing) =>
                {
                    if (now - existing.WindowStart > window)
                    {
                        return new RateLimitEntry { Count = 1, WindowStart = now };
                    }
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

        public async Task<bool> IsAllowedAsync(string ip, string key, int maxRequests, TimeSpan window,
            TipoAtaque tipoAtaque, string? endpoint = null, string? usuarioId = null, string? userAgent = null)
        {
            // Primero verificar si la IP está bloqueada
            if (await IsIpBlockedAsync(ip))
            {
                _logger.LogWarning("🚫 IP BLOQUEADA intentando acceder: {Ip}", ip);
                return false;
            }

            var allowed = IsAllowed(key, maxRequests, window);

            if (!allowed)
            {
                // Registrar el intento de ataque
                await RegistrarIntentoAtaqueAsync(ip, tipoAtaque, endpoint, usuarioId, userAgent);
            }

            return allowed;
        }

        private async Task RegistrarIntentoAtaqueAsync(string ip, TipoAtaque tipoAtaque,
            string? endpoint, string? usuarioId, string? userAgent)
        {
            try
            {
                // Incrementar contador de violaciones
                var violaciones = _violaciones.AddOrUpdate(ip, 1, (_, v) => v + 1);

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Registrar el intento
                var intento = new IntentoAtaque
                {
                    DireccionIp = ip,
                    TipoAtaque = tipoAtaque,
                    Endpoint = endpoint?.Length > 200 ? endpoint[..200] : endpoint,
                    UsuarioId = usuarioId,
                    UserAgent = userAgent?.Length > 100 ? userAgent[..100] : userAgent,
                    ResultoEnBloqueo = violaciones >= UMBRAL_AUTOBLOQUEO
                };

                context.IntentosAtaque.Add(intento);

                // Si excede el umbral, auto-bloquear
                if (violaciones >= UMBRAL_AUTOBLOQUEO)
                {
                    await AutoBloquearIpAsync(context, ip, tipoAtaque, violaciones);
                }

                await context.SaveChangesAsync();

                _logger.LogWarning("⚠️ INTENTO DE ATAQUE #{Violaciones} - IP: {Ip}, Tipo: {Tipo}, Endpoint: {Endpoint}",
                    violaciones, ip, tipoAtaque, endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando intento de ataque para IP: {Ip}", ip);
            }
        }

        private async Task AutoBloquearIpAsync(ApplicationDbContext context, string ip, TipoAtaque tipoAtaque, int violaciones)
        {
            // Verificar si ya está bloqueada
            var existente = await context.IpsBloqueadas
                .FirstOrDefaultAsync(i => i.DireccionIp == ip && i.EstaActivo);

            if (existente != null)
            {
                existente.ViolacionesRateLimit = violaciones;
                existente.UltimoIntento = DateTime.Now;
                existente.IntentosBloqueos++;
                return;
            }

            // Crear nuevo bloqueo automático (expira en 24 horas)
            var bloqueo = new IpBloqueada
            {
                DireccionIp = ip,
                Razon = $"Auto-bloqueo: {violaciones} violaciones de rate limit ({tipoAtaque})",
                TipoBloqueo = TipoBloqueoIp.Automatico,
                TipoAtaque = tipoAtaque,
                ViolacionesRateLimit = violaciones,
                FechaExpiracion = DateTime.Now.AddHours(24),
                EstaActivo = true
            };

            context.IpsBloqueadas.Add(bloqueo);

            // Agregar a cache inmediatamente
            _ipsBloqueadasCache.Add(ip);

            _logger.LogError("🔒 IP AUTO-BLOQUEADA: {Ip} por {Violaciones} violaciones ({Tipo})",
                ip, violaciones, tipoAtaque);
        }

        public async Task<bool> IsIpBlockedAsync(string ip)
        {
            // Refrescar cache si expiró
            if (DateTime.UtcNow - _ultimoRefrescoCache > _cacheExpiration)
            {
                await RefrescarCacheIpsBloqueadasAsync();
            }

            return _ipsBloqueadasCache.Contains(ip);
        }

        private async Task RefrescarCacheIpsBloqueadasAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var ahora = DateTime.Now;
                var ipsBloqueadas = await context.IpsBloqueadas
                    .Where(i => i.EstaActivo && (i.FechaExpiracion == null || i.FechaExpiracion > ahora))
                    .Select(i => i.DireccionIp)
                    .ToListAsync();

                _ipsBloqueadasCache = new HashSet<string>(ipsBloqueadas);
                _ultimoRefrescoCache = DateTime.UtcNow;

                _logger.LogDebug("Cache de IPs bloqueadas refrescado: {Count} IPs", ipsBloqueadas.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refrescando cache de IPs bloqueadas");
            }
        }

        public async Task<AtaqueEstadisticas> GetEstadisticasAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var hoy = DateTime.Today;
                var inicioSemana = hoy.AddDays(-7);

                var stats = new AtaqueEstadisticas
                {
                    AtaquesHoy = await context.IntentosAtaque.CountAsync(i => i.Fecha >= hoy),
                    AtaquesSemana = await context.IntentosAtaque.CountAsync(i => i.Fecha >= inicioSemana),
                    IpsBloqueadasAuto = await context.IpsBloqueadas.CountAsync(i => i.EstaActivo && i.TipoBloqueo == TipoBloqueoIp.Automatico),
                    IpsBloqueadasManual = await context.IpsBloqueadas.CountAsync(i => i.EstaActivo && i.TipoBloqueo == TipoBloqueoIp.Manual)
                };

                // Ataques por tipo (última semana)
                var ataquesPorTipo = await context.IntentosAtaque
                    .Where(i => i.Fecha >= inicioSemana)
                    .GroupBy(i => i.TipoAtaque)
                    .Select(g => new { Tipo = g.Key, Count = g.Count() })
                    .ToListAsync();

                stats.AtaquesPorTipo = ataquesPorTipo.ToDictionary(x => x.Tipo, x => x.Count);

                // Top 10 IPs atacantes (última semana)
                var topIps = await context.IntentosAtaque
                    .Where(i => i.Fecha >= inicioSemana)
                    .GroupBy(i => i.DireccionIp)
                    .Select(g => new { Ip = g.Key, Intentos = g.Count() })
                    .OrderByDescending(x => x.Intentos)
                    .Take(10)
                    .ToListAsync();

                stats.TopIpsAtacantes = topIps.Select(x => (x.Ip, x.Intentos)).ToList();

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo estadísticas de ataques");
                return new AtaqueEstadisticas();
            }
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
