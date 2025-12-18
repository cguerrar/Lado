using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;

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
    }

    public class ImageService : IImageService
    {
        private readonly ILogger<ImageService> _logger;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

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
    }
}
