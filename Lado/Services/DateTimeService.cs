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
    }

    public class DateTimeService : IDateTimeService
    {
        private readonly ApplicationDbContext _context;
        private string? _cachedTimeZoneId;
        private DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public DateTimeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public string GetTimeZoneId()
        {
            // Usar cache para evitar consultas repetidas a la BD
            if (_cachedTimeZoneId != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedTimeZoneId;
            }

            var config = _context.ConfiguracionesPlataforma
                .FirstOrDefault(c => c.Clave == ConfiguracionPlataforma.ZONA_HORARIA);

            _cachedTimeZoneId = config?.Valor ?? "America/Bogota";
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

            return _cachedTimeZoneId;
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
            // Usar DateTime.Now ya que las fechas en la BD se guardan con DateTime.Now
            // Esto garantiza consistencia en las comparaciones
            return DateTime.Now;
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
