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

            // Videos
            ["video/mp4"] = new[] {
                new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 }, // ftyp
                new byte[] { 0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70 },
                new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 }
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
                { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" },
            [TipoArchivoValidacion.Video] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v" },
            [TipoArchivoValidacion.Audio] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".mp3", ".wav", ".ogg", ".aac", ".flac", ".m4a" }
        };

        // Content-Types permitidos por tipo
        private static readonly Dictionary<TipoArchivoValidacion, HashSet<string>> ContentTypesPermitidos = new()
        {
            [TipoArchivoValidacion.Imagen] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp" },
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
            var header = await LeerHeaderAsync(archivo, 32);
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
                _logger.LogWarning("Extensión no permitida: {Extension} para tipo {Tipo}", resultado.Extension, tipoEsperado);
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

                if (!esContentTypeGenericoPermitido)
                {
                    resultado.EsValido = false;
                    resultado.MensajeError = $"Content-Type no válido: {contentType}";
                    _logger.LogWarning("Content-Type no válido: {ContentType} para tipo {Tipo}", contentType, tipoEsperado);
                    return resultado;
                }
            }

            // 4. CRÍTICO: Validar magic bytes (firma del archivo)
            var header = await LeerHeaderAsync(archivo, 32);
            var tipoDetectado = DetectarTipoPorMagicBytes(header);

            if (string.IsNullOrEmpty(tipoDetectado))
            {
                resultado.EsValido = false;
                resultado.MensajeError = "No se pudo verificar el tipo real del archivo. El archivo puede estar corrupto o no ser válido.";
                _logger.LogWarning("Magic bytes no reconocidos para archivo: {FileName}, Header: {Header}",
                    archivo.FileName, BitConverter.ToString(header.Take(16).ToArray()));
                return resultado;
            }

            resultado.TipoDetectado = tipoDetectado;

            // 5. Verificar que el tipo detectado coincida con el esperado
            var tipoDetectadoCategoria = ObtenerCategoriaTipo(tipoDetectado);
            if (tipoDetectadoCategoria != tipoEsperado)
            {
                resultado.EsValido = false;
                resultado.MensajeError = $"El contenido real del archivo ({tipoDetectado}) no coincide con el tipo esperado ({tipoEsperado})";
                _logger.LogWarning("Posible spoofing detectado: {FileName} declarado como {ContentType} pero es {TipoReal}",
                    archivo.FileName, contentType, tipoDetectado);
                return resultado;
            }

            // 6. Todo OK
            resultado.EsValido = true;
            _logger.LogDebug("Archivo validado correctamente: {FileName}, Tipo: {Tipo}", archivo.FileName, tipoDetectado);
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
