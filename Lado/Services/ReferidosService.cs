using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface IReferidosService
    {
        // Códigos de referido
        Task<string> GenerarCodigoReferidoAsync(string usuarioId);
        Task<ApplicationUser?> BuscarPorCodigoAsync(string codigo);

        // Registro de referido
        Task<bool> RegistrarReferidoAsync(string referidoId, string codigoReferido);
        Task<bool> EntregarBonosRegistroAsync(string referidoId);

        // Consultas
        Task<List<Referido>> ObtenerMisReferidosAsync(string usuarioId);
        Task<Referido?> ObtenerMiReferidorAsync(string usuarioId);
        Task<int> ContarReferidosAsync(string usuarioId);
        Task<decimal> TotalComisionesGanadasAsync(string usuarioId);

        // Comisiones
        Task<bool> ProcesarComisionAsync(string referidoId, decimal montoPremio);
        Task ProcesarCreadorLadoBAsync(string usuarioId);
        Task ExpirarComisionesAsync();
    }

    public class ReferidosService : IReferidosService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly ILogger<ReferidosService> _logger;
        private readonly ILogEventoService _logEventoService;
        private readonly INotificationService? _notificacionService;

        private static readonly Random _random = new();

        public ReferidosService(
            ApplicationDbContext context,
            ILadoCoinsService ladoCoinsService,
            ILogger<ReferidosService> logger,
            ILogEventoService logEventoService,
            INotificationService? notificacionService = null)
        {
            _context = context;
            _ladoCoinsService = ladoCoinsService;
            _logger = logger;
            _logEventoService = logEventoService;
            _notificacionService = notificacionService;
        }

        #region Códigos de Referido

        public async Task<string> GenerarCodigoReferidoAsync(string usuarioId)
        {
            var usuario = await _context.Users.FindAsync(usuarioId);
            if (usuario == null) return string.Empty;

            // Si ya tiene código, devolverlo
            if (!string.IsNullOrEmpty(usuario.CodigoReferido))
            {
                return usuario.CodigoReferido;
            }

            // Generar código único
            string codigo;
            var intentos = 0;
            do
            {
                codigo = GenerarCodigoAleatorio();
                intentos++;
            }
            while (await _context.Users.AnyAsync(u => u.CodigoReferido == codigo) && intentos < 10);

            if (intentos >= 10)
            {
                // Fallback: usar parte del ID del usuario
                codigo = $"REF{usuarioId.Substring(0, 8).ToUpper()}";
            }

            // Guardar código
            usuario.CodigoReferido = codigo;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generado código de referido {Codigo} para usuario {UsuarioId}", codigo, usuarioId);

            return codigo;
        }

        private string GenerarCodigoAleatorio()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        public async Task<ApplicationUser?> BuscarPorCodigoAsync(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return null;

            return await _context.Users
                .FirstOrDefaultAsync(u => u.CodigoReferido == codigo.ToUpper().Trim());
        }

        #endregion

        #region Registro de Referido

        public async Task<bool> RegistrarReferidoAsync(string referidoId, string codigoReferido)
        {
            try
            {
                // Validar que el código existe
                var referidor = await BuscarPorCodigoAsync(codigoReferido);
                if (referidor == null)
                {
                    _logger.LogWarning("Código de referido no encontrado: {Codigo}", codigoReferido);
                    return false;
                }

                // No puede referirse a sí mismo
                if (referidor.Id == referidoId)
                {
                    _logger.LogWarning("Usuario intentó referirse a sí mismo: {UsuarioId}", referidoId);
                    return false;
                }

                // Verificar que no haya sido referido antes
                var yaReferido = await _context.Referidos
                    .AnyAsync(r => r.ReferidoUsuarioId == referidoId);
                if (yaReferido)
                {
                    _logger.LogWarning("Usuario ya fue referido anteriormente: {UsuarioId}", referidoId);
                    return false;
                }

                // Calcular fecha de expiración de comisiones (3 meses)
                var mesesComision = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.COMISION_REFERIDO_MESES);
                if (mesesComision <= 0) mesesComision = 3;

                // Crear registro de referido
                var referido = new Referido
                {
                    ReferidorId = referidor.Id,
                    ReferidoUsuarioId = referidoId,
                    CodigoUsado = codigoReferido.ToUpper().Trim(),
                    FechaRegistro = DateTime.Now,
                    FechaExpiracionComision = DateTime.Now.AddMonths((int)mesesComision),
                    ComisionActiva = true
                };

                _context.Referidos.Add(referido);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Registrado referido {ReferidoId} con código {Codigo} del referidor {ReferidorId}",
                    referidoId, codigoReferido, referidor.Id);

                return true;
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Usuario, referidoId, null);
                return false;
            }
        }

        public async Task<bool> EntregarBonosRegistroAsync(string referidoId)
        {
            try
            {
                var referido = await _context.Referidos
                    .Include(r => r.Referidor)
                    .FirstOrDefaultAsync(r => r.ReferidoUsuarioId == referidoId);

                if (referido == null) return false;

                var entregados = 0;

                // Bono al referido ($15)
                if (!referido.BonoReferidoEntregado)
                {
                    var exito = await _ladoCoinsService.AcreditarBonoAsync(
                        referidoId,
                        TipoTransaccionLadoCoin.BonoReferido,
                        "Bono por registrarte con código de referido",
                        referido.Id.ToString()
                    );

                    if (exito)
                    {
                        referido.BonoReferidoEntregado = true;
                        entregados++;
                    }
                }

                // Bono al referidor ($10)
                if (!referido.BonoReferidorEntregado)
                {
                    var exito = await _ladoCoinsService.AcreditarBonoAsync(
                        referido.ReferidorId,
                        TipoTransaccionLadoCoin.BonoReferidor,
                        $"Bono por referir a un nuevo usuario",
                        referido.Id.ToString()
                    );

                    if (exito)
                    {
                        referido.BonoReferidorEntregado = true;
                        entregados++;

                        // Notificar al referidor
                        if (_notificacionService != null && referido.Referidor != null)
                        {
                            await _notificacionService.CrearNotificacionSistemaAsync(
                                referido.ReferidorId,
                                "Nuevo referido registrado",
                                "Alguien se registró con tu código. ¡Ganaste LadoCoins!",
                                "/LadoCoins/Referidos"
                            );
                        }
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Entregados {Count} bonos de registro para referido {ReferidoId}", entregados, referidoId);

                return entregados > 0;
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, referidoId, null);
                return false;
            }
        }

        #endregion

        #region Consultas

        public async Task<List<Referido>> ObtenerMisReferidosAsync(string usuarioId)
        {
            return await _context.Referidos
                .Include(r => r.ReferidoUsuario)
                .Where(r => r.ReferidorId == usuarioId)
                .OrderByDescending(r => r.FechaRegistro)
                .ToListAsync();
        }

        public async Task<Referido?> ObtenerMiReferidorAsync(string usuarioId)
        {
            return await _context.Referidos
                .Include(r => r.Referidor)
                .FirstOrDefaultAsync(r => r.ReferidoUsuarioId == usuarioId);
        }

        public async Task<int> ContarReferidosAsync(string usuarioId)
        {
            return await _context.Referidos
                .CountAsync(r => r.ReferidorId == usuarioId);
        }

        public async Task<decimal> TotalComisionesGanadasAsync(string usuarioId)
        {
            return await _context.Referidos
                .Where(r => r.ReferidorId == usuarioId)
                .SumAsync(r => r.TotalComisionGanada);
        }

        #endregion

        #region Comisiones

        public async Task<bool> ProcesarComisionAsync(string referidoId, decimal montoPremio)
        {
            try
            {
                // Buscar si tiene referidor activo
                var referido = await _context.Referidos
                    .FirstOrDefaultAsync(r => r.ReferidoUsuarioId == referidoId &&
                                              r.ComisionActiva &&
                                              r.FechaExpiracionComision > DateTime.Now);

                if (referido == null) return false;

                // Calcular comisión (10% por defecto)
                var porcentajeComision = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.COMISION_REFERIDO_PORCENTAJE);
                if (porcentajeComision <= 0) porcentajeComision = 10;

                var montoComision = Math.Round(montoPremio * porcentajeComision / 100, 2);
                if (montoComision <= 0) return false;

                // Acreditar al referidor
                var exito = await _ladoCoinsService.AcreditarMontoAsync(
                    referido.ReferidorId,
                    montoComision,
                    TipoTransaccionLadoCoin.ComisionReferido,
                    $"Comisión {porcentajeComision}% de premio de tu referido",
                    referido.Id.ToString(),
                    "ComisionReferido"
                );

                if (exito)
                {
                    referido.TotalComisionGanada += montoComision;
                    referido.UltimaComision = DateTime.Now;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Procesada comisión ${Monto} para referidor {ReferidorId} del referido {ReferidoId}",
                        montoComision, referido.ReferidorId, referidoId);
                }

                return exito;
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, referidoId, null);
                return false;
            }
        }

        public async Task ProcesarCreadorLadoBAsync(string usuarioId)
        {
            try
            {
                // Buscar si fue referido y no se ha entregado el bono de creador
                var referido = await _context.Referidos
                    .FirstOrDefaultAsync(r => r.ReferidoUsuarioId == usuarioId && !r.BonoCreadorLadoBEntregado);

                if (referido == null) return;

                // Verificar que el usuario es creador de LadoB
                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario == null || !usuario.EsCreador) return;

                // Verificar que tiene contenido en LadoB
                var tieneContenidoLadoB = await _context.Contenidos
                    .AnyAsync(c => c.UsuarioId == usuarioId && c.TipoLado == TipoLado.LadoB && c.EstaActivo);

                if (!tieneContenidoLadoB) return;

                // Entregar bono al referidor ($50)
                var exito = await _ladoCoinsService.AcreditarBonoAsync(
                    referido.ReferidorId,
                    TipoTransaccionLadoCoin.BonoReferidoCreadorLadoB,
                    "Bono por tu referido convertido en creador LadoB",
                    referido.Id.ToString()
                );

                if (exito)
                {
                    referido.BonoCreadorLadoBEntregado = true;
                    await _context.SaveChangesAsync();

                    // Notificar al referidor
                    if (_notificacionService != null)
                    {
                        await _notificacionService.CrearNotificacionSistemaAsync(
                            referido.ReferidorId,
                            "Tu referido ahora es creador LadoB",
                            "¡Felicidades! Has ganado $50 LadoCoins porque tu referido se convirtió en creador.",
                            "/LadoCoins/Referidos"
                        );
                    }

                    _logger.LogInformation("Entregado bono creador LadoB al referidor {ReferidorId} por usuario {UsuarioId}",
                        referido.ReferidorId, usuarioId);
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, usuarioId, null);
            }
        }

        public async Task ExpirarComisionesAsync()
        {
            try
            {
                var ahora = DateTime.Now;

                var referidosExpirados = await _context.Referidos
                    .Where(r => r.ComisionActiva && r.FechaExpiracionComision <= ahora)
                    .ToListAsync();

                foreach (var referido in referidosExpirados)
                {
                    referido.ComisionActiva = false;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Expiradas comisiones de {Count} referidos", referidosExpirados.Count);
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, null, null);
            }
        }

        #endregion
    }
}
