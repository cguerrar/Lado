using Microsoft.AspNetCore.Http;

namespace Lado.Services
{
    /// <summary>
    /// Servicio centralizado para validación segura de archivos subidos.
    /// Valida usando magic bytes (file signatures) además del Content-Type.
    /// </summary>
    public interface IFileValidationService
    {
        /// <summary>
        /// Valida si un archivo es una imagen válida (JPEG, PNG, GIF, WebP)
        /// </summary>
        Task<FileValidationResult> ValidarImagenAsync(IFormFile archivo);

        /// <summary>
        /// Valida si un archivo es un video válido (MP4, WebM, MOV, AVI)
        /// </summary>
        Task<FileValidationResult> ValidarVideoAsync(IFormFile archivo);

        /// <summary>
        /// Valida si un archivo es un audio válido (MP3, WAV, OGG, M4A)
        /// </summary>
        Task<FileValidationResult> ValidarAudioAsync(IFormFile archivo);

        /// <summary>
        /// Valida cualquier tipo de media (imagen, video o audio)
        /// </summary>
        Task<FileValidationResult> ValidarMediaAsync(IFormFile archivo);

        /// <summary>
        /// Obtiene el tipo real del archivo basado en magic bytes
        /// </summary>
        Task<string?> ObtenerTipoRealAsync(IFormFile archivo);
    }

    /// <summary>
    /// Tipo de archivo detectado por validación de magic bytes.
    /// Nombre distinto a Lado.Models.TipoArchivo para evitar conflictos.
    /// </summary>
    public enum TipoArchivoValidacion
    {
        Desconocido,
        Imagen,
        Video,
        Audio
    }

    public class FileValidationResult
    {
        public bool EsValido { get; set; }
        public string? TipoDetectado { get; set; }
        public string? Extension { get; set; }
        public string? MensajeError { get; set; }
        public TipoArchivoValidacion Tipo { get; set; }
    }

    public class FileValidationService : IFileValidationService
    {
        private readonly ILogger<FileValidationService> _logger;

