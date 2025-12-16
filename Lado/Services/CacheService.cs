using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    public interface ICacheService
    {
        T? Get<T>(string key);
        void Set<T>(string key, T value, TimeSpan? expiration = null);
        void Remove(string key);
        void RemoveByPrefix(string prefix);
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly HashSet<string> _keys = new();
        private readonly object _lockObject = new();
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

        public CacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public T? Get<T>(string key)
        {
            return _cache.TryGetValue(key, out T? value) ? value : default;
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
            };

            options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                lock (_lockObject)
                {
                    _keys.Remove(evictedKey.ToString()!);
                }
            });

            _cache.Set(key, value, options);

            lock (_lockObject)
            {
                _keys.Add(key);
            }
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            lock (_lockObject)
            {
                _keys.Remove(key);
            }
        }

        public void RemoveByPrefix(string prefix)
        {
            List<string> keysToRemove;
            lock (_lockObject)
            {
                keysToRemove = _keys.Where(k => k.StartsWith(prefix)).ToList();
            }

            foreach (var key in keysToRemove)
            {
                Remove(key);
            }
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            if (_cache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
            {
                return cachedValue;
            }

            var value = await factory();
            Set(key, value, expiration);
            return value;
        }
    }

    // Claves de caché predefinidas para evitar errores de tipeo
    public static class CacheKeys
    {
        // Estadísticas globales
        public const string TotalUsuarios = "stats:total_usuarios";
        public const string TotalCreadores = "stats:total_creadores";
        public const string TotalContenidos = "stats:total_contenidos";
        public const string TotalIngresos = "stats:total_ingresos";

        // Dashboard
        public static string DashboardCreador(string userId) => $"dashboard:creador:{userId}";
        public static string DashboardFan(string userId) => $"dashboard:fan:{userId}";

        // Contadores
        public static string ContadorSuscriptores(string creadorId) => $"contador:suscriptores:{creadorId}";
        public static string ContadorLikes(int contenidoId) => $"contador:likes:{contenidoId}";
        public static string ContadorMensajesNoLeidos(string userId) => $"contador:mensajes:{userId}";

        // Listas
        public static string ContenidosRecientes(string creadorId) => $"contenidos:recientes:{creadorId}";
        public static string MusicaPopular => "musica:popular";

        // Prefijos para invalidación masiva
        public const string PrefixDashboard = "dashboard:";
        public const string PrefixContador = "contador:";
        public const string PrefixContenidos = "contenidos:";
    }
}
