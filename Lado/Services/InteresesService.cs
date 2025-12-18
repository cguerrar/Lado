using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface IInteresesService
    {
        /// <summary>
        /// Registra una interaccion y actualiza los intereses implicitos del usuario
        /// </summary>
        Task RegistrarInteraccionAsync(string? usuarioId, int contenidoId, TipoInteraccion tipo, int? segundosVisto = null);

        /// <summary>
        /// Obtiene los intereses del usuario ordenados por peso
        /// </summary>
        Task<List<InteresUsuario>> ObtenerInteresesUsuarioAsync(string usuarioId, int limite = 10);

        /// <summary>
        /// Obtiene las categorias mas relevantes para un usuario (para feed/recomendaciones)
        /// </summary>
        Task<List<int>> ObtenerCategoriasRelevantesAsync(string usuarioId, int limite = 5);

        /// <summary>
        /// Recalcula los pesos de intereses basado en historial reciente
        /// </summary>
        Task RecalcularPesosUsuarioAsync(string usuarioId);

        /// <summary>
        /// Agrega un interes explicito (seleccionado por el usuario)
        /// </summary>
        Task AgregarInteresExplicitoAsync(string usuarioId, int categoriaId);

        /// <summary>
        /// Elimina un interes del usuario
        /// </summary>
        Task EliminarInteresAsync(string usuarioId, int categoriaId);
    }

    public class InteresesService : IInteresesService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InteresesService> _logger;

        // Pesos por tipo de interaccion (mayor = mas relevante)
        private static readonly Dictionary<TipoInteraccion, decimal> PesosInteraccion = new()
        {
            { TipoInteraccion.Vista, 0.5m },
            { TipoInteraccion.VistaCompleta, 1.5m },
            { TipoInteraccion.Like, 2.0m },
            { TipoInteraccion.Comentario, 3.0m },
            { TipoInteraccion.Compartir, 2.5m },
            { TipoInteraccion.Guardar, 3.5m },
            { TipoInteraccion.ClicPerfil, 1.0m }
        };

        // Factor de decaimiento por antiguedad (dias)
        private const decimal FactorDecaimiento = 0.95m;
        private const int DiasMaxHistorial = 30;

        public InteresesService(ApplicationDbContext context, ILogger<InteresesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RegistrarInteraccionAsync(string? usuarioId, int contenidoId, TipoInteraccion tipo, int? segundosVisto = null)
        {
            try
            {
                // Registrar la interaccion
                var interaccion = new InteraccionContenido
                {
                    UsuarioId = usuarioId,
                    ContenidoId = contenidoId,
                    TipoInteraccion = tipo,
                    FechaInteraccion = DateTime.Now,
                    SegundosVisto = segundosVisto
                };

                _context.InteraccionesContenidos.Add(interaccion);

                // Si hay usuario autenticado, actualizar intereses implicitos
                if (!string.IsNullOrEmpty(usuarioId))
                {
                    await ActualizarInteresImplicitoAsync(usuarioId, contenidoId, tipo);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar interaccion: Usuario={UsuarioId}, Contenido={ContenidoId}",
                    usuarioId, contenidoId);
            }
        }

        private async Task ActualizarInteresImplicitoAsync(string usuarioId, int contenidoId, TipoInteraccion tipo)
        {
            // Obtener la categoria del contenido
            var contenido = await _context.Contenidos
                .Where(c => c.Id == contenidoId)
                .Select(c => new { c.CategoriaInteresId })
                .FirstOrDefaultAsync();

            if (contenido?.CategoriaInteresId == null)
                return;

            var categoriaId = contenido.CategoriaInteresId.Value;
            var pesoInteraccion = PesosInteraccion.GetValueOrDefault(tipo, 1.0m);

            // Buscar interes existente
            var interesExistente = await _context.InteresesUsuarios
                .FirstOrDefaultAsync(i => i.UsuarioId == usuarioId && i.CategoriaInteresId == categoriaId);

            if (interesExistente != null)
            {
                // Actualizar interes existente
                interesExistente.ContadorInteracciones++;
                interesExistente.UltimaInteraccion = DateTime.Now;

                // Incrementar peso (con limite maximo de 100)
                interesExistente.PesoInteres = Math.Min(100m,
                    interesExistente.PesoInteres + (pesoInteraccion * 0.1m));

                _logger.LogDebug("Interes actualizado: Usuario={UsuarioId}, Categoria={CategoriaId}, NuevoPeso={Peso}",
                    usuarioId, categoriaId, interesExistente.PesoInteres);
            }
            else
            {
                // Crear nuevo interes implicito
                var nuevoInteres = new InteresUsuario
                {
                    UsuarioId = usuarioId,
                    CategoriaInteresId = categoriaId,
                    Tipo = TipoInteres.Implicito,
                    PesoInteres = pesoInteraccion,
                    FechaCreacion = DateTime.Now,
                    UltimaInteraccion = DateTime.Now,
                    ContadorInteracciones = 1
                };

                _context.InteresesUsuarios.Add(nuevoInteres);

                _logger.LogInformation("Nuevo interes implicito creado: Usuario={UsuarioId}, Categoria={CategoriaId}",
                    usuarioId, categoriaId);
            }
        }

        public async Task<List<InteresUsuario>> ObtenerInteresesUsuarioAsync(string usuarioId, int limite = 10)
        {
            return await _context.InteresesUsuarios
                .Include(i => i.CategoriaInteres)
                .Where(i => i.UsuarioId == usuarioId)
                .OrderByDescending(i => i.PesoInteres)
                .ThenByDescending(i => i.UltimaInteraccion)
                .Take(limite)
                .ToListAsync();
        }

        public async Task<List<int>> ObtenerCategoriasRelevantesAsync(string usuarioId, int limite = 5)
        {
            return await _context.InteresesUsuarios
                .Where(i => i.UsuarioId == usuarioId && i.PesoInteres > 0.5m)
                .OrderByDescending(i => i.PesoInteres)
                .Take(limite)
                .Select(i => i.CategoriaInteresId)
                .ToListAsync();
        }

        public async Task RecalcularPesosUsuarioAsync(string usuarioId)
        {
            try
            {
                var fechaLimite = DateTime.Now.AddDays(-DiasMaxHistorial);

                // Obtener interacciones recientes agrupadas por categoria
                var interaccionesPorCategoria = await _context.InteraccionesContenidos
                    .Where(i => i.UsuarioId == usuarioId && i.FechaInteraccion >= fechaLimite)
                    .Join(_context.Contenidos.Where(c => c.CategoriaInteresId != null),
                        i => i.ContenidoId,
                        c => c.Id,
                        (i, c) => new { i.TipoInteraccion, i.FechaInteraccion, CategoriaId = c.CategoriaInteresId!.Value })
                    .GroupBy(x => x.CategoriaId)
                    .Select(g => new
                    {
                        CategoriaId = g.Key,
                        Interacciones = g.Select(x => new { x.TipoInteraccion, x.FechaInteraccion }).ToList()
                    })
                    .ToListAsync();

                foreach (var grupo in interaccionesPorCategoria)
                {
                    decimal pesoTotal = 0;
                    int contador = 0;

                    foreach (var interaccion in grupo.Interacciones)
                    {
                        var diasAtras = (DateTime.Now - interaccion.FechaInteraccion).Days;
                        var factorTemporal = (decimal)Math.Pow((double)FactorDecaimiento, diasAtras);
                        var pesoBase = PesosInteraccion.GetValueOrDefault(interaccion.TipoInteraccion, 1.0m);

                        pesoTotal += pesoBase * factorTemporal;
                        contador++;
                    }

                    // Normalizar peso (0-100)
                    var pesoNormalizado = Math.Min(100m, pesoTotal);

                    // Actualizar o crear interes
                    var interes = await _context.InteresesUsuarios
                        .FirstOrDefaultAsync(i => i.UsuarioId == usuarioId && i.CategoriaInteresId == grupo.CategoriaId);

                    if (interes != null)
                    {
                        interes.PesoInteres = pesoNormalizado;
                        interes.ContadorInteracciones = contador;
                        interes.UltimaInteraccion = DateTime.Now;
                    }
                    else
                    {
                        _context.InteresesUsuarios.Add(new InteresUsuario
                        {
                            UsuarioId = usuarioId,
                            CategoriaInteresId = grupo.CategoriaId,
                            Tipo = TipoInteres.Implicito,
                            PesoInteres = pesoNormalizado,
                            ContadorInteracciones = contador,
                            FechaCreacion = DateTime.Now,
                            UltimaInteraccion = DateTime.Now
                        });
                    }
                }

                // Aplicar decaimiento a intereses sin actividad reciente
                var interesesSinActividad = await _context.InteresesUsuarios
                    .Where(i => i.UsuarioId == usuarioId &&
                               i.Tipo == TipoInteres.Implicito &&
                               i.UltimaInteraccion < fechaLimite)
                    .ToListAsync();

                foreach (var interes in interesesSinActividad)
                {
                    interes.PesoInteres *= 0.5m; // Reducir peso a la mitad
                    if (interes.PesoInteres < 0.1m)
                    {
                        _context.InteresesUsuarios.Remove(interes);
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Pesos recalculados para usuario {UsuarioId}: {CantidadCategorias} categorias",
                    usuarioId, interaccionesPorCategoria.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recalcular pesos para usuario {UsuarioId}", usuarioId);
            }
        }

        public async Task AgregarInteresExplicitoAsync(string usuarioId, int categoriaId)
        {
            var existe = await _context.InteresesUsuarios
                .AnyAsync(i => i.UsuarioId == usuarioId && i.CategoriaInteresId == categoriaId);

            if (existe)
            {
                // Actualizar a explicito si ya existe
                var interes = await _context.InteresesUsuarios
                    .FirstAsync(i => i.UsuarioId == usuarioId && i.CategoriaInteresId == categoriaId);

                interes.Tipo = TipoInteres.Explicito;
                interes.PesoInteres = Math.Max(interes.PesoInteres, 5.0m); // Minimo peso para explicitos
            }
            else
            {
                _context.InteresesUsuarios.Add(new InteresUsuario
                {
                    UsuarioId = usuarioId,
                    CategoriaInteresId = categoriaId,
                    Tipo = TipoInteres.Explicito,
                    PesoInteres = 5.0m,
                    FechaCreacion = DateTime.Now,
                    UltimaInteraccion = DateTime.Now,
                    ContadorInteracciones = 0
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task EliminarInteresAsync(string usuarioId, int categoriaId)
        {
            var interes = await _context.InteresesUsuarios
                .FirstOrDefaultAsync(i => i.UsuarioId == usuarioId && i.CategoriaInteresId == categoriaId);

            if (interes != null)
            {
                _context.InteresesUsuarios.Remove(interes);
                await _context.SaveChangesAsync();
            }
        }
    }
}
