using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    /// <summary>
    /// Servicio de caché para suscripciones.
    /// Reduce significativamente las consultas a BD para verificaciones frecuentes.
    /// </summary>
    public interface ISuscripcionCacheService
    {
        /// <summary>
        /// Verifica si un usuario está suscrito activamente a un creador
        /// </summary>
        Task<bool> EstaSubscritoAsync(string fanId, string creadorId);

        /// <summary>
        /// Verifica si un usuario está suscrito a un TipoLado específico de un creador
        /// </summary>
        Task<bool> EstaSubscritoATipoAsync(string fanId, string creadorId, TipoLado tipoLado);

        /// <summary>
        /// Verifica si existe relación de suscripción en cualquier dirección
        /// </summary>
        Task<bool> TieneRelacionSuscripcionAsync(string usuarioId1, string usuarioId2);

        /// <summary>
        /// Obtiene todos los creadores a los que está suscrito un usuario (para el feed)
        /// </summary>
        Task<HashSet<string>> ObtenerCreadoresSuscritosAsync(string fanId);

        /// <summary>
        /// Obtiene suscripciones a LadoB específicamente (para contenido premium)
        /// </summary>
        Task<HashSet<string>> ObtenerCreadoresSuscritosLadoBAsync(string fanId);

        /// <summary>
        /// Invalida el caché cuando hay cambios en suscripciones
        /// </summary>
        void InvalidarCache(string fanId, string creadorId);

        /// <summary>
        /// Invalida todo el caché de un usuario
        /// </summary>
        void InvalidarCacheUsuario(string usuarioId);
    }

    public class SuscripcionCacheService : ISuscripcionCacheService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ListCacheDuration = TimeSpan.FromMinutes(2);

        public SuscripcionCacheService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<bool> EstaSubscritoAsync(string fanId, string creadorId)
        {
            if (string.IsNullOrEmpty(fanId) || string.IsNullOrEmpty(creadorId))
                return false;

            var cacheKey = $"suscripcion:{fanId}:{creadorId}";

            if (_cache.TryGetValue(cacheKey, out bool estaSubscrito))
            {
                return estaSubscrito;
            }

            estaSubscrito = await _context.Suscripciones
                .AsNoTracking()
                .AnyAsync(s => s.FanId == fanId && s.CreadorId == creadorId && s.EstaActiva);

            _cache.Set(cacheKey, estaSubscrito, CacheDuration);
            return estaSubscrito;
        }

        public async Task<bool> EstaSubscritoATipoAsync(string fanId, string creadorId, TipoLado tipoLado)
        {
            if (string.IsNullOrEmpty(fanId) || string.IsNullOrEmpty(creadorId))
                return false;

            var cacheKey = $"suscripcion_tipo:{fanId}:{creadorId}:{tipoLado}";

            if (_cache.TryGetValue(cacheKey, out bool estaSubscrito))
            {
                return estaSubscrito;
            }

            estaSubscrito = await _context.Suscripciones
                .AsNoTracking()
                .AnyAsync(s => s.FanId == fanId && s.CreadorId == creadorId && s.TipoLado == tipoLado && s.EstaActiva);

            _cache.Set(cacheKey, estaSubscrito, CacheDuration);
            return estaSubscrito;
        }

        public async Task<bool> TieneRelacionSuscripcionAsync(string usuarioId1, string usuarioId2)
        {
            if (string.IsNullOrEmpty(usuarioId1) || string.IsNullOrEmpty(usuarioId2))
                return false;

            // Cache con orden normalizado para evitar duplicados
            var (menor, mayor) = string.Compare(usuarioId1, usuarioId2) < 0
                ? (usuarioId1, usuarioId2)
                : (usuarioId2, usuarioId1);
            var cacheKey = $"relacion_suscripcion:{menor}:{mayor}";

            if (_cache.TryGetValue(cacheKey, out bool tieneRelacion))
            {
                return tieneRelacion;
            }

            tieneRelacion = await _context.Suscripciones
                .AsNoTracking()
                .AnyAsync(s =>
                    (s.FanId == usuarioId1 && s.CreadorId == usuarioId2 && s.EstaActiva) ||
                    (s.FanId == usuarioId2 && s.CreadorId == usuarioId1 && s.EstaActiva));

            _cache.Set(cacheKey, tieneRelacion, CacheDuration);
            return tieneRelacion;
        }

        public async Task<HashSet<string>> ObtenerCreadoresSuscritosAsync(string fanId)
        {
            if (string.IsNullOrEmpty(fanId))
                return new HashSet<string>();

            var cacheKey = $"creadores_suscritos:{fanId}";

            if (_cache.TryGetValue(cacheKey, out HashSet<string>? creadores) && creadores != null)
            {
                return creadores;
            }

            var lista = await _context.Suscripciones
                .AsNoTracking()
                .Where(s => s.FanId == fanId && s.EstaActiva)
                .Select(s => s.CreadorId)
                .Distinct()
                .ToListAsync();

            creadores = new HashSet<string>(lista);
            _cache.Set(cacheKey, creadores, ListCacheDuration);
            return creadores;
        }

        public async Task<HashSet<string>> ObtenerCreadoresSuscritosLadoBAsync(string fanId)
        {
            if (string.IsNullOrEmpty(fanId))
                return new HashSet<string>();

            var cacheKey = $"creadores_suscritos_ladob:{fanId}";

            if (_cache.TryGetValue(cacheKey, out HashSet<string>? creadores) && creadores != null)
            {
                return creadores;
            }

            var lista = await _context.Suscripciones
                .AsNoTracking()
                .Where(s => s.FanId == fanId && s.EstaActiva && s.TipoLado == TipoLado.LadoB)
                .Select(s => s.CreadorId)
                .Distinct()
                .ToListAsync();

            creadores = new HashSet<string>(lista);
            _cache.Set(cacheKey, creadores, ListCacheDuration);
            return creadores;
        }

        public void InvalidarCache(string fanId, string creadorId)
        {
            if (string.IsNullOrEmpty(fanId) || string.IsNullOrEmpty(creadorId))
                return;

            // Invalidar caché específico
            _cache.Remove($"suscripcion:{fanId}:{creadorId}");
            _cache.Remove($"suscripcion_tipo:{fanId}:{creadorId}:{TipoLado.LadoA}");
            _cache.Remove($"suscripcion_tipo:{fanId}:{creadorId}:{TipoLado.LadoB}");

            // Invalidar relación bidireccional
            var (menor, mayor) = string.Compare(fanId, creadorId) < 0
                ? (fanId, creadorId)
                : (creadorId, fanId);
            _cache.Remove($"relacion_suscripcion:{menor}:{mayor}");

            // Invalidar listas
            _cache.Remove($"creadores_suscritos:{fanId}");
            _cache.Remove($"creadores_suscritos_ladob:{fanId}");
        }

        public void InvalidarCacheUsuario(string usuarioId)
        {
            if (string.IsNullOrEmpty(usuarioId))
                return;

            // Invalidar listas del usuario
            _cache.Remove($"creadores_suscritos:{usuarioId}");
            _cache.Remove($"creadores_suscritos_ladob:{usuarioId}");

            // Nota: No podemos invalidar todas las claves individuales sin un tracking adicional
            // pero con un TTL corto (5 min) esto se auto-limpia
        }
    }
}
