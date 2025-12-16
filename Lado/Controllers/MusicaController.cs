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

        private static string FormatDuration(int seconds)
        {
            var minutes = seconds / 60;
            var secs = seconds % 60;
            return $"{minutes}:{secs:D2}";
        }
    }
}
