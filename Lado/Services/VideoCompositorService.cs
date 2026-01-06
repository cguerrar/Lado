using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Lado.Models;

namespace Lado.Services
{
    /// <summary>
    /// Servicio para composición de videos con FFmpeg
    /// Permite crear Beat Sync videos con transiciones y efectos
    /// </summary>
    public interface IVideoCompositorService
    {
        /// <summary>
        /// Genera un video Beat Sync a partir de imágenes y audio
        /// </summary>
        Task<VideoCompositorResult> GenerarBeatSyncAsync(BeatSyncRequest request);

        /// <summary>
        /// Aplica transiciones entre clips de video
        /// </summary>
        Task<string?> AplicarTransicionesAsync(List<string> clips, string transicion, string outputPath, double duracionTransicion = 0.3);

        /// <summary>
        /// Combina video y audio
        /// </summary>
        Task<string?> CombinarVideoAudioAsync(string videoPath, string audioPath, string outputPath, double trimStart = 0, double duration = 0);

        /// <summary>
        /// Aplica efectos de video (slow motion, reverse, etc.)
        /// </summary>
        Task<string?> AplicarEfectoAsync(string videoPath, string efecto, string outputPath, Dictionary<string, object>? parametros = null);
    }

    public class BeatSyncRequest
    {
        public List<string> ImagePaths { get; set; } = new();
        public string AudioPath { get; set; } = string.Empty;
        public List<double> BeatTimes { get; set; } = new();
        public string Transicion { get; set; } = "cut";
        public double TrimStart { get; set; } = 0;
        public double TrimEnd { get; set; } = 30;
        public string OutputPath { get; set; } = string.Empty;
        public int Width { get; set; } = 1080;
        public int Height { get; set; } = 1920;
        public int Fps { get; set; } = 30;
        public int Crf { get; set; } = 23;
    }

    public class VideoCompositorResult
    {
        public bool Success { get; set; }
        public string? OutputPath { get; set; }
        public string? Error { get; set; }
        public double Duration { get; set; }
        public long FileSize { get; set; }
    }

    public class VideoCompositorService : IVideoCompositorService
    {
        private readonly ILogger<VideoCompositorService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogEventoService? _logEventoService;
        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;
        private readonly string _tempPath;

        // Mapeo de transiciones a filtros FFmpeg xfade
        private static readonly Dictionary<string, string> TransicionesFfmpeg = new()
        {
            { "cut", "" }, // Sin transición
            { "fade", "fade" },
            { "zoom", "zoomin" },
            { "zoom-out", "fadeblack" },
            { "flash", "fadewhite" },
            { "slide", "slideleft" },
            { "wipe-left", "wipeleft" },
            { "wipe-right", "wiperight" },
            { "wipe-up", "wipeup" },
            { "wipe-down", "wipedown" },
            { "spin", "circleopen" },
            { "pixelize", "pixelize" },
            { "blur", "smoothleft" },
            { "glitch", "horzopen" },
            { "shake", "vertopen" }
        };

        public VideoCompositorService(
            ILogger<VideoCompositorService> logger,
            IWebHostEnvironment environment,
            ILogEventoService? logEventoService = null)
        {
            _logger = logger;
            _environment = environment;
            _logEventoService = logEventoService;

            // Buscar FFmpeg
            var ubicaciones = new[]
            {
                Path.Combine(_environment.ContentRootPath, "Tools", "ffmpeg", "ffmpeg.exe"),
                @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
                @"C:\ffmpeg\bin\ffmpeg.exe",
            };

            _ffmpegPath = "ffmpeg";
            _ffprobePath = "ffprobe";

            foreach (var ruta in ubicaciones)
            {
                if (File.Exists(ruta))
                {
                    _ffmpegPath = ruta;
                    _ffprobePath = Path.Combine(Path.GetDirectoryName(ruta)!, "ffprobe.exe");
                    _logger.LogInformation("[VideoCompositor] FFmpeg: {Path}", _ffmpegPath);
                    break;
                }
            }

            // Crear carpeta temporal
            _tempPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "temp", "compositor");
            Directory.CreateDirectory(_tempPath);
        }

