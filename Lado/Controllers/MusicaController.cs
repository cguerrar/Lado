using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para la biblioteca de música del creador de Reels
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MusicaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MusicaController> _logger;

        public MusicaController(ApplicationDbContext context, ILogger<MusicaController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todas las pistas de la biblioteca
        /// </summary>
        [HttpGet("biblioteca")]
        public async Task<ActionResult<IEnumerable<object>>> GetBiblioteca()
        {
            var pistas = await _context.PistasMusica
                .Where(p => p.Activo)
                .OrderByDescending(p => p.ContadorUsos)
                .Select(p => new
                {
                    id = p.Id,
                    titulo = p.Titulo,
                    artista = p.Artista,
                    genero = p.Genero,
                    duracion = p.Duracion,
                    duracionFormateada = FormatDuration(p.Duracion),
                    rutaArchivo = p.RutaArchivo,
                    rutaPortada = p.RutaPortada,
                    bpm = p.Bpm,
                    energia = p.Energia,
                    estadoAnimo = p.EstadoAnimo
                })
                .ToListAsync();

            return Ok(pistas);
        }

        /// <summary>
        /// Obtiene pistas filtradas por género
        /// </summary>
        [HttpGet("genero/{genero}")]
        public async Task<ActionResult<IEnumerable<object>>> GetPorGenero(string genero)
        {
            var pistas = await _context.PistasMusica
                .Where(p => p.Activo && p.Genero == genero)
                .OrderByDescending(p => p.ContadorUsos)
                .Select(p => new
                {
                    id = p.Id,
                    titulo = p.Titulo,
                    artista = p.Artista,
                    genero = p.Genero,
                    duracion = p.Duracion,
                    duracionFormateada = FormatDuration(p.Duracion),
                    rutaArchivo = p.RutaArchivo,
                    rutaPortada = p.RutaPortada,
                    bpm = p.Bpm,
                    energia = p.Energia,
                    estadoAnimo = p.EstadoAnimo
                })
                .ToListAsync();

            return Ok(pistas);
        }

        /// <summary>
        /// Obtiene la lista de géneros disponibles
        /// </summary>
        [HttpGet("generos")]
        public async Task<ActionResult<IEnumerable<object>>> GetGeneros()
        {
            var generos = await _context.PistasMusica
                .Where(p => p.Activo)
                .GroupBy(p => p.Genero)
                .Select(g => new
                {
                    Nombre = g.Key,
                    CantidadPistas = g.Count()
                })
                .OrderBy(g => g.Nombre)
                .ToListAsync();

            return Ok(generos);
        }

        /// <summary>
        /// Busca pistas por título o artista
        /// </summary>
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<object>>> Buscar([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Término de búsqueda requerido" });
            }

            var termino = q.ToLower();
            var pistas = await _context.PistasMusica
                .Where(p => p.Activo &&
                    (p.Titulo.ToLower().Contains(termino) ||
                     p.Artista.ToLower().Contains(termino) ||
                     (p.EstadoAnimo != null && p.EstadoAnimo.ToLower().Contains(termino))))
                .OrderByDescending(p => p.ContadorUsos)
                .Select(p => new
                {
                    id = p.Id,
                    titulo = p.Titulo,
                    artista = p.Artista,
                    genero = p.Genero,
                    duracion = p.Duracion,
                    duracionFormateada = FormatDuration(p.Duracion),
                    rutaArchivo = p.RutaArchivo,
                    rutaPortada = p.RutaPortada,
                    bpm = p.Bpm,
                    energia = p.Energia,
                    estadoAnimo = p.EstadoAnimo
                })
                .ToListAsync();

            return Ok(pistas);
        }

        /// <summary>
        /// Obtiene pistas filtradas por estado de ánimo
        /// </summary>
        [HttpGet("mood/{mood}")]
        public async Task<ActionResult<IEnumerable<object>>> GetPorMood(string mood)
        {
            var pistas = await _context.PistasMusica
                .Where(p => p.Activo && p.EstadoAnimo == mood)
                .OrderByDescending(p => p.ContadorUsos)
                .Select(p => new
                {
                    id = p.Id,
                    titulo = p.Titulo,
                    artista = p.Artista,
                    genero = p.Genero,
                    duracion = p.Duracion,
                    duracionFormateada = FormatDuration(p.Duracion),
                    rutaArchivo = p.RutaArchivo,
                    rutaPortada = p.RutaPortada,
                    bpm = p.Bpm,
                    energia = p.Energia,
                    estadoAnimo = p.EstadoAnimo
                })
                .ToListAsync();

            return Ok(pistas);
        }

        /// <summary>
        /// Obtiene pistas filtradas por nivel de energía
        /// </summary>
        [HttpGet("energia/{nivel}")]
        public async Task<ActionResult<IEnumerable<object>>> GetPorEnergia(string nivel)
        {
            var pistas = await _context.PistasMusica
                .Where(p => p.Activo && p.Energia == nivel)
                .OrderByDescending(p => p.ContadorUsos)
                .Select(p => new
                {
                    id = p.Id,
                    titulo = p.Titulo,
                    artista = p.Artista,
                    genero = p.Genero,
                    duracion = p.Duracion,
                    duracionFormateada = FormatDuration(p.Duracion),
                    rutaArchivo = p.RutaArchivo,
                    rutaPortada = p.RutaPortada,
                    bpm = p.Bpm,
                    energia = p.Energia,
                    estadoAnimo = p.EstadoAnimo
                })
                .ToListAsync();

            return Ok(pistas);
        }

        /// <summary>
        /// Obtiene una pista específica por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetPista(int id)
        {
            var pista = await _context.PistasMusica
                .FirstOrDefaultAsync(p => p.Id == id && p.Activo);

            if (pista == null)
            {
                return NotFound(new { message = "Pista no encontrada" });
            }

            return Ok(new
            {
                pista.Id,
                pista.Titulo,
                pista.Artista,
                pista.Album,
                pista.Genero,
                pista.Duracion,
                DuracionFormateada = FormatDuration(pista.Duracion),
                pista.RutaArchivo,
                pista.RutaPortada,
                pista.Bpm,
                pista.Energia,
                pista.EstadoAnimo,
                pista.EsLibreDeRegalias
            });
        }

        /// <summary>
        /// Obtiene pistas populares/tendencias (más usadas)
        /// </summary>
        [HttpGet("trending")]
        public async Task<ActionResult<IEnumerable<object>>> GetTrending()
        {
            var pistas = await _context.PistasMusica
                .Where(p => p.Activo && p.ContadorUsos > 0)
                .OrderByDescending(p => p.ContadorUsos)
                .Take(10)
                .Select(p => new
                {
                    id = p.Id,
                    titulo = p.Titulo,
                    artista = p.Artista,
                    genero = p.Genero,
                    duracion = p.Duracion,
                    duracionFormateada = FormatDuration(p.Duracion),
                    rutaArchivo = p.RutaArchivo,
                    rutaPortada = p.RutaPortada,
                    bpm = p.Bpm,
                    energia = p.Energia,
                    estadoAnimo = p.EstadoAnimo,
                    contadorUsos = p.ContadorUsos
                })
                .ToListAsync();

            return Ok(pistas);
        }

        /// <summary>
        /// Incrementa el contador de uso de una pista
        /// </summary>
        [HttpPost("{id}/usar")]
        public async Task<ActionResult> IncrementarUso(int id)
        {
            var pista = await _context.PistasMusica.FindAsync(id);
            if (pista == null)
            {
                return NotFound();
            }

            pista.ContadorUsos++;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, contadorUsos = pista.ContadorUsos });
        }

        /// <summary>
        /// Proxy para servir archivos de audio con headers correctos para CORS y iOS
        /// Esto evita problemas de fetch() con AudioContext en navegadores móviles
        /// </summary>
        [HttpGet("audio/{id}")]
        [AllowAnonymous] // Permitir acceso sin autenticación para reproductores
        public async Task<IActionResult> GetAudio(int id)
        {
            var pista = await _context.PistasMusica.FindAsync(id);
            if (pista == null || string.IsNullOrEmpty(pista.RutaArchivo))
            {
                return NotFound();
            }

            try
            {
                // Construir la ruta del archivo
                var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var relativePath = pista.RutaArchivo.TrimStart('/');
                var fullPath = Path.Combine(webRootPath, relativePath);

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning("Archivo de audio no encontrado: {Path}", fullPath);
                    return NotFound();
                }

                // Detectar el tipo MIME
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".mp3" => "audio/mpeg",
                    ".m4a" => "audio/mp4",
                    ".aac" => "audio/aac",
                    ".ogg" => "audio/ogg",
                    ".wav" => "audio/wav",
                    ".webm" => "audio/webm",
                    _ => "audio/mpeg"
                };

                // Leer el archivo
                var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);

                // Agregar headers para CORS y caché
                Response.Headers["Access-Control-Allow-Origin"] = "*";
                Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
                Response.Headers["Access-Control-Allow-Headers"] = "Range, Content-Type";
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.Headers["Cache-Control"] = "public, max-age=86400"; // Cachear por 24h

                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al servir audio para pista {Id}", id);
                return StatusCode(500, "Error al cargar el audio");
            }
        }

        /// <summary>
        /// Proxy para servir audio desde URL externa (si se necesita)
        /// </summary>
        [HttpGet("proxy")]
        public async Task<IActionResult> ProxyAudio([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return BadRequest("URL requerida");
            }

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode);
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";
                var audioBytes = await response.Content.ReadAsByteArrayAsync();

                // Headers CORS
                Response.Headers["Access-Control-Allow-Origin"] = "*";
                Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.Headers["Cache-Control"] = "public, max-age=3600";

                return File(audioBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al hacer proxy de audio desde {Url}", url);
                return StatusCode(500, "Error al cargar el audio");
            }
        }

        private static string FormatDuration(int seconds)
        {
            var minutes = seconds / 60;
            var secs = seconds % 60;
            return $"{minutes}:{secs:D2}";
        }
    }
}
