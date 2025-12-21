using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using System.Text.Json;

namespace Lado.Services
{
    public interface IExifService
    {
        (double Lat, double Lon)? ExtraerCoordenadas(string rutaArchivo);
        Task<string?> ObtenerNombreUbicacion(double lat, double lon);
    }

    public class ExifService : IExifService
    {
        private readonly ILogger<ExifService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly Dictionary<string, (string Nombre, DateTime Timestamp)> _cacheUbicaciones = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        public ExifService(ILogger<ExifService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Extrae las coordenadas GPS de los metadatos EXIF de una imagen
        /// </summary>
        public (double Lat, double Lon)? ExtraerCoordenadas(string rutaArchivo)
        {
            try
            {
                if (string.IsNullOrEmpty(rutaArchivo) || !File.Exists(rutaArchivo))
                {
                    _logger.LogWarning("Archivo no existe: {Ruta}", rutaArchivo);
                    return null;
                }

                // Solo procesar imagenes JPEG y PNG
                var extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                {
                    _logger.LogDebug("Archivo no es imagen soportada: {Extension}", extension);
                    return null;
                }

                var directories = ImageMetadataReader.ReadMetadata(rutaArchivo);
                var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();

                if (gpsDirectory == null)
                {
                    _logger.LogDebug("No se encontro directorio GPS en: {Ruta}", rutaArchivo);
                    return null;
                }

                // Extraer latitud
                var latArray = gpsDirectory.GetRationalArray(GpsDirectory.TagLatitude);
                var latRef = gpsDirectory.GetString(GpsDirectory.TagLatitudeRef);

                // Extraer longitud
                var lonArray = gpsDirectory.GetRationalArray(GpsDirectory.TagLongitude);
                var lonRef = gpsDirectory.GetString(GpsDirectory.TagLongitudeRef);

                if (latArray == null || lonArray == null || latArray.Length < 3 || lonArray.Length < 3)
                {
                    _logger.LogDebug("No hay coordenadas GPS completas en: {Ruta}", rutaArchivo);
                    return null;
                }

                // Convertir a grados decimales
                double lat = ConvertirAGrados(latArray);
                double lon = ConvertirAGrados(lonArray);

                // Aplicar referencia (N/S, E/W)
                if (latRef == "S") lat = -lat;
                if (lonRef == "W") lon = -lon;

                _logger.LogInformation("Coordenadas extraidas: {Lat}, {Lon} de {Ruta}",
                    lat, lon, rutaArchivo);

                return (lat, lon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al extraer coordenadas EXIF de: {Ruta}", rutaArchivo);
                return null;
            }
        }

        /// <summary>
        /// Convierte un array de valores racionales (grados, minutos, segundos) a grados decimales
        /// </summary>
        private static double ConvertirAGrados(MetadataExtractor.Rational[] dms)
        {
            double grados = dms[0].ToDouble();
            double minutos = dms[1].ToDouble();
            double segundos = dms[2].ToDouble();

            return grados + (minutos / 60) + (segundos / 3600);
        }

        /// <summary>
        /// Obtiene el nombre de ubicacion usando geocodificacion inversa (Nominatim/OpenStreetMap)
        /// </summary>
        public async Task<string?> ObtenerNombreUbicacion(double lat, double lon)
        {
            try
            {
                // Redondear para cache (precision de ~1km)
                var cacheKey = $"{Math.Round(lat, 2)},{Math.Round(lon, 2)}";

                // Verificar cache
                if (_cacheUbicaciones.TryGetValue(cacheKey, out var cached))
                {
                    if (DateTime.UtcNow - cached.Timestamp < CacheDuration)
                    {
                        _logger.LogDebug("Cache hit para ubicacion: {Key}", cacheKey);
                        return cached.Nombre;
                    }
                    _cacheUbicaciones.Remove(cacheKey);
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Lado/1.0");

                // Usar Nominatim (OpenStreetMap) - gratuito
                var url = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&format=json&zoom=10";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Error en geocodificacion: {Status}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? ciudad = null;
                string? pais = null;

                if (root.TryGetProperty("address", out var address))
                {
                    // Intentar obtener ciudad (en orden de preferencia)
                    if (address.TryGetProperty("city", out var cityProp))
                        ciudad = cityProp.GetString();
                    else if (address.TryGetProperty("town", out var townProp))
                        ciudad = townProp.GetString();
                    else if (address.TryGetProperty("village", out var villageProp))
                        ciudad = villageProp.GetString();
                    else if (address.TryGetProperty("municipality", out var muniProp))
                        ciudad = muniProp.GetString();
                    else if (address.TryGetProperty("state", out var stateProp))
                        ciudad = stateProp.GetString();

                    // Obtener pais
                    if (address.TryGetProperty("country", out var countryProp))
                        pais = countryProp.GetString();
                }

                string? nombreUbicacion = null;

                if (!string.IsNullOrEmpty(ciudad) && !string.IsNullOrEmpty(pais))
                {
                    nombreUbicacion = $"{ciudad}, {pais}";
                }
                else if (!string.IsNullOrEmpty(pais))
                {
                    nombreUbicacion = pais;
                }
                else if (!string.IsNullOrEmpty(ciudad))
                {
                    nombreUbicacion = ciudad;
                }

                // Limitar longitud
                if (nombreUbicacion?.Length > 50)
                {
                    nombreUbicacion = nombreUbicacion.Substring(0, 47) + "...";
                }

                // Guardar en cache
                if (!string.IsNullOrEmpty(nombreUbicacion))
                {
                    _cacheUbicaciones[cacheKey] = (nombreUbicacion, DateTime.UtcNow);
                    _logger.LogInformation("Ubicacion obtenida: {Ubicacion} para {Lat}, {Lon}",
                        nombreUbicacion, lat, lon);
                }

                return nombreUbicacion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en geocodificacion inversa para: {Lat}, {Lon}", lat, lon);
                return null;
            }
        }
    }
}
