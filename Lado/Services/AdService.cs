using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface IAdService
    {
        Task<List<Anuncio>> ObtenerAnunciosActivos(int cantidad = 2, string? usuarioId = null);
        Task RegistrarImpresion(int anuncioId, string? usuarioId = null, string? ipAddress = null);
        Task<bool> RegistrarClic(int anuncioId, string? usuarioId = null, string? ipAddress = null);
        Task ResetearGastoDiario();
    }

    public class AdService : IAdService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdService> _logger;

        public AdService(ApplicationDbContext context, ILogger<AdService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene anuncios activos mezclando anuncios de Lado (empresa) y de agencias
        /// Los anuncios de Lado tienen prioridad y no consumen presupuesto
        /// </summary>
        public async Task<List<Anuncio>> ObtenerAnunciosActivos(int cantidad = 2, string? usuarioId = null)
        {
            try
            {
                var ahora = DateTime.Now;
                var anunciosSeleccionados = new List<Anuncio>();
                var random = new Random();

                // 1. OBTENER ANUNCIOS DE LADO (empresa) - No requieren presupuesto
                var anunciosLado = await _context.Anuncios
                    .Where(a => a.EsAnuncioLado
                        && a.Estado == EstadoAnuncio.Activo
                        && (a.FechaFin == null || a.FechaFin > ahora)
                        && !string.IsNullOrEmpty(a.UrlCreativo))
                    .OrderByDescending(a => a.Prioridad)
                    .ThenBy(a => Guid.NewGuid()) // Aleatorio entre misma prioridad
                    .ToListAsync();

                // 2. OBTENER ANUNCIOS DE AGENCIAS - Requieren presupuesto
                var anunciosAgencias = await _context.Anuncios
                    .Include(a => a.Agencia)
                    .Where(a => !a.EsAnuncioLado
                        && a.Estado == EstadoAnuncio.Activo
                        && a.Agencia != null
                        && a.Agencia.Estado == EstadoAgencia.Activa
                        && a.Agencia.SaldoPublicitario > 0
                        && a.GastoHoy < a.PresupuestoDiario
                        && a.GastoTotal < a.PresupuestoTotal
                        && (a.FechaFin == null || a.FechaFin > ahora)
                        && !string.IsNullOrEmpty(a.UrlCreativo))
                    .OrderByDescending(a => a.Prioridad)
                    .ThenBy(a => Guid.NewGuid())
                    .Take(cantidad * 3)
                    .ToListAsync();

                // 3. MEZCLAR ANUNCIOS - Algoritmo de rotación
                // Los anuncios de Lado tienen 60% de probabilidad de aparecer primero
                var todosAnuncios = new List<Anuncio>();

                // Agregar anuncios de Lado con alta probabilidad
                foreach (var anuncio in anunciosLado)
                {
                    // Probabilidad basada en prioridad (prioridad 10 = 90%, prioridad 1 = 50%)
                    var probabilidad = 0.5 + (anuncio.Prioridad * 0.04);
                    if (random.NextDouble() < probabilidad)
                    {
                        todosAnuncios.Add(anuncio);
                    }
                }

                // Agregar anuncios de agencias con probabilidad basada en presupuesto
                foreach (var anuncio in anunciosAgencias)
                {
                    var presupuestoRestante = anuncio.PresupuestoTotal - anuncio.GastoTotal;
                    var peso = Math.Min((double)presupuestoRestante / 100, 0.9);
                    if (random.NextDouble() < peso)
                    {
                        todosAnuncios.Add(anuncio);
                    }
                }

                // Si no hay suficientes, agregar los restantes
                if (todosAnuncios.Count < cantidad)
                {
                    foreach (var anuncio in anunciosLado.Concat(anunciosAgencias))
                    {
                        if (!todosAnuncios.Contains(anuncio))
                        {
                            todosAnuncios.Add(anuncio);
                        }
                    }
                }

                // 4. SELECCIONAR CANTIDAD SOLICITADA mezclando tipos
                // Alternar entre anuncios de Lado y agencias si hay de ambos
                var anunciosLadoDisponibles = todosAnuncios.Where(a => a.EsAnuncioLado).ToList();
                var anunciosAgenciasDisponibles = todosAnuncios.Where(a => !a.EsAnuncioLado).ToList();

                for (int i = 0; i < cantidad; i++)
                {
                    if (anunciosSeleccionados.Count >= cantidad)
                        break;

                    // Alternar: posición par = preferir Lado, impar = preferir agencia
                    if (i % 2 == 0 && anunciosLadoDisponibles.Any())
                    {
                        var anuncio = anunciosLadoDisponibles.First();
                        anunciosSeleccionados.Add(anuncio);
                        anunciosLadoDisponibles.Remove(anuncio);
                    }
                    else if (anunciosAgenciasDisponibles.Any())
                    {
                        var anuncio = anunciosAgenciasDisponibles.First();
                        anunciosSeleccionados.Add(anuncio);
                        anunciosAgenciasDisponibles.Remove(anuncio);
                    }
                    else if (anunciosLadoDisponibles.Any())
                    {
                        var anuncio = anunciosLadoDisponibles.First();
                        anunciosSeleccionados.Add(anuncio);
                        anunciosLadoDisponibles.Remove(anuncio);
                    }
                }

                return anunciosSeleccionados;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener anuncios activos");
                return new List<Anuncio>();
            }
        }

        /// <summary>
        /// Registra una impresion de anuncio y cobra CPM a la agencia (si no es anuncio de Lado)
        /// </summary>
        public async Task RegistrarImpresion(int anuncioId, string? usuarioId = null, string? ipAddress = null)
        {
            try
            {
                var anuncio = await _context.Anuncios
                    .Include(a => a.Agencia)
                    .FirstOrDefaultAsync(a => a.Id == anuncioId);

                if (anuncio == null)
                    return;

                // Si es anuncio de Lado, solo registrar impresión sin cobrar
                if (anuncio.EsAnuncioLado)
                {
                    var impresionLado = new ImpresionAnuncio
                    {
                        AnuncioId = anuncioId,
                        UsuarioId = usuarioId,
                        FechaImpresion = DateTime.Now,
                        CostoImpresion = 0, // Sin costo
                        IpAddress = ipAddress
                    };
                    _context.ImpresionesAnuncios.Add(impresionLado);
                    anuncio.Impresiones++;
                    anuncio.UltimaActualizacion = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return;
                }

                // Para anuncios de agencia, verificar que existe la agencia
                if (anuncio.Agencia == null)
                    return;

                // Calcular costo de esta impresion (CPM / 1000)
                var costoImpresion = anuncio.CostoPorMilImpresiones / 1000m;

                // Verificar que la agencia tiene saldo suficiente
                if (anuncio.Agencia.SaldoPublicitario < costoImpresion)
                {
                    // Sin saldo, pausar anuncio
                    anuncio.Estado = EstadoAnuncio.Pausado;
                    anuncio.FechaPausa = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return;
                }

                // Registrar la impresion
                var impresion = new ImpresionAnuncio
                {
                    AnuncioId = anuncioId,
                    UsuarioId = usuarioId,
                    FechaImpresion = DateTime.Now,
                    CostoImpresion = costoImpresion,
                    IpAddress = ipAddress
                };
                _context.ImpresionesAnuncios.Add(impresion);

                // Actualizar contadores del anuncio
                anuncio.Impresiones++;
                anuncio.GastoTotal += costoImpresion;
                anuncio.GastoHoy += costoImpresion;
                anuncio.UltimaActualizacion = DateTime.Now;

                // Descontar del saldo de la agencia
                anuncio.Agencia.SaldoPublicitario -= costoImpresion;
                anuncio.Agencia.TotalGastado += costoImpresion;

                // Registrar transaccion de la agencia
                var transaccion = new TransaccionAgencia
                {
                    AgenciaId = anuncio.AgenciaId!.Value,
                    Tipo = TipoTransaccionAgencia.CobroCPM,
                    Monto = -costoImpresion,
                    Descripcion = $"Impresion - {anuncio.Titulo}",
                    AnuncioId = anuncioId,
                    SaldoAnterior = anuncio.Agencia.SaldoPublicitario + costoImpresion,
                    SaldoPosterior = anuncio.Agencia.SaldoPublicitario,
                    FechaTransaccion = DateTime.Now
                };
                _context.TransaccionesAgencias.Add(transaccion);

                // Verificar si alcanzo el limite diario o total
                if (anuncio.GastoHoy >= anuncio.PresupuestoDiario || anuncio.GastoTotal >= anuncio.PresupuestoTotal)
                {
                    anuncio.Estado = EstadoAnuncio.Pausado;
                    anuncio.FechaPausa = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar impresion para anuncio {AnuncioId}", anuncioId);
            }
        }

        /// <summary>
        /// Registra un clic en un anuncio y cobra CPC a la agencia (si no es anuncio de Lado)
        /// </summary>
        public async Task<bool> RegistrarClic(int anuncioId, string? usuarioId = null, string? ipAddress = null)
        {
            try
            {
                var anuncio = await _context.Anuncios
                    .Include(a => a.Agencia)
                    .FirstOrDefaultAsync(a => a.Id == anuncioId);

                if (anuncio == null)
                    return false;

                // Si es anuncio de Lado, solo registrar clic sin cobrar
                if (anuncio.EsAnuncioLado)
                {
                    var clicLado = new ClicAnuncio
                    {
                        AnuncioId = anuncioId,
                        UsuarioId = usuarioId,
                        FechaClic = DateTime.Now,
                        CostoClic = 0, // Sin costo
                        IpAddress = ipAddress
                    };
                    _context.ClicsAnuncios.Add(clicLado);
                    anuncio.Clics++;
                    anuncio.UltimaActualizacion = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return true;
                }

                // Para anuncios de agencia, verificar que existe la agencia
                if (anuncio.Agencia == null)
                    return false;

                var costoClic = anuncio.CostoPorClic;

                // Verificar que la agencia tiene saldo suficiente
                if (anuncio.Agencia.SaldoPublicitario < costoClic)
                {
                    return false;
                }

                // Registrar el clic
                var clic = new ClicAnuncio
                {
                    AnuncioId = anuncioId,
                    UsuarioId = usuarioId,
                    FechaClic = DateTime.Now,
                    CostoClic = costoClic,
                    IpAddress = ipAddress
                };
                _context.ClicsAnuncios.Add(clic);

                // Actualizar contadores del anuncio
                anuncio.Clics++;
                anuncio.GastoTotal += costoClic;
                anuncio.GastoHoy += costoClic;
                anuncio.UltimaActualizacion = DateTime.Now;

                // Descontar del saldo de la agencia
                anuncio.Agencia.SaldoPublicitario -= costoClic;
                anuncio.Agencia.TotalGastado += costoClic;

                // Registrar transaccion de la agencia
                var transaccion = new TransaccionAgencia
                {
                    AgenciaId = anuncio.AgenciaId!.Value,
                    Tipo = TipoTransaccionAgencia.CobroCPC,
                    Monto = -costoClic,
                    Descripcion = $"Clic - {anuncio.Titulo}",
                    AnuncioId = anuncioId,
                    SaldoAnterior = anuncio.Agencia.SaldoPublicitario + costoClic,
                    SaldoPosterior = anuncio.Agencia.SaldoPublicitario,
                    FechaTransaccion = DateTime.Now
                };
                _context.TransaccionesAgencias.Add(transaccion);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar clic para anuncio {AnuncioId}", anuncioId);
                return false;
            }
        }

        /// <summary>
        /// Resetea el gasto diario de todos los anuncios (ejecutar a medianoche)
        /// </summary>
        public async Task ResetearGastoDiario()
        {
            try
            {
                var anuncios = await _context.Anuncios
                    .Where(a => a.GastoHoy > 0)
                    .ToListAsync();

                foreach (var anuncio in anuncios)
                {
                    anuncio.GastoHoy = 0;

                    // Reactivar anuncios pausados por limite diario si tienen presupuesto total
                    if (anuncio.Estado == EstadoAnuncio.Pausado && anuncio.GastoTotal < anuncio.PresupuestoTotal)
                    {
                        anuncio.Estado = EstadoAnuncio.Activo;
                        anuncio.FechaPausa = null;
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Gasto diario reseteado para {Count} anuncios", anuncios.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al resetear gasto diario de anuncios");
            }
        }
    }
}
