using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Información de cuota de almacenamiento de galería
    /// </summary>
    public class CuotaGaleriaInfo
    {
        /// <summary>
        /// Espacio usado en bytes
        /// </summary>
        public long EspacioUsadoBytes { get; set; }

        /// <summary>
        /// Cuota máxima permitida en bytes
        /// </summary>
        public long CuotaMaximaBytes { get; set; }

        /// <summary>
        /// Porcentaje de uso (0-100)
        /// </summary>
        public int PorcentajeUso { get; set; }

        /// <summary>
        /// Espacio usado formateado (ej: "1.5 GB")
        /// </summary>
        public string EspacioUsadoFormateado { get; set; } = string.Empty;

        /// <summary>
        /// Cuota máxima formateada (ej: "25 GB")
        /// </summary>
        public string CuotaMaximaFormateada { get; set; } = string.Empty;

        /// <summary>
        /// Nivel de alerta: null (ok), "warning" (80%+), "danger" (100%)
        /// </summary>
        public string? NivelAlerta { get; set; }

        /// <summary>
        /// Mensaje de alerta para mostrar al usuario
        /// </summary>
        public string? MensajeAlerta { get; set; }

        /// <summary>
        /// Espacio disponible en bytes
        /// </summary>
        public long EspacioDisponibleBytes => Math.Max(0, CuotaMaximaBytes - EspacioUsadoBytes);

        /// <summary>
        /// Espacio disponible formateado
        /// </summary>
        public string EspacioDisponibleFormateado => FormatearBytes(EspacioDisponibleBytes);

        /// <summary>
        /// Indica si el usuario ha excedido su cuota
        /// </summary>
        public bool CuotaExcedida => EspacioUsadoBytes >= CuotaMaximaBytes;

        /// <summary>
        /// Formatea bytes a unidad legible
        /// </summary>
        public static string FormatearBytes(long bytes)
        {
            string[] sufijos = { "B", "KB", "MB", "GB", "TB" };
            int indice = 0;
            double tamano = bytes;

            while (tamano >= 1024 && indice < sufijos.Length - 1)
            {
                tamano /= 1024;
                indice++;
            }

            return $"{tamano:0.##} {sufijos[indice]}";
        }
    }

    public interface ICuotaGaleriaService
    {
        /// <summary>
        /// Obtiene la información completa de cuota del usuario
        /// </summary>
        Task<CuotaGaleriaInfo> ObtenerInfoCuotaAsync(string usuarioId);

        /// <summary>
        /// Obtiene la cuota máxima del usuario en bytes
        /// </summary>
        Task<long> ObtenerCuotaMaximaAsync(string usuarioId);

        /// <summary>
        /// Obtiene el espacio usado por el usuario en bytes
        /// </summary>
        Task<long> ObtenerEspacioUsadoAsync(string usuarioId);

        /// <summary>
        /// Verifica si el usuario puede subir un archivo del tamaño especificado
        /// </summary>
        Task<bool> PuedeSubirAsync(string usuarioId, long tamanoArchivo);
    }

    public class CuotaGaleriaService : ICuotaGaleriaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CuotaGaleriaService> _logger;

        // Cache de configuración (se actualiza cada 5 minutos)
        private static Dictionary<string, long> _configCache = new();
        private static DateTime _ultimaActualizacionCache = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        // Valores por defecto en MB
        private const long DEFAULT_CUOTA_FAN_MB = 5120;                // 5 GB
        private const long DEFAULT_CUOTA_CREADOR_MB = 25600;           // 25 GB
        private const long DEFAULT_CUOTA_CREADOR_ACTIVO_MB = 102400;   // 100 GB
        private const long DEFAULT_CUOTA_CREADOR_TOP_MB = 256000;      // 250 GB
        private const decimal DEFAULT_UMBRAL_CREADOR_ACTIVO = 100m;    // $100 USD
        private const decimal DEFAULT_UMBRAL_CREADOR_TOP = 1000m;      // $1000 USD
        private const int DEFAULT_ALERTA_80 = 80;
        private const int DEFAULT_ALERTA_100 = 100;

        public CuotaGaleriaService(
            ApplicationDbContext context,
            ILogger<CuotaGaleriaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la información completa de cuota del usuario
        /// </summary>
        public async Task<CuotaGaleriaInfo> ObtenerInfoCuotaAsync(string usuarioId)
        {
            try
            {
                var espacioUsado = await ObtenerEspacioUsadoAsync(usuarioId);
                var cuotaMaxima = await ObtenerCuotaMaximaAsync(usuarioId);
                var config = await ObtenerConfiguracionCuotasAsync();

                var porcentajeUso = cuotaMaxima > 0
                    ? (int)Math.Round((double)espacioUsado / cuotaMaxima * 100)
                    : 0;

                var info = new CuotaGaleriaInfo
                {
                    EspacioUsadoBytes = espacioUsado,
                    CuotaMaximaBytes = cuotaMaxima,
                    PorcentajeUso = Math.Min(porcentajeUso, 100),
                    EspacioUsadoFormateado = CuotaGaleriaInfo.FormatearBytes(espacioUsado),
                    CuotaMaximaFormateada = CuotaGaleriaInfo.FormatearBytes(cuotaMaxima)
                };

                // Determinar nivel de alerta
                var umbral80 = config.TryGetValue("Alerta80", out var v80) ? (int)v80 : DEFAULT_ALERTA_80;
                var umbral100 = config.TryGetValue("Alerta100", out var v100) ? (int)v100 : DEFAULT_ALERTA_100;

                if (porcentajeUso >= umbral100)
                {
                    info.NivelAlerta = "danger";
                    info.MensajeAlerta = "Has alcanzado tu limite de almacenamiento. Elimina archivos o contacta soporte para aumentar tu cuota.";
                }
                else if (porcentajeUso >= umbral80)
                {
                    info.NivelAlerta = "warning";
                    info.MensajeAlerta = $"Has usado el {porcentajeUso}% de tu almacenamiento. Considera liberar espacio pronto.";
                }

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo info de cuota para usuario {UsuarioId}", usuarioId);

                // Devolver info por defecto en caso de error
                return new CuotaGaleriaInfo
                {
                    EspacioUsadoBytes = 0,
                    CuotaMaximaBytes = DEFAULT_CUOTA_FAN_MB * 1024 * 1024,
                    PorcentajeUso = 0,
                    EspacioUsadoFormateado = "0 B",
                    CuotaMaximaFormateada = "5 GB"
                };
            }
        }

        /// <summary>
        /// Obtiene la cuota máxima del usuario en bytes
        /// </summary>
        public async Task<long> ObtenerCuotaMaximaAsync(string usuarioId)
        {
            try
            {
                var usuario = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == usuarioId)
                    .Select(u => new
                    {
                        u.CuotaAlmacenamientoMB,
                        u.TipoUsuario,
                        u.TotalGanancias
                    })
                    .FirstOrDefaultAsync();

                if (usuario == null)
                {
                    _logger.LogWarning("Usuario {UsuarioId} no encontrado al obtener cuota maxima", usuarioId);
                    return DEFAULT_CUOTA_FAN_MB * 1024 * 1024;
                }

                // Si tiene cuota personalizada, usar esa
                if (usuario.CuotaAlmacenamientoMB.HasValue && usuario.CuotaAlmacenamientoMB.Value > 0)
                {
                    return usuario.CuotaAlmacenamientoMB.Value * 1024 * 1024;
                }

                // Calcular según tipo de usuario y ganancias
                var config = await ObtenerConfiguracionCuotasAsync();

                // Si es Fan (TipoUsuario == 0)
                if (usuario.TipoUsuario == 0)
                {
                    var cuotaFan = config.TryGetValue("CuotaFan", out var cf) ? cf : DEFAULT_CUOTA_FAN_MB;
                    return cuotaFan * 1024 * 1024;
                }

                // Es creador (TipoUsuario == 1) - calcular por ingresos
                var umbralTop = config.TryGetValue("UmbralTop", out var ut) ? ut : (long)DEFAULT_UMBRAL_CREADOR_TOP;
                var umbralActivo = config.TryGetValue("UmbralActivo", out var ua) ? ua : (long)DEFAULT_UMBRAL_CREADOR_ACTIVO;

                if (usuario.TotalGanancias >= umbralTop)
                {
                    // Creador TOP
                    var cuotaTop = config.TryGetValue("CuotaCreadorTop", out var ct) ? ct : DEFAULT_CUOTA_CREADOR_TOP_MB;
                    return cuotaTop * 1024 * 1024;
                }
                else if (usuario.TotalGanancias >= umbralActivo)
                {
                    // Creador Activo
                    var cuotaActivo = config.TryGetValue("CuotaCreadorActivo", out var ca) ? ca : DEFAULT_CUOTA_CREADOR_ACTIVO_MB;
                    return cuotaActivo * 1024 * 1024;
                }
                else
                {
                    // Creador básico
                    var cuotaCreador = config.TryGetValue("CuotaCreador", out var cc) ? cc : DEFAULT_CUOTA_CREADOR_MB;
                    return cuotaCreador * 1024 * 1024;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo cuota maxima para usuario {UsuarioId}", usuarioId);
                return DEFAULT_CUOTA_FAN_MB * 1024 * 1024;
            }
        }

        /// <summary>
        /// Obtiene el espacio usado por el usuario en bytes
        /// </summary>
        public async Task<long> ObtenerEspacioUsadoAsync(string usuarioId)
        {
            try
            {
                var espacioUsado = await _context.MediasGaleria
                    .Where(m => m.UsuarioId == usuarioId)
                    .SumAsync(m => m.TamanoBytes);

                return espacioUsado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo espacio usado para usuario {UsuarioId}", usuarioId);
                return 0;
            }
        }

        /// <summary>
        /// Verifica si el usuario puede subir un archivo del tamaño especificado
        /// </summary>
        public async Task<bool> PuedeSubirAsync(string usuarioId, long tamanoArchivo)
        {
            try
            {
                var espacioUsado = await ObtenerEspacioUsadoAsync(usuarioId);
                var cuotaMaxima = await ObtenerCuotaMaximaAsync(usuarioId);

                var espacioDisponible = cuotaMaxima - espacioUsado;
                return tamanoArchivo <= espacioDisponible;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando si usuario {UsuarioId} puede subir archivo de {Tamano} bytes",
                    usuarioId, tamanoArchivo);
                return false; // En caso de error, no permitir subida por seguridad
            }
        }

        /// <summary>
        /// Obtiene la configuración de cuotas desde la BD con cache
        /// </summary>
        private async Task<Dictionary<string, long>> ObtenerConfiguracionCuotasAsync()
        {
            // Usar cache si es válido
            if (_configCache.Any() && DateTime.Now - _ultimaActualizacionCache < CacheDuration)
            {
                return _configCache;
            }

            var config = new Dictionary<string, long>
            {
                { "CuotaFan", DEFAULT_CUOTA_FAN_MB },
                { "CuotaCreador", DEFAULT_CUOTA_CREADOR_MB },
                { "CuotaCreadorActivo", DEFAULT_CUOTA_CREADOR_ACTIVO_MB },
                { "CuotaCreadorTop", DEFAULT_CUOTA_CREADOR_TOP_MB },
                { "UmbralActivo", (long)DEFAULT_UMBRAL_CREADOR_ACTIVO },
                { "UmbralTop", (long)DEFAULT_UMBRAL_CREADOR_TOP },
                { "Alerta80", DEFAULT_ALERTA_80 },
                { "Alerta100", DEFAULT_ALERTA_100 }
            };

            try
            {
                // Mapeo de claves de ConfiguracionPlataforma a claves internas
                var mapeoClaves = new Dictionary<string, string>
                {
                    { ConfiguracionPlataforma.CUOTA_ALMACENAMIENTO_FAN_MB, "CuotaFan" },
                    { ConfiguracionPlataforma.CUOTA_ALMACENAMIENTO_CREADOR_MB, "CuotaCreador" },
                    { ConfiguracionPlataforma.CUOTA_ALMACENAMIENTO_CREADOR_ACTIVO_MB, "CuotaCreadorActivo" },
                    { ConfiguracionPlataforma.CUOTA_ALMACENAMIENTO_CREADOR_TOP_MB, "CuotaCreadorTop" },
                    { ConfiguracionPlataforma.UMBRAL_CREADOR_ACTIVO_INGRESOS, "UmbralActivo" },
                    { ConfiguracionPlataforma.UMBRAL_CREADOR_TOP_INGRESOS, "UmbralTop" },
                    { ConfiguracionPlataforma.CUOTA_ALERTA_PORCENTAJE_80, "Alerta80" },
                    { ConfiguracionPlataforma.CUOTA_ALERTA_PORCENTAJE_100, "Alerta100" }
                };

                var configuraciones = await _context.ConfiguracionesPlataforma
                    .Where(c => mapeoClaves.Keys.Contains(c.Clave))
                    .ToListAsync();

                foreach (var configuracion in configuraciones)
                {
                    if (mapeoClaves.TryGetValue(configuracion.Clave, out var claveInterna) &&
                        long.TryParse(configuracion.Valor, out var valor))
                    {
                        config[claveInterna] = valor;
                    }
                }

                _configCache = config;
                _ultimaActualizacionCache = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener configuracion de cuotas, usando valores por defecto");
            }

            return config;
        }

        /// <summary>
        /// Invalida el cache de configuración (llamar después de cambios en ConfiguracionPlataforma)
        /// </summary>
        public static void InvalidarCache()
        {
            _configCache.Clear();
            _ultimaActualizacionCache = DateTime.MinValue;
        }
    }
}
