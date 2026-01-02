using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface ILadoCoinsService
    {
        // Consultas
        Task<LadoCoin?> ObtenerSaldoAsync(string usuarioId);
        Task<decimal> ObtenerSaldoDisponibleAsync(string usuarioId);
        Task<List<TransaccionLadoCoin>> ObtenerHistorialAsync(string usuarioId, int pagina = 1, int porPagina = 20);
        Task<decimal> ObtenerConfiguracionAsync(string clave);

        // Operaciones de crédito (ingresos)
        Task<bool> AcreditarBonoAsync(string usuarioId, TipoTransaccionLadoCoin tipoBono, string? descripcion = null, string? referenciaId = null);
        Task<bool> AcreditarMontoAsync(string usuarioId, decimal monto, TipoTransaccionLadoCoin tipo, string? descripcion = null, string? referenciaId = null, string? tipoReferencia = null);

        // Operaciones de débito (gastos)
        Task<(bool exito, decimal montoQuemado)> DebitarAsync(string usuarioId, decimal monto, TipoTransaccionLadoCoin tipo, string? descripcion = null, string? referenciaId = null, string? tipoReferencia = null);
        Task<bool> PuedeUsarLadoCoinsAsync(string usuarioId, decimal monto);

        // Vencimiento
        Task<decimal> ObtenerMontoPorVencerAsync(string usuarioId, int diasAnticipacion = 7);
        Task ProcesarVencimientosAsync();

        // Pagos mixtos
        Task<(decimal montoLadoCoins, decimal montoReal)> CalcularPagoMixtoAsync(decimal montoTotal, decimal porcentajeMaxLadoCoins, decimal saldoDisponible);

        // Inicialización
        Task<LadoCoin> ObtenerOCrearSaldoAsync(string usuarioId);
    }

    public class LadoCoinsService : ILadoCoinsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LadoCoinsService> _logger;
        private readonly ILogEventoService _logEventoService;
        private readonly INotificationService? _notificacionService;

        // Cache de configuraciones (se recarga cada 5 minutos)
        private static Dictionary<string, decimal> _configCache = new();
        private static DateTime _ultimaRecargaConfig = DateTime.MinValue;
        private static readonly object _lockConfig = new();

        public LadoCoinsService(
            ApplicationDbContext context,
            ILogger<LadoCoinsService> logger,
            ILogEventoService logEventoService,
            INotificationService? notificacionService = null)
        {
            _context = context;
            _logger = logger;
            _logEventoService = logEventoService;
            _notificacionService = notificacionService;
        }

        #region Consultas

        public async Task<LadoCoin?> ObtenerSaldoAsync(string usuarioId)
        {
            return await _context.LadoCoins
                .FirstOrDefaultAsync(l => l.UsuarioId == usuarioId);
        }

        public async Task<decimal> ObtenerSaldoDisponibleAsync(string usuarioId)
        {
            var saldo = await _context.LadoCoins
                .Where(l => l.UsuarioId == usuarioId)
                .Select(l => l.SaldoDisponible)
                .FirstOrDefaultAsync();
            return saldo;
        }

        public async Task<List<TransaccionLadoCoin>> ObtenerHistorialAsync(string usuarioId, int pagina = 1, int porPagina = 20)
        {
            return await _context.TransaccionesLadoCoins
                .Where(t => t.UsuarioId == usuarioId)
                .OrderByDescending(t => t.FechaTransaccion)
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();
        }

        public async Task<decimal> ObtenerConfiguracionAsync(string clave)
        {
            // Verificar cache
            lock (_lockConfig)
            {
                if (_configCache.ContainsKey(clave) &&
                    (DateTime.Now - _ultimaRecargaConfig).TotalMinutes < 5)
                {
                    return _configCache[clave];
                }
            }

            // Cargar desde BD
            var config = await _context.ConfiguracionesLadoCoins
                .Where(c => c.Clave == clave && c.Activo)
                .Select(c => c.Valor)
                .FirstOrDefaultAsync();

            lock (_lockConfig)
            {
                _configCache[clave] = config;
                _ultimaRecargaConfig = DateTime.Now;
            }

            return config;
        }

        #endregion

        #region Operaciones de Crédito

        public async Task<bool> AcreditarBonoAsync(string usuarioId, TipoTransaccionLadoCoin tipoBono, string? descripcion = null, string? referenciaId = null)
        {
            try
            {
                // Obtener el monto del bono desde configuración
                var claveConfig = ObtenerClaveConfiguracion(tipoBono);
                if (string.IsNullOrEmpty(claveConfig))
                {
                    _logger.LogWarning("No se encontró clave de configuración para el tipo de bono {Tipo}", tipoBono);
                    return false;
                }

                var monto = await ObtenerConfiguracionAsync(claveConfig);
                if (monto <= 0)
                {
                    _logger.LogWarning("El monto del bono {Tipo} es 0 o negativo", tipoBono);
                    return false;
                }

                return await AcreditarMontoAsync(usuarioId, monto, tipoBono, descripcion ?? ObtenerDescripcionBono(tipoBono), referenciaId, "Bono");
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, usuarioId, null);
                return false;
            }
        }

        public async Task<bool> AcreditarMontoAsync(string usuarioId, decimal monto, TipoTransaccionLadoCoin tipo, string? descripcion = null, string? referenciaId = null, string? tipoReferencia = null)
        {
            if (monto <= 0) return false;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var saldo = await ObtenerOCrearSaldoAsync(usuarioId);
                var saldoAnterior = saldo.SaldoDisponible;

                // Calcular fecha de vencimiento (30 días por defecto)
                var diasVencimiento = await ObtenerConfiguracionAsync(ConfiguracionLadoCoin.DIAS_VENCIMIENTO);
                if (diasVencimiento <= 0) diasVencimiento = 30;
                var fechaVencimiento = DateTime.Now.AddDays((double)diasVencimiento);

                // Actualizar saldo
                saldo.SaldoDisponible += monto;
                saldo.TotalGanado += monto;
                saldo.UltimaActualizacion = DateTime.Now;

                // Registrar transacción
                var transaccion = new TransaccionLadoCoin
                {
                    UsuarioId = usuarioId,
                    Tipo = tipo,
                    Monto = monto,
                    SaldoAnterior = saldoAnterior,
                    SaldoPosterior = saldo.SaldoDisponible,
                    Descripcion = descripcion,
                    ReferenciaId = referenciaId,
                    TipoReferencia = tipoReferencia,
                    FechaTransaccion = DateTime.Now,
                    FechaVencimiento = fechaVencimiento,
                    MontoRestante = monto,
                    Vencido = false
                };

                _context.TransaccionesLadoCoins.Add(transaccion);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Acreditado ${Monto} LadoCoins a usuario {UsuarioId} por {Tipo}", monto, usuarioId, tipo);

                // Notificar si es un bono significativo
                if (monto >= 5 && _notificacionService != null)
                {
                    await _notificacionService.CrearNotificacionSistemaAsync(
                        usuarioId,
                        $"Has ganado ${monto:F2} LadoCoins",
                        descripcion ?? $"Bono por {tipo}",
                        "/LadoCoins"
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, usuarioId, null);
                return false;
            }
        }

        #endregion

        #region Operaciones de Débito

        public async Task<(bool exito, decimal montoQuemado)> DebitarAsync(string usuarioId, decimal monto, TipoTransaccionLadoCoin tipo, string? descripcion = null, string? referenciaId = null, string? tipoReferencia = null)
        {
            if (monto <= 0) return (false, 0);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var saldo = await ObtenerSaldoAsync(usuarioId);
                if (saldo == null || saldo.SaldoDisponible < monto)
                {
                    return (false, 0);
                }

                var saldoAnterior = saldo.SaldoDisponible;

                // Calcular quema (5% por defecto)
                var porcentajeQuema = await ObtenerConfiguracionAsync(ConfiguracionLadoCoin.PORCENTAJE_QUEMA);
                if (porcentajeQuema <= 0) porcentajeQuema = 5;
                var montoQuemado = Math.Round(monto * porcentajeQuema / 100, 2);

                // Actualizar saldo
                saldo.SaldoDisponible -= monto;
                saldo.TotalGastado += monto;
                saldo.TotalQuemado += montoQuemado;
                saldo.UltimaActualizacion = DateTime.Now;

                // Descontar de transacciones usando FIFO
                await DescontarFIFOAsync(usuarioId, monto);

                // Registrar transacción de gasto
                var transaccionGasto = new TransaccionLadoCoin
                {
                    UsuarioId = usuarioId,
                    Tipo = tipo,
                    Monto = -monto,
                    MontoQuemado = montoQuemado,
                    SaldoAnterior = saldoAnterior,
                    SaldoPosterior = saldo.SaldoDisponible,
                    Descripcion = descripcion,
                    ReferenciaId = referenciaId,
                    TipoReferencia = tipoReferencia,
                    FechaTransaccion = DateTime.Now
                };

                _context.TransaccionesLadoCoins.Add(transaccionGasto);

                // Registrar transacción de quema separada
                if (montoQuemado > 0)
                {
                    var transaccionQuema = new TransaccionLadoCoin
                    {
                        UsuarioId = usuarioId,
                        Tipo = TipoTransaccionLadoCoin.Quema5Porciento,
                        Monto = -montoQuemado,
                        SaldoAnterior = saldo.SaldoDisponible,
                        SaldoPosterior = saldo.SaldoDisponible,
                        Descripcion = $"Quema 5% de transacción #{referenciaId}",
                        ReferenciaId = referenciaId,
                        TipoReferencia = "Quema",
                        FechaTransaccion = DateTime.Now
                    };
                    _context.TransaccionesLadoCoins.Add(transaccionQuema);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Debitado ${Monto} LadoCoins de usuario {UsuarioId} por {Tipo}, quema: ${Quema}", monto, usuarioId, tipo, montoQuemado);

                return (true, montoQuemado);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, usuarioId, null);
                return (false, 0);
            }
        }

        public async Task<bool> PuedeUsarLadoCoinsAsync(string usuarioId, decimal monto)
        {
            if (monto <= 0) return true;
            var saldoDisponible = await ObtenerSaldoDisponibleAsync(usuarioId);
            return saldoDisponible >= monto;
        }

        private async Task DescontarFIFOAsync(string usuarioId, decimal montoADescontar)
        {
            // Obtener transacciones de ingreso ordenadas por fecha de vencimiento (las que vencen primero)
            var transaccionesConSaldo = await _context.TransaccionesLadoCoins
                .Where(t => t.UsuarioId == usuarioId &&
                           t.MontoRestante > 0 &&
                           !t.Vencido &&
                           t.FechaVencimiento.HasValue)
                .OrderBy(t => t.FechaVencimiento)
                .ToListAsync();

            var montoRestante = montoADescontar;

            foreach (var transaccion in transaccionesConSaldo)
            {
                if (montoRestante <= 0) break;

                if (transaccion.MontoRestante >= montoRestante)
                {
                    transaccion.MontoRestante -= montoRestante;
                    montoRestante = 0;
                }
                else
                {
                    montoRestante -= transaccion.MontoRestante;
                    transaccion.MontoRestante = 0;
                }
            }
        }

        #endregion

        #region Vencimiento

        public async Task<decimal> ObtenerMontoPorVencerAsync(string usuarioId, int diasAnticipacion = 7)
        {
            var fechaLimite = DateTime.Now.AddDays(diasAnticipacion);

            return await _context.TransaccionesLadoCoins
                .Where(t => t.UsuarioId == usuarioId &&
                           t.MontoRestante > 0 &&
                           !t.Vencido &&
                           t.FechaVencimiento.HasValue &&
                           t.FechaVencimiento.Value <= fechaLimite)
                .SumAsync(t => t.MontoRestante);
        }

        public async Task ProcesarVencimientosAsync()
        {
            var ahora = DateTime.Now;

            // Obtener transacciones vencidas que aún tienen saldo
            var transaccionesVencidas = await _context.TransaccionesLadoCoins
                .Where(t => !t.Vencido &&
                           t.MontoRestante > 0 &&
                           t.FechaVencimiento.HasValue &&
                           t.FechaVencimiento.Value <= ahora)
                .Include(t => t.Usuario)
                .ToListAsync();

            // Agrupar por usuario para procesar en lote
            var porUsuario = transaccionesVencidas.GroupBy(t => t.UsuarioId);

            foreach (var grupo in porUsuario)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var usuarioId = grupo.Key;
                    var montoTotalVencido = grupo.Sum(t => t.MontoRestante);

                    var saldo = await ObtenerSaldoAsync(usuarioId);
                    if (saldo == null) continue;

                    var saldoAnterior = saldo.SaldoDisponible;

                    // Marcar transacciones como vencidas
                    foreach (var transaccion in grupo)
                    {
                        transaccion.Vencido = true;
                    }

                    // Actualizar saldo
                    saldo.SaldoDisponible -= montoTotalVencido;
                    if (saldo.SaldoDisponible < 0) saldo.SaldoDisponible = 0;
                    saldo.UltimaActualizacion = DateTime.Now;

                    // Registrar transacción de vencimiento
                    var transaccionVencimiento = new TransaccionLadoCoin
                    {
                        UsuarioId = usuarioId,
                        Tipo = TipoTransaccionLadoCoin.Vencimiento,
                        Monto = -montoTotalVencido,
                        SaldoAnterior = saldoAnterior,
                        SaldoPosterior = saldo.SaldoDisponible,
                        Descripcion = $"Vencimiento de {grupo.Count()} transacciones",
                        FechaTransaccion = DateTime.Now
                    };

                    _context.TransaccionesLadoCoins.Add(transaccionVencimiento);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Procesado vencimiento de ${Monto} LadoCoins para usuario {UsuarioId}", montoTotalVencido, usuarioId);

                    // Notificar al usuario
                    if (_notificacionService != null)
                    {
                        await _notificacionService.CrearNotificacionSistemaAsync(
                            usuarioId,
                            "LadoCoins vencidos",
                            $"${montoTotalVencido:F2} LadoCoins han vencido. Recuerda usarlos antes de 30 días.",
                            "/LadoCoins"
                        );
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, grupo.Key, null);
                }
            }
        }

        #endregion

        #region Pagos Mixtos

        public async Task<(decimal montoLadoCoins, decimal montoReal)> CalcularPagoMixtoAsync(decimal montoTotal, decimal porcentajeMaxLadoCoins, decimal saldoDisponible)
        {
            // Calcular máximo de LadoCoins permitido
            var maxLadoCoins = Math.Round(montoTotal * porcentajeMaxLadoCoins / 100, 2);

            // El monto real de LadoCoins a usar es el menor entre: máximo permitido y saldo disponible
            var montoLadoCoins = Math.Min(maxLadoCoins, saldoDisponible);

            // El resto se paga en dinero real
            var montoReal = montoTotal - montoLadoCoins;

            return (montoLadoCoins, montoReal);
        }

        #endregion

        #region Inicialización

        public async Task<LadoCoin> ObtenerOCrearSaldoAsync(string usuarioId)
        {
            var saldo = await ObtenerSaldoAsync(usuarioId);

            if (saldo == null)
            {
                saldo = new LadoCoin
                {
                    UsuarioId = usuarioId,
                    SaldoDisponible = 0,
                    SaldoPorVencer = 0,
                    TotalGanado = 0,
                    TotalGastado = 0,
                    TotalQuemado = 0,
                    TotalRecibido = 0,
                    FechaCreacion = DateTime.Now,
                    UltimaActualizacion = DateTime.Now
                };

                _context.LadoCoins.Add(saldo);
                await _context.SaveChangesAsync();
            }

            return saldo;
        }

        #endregion

        #region Helpers

        private string? ObtenerClaveConfiguracion(TipoTransaccionLadoCoin tipo)
        {
            return tipo switch
            {
                TipoTransaccionLadoCoin.BonoBienvenida => ConfiguracionLadoCoin.BONO_BIENVENIDA,
                TipoTransaccionLadoCoin.BonoPrimerContenido => ConfiguracionLadoCoin.BONO_PRIMER_CONTENIDO,
                TipoTransaccionLadoCoin.BonoVerificarEmail => ConfiguracionLadoCoin.BONO_VERIFICAR_EMAIL,
                TipoTransaccionLadoCoin.BonoCompletarPerfil => ConfiguracionLadoCoin.BONO_COMPLETAR_PERFIL,
                TipoTransaccionLadoCoin.BonoLoginDiario => ConfiguracionLadoCoin.BONO_LOGIN_DIARIO,
                TipoTransaccionLadoCoin.BonoSubirContenido => ConfiguracionLadoCoin.BONO_CONTENIDO_DIARIO,
                TipoTransaccionLadoCoin.BonoDarLikes => ConfiguracionLadoCoin.BONO_5_LIKES,
                TipoTransaccionLadoCoin.BonoComentar => ConfiguracionLadoCoin.BONO_3_COMENTARIOS,
                TipoTransaccionLadoCoin.BonoRacha7Dias => ConfiguracionLadoCoin.BONO_RACHA_7_DIAS,
                TipoTransaccionLadoCoin.BonoReferidor => ConfiguracionLadoCoin.BONO_REFERIDOR,
                TipoTransaccionLadoCoin.BonoReferido => ConfiguracionLadoCoin.BONO_REFERIDO,
                TipoTransaccionLadoCoin.BonoReferidoCreadorLadoB => ConfiguracionLadoCoin.BONO_REFERIDO_CREADOR,
                _ => null
            };
        }

        private string ObtenerDescripcionBono(TipoTransaccionLadoCoin tipo)
        {
            return tipo switch
            {
                TipoTransaccionLadoCoin.BonoBienvenida => "Bono de bienvenida por registrarte",
                TipoTransaccionLadoCoin.BonoPrimerContenido => "Bono por tu primera publicación",
                TipoTransaccionLadoCoin.BonoVerificarEmail => "Bono por verificar tu email",
                TipoTransaccionLadoCoin.BonoCompletarPerfil => "Bono por completar tu perfil",
                TipoTransaccionLadoCoin.BonoLoginDiario => "Bono por login diario",
                TipoTransaccionLadoCoin.BonoSubirContenido => "Bono por subir contenido hoy",
                TipoTransaccionLadoCoin.BonoDarLikes => "Bono por dar 5 likes hoy",
                TipoTransaccionLadoCoin.BonoComentar => "Bono por comentar 3 veces hoy",
                TipoTransaccionLadoCoin.BonoRacha7Dias => "Bono por racha de 7 días consecutivos",
                TipoTransaccionLadoCoin.BonoReferidor => "Bono por referir un nuevo usuario",
                TipoTransaccionLadoCoin.BonoReferido => "Bono por registrarte con código de referido",
                TipoTransaccionLadoCoin.BonoReferidoCreadorLadoB => "Bono por tu referido convertido en creador LadoB",
                _ => "Bono"
            };
        }

        #endregion
    }
}