        public async Task<VideoCompositorResult> GenerarBeatSyncAsync(BeatSyncRequest request)
        {
            var result = new VideoCompositorResult();
            var tempFiles = new List<string>();

            try
            {
                _logger.LogInformation("[VideoCompositor] Iniciando Beat Sync: {Images} imágenes, {Beats} beats",
                    request.ImagePaths.Count, request.BeatTimes.Count);

                // Validar request
                if (request.ImagePaths.Count < 2)
                {
                    result.Error = "Se requieren al menos 2 imágenes";
                    return result;
                }

                if (string.IsNullOrEmpty(request.AudioPath) || !File.Exists(request.AudioPath))
                {
                    result.Error = "Audio no encontrado";
                    return result;
                }

                var duration = request.TrimEnd - request.TrimStart;
                if (duration <= 0)
                {
                    result.Error = "Duración inválida";
                    return result;
                }

                // Calcular beats válidos y asignar imágenes
                var validBeats = request.BeatTimes
                    .Select(b => b - request.TrimStart)
                    .Where(b => b >= 0 && b <= duration)
                    .ToList();

                if (validBeats.Count == 0)
                {
                    // Crear beats uniformes
                    var interval = duration / Math.Max(request.ImagePaths.Count, 5);
                    for (double t = 0; t < duration; t += interval)
                    {
                        validBeats.Add(t);
                    }
                }

                // Asegurar que hay un beat al inicio
                if (validBeats.Count == 0 || validBeats[0] > 0)
                {
                    validBeats.Insert(0, 0);
                }

                // Agregar beat final para la última imagen
                if (validBeats[^1] < duration - 0.1)
                {
                    validBeats.Add(duration);
                }

                _logger.LogInformation("[VideoCompositor] Beats válidos: {Count}, Duración: {Duration}s",
                    validBeats.Count, duration);

                // Generar clips de imagen con duración basada en beats
                var clipPaths = new List<string>();
                var clipDurations = new List<double>();

                for (int i = 0; i < validBeats.Count - 1; i++)
                {
                    var imageIndex = i % request.ImagePaths.Count;
                    var imagePath = request.ImagePaths[imageIndex];
                    var clipDuration = validBeats[i + 1] - validBeats[i];

                    if (clipDuration < 0.05) continue; // Ignorar clips muy cortos

                    var clipPath = Path.Combine(_tempPath, $"clip_{Guid.NewGuid()}.mp4");
                    tempFiles.Add(clipPath);

                    // Crear clip de video desde imagen
                    var imageToVideoResult = await ImagenAVideoAsync(
                        imagePath, clipPath, clipDuration,
                        request.Width, request.Height, request.Fps, request.Crf);

                    if (!imageToVideoResult)
                    {
                        _logger.LogWarning("[VideoCompositor] Error creando clip {Index}", i);
                        continue;
                    }

                    clipPaths.Add(clipPath);
                    clipDurations.Add(clipDuration);
                }

                if (clipPaths.Count < 2)
                {
                    result.Error = "No se pudieron crear suficientes clips";
                    return result;
                }

                // Concatenar clips con transiciones
                var videoSinAudio = Path.Combine(_tempPath, $"video_{Guid.NewGuid()}.mp4");
                tempFiles.Add(videoSinAudio);

                string? concatResult;
                if (request.Transicion == "cut" || string.IsNullOrEmpty(request.Transicion))
                {
                    // Concatenación simple sin transiciones
                    concatResult = await ConcatenarClipsAsync(clipPaths, videoSinAudio);
                }
                else
                {
                    // Con transiciones xfade
                    concatResult = await AplicarTransicionesAsync(
                        clipPaths, request.Transicion, videoSinAudio, 0.15);
                }

                if (string.IsNullOrEmpty(concatResult))
                {
                    result.Error = "Error concatenando clips";
                    return result;
                }

                // Combinar con audio
                var finalPath = string.IsNullOrEmpty(request.OutputPath)
                    ? Path.Combine(_tempPath, $"beatsync_{Guid.NewGuid()}.mp4")
                    : request.OutputPath;

                var combineResult = await CombinarVideoAudioAsync(
                    concatResult, request.AudioPath, finalPath,
                    request.TrimStart, duration);

                if (string.IsNullOrEmpty(combineResult))
                {
                    result.Error = "Error combinando video y audio";
                    return result;
                }

                // Resultado exitoso
                var fileInfo = new FileInfo(combineResult);
                result.Success = true;
                result.OutputPath = combineResult;
                result.Duration = duration;
                result.FileSize = fileInfo.Length;

                _logger.LogInformation("[VideoCompositor] Beat Sync completado: {Path}, {Size}MB",
                    combineResult, (fileInfo.Length / 1024.0 / 1024.0).ToString("F2"));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VideoCompositor] Error en GenerarBeatSyncAsync");
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                // Limpiar archivos temporales
                foreach (var tempFile in tempFiles)
                {
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); }
                    catch { }
                }
            }
        }

        private async Task<bool> ImagenAVideoAsync(string imagePath, string outputPath,
            double duration, int width, int height, int fps, int crf)
        {
            // FFmpeg: crear video desde imagen
            // -loop 1: repetir imagen
            // -i: input
            // -c:v libx264: codec H.264
            // -t: duración
            // -pix_fmt yuv420p: formato compatible
            // -vf scale: escalar y centrar
            var scaleFilter = $"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black";
            var args = $"-y -loop 1 -i \"{imagePath}\" -c:v libx264 -preset fast -crf {crf} -t {duration:F3} -pix_fmt yuv420p -r {fps} -vf \"{scaleFilter}\" \"{outputPath}\"";

            return await EjecutarFFmpegAsync(args);
        }

        private async Task<string?> ConcatenarClipsAsync(List<string> clips, string outputPath)
        {
            // Crear archivo de lista para concat
            var listPath = Path.Combine(_tempPath, $"list_{Guid.NewGuid()}.txt");

            try
            {
                var listContent = new StringBuilder();
                foreach (var clip in clips)
                {
                    listContent.AppendLine($"file '{clip.Replace("'", "\\'")}'");
                }
                await File.WriteAllTextAsync(listPath, listContent.ToString());

                // FFmpeg concat demuxer
                var args = $"-y -f concat -safe 0 -i \"{listPath}\" -c copy \"{outputPath}\"";
                var result = await EjecutarFFmpegAsync(args);

                return result ? outputPath : null;
            }
            finally
            {
                try { if (File.Exists(listPath)) File.Delete(listPath); }
                catch { }
            }
        }

        public async Task<string?> AplicarTransicionesAsync(List<string> clips, string transicion,
            string outputPath, double duracionTransicion = 0.3)
        {
            if (clips.Count < 2) return null;

            var xfadeType = TransicionesFfmpeg.GetValueOrDefault(transicion, "fade");

            // Para muchos clips, usar concat filter con xfade es complejo
            // Usamos un enfoque iterativo: aplicar transición de a pares
            var currentClip = clips[0];

            for (int i = 1; i < clips.Count; i++)
            {
                var nextClip = clips[i];
                var tempOutput = i == clips.Count - 1
                    ? outputPath
                    : Path.Combine(_tempPath, $"xfade_{Guid.NewGuid()}.mp4");

                // FFmpeg xfade filter
                // -i: inputs
                // -filter_complex xfade: aplicar transición
                var offset = await ObtenerDuracionVideoAsync(currentClip) - duracionTransicion;
                if (offset < 0) offset = 0;

                var filterComplex = $"xfade=transition={xfadeType}:duration={duracionTransicion:F2}:offset={offset:F2}";
                var args = $"-y -i \"{currentClip}\" -i \"{nextClip}\" -filter_complex \"{filterComplex}\" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p \"{tempOutput}\"";

                var result = await EjecutarFFmpegAsync(args);
                if (!result) return null;

                // Limpiar clip temporal anterior (excepto el primer input)
                if (i > 1 && currentClip.Contains("xfade_"))
                {
                    try { File.Delete(currentClip); } catch { }
                }

                currentClip = tempOutput;
            }

            return outputPath;
        }

        public async Task<string?> CombinarVideoAudioAsync(string videoPath, string audioPath,
            string outputPath, double trimStart = 0, double duration = 0)
        {
            var args = new StringBuilder();
            args.Append($"-y -i \"{videoPath}\" -ss {trimStart:F3} -i \"{audioPath}\"");

            if (duration > 0)
            {
                args.Append($" -t {duration:F3}");
            }

            // Usar el video original y agregar audio
            args.Append($" -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -shortest \"{outputPath}\"");

            var result = await EjecutarFFmpegAsync(args.ToString());
            return result ? outputPath : null;
        }

        public async Task<string?> AplicarEfectoAsync(string videoPath, string efecto,
            string outputPath, Dictionary<string, object>? parametros = null)
        {
            var filterComplex = efecto switch
            {
                "slowmo" => "setpts=2*PTS", // 0.5x velocidad
                "fast" => "setpts=0.5*PTS", // 2x velocidad
                "reverse" => "reverse",
                "boomerang" => "[0:v]split[a][b];[b]reverse[r];[a][r]concat=n=2:v=1",
                "glitch" => "noise=alls=30:allf=t+u",
                "vhs" => "noise=alls=10:allf=t,colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131",
                "retro" => "curves=vintage",
                "cinematic" => "eq=contrast=1.1:brightness=-0.05:saturation=0.9",
                "noir" => "colorchannelmixer=.3:.4:.3:0:.3:.4:.3:0:.3:.4:.3",
                _ => ""
            };

            if (string.IsNullOrEmpty(filterComplex))
            {
                // Sin efecto, solo copiar
                File.Copy(videoPath, outputPath, true);
                return outputPath;
            }

            // Manejar efectos que afectan el audio
            var audioFilter = efecto switch
            {
                "slowmo" => "-filter:a \"atempo=0.5\"",
                "fast" => "-filter:a \"atempo=2.0\"",
                "reverse" => "-filter:a \"areverse\"",
                _ => "-c:a copy"
            };

            var args = $"-y -i \"{videoPath}\" -vf \"{filterComplex}\" {audioFilter} -c:v libx264 -preset fast -crf 23 \"{outputPath}\"";
            var result = await EjecutarFFmpegAsync(args);
            return result ? outputPath : null;
        }

        private async Task<double> ObtenerDuracionVideoAsync(string videoPath)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null) return 0;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var duration))
                {
                    return duration;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[VideoCompositor] Error obteniendo duración");
            }

            return 0;
        }

        private async Task<bool> EjecutarFFmpegAsync(string argumentos, int timeoutSeconds = 600)
        {
            try
            {
                _logger.LogDebug("[VideoCompositor] FFmpeg: {Args}", argumentos);

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
                    _logger.LogError("[VideoCompositor] No se pudo iniciar FFmpeg");
                    return false;
                }

                var errorTask = process.StandardError.ReadToEndAsync();
                var completed = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000));

                if (!completed)
                {
                    process.Kill();
                    _logger.LogError("[VideoCompositor] FFmpeg timeout");
                    return false;
                }

                if (process.ExitCode != 0)
                {
                    var error = await errorTask;
                    _logger.LogError("[VideoCompositor] FFmpeg error: {Error}", error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VideoCompositor] Error ejecutando FFmpeg");
                return false;
            }
        }

        /// <summary>
        /// Limpia archivos temporales antiguos (más de 1 hora)
        /// </summary>
        public void LimpiarTemporales()
        {
            try
            {
                var threshold = DateTime.Now.AddHours(-1);
                var files = Directory.GetFiles(_tempPath);

                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < threshold)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[VideoCompositor] Error limpiando temporales");
            }
        }
    }
}
