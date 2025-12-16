using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Lado.Services
{
    public interface IFeedAlgorithmService
    {
        Task<List<Contenido>> AplicarAlgoritmoAsync(
            List<Contenido> contenidos,
            string algoritmo,
            string usuarioId,
            ApplicationDbContext context);

        Task<List<AlgoritmoFeed>> ObtenerAlgoritmosActivosAsync(ApplicationDbContext context);
        Task<AlgoritmoFeed?> ObtenerAlgoritmoUsuarioAsync(string usuarioId, ApplicationDbContext context);
        Task GuardarPreferenciaUsuarioAsync(string usuarioId, int algoritmoId, ApplicationDbContext context);
        Task IncrementarUsoAsync(int algoritmoId, ApplicationDbContext context);
    }

    public class FeedAlgorithmService : IFeedAlgorithmService
    {
        private readonly ILogger<FeedAlgorithmService> _logger;

        public FeedAlgorithmService(ILogger<FeedAlgorithmService> logger)
        {
            _logger = logger;
        }

        public async Task<List<Contenido>> AplicarAlgoritmoAsync(
            List<Contenido> contenidos,
            string algoritmo,
            string usuarioId,
            ApplicationDbContext context)
        {
            if (!contenidos.Any()) return contenidos;

            _logger.LogInformation("Aplicando algoritmo {Algoritmo} para usuario {UsuarioId}", algoritmo, usuarioId);

            return algoritmo.ToLower() switch
            {
                "cronologico" => AplicarCronologico(contenidos),
                "trending" => await AplicarTrendingAsync(contenidos, context),
                "seguidos" => await AplicarSeguidosPrimeroAsync(contenidos, usuarioId, context),
                "para_ti" => await AplicarParaTiAsync(contenidos, usuarioId, context),
                _ => AplicarCronologico(contenidos)
            };
        }

        #region Algoritmo Cronológico
        private List<Contenido> AplicarCronologico(List<Contenido> contenidos)
        {
            return contenidos
                .OrderByDescending(c => c.FechaPublicacion)
                .ToList();
        }
        #endregion

        #region Algoritmo Trending
        private async Task<List<Contenido>> AplicarTrendingAsync(List<Contenido> contenidos, ApplicationDbContext context)
        {
            var contenidoIds = contenidos.Select(c => c.Id).ToList();

            // Obtener reacciones para calcular engagement
            var reaccionesPorContenido = await context.Reacciones
                .Where(r => contenidoIds.Contains(r.ContenidoId))
                .GroupBy(r => r.ContenidoId)
                .Select(g => new { ContenidoId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ContenidoId, x => x.Count);

            return contenidos
                .Select(c => new
                {
                    Contenido = c,
                    Score = CalcularScoreTrending(c, reaccionesPorContenido.GetValueOrDefault(c.Id, 0))
                })
                .OrderByDescending(x => x.Score)
                .Select(x => x.Contenido)
                .ToList();
        }

        private double CalcularScoreTrending(Contenido contenido, int totalReacciones)
        {
            var horasDesdePublicacion = (DateTime.Now - contenido.FechaPublicacion).TotalHours;

            // Decay factor: contenido reciente tiene más peso
            double decayFactor = Math.Pow(0.95, horasDesdePublicacion / 6); // Decay cada 6 horas

            // Engagement score
            double engagement =
                (contenido.NumeroLikes * 1.0) +
                (contenido.NumeroComentarios * 3.0) +
                (totalReacciones * 1.5) +
                (contenido.NumeroVistas * 0.1) +
                (contenido.NumeroCompartidos * 3.0);

            // Boost para contenido muy reciente (< 6 horas)
            double recencyBoost = horasDesdePublicacion < 6 ? 50 : (horasDesdePublicacion < 24 ? 25 : 0);

            // Score final
            return (engagement * decayFactor) + recencyBoost;
        }
        #endregion

        #region Algoritmo Seguidos Primero
        private async Task<List<Contenido>> AplicarSeguidosPrimeroAsync(
            List<Contenido> contenidos,
            string usuarioId,
            ApplicationDbContext context)
        {
            // Obtener IDs de creadores seguidos
            var creadoresSeguidos = await context.Suscripciones
                .Where(s => s.FanId == usuarioId && s.EstaActiva)
                .Select(s => s.CreadorId)
                .Distinct()
                .ToListAsync();

            // Separar contenido de seguidos y descubrimiento
            var contenidoSeguidos = contenidos
                .Where(c => creadoresSeguidos.Contains(c.UsuarioId))
                .OrderByDescending(c => c.FechaPublicacion)
                .ToList();

            var contenidoDescubrimiento = contenidos
                .Where(c => !creadoresSeguidos.Contains(c.UsuarioId))
                .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                .ToList();

            // 70% seguidos, 30% descubrimiento, intercalados
            var resultado = new List<Contenido>();
            int indexSeguidos = 0;
            int indexDescubrimiento = 0;

            for (int i = 0; i < contenidos.Count; i++)
            {
                bool usarSeguidos = (i % 10) < 7; // 7 de cada 10 son seguidos

                if (usarSeguidos && indexSeguidos < contenidoSeguidos.Count)
                {
                    resultado.Add(contenidoSeguidos[indexSeguidos++]);
                }
                else if (indexDescubrimiento < contenidoDescubrimiento.Count)
                {
                    resultado.Add(contenidoDescubrimiento[indexDescubrimiento++]);
                }
                else if (indexSeguidos < contenidoSeguidos.Count)
                {
                    resultado.Add(contenidoSeguidos[indexSeguidos++]);
                }
            }

            return resultado;
        }
        #endregion

        #region Algoritmo Para Ti (Personalizado)
        private async Task<List<Contenido>> AplicarParaTiAsync(
            List<Contenido> contenidos,
            string usuarioId,
            ApplicationDbContext context)
        {
            // Obtener historial de interacciones del usuario
            var likesUsuario = await context.Likes
                .Where(l => l.UsuarioId == usuarioId)
                .OrderByDescending(l => l.FechaLike)
                .Take(100)
                .Select(l => l.ContenidoId)
                .ToListAsync();

            var comentariosUsuario = await context.Comentarios
                .Where(c => c.UsuarioId == usuarioId)
                .OrderByDescending(c => c.FechaCreacion)
                .Take(50)
                .Select(c => c.ContenidoId)
                .ToListAsync();

            // Creadores con los que más interactúa
            var contenidosInteractuados = likesUsuario.Union(comentariosUsuario).ToList();
            var creadoresInteractuados = await context.Contenidos
                .Where(c => contenidosInteractuados.Contains(c.Id))
                .GroupBy(c => c.UsuarioId)
                .Select(g => new { UsuarioId = g.Key, Interacciones = g.Count() })
                .OrderByDescending(x => x.Interacciones)
                .Take(20)
                .ToDictionaryAsync(x => x.UsuarioId, x => x.Interacciones);

            // Obtener tipo de contenido preferido
            var tipoPreferido = await context.Contenidos
                .Where(c => contenidosInteractuados.Contains(c.Id))
                .GroupBy(c => c.TipoContenido)
                .Select(g => new { Tipo = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefaultAsync();

            var contenidoIds = contenidos.Select(c => c.Id).ToList();
            var reaccionesPorContenido = await context.Reacciones
                .Where(r => contenidoIds.Contains(r.ContenidoId))
                .GroupBy(r => r.ContenidoId)
                .Select(g => new { ContenidoId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ContenidoId, x => x.Count);

            return contenidos
                .Select(c => new
                {
                    Contenido = c,
                    Score = CalcularScoreParaTi(
                        c,
                        usuarioId,
                        creadoresInteractuados,
                        tipoPreferido?.Tipo,
                        reaccionesPorContenido.GetValueOrDefault(c.Id, 0))
                })
                .OrderByDescending(x => x.Score)
                .Select(x => x.Contenido)
                .ToList();
        }

        private double CalcularScoreParaTi(
            Contenido contenido,
            string usuarioId,
            Dictionary<string, int> creadoresInteractuados,
            TipoContenido? tipoPreferido,
            int totalReacciones)
        {
            double score = 0;

            // 1. Score base por engagement (40%)
            double engagement =
                (contenido.NumeroLikes * 1.0) +
                (contenido.NumeroComentarios * 3.0) +
                (totalReacciones * 1.5) +
                (contenido.NumeroVistas * 0.1);
            score += Math.Log(1 + engagement) * 10 * 0.4;

            // 2. Boost por creador favorito (30%)
            if (creadoresInteractuados.TryGetValue(contenido.UsuarioId, out int interacciones))
            {
                score += Math.Min(interacciones * 5, 50) * 0.3;
            }

            // 3. Boost por tipo de contenido preferido (15%)
            if (tipoPreferido.HasValue && contenido.TipoContenido == tipoPreferido.Value)
            {
                score += 30 * 0.15;
            }

            // 4. Recency boost (15%)
            var horasDesdePublicacion = (DateTime.Now - contenido.FechaPublicacion).TotalHours;
            if (horasDesdePublicacion < 6)
                score += 50 * 0.15;
            else if (horasDesdePublicacion < 24)
                score += 25 * 0.15;
            else if (horasDesdePublicacion < 72)
                score += 10 * 0.15;

            // 5. Boost por contenido propio
            if (contenido.UsuarioId == usuarioId)
                score += 20;

            // 6. Boost por contenido premium
            if (contenido.TipoLado == TipoLado.LadoB)
                score += 15;

            return score;
        }
        #endregion

        #region Gestión de Algoritmos
        public async Task<List<AlgoritmoFeed>> ObtenerAlgoritmosActivosAsync(ApplicationDbContext context)
        {
            var algoritmos = await context.AlgoritmosFeed
                .Where(a => a.Activo)
                .OrderBy(a => a.Orden)
                .ToListAsync();

            // Auto-reparación: si no hay algoritmos, crearlos
            if (!algoritmos.Any())
            {
                _logger.LogWarning("No se encontraron algoritmos en la BD. Ejecutando auto-reparación...");
                await CrearAlgoritmosPorDefectoAsync(context);
                algoritmos = await context.AlgoritmosFeed
                    .Where(a => a.Activo)
                    .OrderBy(a => a.Orden)
                    .ToListAsync();
            }

            return algoritmos;
        }

        private async Task CrearAlgoritmosPorDefectoAsync(ApplicationDbContext context)
        {
            var algoritmosExistentes = await context.AlgoritmosFeed.AnyAsync();
            if (algoritmosExistentes) return;

            var algoritmos = new List<AlgoritmoFeed>
            {
                new AlgoritmoFeed
                {
                    Codigo = "cronologico",
                    Nombre = "Cronológico",
                    Descripcion = "Muestra los posts ordenados por fecha de publicación, los más recientes primero",
                    Icono = "clock",
                    Activo = true,
                    EsPorDefecto = true,
                    Orden = 1,
                    TotalUsos = 0,
                    FechaCreacion = DateTime.Now
                },
                new AlgoritmoFeed
                {
                    Codigo = "trending",
                    Nombre = "Trending",
                    Descripcion = "Prioriza contenido con alto engagement reciente (likes, comentarios, vistas)",
                    Icono = "trending-up",
                    Activo = true,
                    EsPorDefecto = false,
                    Orden = 2,
                    TotalUsos = 0,
                    FechaCreacion = DateTime.Now
                },
                new AlgoritmoFeed
                {
                    Codigo = "seguidos",
                    Nombre = "Seguidos Primero",
                    Descripcion = "70% contenido de creadores que sigues, 30% descubrimiento de nuevos",
                    Icono = "users",
                    Activo = true,
                    EsPorDefecto = false,
                    Orden = 3,
                    TotalUsos = 0,
                    FechaCreacion = DateTime.Now
                },
                new AlgoritmoFeed
                {
                    Codigo = "para_ti",
                    Nombre = "Para Ti",
                    Descripcion = "Personalizado basado en tu historial de interacciones y preferencias",
                    Icono = "heart",
                    Activo = true,
                    EsPorDefecto = false,
                    Orden = 4,
                    TotalUsos = 0,
                    FechaCreacion = DateTime.Now
                }
            };

            context.AlgoritmosFeed.AddRange(algoritmos);
            await context.SaveChangesAsync();
            _logger.LogInformation("Algoritmos por defecto creados exitosamente");
        }

        public async Task<AlgoritmoFeed?> ObtenerAlgoritmoUsuarioAsync(string usuarioId, ApplicationDbContext context)
        {
            var preferencia = await context.PreferenciasAlgoritmoUsuario
                .Include(p => p.AlgoritmoFeed)
                .FirstOrDefaultAsync(p => p.UsuarioId == usuarioId);

            if (preferencia?.AlgoritmoFeed != null && preferencia.AlgoritmoFeed.Activo)
            {
                return preferencia.AlgoritmoFeed;
            }

            // Si no tiene preferencia o el algoritmo no está activo, devolver el por defecto
            var algoritmoDefecto = await context.AlgoritmosFeed
                .FirstOrDefaultAsync(a => a.EsPorDefecto && a.Activo);

            // Si no hay algoritmo por defecto, intentar obtener cualquier algoritmo activo
            if (algoritmoDefecto == null)
            {
                algoritmoDefecto = await context.AlgoritmosFeed
                    .Where(a => a.Activo)
                    .OrderBy(a => a.Orden)
                    .FirstOrDefaultAsync();

                // Si aún no hay, crear los algoritmos
                if (algoritmoDefecto == null)
                {
                    await CrearAlgoritmosPorDefectoAsync(context);
                    algoritmoDefecto = await context.AlgoritmosFeed
                        .FirstOrDefaultAsync(a => a.EsPorDefecto && a.Activo);
                }
            }

            return algoritmoDefecto;
        }

        public async Task GuardarPreferenciaUsuarioAsync(string usuarioId, int algoritmoId, ApplicationDbContext context)
        {
            var preferencia = await context.PreferenciasAlgoritmoUsuario
                .FirstOrDefaultAsync(p => p.UsuarioId == usuarioId);

            if (preferencia != null)
            {
                preferencia.AlgoritmoFeedId = algoritmoId;
                preferencia.FechaSeleccion = DateTime.Now;
            }
            else
            {
                context.PreferenciasAlgoritmoUsuario.Add(new PreferenciaAlgoritmoUsuario
                {
                    UsuarioId = usuarioId,
                    AlgoritmoFeedId = algoritmoId,
                    FechaSeleccion = DateTime.Now
                });
            }

            await context.SaveChangesAsync();
        }

        public async Task IncrementarUsoAsync(int algoritmoId, ApplicationDbContext context)
        {
            var algoritmo = await context.AlgoritmosFeed.FindAsync(algoritmoId);
            if (algoritmo != null)
            {
                algoritmo.TotalUsos++;
                await context.SaveChangesAsync();
            }
        }
        #endregion
    }
}
