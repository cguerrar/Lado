using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using ImageMagick;
using System.Diagnostics;
using Lado.Models;

namespace Lado.Services
{
    public interface IMediaConversionService
    {
        /// <summary>
        /// Convierte cualquier imagen a JPEG estándar
        /// </summary>
        /// <param name="rutaOrigen">Ruta del archivo origen</param>
        /// <param name="carpetaDestino">Carpeta donde guardar el resultado</param>
        /// <param name="nombreBase">Nombre base sin extensión (opcional, usa GUID si es null)</param>
        /// <param name="maxDimension">Dimensión máxima (2048 por defecto)</param>
        /// <param name="quality">Calidad JPEG (90 por defecto)</param>
        /// <returns>Ruta del archivo JPEG convertido o null si falla</returns>
        Task<string?> ConvertirImagenAsync(string rutaOrigen, string carpetaDestino, string? nombreBase = null, int maxDimension = 2048, int quality = 90);

        /// <summary>
        /// Convierte cualquier imagen desde stream a JPEG estándar
        /// </summary>
        Task<string?> ConvertirImagenAsync(Stream inputStream, string carpetaDestino, string extensionOriginal, string? nombreBase = null, int maxDimension = 2048, int quality = 90);

        /// <summary>
        /// Convierte cualquier video a MP4 H.264 estándar
        /// </summary>
        /// <param name="rutaOrigen">Ruta del archivo origen</param>
        /// <param name="carpetaDestino">Carpeta donde guardar el resultado</param>
        /// <param name="nombreBase">Nombre base sin extensión (opcional)</param>
        /// <param name="crf">Constant Rate Factor para calidad (20 por defecto, menor = mejor)</param>
        /// <param name="maxWidth">Ancho máximo (1920 por defecto)</param>
        /// <returns>Ruta del archivo MP4 convertido o null si falla</returns>
        Task<string?> ConvertirVideoAsync(string rutaOrigen, string carpetaDestino, string? nombreBase = null, int crf = 20, int maxWidth = 1920);

        /// <summary>
        /// Convierte video desde stream a MP4 H.264
        /// </summary>
        Task<string?> ConvertirVideoAsync(Stream inputStream, string carpetaDestino, string extensionOriginal, string? nombreBase = null, int crf = 20, int maxWidth = 1920);

        /// <summary>
        /// Verifica si una imagen necesita conversión (no es JPEG)
        /// </summary>
        bool ImagenRequiereConversion(string extension);

        /// <summary>
        /// Verifica si un video necesita conversión (no es MP4 H.264)
        /// </summary>
        bool VideoRequiereConversion(string extension);

        /// <summary>
        /// Obtiene información de un archivo multimedia
        /// </summary>
        Task<MediaInfo?> ObtenerInfoAsync(string rutaArchivo);

        /// <summary>
        /// Procesa un archivo multimedia (imagen o video) y lo convierte al formato estándar.
        /// Este es el método UNIFICADO que debe usarse para todo el procesamiento de contenido.
        /// </summary>
        /// <param name="inputStream">Stream del archivo</param>
        /// <param name="extension">Extensión del archivo original</param>
        /// <param name="carpetaDestino">Carpeta donde guardar el resultado</param>
        /// <param name="nombreBase">Nombre base sin extensión (opcional)</param>
        /// <returns>Resultado con la ruta del archivo procesado o error</returns>
        Task<MediaProcessingResult> ProcesarArchivoAsync(Stream inputStream, string extension, string carpetaDestino, string? nombreBase = null);

        /// <summary>
        /// Verifica si una extensión corresponde a una imagen soportada
        /// </summary>
        bool EsImagenSoportada(string extension);

        /// <summary>
        /// Verifica si una extensión corresponde a un video soportado
        /// </summary>
        bool EsVideoSoportado(string extension);

        /// <summary>
        /// Obtiene el último error de FFmpeg para diagnóstico
        /// </summary>
        string? ObtenerUltimoError();

        /// <summary>
        /// Genera un thumbnail de un video usando FFmpeg
        /// </summary>
        /// <param name="rutaVideo">Ruta del archivo de video</param>
        /// <param name="carpetaDestino">Carpeta donde guardar el thumbnail</param>
        /// <param name="segundos">Segundo del video del cual extraer el frame (default: 1)</param>
        /// <param name="maxWidth">Ancho máximo del thumbnail</param>
        /// <returns>Ruta del thumbnail generado o null si falla</returns>
        Task<string?> GenerarVideoThumbnailAsync(string rutaVideo, string? carpetaDestino = null, double segundos = 1.0, int maxWidth = 480);
    }

    /// <summary>
    /// Resultado del procesamiento de un archivo multimedia
    /// </summary>
    public class MediaProcessingResult
    {
        public bool Exitoso { get; set; }
        public string? RutaArchivo { get; set; }
        public string? NombreArchivo { get; set; }
        public string? Error { get; set; }
        public string? ErrorDetallado { get; set; }
        public TipoMediaProcesado TipoMedia { get; set; }
        public long TamanoOriginal { get; set; }
        public long TamanoFinal { get; set; }

