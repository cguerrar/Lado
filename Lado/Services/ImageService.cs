using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using ImageMagick;

namespace Lado.Services
{
    public interface IImageService
    {
        /// <summary>
        /// Genera un thumbnail de una imagen
        /// </summary>
        /// <param name="rutaOriginal">Ruta completa de la imagen original</param>
        /// <param name="maxWidth">Ancho máximo del thumbnail</param>
        /// <param name="maxHeight">Alto máximo del thumbnail</param>
        /// <param name="quality">Calidad de compresión (1-100)</param>
        /// <returns>Ruta del thumbnail generado</returns>
        Task<string?> GenerarThumbnailAsync(string rutaOriginal, int maxWidth = 400, int maxHeight = 400, int quality = 80);

        /// <summary>
        /// Genera un thumbnail a partir de un stream
        /// </summary>
        Task<string?> GenerarThumbnailAsync(Stream imageStream, string carpetaDestino, string nombreArchivo, int maxWidth = 400, int maxHeight = 400, int quality = 80);

        /// <summary>
        /// Comprime una imagen existente
        /// </summary>
        Task<bool> ComprimirImagenAsync(string rutaImagen, int quality = 85);

        /// <summary>
        /// Verifica si un archivo es una imagen válida
        /// </summary>
        bool EsImagenValida(string extension);

        /// <summary>
        /// Verifica si la extensión requiere conversión a JPEG (RAW, HEIC, DNG, etc.)
        /// </summary>
        bool RequiereConversion(string extension);

        /// <summary>
        /// Convierte formatos RAW/HEIC/DNG a JPEG usando Magick.NET
        /// </summary>
        /// <param name="inputStream">Stream del archivo original</param>
        /// <param name="carpetaDestino">Carpeta donde guardar el JPEG</param>
        /// <param name="nombreBase">Nombre base del archivo (sin extensión)</param>
        /// <param name="maxDimension">Dimensión máxima (ancho o alto)</param>
        /// <param name="quality">Calidad JPEG (1-100)</param>
        /// <returns>Ruta del archivo JPEG convertido, o null si falla</returns>
        Task<string?> ConvertirAJpegAsync(Stream inputStream, string carpetaDestino, string nombreBase, int maxDimension = 2048, int quality = 85);
    }

    public class ImageService : IImageService
    {
        private readonly ILogger<ImageService> _logger;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

        // Extensiones que requieren conversión a JPEG (formatos RAW/especiales)
        private static readonly string[] ExtensionesConversion = { ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw", ".orf", ".rw2", ".tiff", ".tif" };

        public ImageService(ILogger<ImageService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public bool EsImagenValida(string extension)
        {
            return ExtensionesPermitidas.Contains(extension.ToLower());
        }

        public async Task<string?> GenerarThumbnailAsync(string rutaOriginal, int maxWidth = 400, int maxHeight = 400, int quality = 80)
        {
            try
            {
                if (!File.Exists(rutaOriginal))
                {
                    _logger.LogWarning("Archivo no encontrado para thumbnail: {Ruta}", rutaOriginal);
                    return null;
                }

                var extension = Path.GetExtension(rutaOriginal).ToLower();
                if (!EsImagenValida(extension))
                {
                    _logger.LogWarning("Extensión no válida para thumbnail: {Extension}", extension);
                    return null;
                }

                var directorio = Path.GetDirectoryName(rutaOriginal);
                var nombreSinExtension = Path.GetFileNameWithoutExtension(rutaOriginal);
                var rutaThumbnail = Path.Combine(directorio!, $"{nombreSinExtension}_thumb.jpg");

                using var image = await Image.LoadAsync(rutaOriginal);

                // Redimensionar manteniendo proporción
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, maxHeight),
                    Mode = ResizeMode.Max
                }));

                // Guardar como JPEG con compresión
                var encoder = new JpegEncoder
                {
                    Quality = quality
                };

                await image.SaveAsJpegAsync(rutaThumbnail, encoder);

                _logger.LogInformation("Thumbnail generado: {Ruta}", rutaThumbnail);

