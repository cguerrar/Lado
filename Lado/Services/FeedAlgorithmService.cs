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
            ApplicationDbContext context,
            int? semilla = null);

        Task<List<AlgoritmoFeed>> ObtenerAlgoritmosActivosAsync(ApplicationDbContext context);
        Task<AlgoritmoFeed?> ObtenerAlgoritmoUsuarioAsync(string usuarioId, ApplicationDbContext context);
        Task GuardarPreferenciaUsuarioAsync(string usuarioId, int algoritmoId, ApplicationDbContext context);
        Task IncrementarUsoAsync(int algoritmoId, ApplicationDbContext context);
    }

    public class FeedAlgorithmService : IFeedAlgorithmService
    {
        private readonly ILogger<FeedAlgorithmService> _logger;

        // Cache de pesos (se actualiza cada 5 minutos)
        private static Dictionary<string, double> _pesosCache = new();
        private static DateTime _ultimaActualizacionCache = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public FeedAlgorithmService(ILogger<FeedAlgorithmService> logger)
        {
            _logger = logger;
        }

        #region Configuración de Pesos
        private async Task<Dictionary<string, double>> ObtenerPesosAsync(ApplicationDbContext context)
        {
            // Usar cache si es válido
            if (_pesosCache.Any() && DateTime.Now - _ultimaActualizacionCache < _cacheDuration)
            {
                return _pesosCache;
            }

            // Valores por defecto
            var pesosDefault = new Dictionary<string, double>
            {
                // Para Ti
                { ConfiguracionPlataforma.PARATI_PESO_ENGAGEMENT, 30 },
                { ConfiguracionPlataforma.PARATI_PESO_INTERESES, 25 },
                { ConfiguracionPlataforma.PARATI_PESO_CREADOR_FAVORITO, 20 },
                { ConfiguracionPlataforma.PARATI_PESO_TIPO_CONTENIDO, 10 },
                { ConfiguracionPlataforma.PARATI_PESO_RECENCIA, 15 },
                // Por Intereses
                { ConfiguracionPlataforma.INTERESES_PESO_CATEGORIA, 80 },
                { ConfiguracionPlataforma.INTERESES_PESO_DESCUBRIMIENTO, 20 }
            };

            try
            {
                // Obtener valores de la BD
                var configuraciones = await context.ConfiguracionesPlataforma
                    .Where(c => c.Categoria == "Algoritmos")
                    .ToListAsync();

                foreach (var config in configuraciones)
                {
                    if (pesosDefault.ContainsKey(config.Clave) && double.TryParse(config.Valor, out double valor))
                    {
                        pesosDefault[config.Clave] = valor;
                    }
                }

                _pesosCache = pesosDefault;
                _ultimaActualizacionCache = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener pesos de configuración, usando valores por defecto");
            }

            return pesosDefault;
        }

        private double ObtenerPeso(Dictionary<string, double> pesos, string clave)
        {
            return pesos.TryGetValue(clave, out double valor) ? valor / 100.0 : 0;
        }
        #endregion

        // ⚡ NUEVO: Función hash para desempate determinístico
        private static int CalcularHashDeterministico(int id, int semilla)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + id;
                hash = hash * 31 + semilla;
                return hash;
            }
        }

        public async Task<List<Contenido>> AplicarAlgoritmoAsync(
            List<Contenido> contenidos,
            string algoritmo,
            string usuarioId,
            ApplicationDbContext context,
            int? semilla = null)
        {
            if (!contenidos.Any()) return contenidos;

            // Usar semilla proporcionada o generar una basada en el día (para consistencia diaria)
            var semillaActual = semilla ?? DateTime.Today.GetHashCode();

            _logger.LogInformation("Aplicando algoritmo {Algoritmo} para usuario {UsuarioId} con semilla {Semilla}",
                algoritmo, usuarioId, semillaActual);

            return algoritmo.ToLower() switch
            {
                "cronologico" => AplicarCronologico(contenidos, semillaActual),
                "trending" => await AplicarTrendingAsync(contenidos, context, semillaActual),
                "seguidos" => await AplicarSeguidosPrimeroAsync(contenidos, usuarioId, context, semillaActual),
                "para_ti" => await AplicarParaTiAsync(contenidos, usuarioId, context, semillaActual),
                "por_intereses" => await AplicarPorInteresesAsync(contenidos, usuarioId, context, semillaActual),
                _ => AplicarCronologico(contenidos, semillaActual)
            };
        }

        #region Algoritmo Cronológico
        private List<Contenido> AplicarCronologico(List<Contenido> contenidos, int semilla)
        {
            return contenidos
                .OrderByDescending(c => c.FechaPublicacion)
                .ThenBy(c => CalcularHashDeterministico(c.Id, semilla)) // Desempate determinístico
                .ToList();
        }
        #endregion

        #region Algoritmo Trending
        private async Task<List<Contenido>> AplicarTrendingAsync(List<Contenido> contenidos, ApplicationDbContext context, int semilla)
        {
            var contenidoIds = contenidos.Select(c => c.Id).ToList();
            var usuarioIds = contenidos.Select(c => c.UsuarioId).Distinct().ToList();

            // Obtener reacciones para calcular engagement
            var reaccionesPorContenido = await context.Reacciones
                .Where(r => contenidoIds.Contains(r.ContenidoId))
                .GroupBy(r => r.ContenidoId)
                .Select(g => new { ContenidoId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ContenidoId, x => x.Count);

            // ⭐ Obtener multiplicadores de boost activos de los usuarios
            var boostPorUsuario = await context.Users
                .Where(u => usuarioIds.Contains(u.Id) &&
                           u.BoostMultiplicador > 1.0m &&
                           u.BoostFechaFin.HasValue &&
                           u.BoostFechaFin.Value > DateTime.Now)
                .ToDictionaryAsync(u => u.Id, u => (double)u.BoostMultiplicador);

            return contenidos
                .Select(c => new
                {
                    Contenido = c,
                    Score = CalcularScoreTrending(
                        c,
                        reaccionesPorContenido.GetValueOrDefault(c.Id, 0),
                        boostPorUsuario.GetValueOrDefault(c.UsuarioId, 1.0))
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => CalcularHashDeterministico(x.Contenido.Id, semilla)) // Desempate determinístico
                .Select(x => x.Contenido)
                .ToList();
        }

        private double CalcularScoreTrending(Contenido contenido, int totalReacciones, double boostMultiplicador = 1.0)
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

            // Score final con multiplicador de boost
            return ((engagement * decayFactor) + recencyBoost) * boostMultiplicador;
        }
        #endregion

        #region Algoritmo Seguidos Primero
        private async Task<List<Contenido>> AplicarSeguidosPrimeroAsync(
            List<Contenido> contenidos,
            string usuarioId,
            ApplicationDbContext context,
            int semilla)
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
                .ThenBy(c => CalcularHashDeterministico(c.Id, semilla)) // Desempate determinístico
                .ToList();

            var contenidoDescubrimiento = contenidos
                .Where(c => !creadoresSeguidos.Contains(c.UsuarioId))
                .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                .ThenBy(c => CalcularHashDeterministico(c.Id, semilla)) // Desempate determinístico
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
            ApplicationDbContext context,
            int semilla)
        {
            // Obtener pesos configurables
            var pesos = await ObtenerPesosAsync(context);

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

            // Creadores con los que mas interactua
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

            // Obtener intereses del usuario con sus pesos
            var interesesUsuario = await context.InteresesUsuarios
                .Where(i => i.UsuarioId == usuarioId && i.PesoInteres > 0.5m)
                .ToDictionaryAsync(i => i.CategoriaInteresId, i => i.PesoInteres);

            var contenidoIds = contenidos.Select(c => c.Id).ToList();
            var usuarioIds = contenidos.Select(c => c.UsuarioId).Distinct().ToList();

            var reaccionesPorContenido = await context.Reacciones
                .Where(r => contenidoIds.Contains(r.ContenidoId))
                .GroupBy(r => r.ContenidoId)
                .Select(g => new { ContenidoId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ContenidoId, x => x.Count);

            // ⭐ Obtener multiplicadores de boost activos de los usuarios
            var boostPorUsuario = await context.Users
                .Where(u => usuarioIds.Contains(u.Id) &&
                           u.BoostMultiplicador > 1.0m &&
                           u.BoostFechaFin.HasValue &&
                           u.BoostFechaFin.Value > DateTime.Now)
                .ToDictionaryAsync(u => u.Id, u => (double)u.BoostMultiplicador);

            return contenidos
                .Select(c => new
                {
                    Contenido = c,
                    Score = CalcularScoreParaTi(
                        c,
                        usuarioId,
                        creadoresInteractuados,
                        tipoPreferido?.Tipo,
                        reaccionesPorContenido.GetValueOrDefault(c.Id, 0),
                        interesesUsuario,
                        pesos,
                        boostPorUsuario.GetValueOrDefault(c.UsuarioId, 1.0))
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => CalcularHashDeterministico(x.Contenido.Id, semilla)) // Desempate determinístico
                .Select(x => x.Contenido)
                .ToList();
        }

        private double CalcularScoreParaTi(
            Contenido contenido,
            string usuarioId,
            Dictionary<string, int> creadoresInteractuados,
            TipoContenido? tipoPreferido,
            int totalReacciones,
            Dictionary<int, decimal> interesesUsuario,
            Dictionary<string, double> pesos,
            double boostMultiplicador = 1.0)
        {
            double score = 0;

            // Obtener pesos configurados (divididos por 100 para convertir a decimales)
            double pesoEngagement = ObtenerPeso(pesos, ConfiguracionPlataforma.PARATI_PESO_ENGAGEMENT);
            double pesoIntereses = ObtenerPeso(pesos, ConfiguracionPlataforma.PARATI_PESO_INTERESES);
            double pesoCreadorFavorito = ObtenerPeso(pesos, ConfiguracionPlataforma.PARATI_PESO_CREADOR_FAVORITO);
            double pesoTipoContenido = ObtenerPeso(pesos, ConfiguracionPlataforma.PARATI_PESO_TIPO_CONTENIDO);
            double pesoRecencia = ObtenerPeso(pesos, ConfiguracionPlataforma.PARATI_PESO_RECENCIA);

            // 1. Score base por engagement
            double engagement =
                (contenido.NumeroLikes * 1.0) +
                (contenido.NumeroComentarios * 3.0) +
                (totalReacciones * 1.5) +
                (contenido.NumeroVistas * 0.1);
            score += Math.Log(1 + engagement) * 10 * pesoEngagement;

            // 2. Boost por creador favorito
            if (creadoresInteractuados.TryGetValue(contenido.UsuarioId, out int interacciones))
            {
                score += Math.Min(interacciones * 5, 50) * pesoCreadorFavorito;
            }

            // 3. Boost por categoria de interes
            if (contenido.CategoriaInteresId.HasValue && interesesUsuario.Count > 0)
            {
                if (interesesUsuario.TryGetValue(contenido.CategoriaInteresId.Value, out decimal pesoInteres))
                {
                    double boostInteres = (double)pesoInteres * 0.6;
                    score += boostInteres * pesoIntereses;
                }
            }

            // 4. Boost por tipo de contenido preferido
            if (tipoPreferido.HasValue && contenido.TipoContenido == tipoPreferido.Value)
            {
                score += 30 * pesoTipoContenido;
            }

            // 5. Recency boost
            var horasDesdePublicacion = (DateTime.Now - contenido.FechaPublicacion).TotalHours;
            if (horasDesdePublicacion < 6)
                score += 50 * pesoRecencia;
            else if (horasDesdePublicacion < 24)
                score += 25 * pesoRecencia;
            else if (horasDesdePublicacion < 72)
                score += 10 * pesoRecencia;

            // 6. Boost por contenido propio (fijo)
            if (contenido.UsuarioId == usuarioId)
                score += 20;

            // 7. Boost por contenido premium (fijo)
            if (contenido.TipoLado == TipoLado.LadoB)
                score += 15;

            // ⭐ 8. Aplicar multiplicador de boost comprado con LadoCoins
            return score * boostMultiplicador;
        }
        #endregion

        #region Algoritmo Por Intereses
        private async Task<List<Contenido>> AplicarPorInteresesAsync(
            List<Contenido> contenidos,
            string usuarioId,
            ApplicationDbContext context,
            int semilla)
        {
            // Obtener pesos configurables
            var pesos = await ObtenerPesosAsync(context);
            int pesoCategoria = (int)pesos.GetValueOrDefault(ConfiguracionPlataforma.INTERESES_PESO_CATEGORIA, 80);

            // Obtener intereses del usuario ordenados por peso
            var interesesUsuario = await context.InteresesUsuarios
                .Where(i => i.UsuarioId == usuarioId && i.PesoInteres > 0.5m)
                .OrderByDescending(i => i.PesoInteres)
                .ToDictionaryAsync(i => i.CategoriaInteresId, i => i.PesoInteres);

            if (!interesesUsuario.Any())
            {
                // Si no tiene intereses, usar trending como fallback
                _logger.LogInformation("Usuario {UsuarioId} sin intereses, usando trending como fallback", usuarioId);
                return await AplicarTrendingAsync(contenidos, context, semilla);
            }

            var contenidoIds = contenidos.Select(c => c.Id).ToList();
            var reaccionesPorContenido = await context.Reacciones
                .Where(r => contenidoIds.Contains(r.ContenidoId))
                .GroupBy(r => r.ContenidoId)
                .Select(g => new { ContenidoId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ContenidoId, x => x.Count);

            // Separar contenido por si tiene categoria de interes o no
            var contenidoConCategoria = contenidos
                .Where(c => c.CategoriaInteresId.HasValue && interesesUsuario.ContainsKey(c.CategoriaInteresId.Value))
                .Select(c => new
                {
                    Contenido = c,
                    Score = CalcularScorePorIntereses(c, interesesUsuario, reaccionesPorContenido.GetValueOrDefault(c.Id, 0))
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => CalcularHashDeterministico(x.Contenido.Id, semilla)) // Desempate determinístico
                .Select(x => x.Contenido)
                .ToList();

            var contenidoSinCategoria = contenidos
                .Where(c => !c.CategoriaInteresId.HasValue || !interesesUsuario.ContainsKey(c.CategoriaInteresId.Value))
                .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                .ThenBy(c => CalcularHashDeterministico(c.Id, semilla)) // Desempate determinístico
                .ToList();

            // Usar peso configurable: pesoCategoria% contenido de intereses, resto descubrimiento
            var resultado = new List<Contenido>();
            int indexIntereses = 0;
            int indexDescubrimiento = 0;

            for (int i = 0; i < contenidos.Count; i++)
            {
                // Calcular dinamicamente: si pesoCategoria=80, entonces 8 de cada 10 son de intereses
                bool usarIntereses = (i % 10) < (pesoCategoria / 10);

                if (usarIntereses && indexIntereses < contenidoConCategoria.Count)
                {
                    resultado.Add(contenidoConCategoria[indexIntereses++]);
                }
                else if (indexDescubrimiento < contenidoSinCategoria.Count)
                {
                    resultado.Add(contenidoSinCategoria[indexDescubrimiento++]);
                }
                else if (indexIntereses < contenidoConCategoria.Count)
                {
                    resultado.Add(contenidoConCategoria[indexIntereses++]);
                }
            }

            return resultado;
        }

        private double CalcularScorePorIntereses(
            Contenido contenido,
            Dictionary<int, decimal> interesesUsuario,
            int totalReacciones)
        {
            double score = 0;

            // 1. Score por peso del interes (50%)
            if (contenido.CategoriaInteresId.HasValue &&
                interesesUsuario.TryGetValue(contenido.CategoriaInteresId.Value, out decimal pesoInteres))
            {
                score += (double)pesoInteres * 0.5;
            }

            // 2. Score por engagement (30%)
            double engagement =
                (contenido.NumeroLikes * 1.0) +
                (contenido.NumeroComentarios * 2.0) +
                (totalReacciones * 1.0);
            score += Math.Log(1 + engagement) * 5 * 0.3;

            // 3. Recency boost (20%)
            var horasDesdePublicacion = (DateTime.Now - contenido.FechaPublicacion).TotalHours;
            if (horasDesdePublicacion < 12)
                score += 30 * 0.2;
            else if (horasDesdePublicacion < 48)
                score += 15 * 0.2;
            else if (horasDesdePublicacion < 168) // 1 semana
                score += 5 * 0.2;

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
                },
                new AlgoritmoFeed
                {
                    Codigo = "por_intereses",
                    Nombre = "Por Intereses",
                    Descripcion = "Prioriza contenido basado en tus intereses seleccionados y aprendidos",
                    Icono = "star",
                    Activo = true,
                    EsPorDefecto = false,
                    Orden = 5,
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