        // Magic bytes para diferentes tipos de archivo
        private static readonly Dictionary<string, byte[][]> MagicBytes = new()
        {
            // Imágenes
            ["image/jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            ["image/png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            ["image/gif"] = new[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 } },
            ["image/webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // RIFF header, WebP tiene WEBP después
            ["image/bmp"] = new[] { new byte[] { 0x42, 0x4D } },
            ["image/tiff"] = new[] { new byte[] { 0x49, 0x49, 0x2A, 0x00 }, new byte[] { 0x4D, 0x4D, 0x00, 0x2A } },
            // HEIC/HEIF se detecta en DetectarTipoPorMagicBytes por brands específicos (heic, heix, mif1, etc.)

            // Videos - MP4 tiene muchos tamaños de ftyp atom posibles
            ["video/mp4"] = new[] {
                new byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70 }, // ftyp tamaño 20
                new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 }, // ftyp tamaño 24
                new byte[] { 0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70 }, // ftyp tamaño 28
                new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 }, // ftyp tamaño 32
                new byte[] { 0x00, 0x00, 0x00, 0x24, 0x66, 0x74, 0x79, 0x70 }, // ftyp tamaño 36
                new byte[] { 0x00, 0x00, 0x00, 0x28, 0x66, 0x74, 0x79, 0x70 }, // ftyp tamaño 40
                new byte[] { 0x00, 0x00, 0x00, 0x2C, 0x66, 0x74, 0x79, 0x70 }, // ftyp tamaño 44
            },
            ["video/webm"] = new[] { new byte[] { 0x1A, 0x45, 0xDF, 0xA3 } },
            ["video/quicktime"] = new[] {
                new byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x71, 0x74 } // ftypqt
            },
            ["video/x-msvideo"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // AVI usa RIFF
            ["video/x-matroska"] = new[] { new byte[] { 0x1A, 0x45, 0xDF, 0xA3 } }, // MKV

            // Audio
            ["audio/mpeg"] = new[] { new byte[] { 0xFF, 0xFB }, new byte[] { 0xFF, 0xFA }, new byte[] { 0x49, 0x44, 0x33 } }, // MP3
            ["audio/wav"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // WAV usa RIFF
            ["audio/ogg"] = new[] { new byte[] { 0x4F, 0x67, 0x67, 0x53 } },
            ["audio/aac"] = new[] { new byte[] { 0xFF, 0xF1 }, new byte[] { 0xFF, 0xF9 } },
            ["audio/flac"] = new[] { new byte[] { 0x66, 0x4C, 0x61, 0x43 } },
            ["audio/x-m4a"] = new[] { new byte[] { 0x00, 0x00, 0x00 } }, // M4A es MP4 audio
        };

        // Extensiones permitidas por tipo
        private static readonly Dictionary<TipoArchivoValidacion, HashSet<string>> ExtensionesPermitidas = new()
        {
            [TipoArchivoValidacion.Imagen] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic", ".heif", ".dng" },
            [TipoArchivoValidacion.Video] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v" },
            [TipoArchivoValidacion.Audio] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".mp3", ".wav", ".ogg", ".aac", ".flac", ".m4a" }
        };

        // Content-Types permitidos por tipo
        private static readonly Dictionary<TipoArchivoValidacion, HashSet<string>> ContentTypesPermitidos = new()
        {
            [TipoArchivoValidacion.Imagen] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp",
                  "image/heic", "image/heif", "image/heic-sequence", "image/heif-sequence",
                  "image/avif", "image/tiff", "image/x-adobe-dng", "image/dng" },
            [TipoArchivoValidacion.Video] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "video/mp4", "video/webm", "video/quicktime", "video/x-msvideo", "video/x-matroska", "video/mpeg" },
            [TipoArchivoValidacion.Audio] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "audio/mpeg", "audio/wav", "audio/ogg", "audio/aac", "audio/flac", "audio/x-m4a", "audio/mp4" }
        };

        public FileValidationService(ILogger<FileValidationService> logger)
        {
            _logger = logger;
        }

        public async Task<FileValidationResult> ValidarImagenAsync(IFormFile archivo)
        {
            return await ValidarArchivoAsync(archivo, TipoArchivoValidacion.Imagen);
        }

        public async Task<FileValidationResult> ValidarVideoAsync(IFormFile archivo)
        {
            return await ValidarArchivoAsync(archivo, TipoArchivoValidacion.Video);
        }

        public async Task<FileValidationResult> ValidarAudioAsync(IFormFile archivo)
        {
            return await ValidarArchivoAsync(archivo, TipoArchivoValidacion.Audio);
        }

        public async Task<FileValidationResult> ValidarMediaAsync(IFormFile archivo)
        {
            // Intentar validar como imagen primero, luego video, luego audio
            var resultadoImagen = await ValidarArchivoAsync(archivo, TipoArchivoValidacion.Imagen);
            if (resultadoImagen.EsValido) return resultadoImagen;

            var resultadoVideo = await ValidarArchivoAsync(archivo, TipoArchivoValidacion.Video);
            if (resultadoVideo.EsValido) return resultadoVideo;

            var resultadoAudio = await ValidarArchivoAsync(archivo, TipoArchivoValidacion.Audio);
            if (resultadoAudio.EsValido) return resultadoAudio;

            return new FileValidationResult
            {
                EsValido = false,
                MensajeError = "El archivo no es un tipo de media válido (imagen, video o audio)",
                Tipo = TipoArchivoValidacion.Desconocido
            };
        }

        public async Task<string?> ObtenerTipoRealAsync(IFormFile archivo)
        {
            var header = await LeerHeaderAsync(archivo, 64);
            return DetectarTipoPorMagicBytes(header);
        }

        private async Task<FileValidationResult> ValidarArchivoAsync(IFormFile archivo, TipoArchivoValidacion tipoEsperado)
        {
            var resultado = new FileValidationResult
            {
                Tipo = tipoEsperado,
                Extension = Path.GetExtension(archivo.FileName)?.ToLowerInvariant()
            };

            // 1. Validar que el archivo no esté vacío
            if (archivo == null || archivo.Length == 0)
            {
                resultado.EsValido = false;
                resultado.MensajeError = "El archivo está vacío";
                return resultado;
            }

            // 2. Validar extensión
            if (string.IsNullOrEmpty(resultado.Extension) ||
                !ExtensionesPermitidas[tipoEsperado].Contains(resultado.Extension))
            {
                resultado.EsValido = false;
                resultado.MensajeError = $"Extensión no permitida: {resultado.Extension}. Extensiones válidas: {string.Join(", ", ExtensionesPermitidas[tipoEsperado])}";
                _logger.LogWarning("[FileValidation] Extensión no permitida - Archivo: {FileName}, Extensión: {Extension}, Tipo esperado: {Tipo}, ContentType: {ContentType}, Tamaño: {Size}MB",
                    archivo.FileName, resultado.Extension, tipoEsperado, archivo.ContentType, archivo.Length / (1024.0 * 1024.0));
                return resultado;
            }

            // 3. Validar Content-Type declarado
            var contentType = archivo.ContentType?.ToLowerInvariant() ?? "";
            if (!ContentTypesPermitidos[tipoEsperado].Contains(contentType))
            {
                // Permitir algunos casos edge (ej: video/mp4 para .m4v)
                var esContentTypeGenericoPermitido = tipoEsperado switch
                {
                    TipoArchivoValidacion.Video => contentType.StartsWith("video/"),
                    TipoArchivoValidacion.Imagen => contentType.StartsWith("image/"),
                    TipoArchivoValidacion.Audio => contentType.StartsWith("audio/"),
                    _ => false
                };

                // Permitir application/octet-stream para formatos que los navegadores no reconocen bien
                // (HEIC, HEIF, AVIF, DNG) - la validación de magic bytes verificará el tipo real
                var extensionesConOctetStream = new[] { ".heic", ".heif", ".avif", ".dng" };
                var esOctetStreamPermitido = contentType == "application/octet-stream" &&
                    extensionesConOctetStream.Contains(resultado.Extension);

                if (!esContentTypeGenericoPermitido && !esOctetStreamPermitido)
                {
                    resultado.EsValido = false;
                    resultado.MensajeError = $"Content-Type no válido: {contentType}";
                    _logger.LogWarning("[FileValidation] Content-Type no válido - Archivo: {FileName}, ContentType: {ContentType}, Tipo esperado: {Tipo}, Extensión: {Extension}, Tamaño: {Size}MB",
                        archivo.FileName, contentType, tipoEsperado, resultado.Extension, archivo.Length / (1024.0 * 1024.0));
                    return resultado;
                }

                if (esOctetStreamPermitido)
                {
                    _logger.LogInformation("[FileValidation] Permitiendo octet-stream para extensión {Extension}, validación por magic bytes", resultado.Extension);
                }
            }

            // 4. CRÍTICO: Validar magic bytes (firma del archivo)
            // Leer 64 bytes para asegurar detección de HEIC/HEIF (necesita ftyp + brands)
            var header = await LeerHeaderAsync(archivo, 64);
            var tipoDetectado = DetectarTipoPorMagicBytes(header);

            if (string.IsNullOrEmpty(tipoDetectado))
            {
                resultado.EsValido = false;
                resultado.MensajeError = "No se pudo verificar el tipo real del archivo. El archivo puede estar corrupto o no ser válido.";
                _logger.LogWarning("[FileValidation] Magic bytes no reconocidos - Archivo: {FileName}, Extensión: {Extension}, ContentType: {ContentType}, Tamaño: {Size}MB, Header (hex): {Header}",
                    archivo.FileName, resultado.Extension, archivo.ContentType, archivo.Length / (1024.0 * 1024.0), BitConverter.ToString(header.Take(20).ToArray()));
                return resultado;
            }

            resultado.TipoDetectado = tipoDetectado;

            // 5. Verificar que el tipo detectado coincida con el esperado
            var tipoDetectadoCategoria = ObtenerCategoriaTipo(tipoDetectado);
            if (tipoDetectadoCategoria != tipoEsperado)
            {
                resultado.EsValido = false;
                resultado.MensajeError = $"El contenido real del archivo ({tipoDetectado}) no coincide con el tipo esperado ({tipoEsperado})";
                _logger.LogWarning("[FileValidation] Tipo detectado no coincide - Archivo: {FileName}, ContentType declarado: {ContentType}, Tipo real detectado: {TipoReal}, Categoría detectada: {CategoriaReal}, Categoría esperada: {CategoriaEsperada}, Tamaño: {Size}MB",
                    archivo.FileName, contentType, tipoDetectado, tipoDetectadoCategoria, tipoEsperado, archivo.Length / (1024.0 * 1024.0));
                return resultado;
            }

            // 6. Todo OK
            resultado.EsValido = true;
            _logger.LogInformation("[FileValidation] Archivo validado OK - Archivo: {FileName}, Tipo detectado: {Tipo}, Tamaño: {Size}MB",
                archivo.FileName, tipoDetectado, archivo.Length / (1024.0 * 1024.0));
            return resultado;
        }

        private async Task<byte[]> LeerHeaderAsync(IFormFile archivo, int bytes)
        {
            var buffer = new byte[bytes];
            using var stream = archivo.OpenReadStream();
            var bytesRead = await stream.ReadAsync(buffer, 0, bytes);

            if (bytesRead < bytes)
            {
                Array.Resize(ref buffer, bytesRead);
            }

            return buffer;
        }

        private string? DetectarTipoPorMagicBytes(byte[] header)
        {
            if (header.Length < 4) return null;

            foreach (var (mimeType, signatures) in MagicBytes)
            {
                foreach (var signature in signatures)
                {
                    if (header.Length >= signature.Length &&
                        header.Take(signature.Length).SequenceEqual(signature))
                    {
                        // Validación adicional para RIFF (puede ser WAV, AVI o WebP)
                        if (signature.SequenceEqual(new byte[] { 0x52, 0x49, 0x46, 0x46 }) && header.Length >= 12)
                        {
                            var format = System.Text.Encoding.ASCII.GetString(header, 8, 4);
                            return format switch
                            {
                                "WAVE" => "audio/wav",
                                "AVI " => "video/x-msvideo",
                                "WEBP" => "image/webp",
                                _ => null
                            };
                        }

                        // Validación adicional para MP4/MOV (buscar 'ftyp')
                        if (mimeType.StartsWith("video/") && header.Length >= 8)
                        {
                            var hasFtyp = false;
                            for (int i = 0; i < Math.Min(header.Length - 4, 16); i++)
                            {
                                if (header[i] == 0x66 && header[i + 1] == 0x74 &&
                                    header[i + 2] == 0x79 && header[i + 3] == 0x70)
                                {
                                    hasFtyp = true;
                                    break;
                                }
                            }
                            if (hasFtyp) return "video/mp4";
                        }

                        return mimeType;
                    }
                }
            }

            // Detección adicional para archivos que no coinciden exactamente

            // HEIC/HEIF/AVIF - Buscar "ftyp" seguido de brands de imagen (heic, heix, mif1, msf1, avif)
            // Formato ISOBMFF: [4 bytes size][4 bytes "ftyp"][4 bytes major brand][4 bytes minor version][compatible brands...]
            if (header.Length >= 12)
            {
                int ftypPos = -1;
                // Buscar "ftyp" en los primeros bytes
                for (int i = 0; i < Math.Min(header.Length - 4, 16); i++)
                {
                    if (header[i] == 0x66 && header[i + 1] == 0x74 &&
                        header[i + 2] == 0x79 && header[i + 3] == 0x70)
                    {
                        ftypPos = i;
                        break;
                    }
                }

                if (ftypPos >= 0 && ftypPos + 8 <= header.Length)
                {
                    // Leer el major brand (4 bytes después de "ftyp")
                    var majorBrand = System.Text.Encoding.ASCII.GetString(header, ftypPos + 4, 4);

                    // Brands de HEIC/HEIF/AVIF
                    var heicBrands = new[] { "heic", "heix", "hevc", "hevx", "mif1", "msf1", "avif", "avis" };
                    if (heicBrands.Any(b => majorBrand.Equals(b, StringComparison.OrdinalIgnoreCase)))
                    {
                        return majorBrand.StartsWith("avif", StringComparison.OrdinalIgnoreCase)
                            ? "image/avif"
                            : "image/heic";
                    }

                    // Si el major brand no es HEIC, verificar compatible brands (después de minor version)
                    // Buscar en más bytes ya que puede haber múltiples brands compatibles
                    if (ftypPos + 16 <= header.Length)
                    {
                        for (int offset = ftypPos + 12; offset + 4 <= header.Length && offset < ftypPos + 56; offset += 4)
                        {
                            var compatBrand = System.Text.Encoding.ASCII.GetString(header, offset, 4);
                            if (heicBrands.Any(b => compatBrand.Equals(b, StringComparison.OrdinalIgnoreCase)))
                            {
                                return compatBrand.StartsWith("avif", StringComparison.OrdinalIgnoreCase)
                                    ? "image/avif"
                                    : "image/heic";
                            }
                        }
                    }
                }
            }

            // MP4/M4V/MOV - Buscar "ftyp" en los primeros 32 bytes (cualquier tamaño de atom)
            if (header.Length >= 8)
            {
                for (int i = 0; i < Math.Min(header.Length - 4, 32); i++)
                {
                    // Buscar secuencia "ftyp" (0x66, 0x74, 0x79, 0x70)
                    if (header[i] == 0x66 && header[i + 1] == 0x74 &&
                        header[i + 2] == 0x79 && header[i + 3] == 0x70)
                    {
                        return "video/mp4";
                    }
                }
            }

            // WebM/MKV - EBML header (0x1A, 0x45, 0xDF, 0xA3)
            if (header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 &&
                header[2] == 0xDF && header[3] == 0xA3)
            {
                return "video/webm";
            }

            // MP3 con ID3 tag
            if (header.Length >= 3 && header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
            {
                return "audio/mpeg";
            }

            // MP3 sin ID3 (frame sync)
            if (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
            {
                return "audio/mpeg";
            }

            return null;
        }

        private TipoArchivoValidacion ObtenerCategoriaTipo(string mimeType)
        {
            if (mimeType.StartsWith("image/")) return TipoArchivoValidacion.Imagen;
            if (mimeType.StartsWith("video/")) return TipoArchivoValidacion.Video;
            if (mimeType.StartsWith("audio/")) return TipoArchivoValidacion.Audio;
            return TipoArchivoValidacion.Desconocido;
        }
    }
}