                // Retornar ruta relativa para web
                return ConvertirARutaWeb(rutaThumbnail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando thumbnail para {Ruta}", rutaOriginal);
                return null;
            }
        }

        public async Task<string?> GenerarThumbnailAsync(Stream imageStream, string carpetaDestino, string nombreArchivo, int maxWidth = 400, int maxHeight = 400, int quality = 80)
        {
            try
            {
                var extension = Path.GetExtension(nombreArchivo).ToLower();
                if (!EsImagenValida(extension))
                {
                    return null;
                }

                var nombreSinExtension = Path.GetFileNameWithoutExtension(nombreArchivo);
                var rutaThumbnail = Path.Combine(carpetaDestino, $"{nombreSinExtension}_thumb.jpg");

                // Asegurar que el directorio existe
                Directory.CreateDirectory(carpetaDestino);

                // Resetear posición del stream si es posible
                if (imageStream.CanSeek)
                {
                    imageStream.Position = 0;
                }

                using var image = await Image.LoadAsync(imageStream);

                // Redimensionar manteniendo proporción
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxWidth, maxHeight),
                    Mode = ResizeMode.Max
                }));

                // Guardar como JPEG con compresión
                var encoder = new JpegEncoder
                {
                    Quality = quality
                };

                await image.SaveAsJpegAsync(rutaThumbnail, encoder);

                _logger.LogInformation("Thumbnail generado desde stream: {Ruta}", rutaThumbnail);

                return ConvertirARutaWeb(rutaThumbnail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando thumbnail desde stream");
                return null;
            }
        }

        public async Task<bool> ComprimirImagenAsync(string rutaImagen, int quality = 85)
        {
            try
            {
                if (!File.Exists(rutaImagen))
                {
                    return false;
                }

                var extension = Path.GetExtension(rutaImagen).ToLower();
                if (!EsImagenValida(extension))
                {
                    return false;
                }

                // Cargar imagen
                using var image = await Image.LoadAsync(rutaImagen);

                // Si la imagen es muy grande, redimensionar
                if (image.Width > 1920 || image.Height > 1920)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(1920, 1920),
                        Mode = ResizeMode.Max
                    }));
                }

                // Guardar con compresión
                var rutaTemporal = rutaImagen + ".tmp";

                var encoder = new JpegEncoder
                {
                    Quality = quality
                };

                await image.SaveAsJpegAsync(rutaTemporal, encoder);

                // Reemplazar archivo original
                File.Delete(rutaImagen);
                File.Move(rutaTemporal, Path.ChangeExtension(rutaImagen, ".jpg"));

                _logger.LogInformation("Imagen comprimida: {Ruta}", rutaImagen);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comprimiendo imagen {Ruta}", rutaImagen);
                return false;
            }
        }

        private string ConvertirARutaWeb(string rutaFisica)
        {
            // Convertir ruta física a ruta web relativa
            var wwwroot = _environment.WebRootPath;
            if (rutaFisica.StartsWith(wwwroot, StringComparison.OrdinalIgnoreCase))
            {
                var rutaRelativa = rutaFisica.Substring(wwwroot.Length);
                return rutaRelativa.Replace("\\", "/");
            }
            return rutaFisica;
        }

        public bool RequiereConversion(string extension)
        {
            return ExtensionesConversion.Contains(extension.ToLower());
        }

        public async Task<string?> ConvertirAJpegAsync(Stream inputStream, string carpetaDestino, string nombreBase, int maxDimension = 2048, int quality = 85)
        {
            try
            {
                _logger.LogWarning("[ImageConversion] INICIANDO conversión de {Nombre} a JPEG", nombreBase);

                // Asegurar que el directorio existe
                Directory.CreateDirectory(carpetaDestino);

                var rutaDestino = Path.Combine(carpetaDestino, $"{nombreBase}.jpg");
                var rutaTemporal = Path.Combine(carpetaDestino, $"{nombreBase}_temp.dng");

                // Guardar el archivo temporalmente para procesarlo con exiftool
                using (var fileStream = new FileStream(rutaTemporal, FileMode.Create))
                {
                    await inputStream.CopyToAsync(fileStream);
                }

                // Intentar extraer preview con exiftool (mejor calidad para DNG)
                var previewExtraida = await ExtraerPreviewConExiftool(rutaTemporal, rutaDestino);

                if (previewExtraida && File.Exists(rutaDestino))
                {
                    // Limpiar archivo temporal
                    try { File.Delete(rutaTemporal); } catch { }

                    // Redimensionar si es necesario
                    await RedimensionarSiNecesario(rutaDestino, maxDimension, quality);

                    var archivoFinal = new FileInfo(rutaDestino);
                    _logger.LogWarning("[ImageConversion] Preview extraída con exiftool: {Ruta}, Tamaño: {Size}MB",
                        rutaDestino, (archivoFinal.Length / 1024.0 / 1024.0).ToString("F2"));

                    return rutaDestino;
                }

                _logger.LogWarning("[ImageConversion] Exiftool falló, usando Magick.NET como fallback...");

                // Fallback: usar Magick.NET
                using var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(rutaTemporal, FileMode.Open))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }
                memoryStream.Position = 0;

                // Limpiar archivo temporal
                try { File.Delete(rutaTemporal); } catch { }

                // Para DNG/RAW: intentar extraer la preview embebida (ya procesada)
                // Los DNG de iPhone tienen una preview JPEG de alta calidad
                using var images = new MagickImageCollection();

                try
                {
                    // Leer todas las imágenes/capas del archivo
                    images.Read(memoryStream);

                    _logger.LogWarning("[ImageConversion] Archivo tiene {Count} imagen(es)/capa(s)", images.Count);

                    MagickImage? imagenFinal = null;

                    if (images.Count > 1)
                    {
                        // DNG típicamente tiene: [0] = thumbnail, [1] = preview grande, [2+] = RAW
                        // Buscar la preview más grande que no sea el RAW
                        MagickImage? mejorPreview = null;
                        long mejorPixeles = 0;

                        foreach (var img in images)
                        {
                            var pixeles = (long)img.Width * img.Height;
                            _logger.LogInformation("[ImageConversion] Capa: {Width}x{Height}, Format: {Format}",
                                img.Width, img.Height, img.Format);

                            // La preview suele ser más pequeña que el RAW pero aún grande
                            // RAW de iPhone es ~8000x6000, preview es ~1500-3000
                            if (pixeles > mejorPixeles && img.Width < 5000)
                            {
                                mejorPixeles = pixeles;
                                mejorPreview = (MagickImage)img;
                            }
                        }

                        if (mejorPreview != null)
                        {
                            _logger.LogInformation("[ImageConversion] Usando preview embebida: {Width}x{Height}",
                                mejorPreview.Width, mejorPreview.Height);
                            imagenFinal = (MagickImage)mejorPreview.Clone();
                        }
                    }

                    // Si no encontramos preview, usar la imagen principal
                    if (imagenFinal == null)
                    {
                        imagenFinal = (MagickImage)images[0].Clone();
                        _logger.LogInformation("[ImageConversion] Usando imagen principal: {Width}x{Height}",
                            imagenFinal.Width, imagenFinal.Height);

                        // Solo aplicar correcciones si es RAW (imagen muy grande)
                        if (imagenFinal.Width > 4000 || imagenFinal.Height > 4000)
                        {
                            _logger.LogInformation("[ImageConversion] Aplicando correcciones para RAW...");
                            imagenFinal.AutoLevel();
                            imagenFinal.BrightnessContrast(new Percentage(10), new Percentage(5));
                        }
                    }

                    // Auto-orientar según EXIF
                    imagenFinal.AutoOrient();

                    // Redimensionar si excede el máximo
                    if (imagenFinal.Width > maxDimension || imagenFinal.Height > maxDimension)
                    {
                        var geometry = new MagickGeometry((uint)maxDimension, (uint)maxDimension)
                        {
                            IgnoreAspectRatio = false
                        };
                        imagenFinal.Resize(geometry);
                        _logger.LogInformation("[ImageConversion] Redimensionado a: {Width}x{Height}",
                            imagenFinal.Width, imagenFinal.Height);
                    }

                    // Configurar calidad y formato
                    imagenFinal.Format = MagickFormat.Jpeg;
                    imagenFinal.Quality = (uint)quality;
                    imagenFinal.SetProfile(ColorProfile.SRGB);

                    // Guardar
                    await imagenFinal.WriteAsync(rutaDestino);
                    imagenFinal.Dispose();
                }
                catch (MagickException ex)
                {
                    _logger.LogWarning(ex, "[ImageConversion] Error leyendo capas, intentando lectura simple...");

                    // Fallback: lectura simple
                    memoryStream.Position = 0;
                    using var image = new MagickImage(memoryStream);

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
                }

                var fileInfo = new FileInfo(rutaDestino);
                _logger.LogInformation("[ImageConversion] Conversión exitosa: {Ruta}, Tamaño: {Size}MB",
                    rutaDestino, (fileInfo.Length / 1024.0 / 1024.0).ToString("F2"));

                return rutaDestino;
            }
            catch (MagickException ex)
            {
                _logger.LogError(ex, "[ImageConversion] Error de Magick.NET convirtiendo {Nombre}: {Message}", nombreBase, ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ImageConversion] Error convirtiendo {Nombre} a JPEG", nombreBase);
                return null;
            }
        }

        /// <summary>
        /// Extrae la preview JPEG embebida de un archivo DNG/RAW usando exiftool
        /// </summary>
        private async Task<bool> ExtraerPreviewConExiftool(string rutaOrigen, string rutaDestino)
        {
            try
            {
                // Buscar exiftool
                var exiftoolPath = "exiftool";

                // Intentar extraer JpgFromRaw primero (mayor calidad)
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exiftoolPath,
                    Arguments = $"-b -JpgFromRaw \"{rutaOrigen}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null) return false;

                // Leer el output binario (la imagen)
                using (var outputStream = new FileStream(rutaDestino, FileMode.Create))
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(outputStream);
                }

                await process.WaitForExitAsync();

                // Verificar si se extrajo algo
                var fileInfo = new FileInfo(rutaDestino);
                if (fileInfo.Exists && fileInfo.Length > 10000) // Al menos 10KB
                {
                    _logger.LogWarning("[Exiftool] JpgFromRaw extraído: {Size}KB", fileInfo.Length / 1024);
                    return true;
                }

                // Si JpgFromRaw falló, intentar PreviewImage
                File.Delete(rutaDestino);

                processInfo.Arguments = $"-b -PreviewImage \"{rutaOrigen}\"";
                using var process2 = System.Diagnostics.Process.Start(processInfo);
                if (process2 == null) return false;

                using (var outputStream = new FileStream(rutaDestino, FileMode.Create))
                {
                    await process2.StandardOutput.BaseStream.CopyToAsync(outputStream);
                }

                await process2.WaitForExitAsync();

                fileInfo = new FileInfo(rutaDestino);
                if (fileInfo.Exists && fileInfo.Length > 10000)
                {
                    _logger.LogWarning("[Exiftool] PreviewImage extraído: {Size}KB", fileInfo.Length / 1024);
                    return true;
                }

                _logger.LogWarning("[Exiftool] No se encontró preview embebida");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Exiftool] Error extrayendo preview");
                return false;
            }
        }

        /// <summary>
        /// Redimensiona una imagen si excede el tamaño máximo
        /// </summary>
        private async Task RedimensionarSiNecesario(string rutaImagen, int maxDimension, int quality)
        {
            try
            {
                using var image = new MagickImage(rutaImagen);

                // Auto-orientar según EXIF
                image.AutoOrient();

                bool necesitaGuardar = false;

                // Redimensionar si excede el máximo
                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    var geometry = new MagickGeometry((uint)maxDimension, (uint)maxDimension)
                    {
                        IgnoreAspectRatio = false
                    };
                    image.Resize(geometry);
                    necesitaGuardar = true;
                    _logger.LogWarning("[ImageConversion] Redimensionado a: {Width}x{Height}", image.Width, image.Height);
                }

                if (necesitaGuardar || image.Orientation != OrientationType.TopLeft)
                {
                    image.Quality = (uint)quality;
                    await image.WriteAsync(rutaImagen);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ImageConversion] Error redimensionando imagen");
            }
        }
    }
}