        public static MediaProcessingResult Exito(string rutaArchivo, TipoMediaProcesado tipo, long tamanoOriginal, long tamanoFinal)
        {
            return new MediaProcessingResult
            {
                Exitoso = true,
                RutaArchivo = rutaArchivo,
                NombreArchivo = Path.GetFileName(rutaArchivo),
                TipoMedia = tipo,
                TamanoOriginal = tamanoOriginal,
                TamanoFinal = tamanoFinal
            };
        }

        public static MediaProcessingResult Fallo(string error, string? errorDetallado = null)
        {
            return new MediaProcessingResult
            {
                Exitoso = false,
                Error = error,
                ErrorDetallado = errorDetallado
            };
        }
    }

    public enum TipoMediaProcesado
    {
        Desconocido,
        Imagen,
        Video
    }

    public class MediaInfo
    {
        public string? Codec { get; set; }
        public string? Format { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Duration { get; set; }
        public long FileSize { get; set; }
        public bool EsCompatible { get; set; }
    }

    public class MediaConversionService : IMediaConversionService
    {
        private readonly ILogger<MediaConversionService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogEventoService? _logEventoService;
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;
        private string? _ultimoErrorFFmpeg;

        // Extensiones de imagen que YA están en formato estándar
        private static readonly HashSet<string> ImagenesEstandar = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg"
        };

        // Extensiones de imagen que pueden convertirse
        private static readonly HashSet<string> ImagenesSoportadas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif",
            ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2", ".avif"
        };

