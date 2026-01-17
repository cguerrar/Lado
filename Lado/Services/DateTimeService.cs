using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface IDateTimeService
    {
        /// <summary>
        /// Convierte una fecha UTC a la zona horaria configurada en la plataforma
        /// </summary>
        DateTime ConvertToLocal(DateTime utcDateTime);

        /// <summary>
        /// Convierte una fecha local a UTC
        /// </summary>
        DateTime ConvertToUtc(DateTime localDateTime);

        /// <summary>
        /// Obtiene la hora actual en la zona horaria configurada
        /// </summary>
        DateTime GetLocalNow();

        /// <summary>
        /// Obtiene el ID de la zona horaria configurada (ej: "America/Bogota")
        /// </summary>
        string GetTimeZoneId();

        /// <summary>
        /// Formatea una fecha para mostrar (considera si es hoy, ayer, etc.)
        /// </summary>
        string FormatForDisplay(DateTime dateTime, bool includeTime = true);

        // ========================================
        // MÉTODOS POR USUARIO (para LadoCoins) - ASÍNCRONOS
        // ========================================

        /// <summary>
        /// Obtiene la zona horaria del usuario de forma asíncrona (evita conflictos de DbContext)
        /// </summary>
        Task<string> GetUserTimeZoneIdAsync(string? userId);

        /// <summary>
        /// Obtiene la hora actual en la zona horaria del usuario de forma asíncrona
        /// </summary>
        Task<DateTime> GetUserLocalNowAsync(string? userId);

        /// <summary>
        /// Convierte una fecha a la zona horaria del usuario de forma asíncrona
        /// </summary>
        Task<DateTime> ConvertToUserLocalAsync(DateTime dateTime, string? userId);

        // ========================================
        // MÉTODOS POR USUARIO - SÍNCRONOS (solo para contextos sin DbContext activo)
        // ========================================

        /// <summary>
        /// Obtiene la zona horaria del usuario (SOLO usar fuera de operaciones async con DbContext)
        /// </summary>
        string GetUserTimeZoneId(string? userId);

        /// <summary>
        /// Obtiene la hora actual en la zona horaria del usuario
        /// </summary>
        DateTime GetUserLocalNow(string? userId);

        /// <summary>
        /// Convierte una fecha a la zona horaria del usuario
        /// </summary>
        DateTime ConvertToUserLocal(DateTime dateTime, string? userId);
    }

    public class DateTimeService : IDateTimeService
    {
        private readonly ApplicationDbContext _context;

        // OPTIMIZACIÓN: Cache estático para evitar queries repetidas entre requests
        private static string? _staticCachedTimeZoneId;
        private static DateTime _staticCacheExpiry = DateTime.MinValue;
        private static readonly object _cacheLock = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30); // Aumentado a 30 min

        public DateTimeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public string GetTimeZoneId()
        {
            // Usar cache estático para evitar consultas a BD en cada request
            if (_staticCachedTimeZoneId != null && DateTime.UtcNow < _staticCacheExpiry)
            {
                return _staticCachedTimeZoneId;
            }

            lock (_cacheLock)
            {
                // Double-check después del lock
                if (_staticCachedTimeZoneId != null && DateTime.UtcNow < _staticCacheExpiry)
                {
                    return _staticCachedTimeZoneId;
                }

                var config = _context.ConfiguracionesPlataforma
                    .FirstOrDefault(c => c.Clave == ConfiguracionPlataforma.ZONA_HORARIA);

                _staticCachedTimeZoneId = config?.Valor ?? "America/Bogota";
                _staticCacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            }

            return _staticCachedTimeZoneId;
        }

        public DateTime ConvertToLocal(DateTime dateTime)
        {
            try
            {
                // Si la fecha viene de la BD (Kind = Unspecified), asumimos que ya esta en hora local del servidor
                // Solo convertimos si explicitamente es UTC
                if (dateTime.Kind == DateTimeKind.Unspecified)
                {
                    // La fecha ya esta en hora local del servidor, devolverla tal cual
                    return dateTime;
                }

                if (dateTime.Kind == DateTimeKind.Local)
                {
                    // Ya es hora local
                    return dateTime;
                }

                // Solo si es UTC, convertimos a la zona horaria configurada
                var timeZoneId = GetTimeZoneId();
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(timeZoneId));
                return TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone);
            }
            catch
            {
                // Si falla, devolver la fecha sin modificar
                return dateTime;
            }
        }

        public DateTime ConvertToUtc(DateTime localDateTime)
        {
            try
            {
                var timeZoneId = GetTimeZoneId();
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(timeZoneId));
                return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), timeZone);
            }
            catch
            {
                return localDateTime.AddHours(5);
            }
        }

        public DateTime GetLocalNow()
        {
            try
            {
                // Convertir UTC a la zona horaria configurada de la plataforma
                var timeZoneId = GetTimeZoneId();
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(timeZoneId));
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            }
            catch
            {
                // Si falla, usar hora del servidor
                return DateTime.Now;
            }
        }

        public string FormatForDisplay(DateTime dateTime, bool includeTime = true)
        {
            var localNow = GetLocalNow();
            var localDate = ConvertToLocal(dateTime);

            if (localDate.Date == localNow.Date)
            {
                // Hoy - mostrar solo hora
                return localDate.ToString("HH:mm");
            }
            else if (localDate.Date == localNow.Date.AddDays(-1))
            {
                // Ayer
                return includeTime ? $"Ayer {localDate:HH:mm}" : "Ayer";
            }
            else if (localDate.Date > localNow.Date.AddDays(-7))
            {
                // Esta semana - mostrar dia
                var dia = localDate.ToString("ddd", new System.Globalization.CultureInfo("es-ES"));
                return includeTime ? $"{dia} {localDate:HH:mm}" : dia;
            }
            else if (localDate.Year == localNow.Year)
            {
                // Este ano - mostrar dia/mes
                return includeTime ? localDate.ToString("dd/MM HH:mm") : localDate.ToString("dd/MM");
            }
            else
            {
                // Otro ano - mostrar fecha completa
                return includeTime ? localDate.ToString("dd/MM/yyyy HH:mm") : localDate.ToString("dd/MM/yyyy");
            }
        }

        // ========================================
        // MÉTODOS POR USUARIO - ASÍNCRONOS (usar en contextos async)
        // ========================================

        public async Task<string> GetUserTimeZoneIdAsync(string? userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return GetTimeZoneId(); // Fallback a zona de la plataforma
            }

            // Buscar zona horaria del usuario con AsNoTracking para evitar conflictos
            var userTimeZone = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.ZonaHoraria)
                .FirstOrDefaultAsync();

            // Si el usuario tiene zona configurada, usarla; sino, usar la de la plataforma
            return !string.IsNullOrEmpty(userTimeZone) ? userTimeZone : GetTimeZoneId();
        }

        public async Task<DateTime> GetUserLocalNowAsync(string? userId)
        {
            try
            {
                var timeZoneId = await GetUserTimeZoneIdAsync(userId);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(timeZoneId));
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            }
            catch
            {
                // Si falla, usar hora del servidor
                return DateTime.Now;
            }
        }

        public async Task<DateTime> ConvertToUserLocalAsync(DateTime dateTime, string? userId)
        {
            try
            {
                if (dateTime.Kind == DateTimeKind.Unspecified || dateTime.Kind == DateTimeKind.Local)
                {
                    return dateTime;
                }

                var timeZoneId = await GetUserTimeZoneIdAsync(userId);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(timeZoneId));
                return TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone);
            }
            catch
            {
                return dateTime;
            }
        }

        // ========================================
        // MÉTODOS POR USUARIO - SÍNCRONOS (solo para contextos sin DbContext activo)
        // ========================================

        public string GetUserTimeZoneId(string? userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return GetTimeZoneId(); // Fallback a zona de la plataforma
            }

            // Buscar zona horaria del usuario
            var userTimeZone = _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.ZonaHoraria)
                .FirstOrDefault();

            // Si el usuario tiene zona configurada, usarla; sino, usar la de la plataforma
            return !string.IsNullOrEmpty(userTimeZone) ? userTimeZone : GetTimeZoneId();
        }

        public DateTime GetUserLocalNow(string? userId)
        {
            try
            {
                var timeZoneId = GetUserTimeZoneId(userId);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(timeZoneId));
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            }
            catch
            {
                // Si falla, usar hora del servidor
                return DateTime.Now;
            }
        }

        public DateTime ConvertToUserLocal(DateTime dateTime, string? userId)
        {
            try
            {
                if (dateTime.Kind == DateTimeKind.Unspecified || dateTime.Kind == DateTimeKind.Local)
                {
                    return dateTime;
                }

                var timeZoneId = GetUserTimeZoneId(userId);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(timeZoneId));
                return TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone);
            }
            catch
            {
                return dateTime;
            }
        }

        // ========================================
        // CONVERSIÓN DE ZONAS HORARIAS
        // ========================================

        /// <summary>
        /// Convierte IDs de zona horaria IANA a Windows
        /// Windows usa nombres diferentes para las zonas horarias
        /// </summary>
        private string ConvertIanaToWindows(string ianaTimeZoneId)
        {
            // Mapeo de zonas horarias IANA a Windows
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // America
                ["America/Bogota"] = "SA Pacific Standard Time",
                ["America/Mexico_City"] = "Central Standard Time (Mexico)",
                ["America/Lima"] = "SA Pacific Standard Time",
                ["America/Santiago"] = "Pacific SA Standard Time",
                ["America/Argentina/Buenos_Aires"] = "Argentina Standard Time",
                ["America/Caracas"] = "Venezuela Standard Time",
                ["America/New_York"] = "Eastern Standard Time",
                ["America/Los_Angeles"] = "Pacific Standard Time",
                ["America/Sao_Paulo"] = "E. South America Standard Time",

                // Europa
                ["Europe/Madrid"] = "Romance Standard Time",
                ["Europe/London"] = "GMT Standard Time",
                ["Europe/Paris"] = "Romance Standard Time",

                // UTC
                ["UTC"] = "UTC"
            };

            if (mapping.TryGetValue(ianaTimeZoneId, out var windowsId))
            {
                return windowsId;
            }

            // Si no encontramos el mapeo, intentar usar directamente
            // (puede funcionar en sistemas Linux con .NET)
            return ianaTimeZoneId;
        }
    }
}
