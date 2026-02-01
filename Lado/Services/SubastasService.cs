using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface ISubastasService
    {
        // ========================================
        // CONSULTAS
        // ========================================
        Task<List<Subasta>> ObtenerSubastasActivasAsync(int pagina = 1, int porPagina = 20);
        Task<List<Subasta>> ObtenerSubastasCreadorAsync(string creadorId);
        Task<Subasta?> ObtenerSubastaAsync(int subastaId);
        Task<List<SubastaPuja>> ObtenerPujasAsync(int subastaId);
        Task<SubastaPuja?> ObtenerPujaActualAsync(int subastaId);
        Task<int> ContarSubastasActivasAsync();

        // ========================================
        // OPERACIONES
        // ========================================
        Task<(bool exito, string mensaje, Subasta? subasta)> CrearSubastaAsync(
            string creadorId,
            string titulo,
            string? descripcion,
            decimal precioInicial,
            decimal incrementoMinimo,
            int duracionHoras,
            int? contenidoId = null,
            string? imagenPreview = null,
            decimal? precioCompraloYa = null,
            bool soloSuscriptores = false,
            TipoContenidoSubasta tipoContenido = TipoContenidoSubasta.AccesoExclusivo);

        Task<(bool exito, string mensaje)> RealizarPujaAsync(
            int subastaId,
            string usuarioId,
            decimal monto,
            string? ipAddress = null);

        Task<(bool exito, string mensaje)> RealizarCompraloYaAsync(
            int subastaId,
            string usuarioId,
            string? ipAddress = null);

        Task<(bool exito, string mensaje)> CancelarSubastaAsync(int subastaId, string creadorId);

        Task<int> FinalizarSubastasExpiradasAsync();

        Task NotificarGanadorAsync(int subastaId);

        // ========================================
        // HELPERS
        // ========================================
        Task<bool> PuedePujarAsync(string usuarioId, int subastaId);
        Task IncrementarVistasAsync(int subastaId);
    }

    public class SubastasService : ISubastasService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SubastasService> _logger;
        private readonly ILogEventoService _logEventoService;
        private readonly INotificationService _notificationService;

        public SubastasService(
            ApplicationDbContext context,
            ILogger<SubastasService> logger,
            ILogEventoService logEventoService,
            INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _logEventoService = logEventoService;
            _notificationService = notificationService;
        }

        #region Consultas

        public async Task<List<Subasta>> ObtenerSubastasActivasAsync(int pagina = 1, int porPagina = 20)
        {
            var ahora = DateTime.Now;

            return await _context.Subastas
                .Include(s => s.Creador)
                .Include(s => s.Contenido)
                .Include(s => s.Pujas.OrderByDescending(p => p.Monto).Take(1))
                .Where(s => s.Estado == EstadoSubasta.Activa
                         && s.FechaInicio <= ahora
                         && s.FechaFin > ahora)
                .OrderBy(s => s.FechaFin) // Las que terminan pronto primero
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();
        }

        public async Task<List<Subasta>> ObtenerSubastasCreadorAsync(string creadorId)
        {
            return await _context.Subastas
                .Include(s => s.Pujas)
                .Include(s => s.Ganador)
                .Include(s => s.Contenido)
                .Where(s => s.CreadorId == creadorId)
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync();
        }

        public async Task<Subasta?> ObtenerSubastaAsync(int subastaId)
        {
            return await _context.Subastas
                .Include(s => s.Creador)
                .Include(s => s.Contenido)
                .Include(s => s.Ganador)
                .Include(s => s.Pujas.OrderByDescending(p => p.FechaPuja))
                    .ThenInclude(p => p.Usuario)
                .FirstOrDefaultAsync(s => s.Id == subastaId);
        }

        public async Task<List<SubastaPuja>> ObtenerPujasAsync(int subastaId)
        {
            return await _context.SubastasPujas
                .Include(p => p.Usuario)
                .Where(p => p.SubastaId == subastaId)
                .OrderByDescending(p => p.FechaPuja)
                .ToListAsync();
        }

        public async Task<SubastaPuja?> ObtenerPujaActualAsync(int subastaId)
        {
            return await _context.SubastasPujas
                .Include(p => p.Usuario)
                .Where(p => p.SubastaId == subastaId)
                .OrderByDescending(p => p.Monto)
                .FirstOrDefaultAsync();
        }

        public async Task<int> ContarSubastasActivasAsync()
        {
            var ahora = DateTime.Now;
            return await _context.Subastas
                .CountAsync(s => s.Estado == EstadoSubasta.Activa
                              && s.FechaInicio <= ahora
                              && s.FechaFin > ahora);
        }

        #endregion

        #region Operaciones

        public async Task<(bool exito, string mensaje, Subasta? subasta)> CrearSubastaAsync(
            string creadorId,
            string titulo,
            string? descripcion,
            decimal precioInicial,
            decimal incrementoMinimo,
            int duracionHoras,
            int? contenidoId = null,
            string? imagenPreview = null,
            decimal? precioCompraloYa = null,
            bool soloSuscriptores = false,
            TipoContenidoSubasta tipoContenido = TipoContenidoSubasta.AccesoExclusivo)
        {
            try
            {
                // Validaciones basicas
                if (string.IsNullOrWhiteSpace(titulo))
                    return (false, "El titulo es obligatorio", null);

                if (precioInicial < 1)
                    return (false, "El precio inicial debe ser al menos $1", null);

                if (duracionHoras < 1 || duracionHoras > 168)
                    return (false, "La duracion debe ser entre 1 y 168 horas (7 dias)", null);

                if (precioCompraloYa.HasValue && precioCompraloYa.Value <= precioInicial)
                    return (false, "El precio Compralo Ya debe ser mayor al precio inicial", null);

                // Verificar creador
                var creador = await _context.Users.FindAsync(creadorId);
                if (creador == null || !creador.EsCreador)
                    return (false, "Solo los creadores pueden crear subastas", null);

                // Si hay contenido, verificar que pertenece al creador
                if (contenidoId.HasValue)
                {
                    var contenido = await _context.Contenidos.FindAsync(contenidoId.Value);
                    if (contenido == null || contenido.UsuarioId != creadorId)
                        return (false, "El contenido no existe o no te pertenece", null);
                }

                var ahora = DateTime.Now;
                var subasta = new Subasta
                {
                    CreadorId = creadorId,
                    Titulo = titulo.Trim(),
                    Descripcion = descripcion?.Trim(),
                    TipoContenidoSubasta = tipoContenido,
                    ContenidoId = contenidoId,
                    ImagenPreview = imagenPreview,
                    PrecioInicial = precioInicial,
                    PrecioActual = precioInicial,
                    IncrementoMinimo = incrementoMinimo > 0 ? incrementoMinimo : 1.00m,
                    PrecioCompraloYa = precioCompraloYa,
                    FechaInicio = ahora,
                    FechaFin = ahora.AddHours(duracionHoras),
                    FechaCreacion = ahora,
                    Estado = EstadoSubasta.Activa,
                    SoloSuscriptores = soloSuscriptores,
                    ExtensionAutomatica = true,
                    MaximoExtensiones = 5,
                    MostrarHistorialPujas = true
                };

                _context.Subastas.Add(subasta);
                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Subasta creada: {titulo}",
                    CategoriaEvento.Contenido,
                    TipoLogEvento.Evento,
                    creadorId,
                    creador.UserName,
                    $"SubastaId: {subasta.Id}, Precio: ${precioInicial}, Duracion: {duracionHoras}h"
                );

                _logger.LogInformation("Subasta {SubastaId} creada por {CreadorId}: {Titulo}",
                    subasta.Id, creadorId, titulo);

                return (true, "Subasta creada exitosamente", subasta);
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, creadorId, null);
                _logger.LogError(ex, "Error al crear subasta para creador {CreadorId}", creadorId);
                return (false, "Error al crear la subasta. Intenta de nuevo.", null);
            }
        }

        public async Task<(bool exito, string mensaje)> RealizarPujaAsync(
            int subastaId,
            string usuarioId,
            decimal monto,
            string? ipAddress = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var subasta = await _context.Subastas
                    .Include(s => s.Creador)
                    .FirstOrDefaultAsync(s => s.Id == subastaId);

                if (subasta == null)
                    return (false, "Subasta no encontrada");

                // Validaciones
                if (!subasta.EstaActiva())
                    return (false, "Esta subasta no esta activa");

                if (subasta.CreadorId == usuarioId)
                    return (false, "No puedes pujar en tu propia subasta");

                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario == null)
                    return (false, "Usuario no encontrado");

                // Verificar si es solo suscriptores
                if (subasta.SoloSuscriptores)
                {
                    var estaSuscrito = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == usuarioId
                                    && s.CreadorId == subasta.CreadorId
                                    && s.EstaActiva);
                    if (!estaSuscrito)
                        return (false, "Solo suscriptores pueden pujar en esta subasta");
                }

                // Verificar monto minimo
                var montoMinimo = subasta.PrecioActual + subasta.IncrementoMinimo;
                if (monto < montoMinimo)
                    return (false, $"El monto minimo es ${montoMinimo:F2}");

                // Verificar saldo del usuario
                if (usuario.Saldo < monto)
                    return (false, $"Saldo insuficiente. Tu saldo: ${usuario.Saldo:F2}");

                // Marcar pujas anteriores como superadas
                var pujasAnteriores = await _context.SubastasPujas
                    .Where(p => p.SubastaId == subastaId && !p.EsSuperada)
                    .ToListAsync();

                foreach (var p in pujasAnteriores)
                {
                    p.EsSuperada = true;
                }

                // Crear nueva puja
                var puja = new SubastaPuja
                {
                    SubastaId = subastaId,
                    UsuarioId = usuarioId,
                    Monto = monto,
                    FechaPuja = DateTime.Now,
                    EsSuperada = false,
                    EsGanadora = false,
                    IpAddress = ipAddress
                };

                _context.SubastasPujas.Add(puja);

                // Actualizar subasta
                subasta.PrecioActual = monto;
                subasta.ContadorPujas++;

                // Extension automatica si estamos en los ultimos 2 minutos
                if (subasta.ExtensionAutomatica &&
                    subasta.ExtensionesRealizadas < subasta.MaximoExtensiones)
                {
                    var tiempoRestante = subasta.FechaFin - DateTime.Now;
                    if (tiempoRestante.TotalMinutes <= 2)
                    {
                        subasta.FechaFin = subasta.FechaFin.AddMinutes(5);
                        subasta.ExtensionesRealizadas++;
                        _logger.LogInformation("Subasta {SubastaId} extendida 5 minutos. Extension #{Num}",
                            subastaId, subasta.ExtensionesRealizadas);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notificar al pujador anterior (si lo hay)
                var pujadorAnterior = pujasAnteriores
                    .OrderByDescending(p => p.Monto)
                    .FirstOrDefault();

                if (pujadorAnterior != null && pujadorAnterior.UsuarioId != usuarioId)
                {
                    await _notificationService.CrearNotificacionSistemaAsync(
                        pujadorAnterior.UsuarioId,
                        "Tu puja fue superada",
                        $"Alguien pujo ${monto:F2} en \"{subasta.Titulo}\"",
                        $"/Subastas/Detalles/{subastaId}"
                    );
                }

                _logger.LogInformation("Puja realizada: Usuario {UsuarioId} pujo ${Monto} en subasta {SubastaId}",
                    usuarioId, monto, subastaId);

                return (true, "Puja realizada exitosamente");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, usuarioId, null);
                _logger.LogError(ex, "Error al realizar puja en subasta {SubastaId}", subastaId);
                return (false, "Error al procesar la puja. Intenta de nuevo.");
            }
        }

        public async Task<(bool exito, string mensaje)> RealizarCompraloYaAsync(
            int subastaId,
            string usuarioId,
            string? ipAddress = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var subasta = await _context.Subastas
                    .Include(s => s.Creador)
                    .FirstOrDefaultAsync(s => s.Id == subastaId);

                if (subasta == null)
                    return (false, "Subasta no encontrada");

                if (!subasta.EstaActiva())
                    return (false, "Esta subasta no esta activa");

                if (!subasta.PrecioCompraloYa.HasValue)
                    return (false, "Esta subasta no tiene opcion Compralo Ya");

                if (subasta.CreadorId == usuarioId)
                    return (false, "No puedes comprar tu propia subasta");

                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario == null)
                    return (false, "Usuario no encontrado");

                var precio = subasta.PrecioCompraloYa.Value;

                // Verificar saldo
                if (usuario.Saldo < precio)
                    return (false, $"Saldo insuficiente. Necesitas ${precio:F2}");

                // Marcar todas las pujas como superadas
                var pujasAnteriores = await _context.SubastasPujas
                    .Where(p => p.SubastaId == subastaId)
                    .ToListAsync();

                foreach (var p in pujasAnteriores)
                {
                    p.EsSuperada = true;
                }

                // Crear puja de compra inmediata
                var puja = new SubastaPuja
                {
                    SubastaId = subastaId,
                    UsuarioId = usuarioId,
                    Monto = precio,
                    FechaPuja = DateTime.Now,
                    EsSuperada = false,
                    EsGanadora = true,
                    IpAddress = ipAddress
                };

                _context.SubastasPujas.Add(puja);

                // Transferir fondos
                usuario.Saldo -= precio;

                var creador = subasta.Creador;
                if (creador != null)
                {
                    // Aplicar comision de plataforma (ej: 20%)
                    var comision = precio * 0.20m;
                    var netoCreador = precio - comision;
                    creador.Saldo += netoCreador;
                    creador.TotalGanancias += netoCreador;

                    // Registrar transaccion
                    _context.Transacciones.Add(new Transaccion
                    {
                        UsuarioId = creador.Id,
                        TipoTransaccion = TipoTransaccion.VentaContenido,
                        Monto = netoCreador,
                        MontoNeto = netoCreador,
                        Comision = comision,
                        Descripcion = $"Subasta Compralo Ya: {subasta.Titulo}",
                        FechaTransaccion = DateTime.Now,
                        EstadoTransaccion = EstadoTransaccion.Completada
                    });
                }

                // Finalizar subasta
                subasta.Estado = EstadoSubasta.Finalizada;
                subasta.GanadorId = usuarioId;
                subasta.PrecioActual = precio;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notificar
                await NotificarGanadorAsync(subastaId);

                _logger.LogInformation("Compralo Ya ejecutado: Usuario {UsuarioId} compro subasta {SubastaId} por ${Precio}",
                    usuarioId, subastaId, precio);

                return (true, "Compra realizada exitosamente. Eres el ganador!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, usuarioId, null);
                _logger.LogError(ex, "Error en Compralo Ya para subasta {SubastaId}", subastaId);
                return (false, "Error al procesar la compra. Intenta de nuevo.");
            }
        }

        public async Task<(bool exito, string mensaje)> CancelarSubastaAsync(int subastaId, string creadorId)
        {
            try
            {
                var subasta = await _context.Subastas
                    .Include(s => s.Pujas)
                    .FirstOrDefaultAsync(s => s.Id == subastaId);

                if (subasta == null)
                    return (false, "Subasta no encontrada");

                if (subasta.CreadorId != creadorId)
                    return (false, "No tienes permiso para cancelar esta subasta");

                if (subasta.Estado != EstadoSubasta.Activa && subasta.Estado != EstadoSubasta.Pendiente)
                    return (false, "Solo se pueden cancelar subastas activas o pendientes");

                // Si hay pujas, no se puede cancelar
                if (subasta.Pujas.Any())
                    return (false, "No puedes cancelar una subasta con pujas activas");

                subasta.Estado = EstadoSubasta.Cancelada;
                await _context.SaveChangesAsync();

                await _logEventoService.RegistrarEventoAsync(
                    $"Subasta cancelada: {subasta.Titulo}",
                    CategoriaEvento.Contenido,
                    TipoLogEvento.Evento,
                    creadorId,
                    null,
                    $"SubastaId: {subastaId}"
                );

                return (true, "Subasta cancelada exitosamente");
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, creadorId, null);
                _logger.LogError(ex, "Error al cancelar subasta {SubastaId}", subastaId);
                return (false, "Error al cancelar la subasta");
            }
        }

        public async Task<int> FinalizarSubastasExpiradasAsync()
        {
            var ahora = DateTime.Now;
            var subastasExpiradas = await _context.Subastas
                .Include(s => s.Pujas)
                .Include(s => s.Creador)
                .Where(s => s.Estado == EstadoSubasta.Activa && s.FechaFin <= ahora)
                .ToListAsync();

            var contador = 0;

            foreach (var subasta in subastasExpiradas)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var pujaGanadora = subasta.Pujas
                        .OrderByDescending(p => p.Monto)
                        .FirstOrDefault();

                    if (pujaGanadora != null)
                    {
                        // Hay ganador
                        subasta.Estado = EstadoSubasta.Finalizada;
                        subasta.GanadorId = pujaGanadora.UsuarioId;
                        pujaGanadora.EsGanadora = true;

                        // Cobrar al ganador
                        var ganador = await _context.Users.FindAsync(pujaGanadora.UsuarioId);
                        if (ganador != null && ganador.Saldo >= pujaGanadora.Monto)
                        {
                            ganador.Saldo -= pujaGanadora.Monto;

                            // Pagar al creador (menos comision)
                            var comision = pujaGanadora.Monto * 0.20m;
                            var netoCreador = pujaGanadora.Monto - comision;

                            if (subasta.Creador != null)
                            {
                                subasta.Creador.Saldo += netoCreador;
                                subasta.Creador.TotalGanancias += netoCreador;
                            }

                            // Registrar transaccion
                            _context.Transacciones.Add(new Transaccion
                            {
                                UsuarioId = subasta.CreadorId,
                                TipoTransaccion = TipoTransaccion.VentaContenido,
                                Monto = netoCreador,
                                MontoNeto = netoCreador,
                                Comision = comision,
                                Descripcion = $"Subasta finalizada: {subasta.Titulo}",
                                FechaTransaccion = ahora,
                                EstadoTransaccion = EstadoTransaccion.Completada
                            });
                        }

                        await NotificarGanadorAsync(subasta.Id);
                    }
                    else
                    {
                        // Sin ofertas
                        subasta.Estado = EstadoSubasta.SinOfertas;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    contador++;

                    _logger.LogInformation("Subasta {SubastaId} finalizada. Estado: {Estado}, Ganador: {GanadorId}",
                        subasta.Id, subasta.Estado, subasta.GanadorId ?? "ninguno");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error al finalizar subasta {SubastaId}", subasta.Id);
                }
            }

            return contador;
        }

        public async Task NotificarGanadorAsync(int subastaId)
        {
            try
            {
                var subasta = await _context.Subastas
                    .Include(s => s.Creador)
                    .Include(s => s.Ganador)
                    .FirstOrDefaultAsync(s => s.Id == subastaId);

                if (subasta?.GanadorId == null) return;

                // Notificar al ganador
                await _notificationService.CrearNotificacionSistemaAsync(
                    subasta.GanadorId,
                    "Ganaste la subasta!",
                    $"Felicidades! Ganaste \"{subasta.Titulo}\" por ${subasta.PrecioActual:F2}",
                    $"/Subastas/Detalles/{subastaId}"
                );

                // Notificar al creador
                await _notificationService.CrearNotificacionSistemaAsync(
                    subasta.CreadorId,
                    "Tu subasta finalizo",
                    $"\"{subasta.Titulo}\" se vendio por ${subasta.PrecioActual:F2}",
                    $"/Subastas/MisSubastas"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al notificar ganador de subasta {SubastaId}", subastaId);
            }
        }

        #endregion

        #region Helpers

        public async Task<bool> PuedePujarAsync(string usuarioId, int subastaId)
        {
            var subasta = await _context.Subastas.FindAsync(subastaId);
            if (subasta == null) return false;

            if (!subasta.EstaActiva()) return false;
            if (subasta.CreadorId == usuarioId) return false;

            if (subasta.SoloSuscriptores)
            {
                return await _context.Suscripciones
                    .AnyAsync(s => s.FanId == usuarioId
                                && s.CreadorId == subasta.CreadorId
                                && s.EstaActiva);
            }

            return true;
        }

        public async Task IncrementarVistasAsync(int subastaId)
        {
            await _context.Subastas
                .Where(s => s.Id == subastaId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NumeroVistas, x => x.NumeroVistas + 1));
        }

        #endregion
    }
}