        // Extensiones de video que YA están en formato estándar
        private static readonly HashSet<string> VideosEstandar = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4"
        };

        // Extensiones de video que pueden convertirse
        private static readonly HashSet<string> VideosSoportados = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".wmv", ".flv", ".m4v", ".3gp", ".3gpp", ".mpeg", ".mpg", ".mxf"
        };

        // Formatos RAW que requieren exiftool
        private static readonly HashSet<string> FormatosRaw = new(StringComparer.OrdinalIgnoreCase)
        {
            ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2"
        };

        public MediaConversionService(ILogger<MediaConversionService> logger, IWebHostEnvironment environment, ILogEventoService? logEventoService = null)
        {
            _logger = logger;
            _environment = environment;
            _logEventoService = logEventoService;

            // Buscar FFmpeg en múltiples ubicaciones conocidas
            var ubicacionesPosibles = new[]
            {
                // 1. Tools del proyecto (desarrollo local)
                Path.Combine(_environment.ContentRootPath, "Tools", "ffmpeg", "ffmpeg.exe"),
                // 2. Chocolatey (producción Windows)
                @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
                // 3. Ubicación común en Windows
                @"C:\ffmpeg\bin\ffmpeg.exe",
            };

            _ffmpegPath = "ffmpeg"; // Default: usar PATH del sistema
            _ffprobePath = "ffprobe";

            foreach (var ruta in ubicacionesPosibles)
            {
                if (File.Exists(ruta))
                {
                    _ffmpegPath = ruta;
                    _ffprobePath = Path.Combine(Path.GetDirectoryName(ruta)!, "ffprobe.exe");
                    _logger.LogInformation("[MediaConversion] FFmpeg encontrado: {Path}", _ffmpegPath);
                    break;
                }
            }

            if (_ffmpegPath == "ffmpeg")
            {
                _logger.LogWarning("[MediaConversion] FFmpeg no encontrado en ubicaciones conocidas, usando PATH del sistema");
            }
        }

        /// <summary>
        /// Obtiene el último error de FFmpeg (útil para diagnóstico)
        /// </summary>
        public string? ObtenerUltimoError() => _ultimoErrorFFmpeg;

        /// <summary>
        /// Genera un thumbnail de un video usando FFmpeg
        /// </summary>
        public async Task<string?> GenerarVideoThumbnailAsync(string rutaVideo, string? carpetaDestino = null, double segundos = 1.0, int maxWidth = 480)
        {
            try
            {
                if (string.IsNullOrEmpty(rutaVideo) || !File.Exists(rutaVideo))
                {
                    _logger.LogWarning("[VideoThumbnail] Archivo no encontrado: {Ruta}", rutaVideo);
                    return null;
                }

                // Determinar carpeta destino (misma que el video si no se especifica)
                var directorio = carpetaDestino ?? Path.GetDirectoryName(rutaVideo);
                if (string.IsNullOrEmpty(directorio))
                {
                    _logger.LogWarning("[VideoThumbnail] No se pudo determinar directorio para: {Ruta}", rutaVideo);
                    return null;
                }

                Directory.CreateDirectory(directorio);

                var nombreSinExtension = Path.GetFileNameWithoutExtension(rutaVideo);
                var rutaThumbnail = Path.Combine(directorio, $"{nombreSinExtension}_thumb.jpg");

                // Comando FFmpeg para extraer un frame y redimensionar
                // -ss: posición en segundos
                // -vframes 1: solo un frame
                // -vf scale: redimensionar manteniendo aspect ratio
                var argumentos = $"-y -ss {segundos:F1} -i \"{rutaVideo}\" -vframes 1 -vf \"scale={maxWidth}:-1\" -q:v 2 \"{rutaThumbnail}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = argumentos,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation("[VideoThumbnail] Ejecutando: {Cmd} {Args}", _ffmpegPath, argumentos);

                using var proceso = new Process { StartInfo = processInfo };
                proceso.Start();

                var errorOutput = await proceso.StandardError.ReadToEndAsync();
                await proceso.WaitForExitAsync();

                if (proceso.ExitCode != 0)
                {
                    _ultimoErrorFFmpeg = errorOutput;
                    _logger.LogWarning("[VideoThumbnail] FFmpeg falló con código {Code}: {Error}",
                        proceso.ExitCode, errorOutput.Length > 500 ? errorOutput[..500] : errorOutput);
                    return null;
                }

                if (!File.Exists(rutaThumbnail))
                {
                    _logger.LogWarning("[VideoThumbnail] No se generó el archivo: {Ruta}", rutaThumbnail);
                    return null;
                }

                _logger.LogInformation("[VideoThumbnail] Thumbnail generado: {Ruta}", rutaThumbnail);

                // Convertir a ruta web
                var webRoot = _environment.WebRootPath;
                if (rutaThumbnail.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return rutaThumbnail.Substring(webRoot.Length).Replace("\\", "/");
                }

                return rutaThumbnail;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VideoThumbnail] Error generando thumbnail para: {Ruta}", rutaVideo);
                return null;
            }
        }

        public bool ImagenRequiereConversion(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            extension = extension.StartsWith(".") ? extension : "." + extension;
            // FORZAR conversión de TODAS las imágenes para:
            // 1. Normalizar orientación EXIF
            // 2. Redimensionar a max 2048px
            // 3. Comprimir a JPEG calidad 90
            // 4. Garantizar compatibilidad universal
            return ImagenesSoportadas.Contains(extension);
        }

        public bool VideoRequiereConversion(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            extension = extension.StartsWith(".") ? extension : "." + extension;
            // FORZAR conversión de TODOS los videos para:
            // 1. Garantizar codec H.264 compatible con WhatsApp/iOS/Safari
            // 2. Normalizar a 30fps
            // 3. Agregar faststart para streaming
            // 4. Los MP4 que ya son H.264 compatible se copiarán sin re-encoding
            return VideosSoportados.Contains(extension);
        }

        #region Conversión de Imágenes

        public async Task<string?> ConvertirImagenAsync(string rutaOrigen, string carpetaDestino, string? nombreBase = null, int maxDimension = 2048, int quality = 90)
        {
            try
            {
                if (!File.Exists(rutaOrigen))
                {
                    _logger.LogWarning("[MediaConversion] Archivo no encontrado: {Ruta}", rutaOrigen);
                    return null;
                }

                var extension = Path.GetExtension(rutaOrigen).ToLower();
                nombreBase ??= Guid.NewGuid().ToString();

                using var stream = new FileStream(rutaOrigen, FileMode.Open, FileAccess.Read);
                return await ConvertirImagenAsync(stream, carpetaDestino, extension, nombreBase, maxDimension, quality);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error convirtiendo imagen: {Ruta}", rutaOrigen);
                return null;
            }
        }

        public async Task<string?> ConvertirImagenAsync(Stream inputStream, string carpetaDestino, string extensionOriginal, string? nombreBase = null, int maxDimension = 2048, int quality = 90)
        {
            try
            {
                Directory.CreateDirectory(carpetaDestino);
                nombreBase ??= Guid.NewGuid().ToString();
                var rutaDestino = Path.Combine(carpetaDestino, $"{nombreBase}.jpg");

                extensionOriginal = extensionOriginal.StartsWith(".") ? extensionOriginal : "." + extensionOriginal;
                _logger.LogInformation("[MediaConversion] Convirtiendo imagen {Extension} a JPEG", extensionOriginal);

                // Si es formato RAW, usar exiftool para extraer preview
                if (FormatosRaw.Contains(extensionOriginal))
                {
                    var resultado = await ConvertirRawAJpegAsync(inputStream, carpetaDestino, nombreBase, maxDimension, quality);
                    return resultado;
                }

                // Si es HEIC/HEIF, usar Magick.NET
                if (extensionOriginal.Equals(".heic", StringComparison.OrdinalIgnoreCase) ||
                    extensionOriginal.Equals(".heif", StringComparison.OrdinalIgnoreCase))
                {
                    return await ConvertirHeicAJpegAsync(inputStream, rutaDestino, maxDimension, quality);
                }

                // Para formatos estándar, usar ImageSharp
                return await ConvertirImagenEstandarAsync(inputStream, rutaDestino, maxDimension, quality);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error convirtiendo imagen desde stream");
                return null;
            }
        }

        private async Task<string?> ConvertirImagenEstandarAsync(Stream inputStream, string rutaDestino, int maxDimension, int quality)
        {
            try
            {
                if (inputStream.CanSeek)
                    inputStream.Position = 0;

                using var image = await Image.LoadAsync(inputStream);

                // Redimensionar si excede el máximo
                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(maxDimension, maxDimension),
                        Mode = ResizeMode.Max
                    }));
                }

                // Guardar como JPEG
                var encoder = new JpegEncoder { Quality = quality };
                await image.SaveAsJpegAsync(rutaDestino, encoder);

                var fileInfo = new FileInfo(rutaDestino);
                _logger.LogInformation("[MediaConversion] Imagen convertida: {Ruta}, {Size}KB", rutaDestino, fileInfo.Length / 1024);

                return rutaDestino;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error en conversión estándar de imagen");
                return null;
            }
        }

        private async Task<string?> ConvertirHeicAJpegAsync(Stream inputStream, string rutaDestino, int maxDimension, int quality)
        {
            try
            {
                if (inputStream.CanSeek)
                    inputStream.Position = 0;

                using var image = new MagickImage(inputStream);

                image.AutoOrient();

                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    var geometry = new MagickGeometry((uint)maxDimension, (uint)maxDimension)
                    {
                        IgnoreAspectRatio = false
                    };
                    image.Resize(geometry);
                }

                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)quality;
                await image.WriteAsync(rutaDestino);

                var fileInfo = new FileInfo(rutaDestino);
                _logger.LogInformation("[MediaConversion] HEIC convertido: {Ruta}, {Size}KB", rutaDestino, fileInfo.Length / 1024);

                return rutaDestino;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error convirtiendo HEIC");
                return null;
            }
        }

        private async Task<string?> ConvertirRawAJpegAsync(Stream inputStream, string carpetaDestino, string nombreBase, int maxDimension, int quality)
        {
            var rutaDestino = Path.Combine(carpetaDestino, $"{nombreBase}.jpg");
            var rutaTemporal = Path.Combine(carpetaDestino, $"{nombreBase}_temp.raw");

            try
            {
                // Guardar temporalmente para procesar con exiftool
                using (var fileStream = new FileStream(rutaTemporal, FileMode.Create))
                {
                    if (inputStream.CanSeek)
                        inputStream.Position = 0;
                    await inputStream.CopyToAsync(fileStream);
                }

                // Intentar extraer preview con exiftool
                var previewExtraida = await ExtraerPreviewConExiftool(rutaTemporal, rutaDestino);

                if (previewExtraida && File.Exists(rutaDestino))
                {
                    // Redimensionar si es necesario
                    await RedimensionarImagenAsync(rutaDestino, maxDimension, quality);

                    var fileInfo = new FileInfo(rutaDestino);
                    _logger.LogInformation("[MediaConversion] RAW convertido via exiftool: {Ruta}, {Size}KB", rutaDestino, fileInfo.Length / 1024);

                    return rutaDestino;
                }

                // Fallback: Magick.NET
                _logger.LogWarning("[MediaConversion] Exiftool falló, usando Magick.NET...");
                return await ConvertirRawConMagickAsync(rutaTemporal, rutaDestino, maxDimension, quality);
            }
            finally
            {
                // Limpiar archivo temporal
                try { if (File.Exists(rutaTemporal)) File.Delete(rutaTemporal); } catch { }
            }
        }

        private async Task<bool> ExtraerPreviewConExiftool(string rutaOrigen, string rutaDestino)
        {
            try
            {
                // Intentar JpgFromRaw primero (mejor calidad)
                var resultado = await EjecutarExiftool($"-b -JpgFromRaw \"{rutaOrigen}\"", rutaDestino);
                if (resultado) return true;

                // Intentar PreviewImage
                return await EjecutarExiftool($"-b -PreviewImage \"{rutaOrigen}\"", rutaDestino);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error con exiftool");
                return false;
            }
        }

        private async Task<bool> EjecutarExiftool(string argumentos, string rutaDestino)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "exiftool",
                    Arguments = argumentos,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null) return false;

                using (var outputStream = new FileStream(rutaDestino, FileMode.Create))
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(outputStream);
                }

                await process.WaitForExitAsync();

                var fileInfo = new FileInfo(rutaDestino);
                if (fileInfo.Exists && fileInfo.Length > 10000)
                {
                    _logger.LogInformation("[MediaConversion] Exiftool extrajo: {Size}KB", fileInfo.Length / 1024);
                    return true;
                }

                if (File.Exists(rutaDestino)) File.Delete(rutaDestino);
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string?> ConvertirRawConMagickAsync(string rutaOrigen, string rutaDestino, int maxDimension, int quality)
        {
            try
            {
                using var images = new MagickImageCollection(rutaOrigen);

                MagickImage? imagenFinal = null;

                if (images.Count > 1)
                {
                    // Buscar la mejor preview
                    MagickImage? mejorPreview = null;
                    long mejorPixeles = 0;

                    foreach (var img in images)
                    {
                        var pixeles = (long)img.Width * img.Height;
                        if (pixeles > mejorPixeles && img.Width < 5000)
                        {
                            mejorPixeles = pixeles;
                            mejorPreview = (MagickImage)img;
                        }
                    }

                    if (mejorPreview != null)
                        imagenFinal = (MagickImage)mejorPreview.Clone();
                }

                imagenFinal ??= (MagickImage)images[0].Clone();

                imagenFinal.AutoOrient();

                if (imagenFinal.Width > maxDimension || imagenFinal.Height > maxDimension)
                {
                    var geometry = new MagickGeometry((uint)maxDimension, (uint)maxDimension)
                    {
                        IgnoreAspectRatio = false
                    };
                    imagenFinal.Resize(geometry);
                }

                imagenFinal.Format = MagickFormat.Jpeg;
                imagenFinal.Quality = (uint)quality;
                await imagenFinal.WriteAsync(rutaDestino);
                imagenFinal.Dispose();

                return rutaDestino;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error con Magick.NET");
                return null;
            }
        }

        private async Task RedimensionarImagenAsync(string rutaImagen, int maxDimension, int quality)
        {
            try
            {
                using var image = new MagickImage(rutaImagen);

                image.AutoOrient();

                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    var geometry = new MagickGeometry((uint)maxDimension, (uint)maxDimension)
                    {
                        IgnoreAspectRatio = false
                    };
                    image.Resize(geometry);
                    image.Quality = (uint)quality;
                    await image.WriteAsync(rutaImagen);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MediaConversion] Error redimensionando imagen");
            }
        }

        #endregion

        #region Conversión de Videos

        public async Task<string?> ConvertirVideoAsync(string rutaOrigen, string carpetaDestino, string? nombreBase = null, int crf = 20, int maxWidth = 1920)
        {
            try
            {
                if (!File.Exists(rutaOrigen))
                {
                    _logger.LogWarning("[MediaConversion] Video no encontrado: {Ruta}", rutaOrigen);
                    return null;
                }

                Directory.CreateDirectory(carpetaDestino);
                nombreBase ??= Guid.NewGuid().ToString();
                var rutaDestino = Path.Combine(carpetaDestino, $"{nombreBase}.mp4");

                _logger.LogInformation("[MediaConversion] Convirtiendo video a MP4 H.264: {Origen}", rutaOrigen);

                // Verificar si el video ya es MP4 H.264 compatible
                var info = await ObtenerInfoAsync(rutaOrigen);
                if (info?.EsCompatible == true && Path.GetExtension(rutaOrigen).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    // Solo copiar si ya es compatible
                    File.Copy(rutaOrigen, rutaDestino, true);
                    _logger.LogInformation("[MediaConversion] Video ya compatible, copiado: {Ruta}", rutaDestino);
                    return rutaDestino;
                }

                // Construir comando FFmpeg (parámetros optimizados para WhatsApp/iOS)
                // -y: sobrescribir sin preguntar
                // -i: input
                // -c:v libx264: codec video H.264
                // -profile:v baseline -level 3.0: perfil más compatible con WhatsApp/iOS
                // -preset medium: balance calidad/velocidad
                // -crf: calidad (menor = mejor, 18-28 rango típico)
                // -pix_fmt yuv420p: formato de pixel compatible con todos los dispositivos
                // -r 30: frame rate 30fps de salida
                // -vsync cfr: forzar frame rate constante (interpolar si es necesario)
                // -c:a aac: codec audio AAC
                // -b:a 128k: bitrate audio 128kbps (más compatible)
                // -ar 44100: sample rate 44.1kHz (estándar para WhatsApp)
                // -ac 2: audio estéreo
                // -movflags +faststart: optimizar para streaming web (CRÍTICO para WhatsApp)
                // -vf fps=30,scale: forzar fps y escalar
                var videoFilter = $"fps=30,scale='min({maxWidth},iw)':-2";
                var arguments = $"-y -i \"{rutaOrigen}\" -c:v libx264 -profile:v baseline -level 3.0 -preset medium -crf {crf} -pix_fmt yuv420p -vsync cfr -c:a aac -b:a 128k -ar 44100 -ac 2 -movflags +faststart -vf \"{videoFilter}\" \"{rutaDestino}\"";

                var resultado = await EjecutarFFmpegAsync(arguments);

                if (resultado && File.Exists(rutaDestino))
                {
                    var fileInfo = new FileInfo(rutaDestino);
                    _logger.LogInformation("[MediaConversion] Video convertido: {Ruta}, {Size}MB", rutaDestino, (fileInfo.Length / 1024.0 / 1024.0).ToString("F2"));
                    return rutaDestino;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error convirtiendo video: {Ruta}", rutaOrigen);
                return null;
            }
        }

        public async Task<string?> ConvertirVideoAsync(Stream inputStream, string carpetaDestino, string extensionOriginal, string? nombreBase = null, int crf = 20, int maxWidth = 1920)
        {
            var rutaTemporal = Path.Combine(carpetaDestino, $"temp_{Guid.NewGuid()}{extensionOriginal}");

            try
            {
                Directory.CreateDirectory(carpetaDestino);
                _logger.LogInformation("[MediaConversion] Guardando stream a archivo temporal: {Ruta}", rutaTemporal);

                // Guardar stream a archivo temporal
                long bytesCopiados = 0;
                using (var fileStream = new FileStream(rutaTemporal, FileMode.Create))
                {
                    if (inputStream.CanSeek)
                        inputStream.Position = 0;
                    await inputStream.CopyToAsync(fileStream);
                    bytesCopiados = fileStream.Length;
                }

                _logger.LogInformation("[MediaConversion] Archivo temporal creado: {Size}MB", (bytesCopiados / 1024.0 / 1024.0).ToString("F2"));

                if (!File.Exists(rutaTemporal))
                {
                    _logger.LogError("[MediaConversion] ERROR: Archivo temporal no existe después de escribir");
                    await RegistrarErrorAsync("Archivo temporal no creado", rutaTemporal);
                    return null;
                }

                var resultado = await ConvertirVideoAsync(rutaTemporal, carpetaDestino, nombreBase, crf, maxWidth);

                if (resultado == null)
                {
                    _logger.LogWarning("[MediaConversion] Conversión falló. Último error: {Error}", _ultimoErrorFFmpeg ?? "desconocido");
                }

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error procesando stream de video");
                await RegistrarErrorAsync("Error procesando stream", ex.Message);
                return null;
            }
            finally
            {
                // Limpiar archivo temporal
                try { if (File.Exists(rutaTemporal)) File.Delete(rutaTemporal); } catch { }
            }
        }

        private async Task<bool> EjecutarFFmpegAsync(string argumentos, int timeoutSeconds = 300)
        {
            _ultimoErrorFFmpeg = null;

            try
            {
                _logger.LogInformation("[MediaConversion] Ejecutando FFmpeg: {Path}", _ffmpegPath);
                _logger.LogInformation("[MediaConversion] Argumentos: {Args}", argumentos);

                var processInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = argumentos,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    _ultimoErrorFFmpeg = "No se pudo iniciar el proceso FFmpeg";
                    _logger.LogError("[MediaConversion] {Error}", _ultimoErrorFFmpeg);
                    await RegistrarErrorAsync(_ultimoErrorFFmpeg, "FFmpeg no disponible");
                    return false;
                }

                var errorOutput = await process.StandardError.ReadToEndAsync();

                var completed = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000));
                if (!completed)
                {
                    process.Kill();
                    _ultimoErrorFFmpeg = $"FFmpeg timeout después de {timeoutSeconds}s";
                    _logger.LogError("[MediaConversion] {Error}", _ultimoErrorFFmpeg);
                    await RegistrarErrorAsync(_ultimoErrorFFmpeg, errorOutput);
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    // Extraer las últimas líneas relevantes del error
                    var errorLines = errorOutput?.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray() ?? Array.Empty<string>();
                    var errorResumen = errorLines.Length > 5
                        ? string.Join("\n", errorLines.TakeLast(5))
                        : errorOutput ?? "Sin detalles";

                    _ultimoErrorFFmpeg = $"FFmpeg error (código {process.ExitCode}): {errorResumen}";
                    _logger.LogError("[MediaConversion] FFmpeg error (código {Code}): {Error}", process.ExitCode, errorOutput);
                    await RegistrarErrorAsync($"FFmpeg falló con código {process.ExitCode}", errorResumen);
                    return false;
                }

                _logger.LogInformation("[MediaConversion] FFmpeg completado exitosamente");
                return true;
            }
            catch (Exception ex)
            {
                _ultimoErrorFFmpeg = $"Excepción ejecutando FFmpeg: {ex.Message}";
                _logger.LogError(ex, "[MediaConversion] Error ejecutando FFmpeg");
                await RegistrarErrorAsync("Excepción en FFmpeg", ex.Message);
                return false;
            }
        }

        private async Task RegistrarErrorAsync(string mensaje, string? detalle)
        {
            if (_logEventoService == null) return;

            try
            {
                await _logEventoService.RegistrarEventoAsync(
                    $"[MediaConversion] {mensaje}",
                    CategoriaEvento.Sistema,
                    TipoLogEvento.Error,
                    detalle: detalle
                );
            }
            catch { /* Ignorar errores de logging */ }
        }

        #endregion

        #region Información de Medios

        public async Task<MediaInfo?> ObtenerInfoAsync(string rutaArchivo)
        {
            try
            {
                if (!File.Exists(rutaArchivo)) return null;

                var extension = Path.GetExtension(rutaArchivo).ToLower();
                var fileInfo = new FileInfo(rutaArchivo);

                // Para imágenes, usar Magick.NET
                if (ImagenesSoportadas.Contains(extension))
                {
                    return await ObtenerInfoImagenAsync(rutaArchivo, fileInfo.Length);
                }

                // Para videos, usar FFprobe
                if (VideosSoportados.Contains(extension))
                {
                    return await ObtenerInfoVideoAsync(rutaArchivo, fileInfo.Length);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error obteniendo info de: {Ruta}", rutaArchivo);
                return null;
            }
        }

        private async Task<MediaInfo?> ObtenerInfoImagenAsync(string rutaArchivo, long fileSize)
        {
            try
            {
                using var image = new MagickImage(rutaArchivo);
                var extension = Path.GetExtension(rutaArchivo).ToLower();

                return new MediaInfo
                {
                    Format = image.Format.ToString(),
                    Codec = image.Format.ToString(),
                    Width = (int)image.Width,
                    Height = (int)image.Height,
                    FileSize = fileSize,
                    EsCompatible = ImagenesEstandar.Contains(extension)
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<MediaInfo?> ObtenerInfoVideoAsync(string rutaArchivo, long fileSize)
        {
            try
            {
                // Usar ffprobe para obtener información del video
                var processInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    Arguments = $"-v quiet -print_format json -show_format -show_streams \"{rutaArchivo}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null) return null;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (string.IsNullOrEmpty(output)) return null;

                // Parsear JSON básico para obtener codec
                var info = new MediaInfo { FileSize = fileSize };

                // Buscar codec de video
                if (output.Contains("\"codec_name\""))
                {
                    var codecMatch = System.Text.RegularExpressions.Regex.Match(output, "\"codec_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (codecMatch.Success)
                        info.Codec = codecMatch.Groups[1].Value;
                }

                // Buscar dimensiones
                var widthMatch = System.Text.RegularExpressions.Regex.Match(output, "\"width\"\\s*:\\s*(\\d+)");
                var heightMatch = System.Text.RegularExpressions.Regex.Match(output, "\"height\"\\s*:\\s*(\\d+)");
                if (widthMatch.Success) info.Width = int.Parse(widthMatch.Groups[1].Value);
                if (heightMatch.Success) info.Height = int.Parse(heightMatch.Groups[1].Value);

                // Buscar duración
                var durationMatch = System.Text.RegularExpressions.Regex.Match(output, "\"duration\"\\s*:\\s*\"([\\d.]+)\"");
                if (durationMatch.Success) info.Duration = double.Parse(durationMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

                // Buscar formato
                var formatMatch = System.Text.RegularExpressions.Regex.Match(output, "\"format_name\"\\s*:\\s*\"([^\"]+)\"");
                if (formatMatch.Success) info.Format = formatMatch.Groups[1].Value;

                // Buscar frame rate (r_frame_rate o avg_frame_rate)
                double frameRate = 0;
                var fpsMatch = System.Text.RegularExpressions.Regex.Match(output, "\"r_frame_rate\"\\s*:\\s*\"(\\d+)/(\\d+)\"");
                if (fpsMatch.Success)
                {
                    var num = double.Parse(fpsMatch.Groups[1].Value);
                    var den = double.Parse(fpsMatch.Groups[2].Value);
                    if (den > 0) frameRate = num / den;
                }

                // Buscar level
                int level = 0;
                var levelMatch = System.Text.RegularExpressions.Regex.Match(output, "\"level\"\\s*:\\s*(\\d+)");
                if (levelMatch.Success) level = int.Parse(levelMatch.Groups[1].Value);

                // Verificar si es compatible con WhatsApp/iOS:
                // - H.264 en MP4/MOV
                // - Frame rate >= 24fps (WhatsApp rechaza videos con frame rate bajo)
                // - Level <= 31 (3.1) para mejor compatibilidad
                var esH264 = info.Codec?.Equals("h264", StringComparison.OrdinalIgnoreCase) == true;
                var esFormatoCompatible = info.Format?.Contains("mp4") == true || info.Format?.Contains("mov") == true;
                var esFpsCompatible = frameRate >= 24;
                var esLevelCompatible = level > 0 && level <= 31;

                info.EsCompatible = esH264 && esFormatoCompatible && esFpsCompatible && esLevelCompatible;

                if (!info.EsCompatible)
                {
                    _logger.LogInformation("[MediaConversion] Video no compatible: codec={Codec}, fps={Fps}, level={Level}",
                        info.Codec, frameRate.ToString("F1"), level);
                }

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] Error obteniendo info de video");
                return null;
            }
        }

        #endregion

        #region Métodos Unificados de Procesamiento

        public bool EsImagenSoportada(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            extension = extension.StartsWith(".") ? extension : "." + extension;
            return ImagenesSoportadas.Contains(extension);
        }

        public bool EsVideoSoportado(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            extension = extension.StartsWith(".") ? extension : "." + extension;
            return VideosSoportados.Contains(extension);
        }

        /// <summary>
        /// Método UNIFICADO para procesar cualquier archivo multimedia.
        /// Este método DEBE usarse en lugar de llamar directamente a ConvertirImagenAsync/ConvertirVideoAsync.
        /// NO tiene fallback - si falla la conversión, retorna error.
        /// </summary>
        public async Task<MediaProcessingResult> ProcesarArchivoAsync(
            Stream inputStream,
            string extension,
            string carpetaDestino,
            string? nombreBase = null)
        {
            try
            {
                if (inputStream == null || inputStream.Length == 0)
                {
                    return MediaProcessingResult.Fallo(
                        "El archivo está vacío",
                        "Stream es null o tiene longitud 0");
                }

                extension = extension.StartsWith(".") ? extension : "." + extension;
                extension = extension.ToLowerInvariant();
                nombreBase ??= Guid.NewGuid().ToString();
                var tamanoOriginal = inputStream.Length;

                _logger.LogInformation("[MediaConversion] 🔄 Procesando archivo: Extension={Extension}, Tamaño={Size}KB",
                    extension, tamanoOriginal / 1024);

                // Determinar tipo de archivo
                var esImagen = EsImagenSoportada(extension);
                var esVideo = EsVideoSoportado(extension);

                if (!esImagen && !esVideo)
                {
                    return MediaProcessingResult.Fallo(
                        $"Formato no soportado: {extension}",
                        $"La extensión {extension} no es una imagen ni video soportado.\nImágenes: {string.Join(", ", ImagenesSoportadas)}\nVideos: {string.Join(", ", VideosSoportados)}");
                }

                Directory.CreateDirectory(carpetaDestino);

                if (esImagen)
                {
                    // Procesar imagen
                    var rutaConvertida = await ConvertirImagenAsync(
                        inputStream, carpetaDestino, extension, nombreBase, 2048, 90);

                    if (string.IsNullOrEmpty(rutaConvertida))
                    {
                        return MediaProcessingResult.Fallo(
                            "Error al procesar la imagen. Por favor intenta con otro archivo.",
                            $"ConvertirImagenAsync retornó null.\nExtensión: {extension}\nTamaño: {tamanoOriginal / 1024}KB");
                    }

                    var tamanoFinal = new FileInfo(rutaConvertida).Length;
                    _logger.LogInformation("[MediaConversion] ✅ Imagen procesada: {Original}KB -> {Final}KB ({Ratio}%)",
                        tamanoOriginal / 1024, tamanoFinal / 1024,
                        Math.Round((double)tamanoFinal / tamanoOriginal * 100, 1));

                    return MediaProcessingResult.Exito(rutaConvertida, TipoMediaProcesado.Imagen, tamanoOriginal, tamanoFinal);
                }
                else // esVideo
                {
                    // Procesar video
                    var rutaConvertida = await ConvertirVideoAsync(
                        inputStream, carpetaDestino, extension, nombreBase, 20, 1920);

                    if (string.IsNullOrEmpty(rutaConvertida))
                    {
                        var errorFFmpeg = ObtenerUltimoError();
                        return MediaProcessingResult.Fallo(
                            "Error al procesar el video. Verifica que el archivo no esté corrupto.",
                            $"ConvertirVideoAsync retornó null.\nExtensión: {extension}\nTamaño: {tamanoOriginal / 1024}KB\nError FFmpeg: {errorFFmpeg ?? "desconocido"}");
                    }

                    var tamanoFinal = new FileInfo(rutaConvertida).Length;
                    _logger.LogInformation("[MediaConversion] ✅ Video procesado: {Original}MB -> {Final}MB ({Ratio}%)",
                        (tamanoOriginal / 1024.0 / 1024.0).ToString("F1"),
                        (tamanoFinal / 1024.0 / 1024.0).ToString("F1"),
                        Math.Round((double)tamanoFinal / tamanoOriginal * 100, 1));

                    return MediaProcessingResult.Exito(rutaConvertida, TipoMediaProcesado.Video, tamanoOriginal, tamanoFinal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaConversion] ❌ Error procesando archivo: {Extension}", extension);
                return MediaProcessingResult.Fallo(
                    "Error interno al procesar el archivo. Por favor intenta de nuevo.",
                    $"Excepción: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        #endregion
    }
}
