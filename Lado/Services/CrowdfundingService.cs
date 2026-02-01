using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface ICrowdfundingService
    {
        // Consultas
        Task<List<CampanaCrowdfunding>> ObtenerCampanasActivasAsync(int pagina = 1, int porPagina = 20, string? categoria = null);
        Task<List<CampanaCrowdfunding>> ObtenerCampanasDelCreadorAsync(string creadorId);
        Task<CampanaCrowdfunding?> ObtenerCampanaAsync(int id);
        Task<List<AporteCrowdfunding>> ObtenerAportantesAsync(int campanaId, int limite = 50);
        Task<AporteCrowdfunding?> ObtenerAporteUsuarioAsync(int campanaId, string usuarioId);
        Task<bool> UsuarioYaAportoAsync(int campanaId, string usuarioId);
        Task<decimal> ObtenerTotalAportadoPorUsuarioAsync(string usuarioId);
        Task<List<CampanaCrowdfunding>> ObtenerCampanasAportadasAsync(string usuarioId);

        // Operaciones de campana
        Task<(bool exito, string mensaje, int? campanaId)> CrearCampanaAsync(CampanaCrowdfunding campana);
        Task<(bool exito, string mensaje)> ActualizarCampanaAsync(CampanaCrowdfunding campana);
        Task<(bool exito, string mensaje)> PublicarCampanaAsync(int campanaId, string creadorId);
        Task<(bool exito, string mensaje)> CancelarCampanaAsync(int campanaId, string creadorId);

        // Aportes
        Task<(bool exito, string mensaje)> RealizarAporteAsync(int campanaId, string aportanteId, decimal monto, string? mensaje = null, bool esAnonimo = false);
        Task<decimal> CalcularProgresoAsync(int campanaId);

        // Finalizacion
        Task<(bool exito, string mensaje)> FinalizarCampanaExitosaAsync(int campanaId, int contenidoId, string? mensajeAgradecimiento = null);
        Task ProcesarCampanasExpiradasAsync();
        Task<(bool exito, string mensaje)> ProcesarCampanaFallidaAsync(int campanaId);

        // Estadisticas
        Task<CrowdfundingEstadisticas> ObtenerEstadisticasCreadorAsync(string creadorId);
        Task IncrementarVistasAsync(int campanaId);
    }

    public class CrowdfundingEstadisticas
    {
        public int TotalCampanas { get; set; }
        public int CampanasActivas { get; set; }
        public int CampanasExitosas { get; set; }
        public int CampanasFallidas { get; set; }
        public decimal TotalRecaudado { get; set; }
        public int TotalAportantes { get; set; }
        public decimal PromedioRecaudado { get; set; }
        public decimal TasaExito { get; set; }
    }

    public class CrowdfundingService : ICrowdfundingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CrowdfundingService> _logger;
        private readonly ILogEventoService _logEventoService;
        private readonly INotificationService? _notificacionService;

        public CrowdfundingService(
            ApplicationDbContext context,
            ILogger<CrowdfundingService> logger,
            ILogEventoService logEventoService,
            INotificationService? notificacionService = null)
        {
            _context = context;
            _logger = logger;
            _logEventoService = logEventoService;
            _notificacionService = notificacionService;
        }

        #region Consultas

        public async Task<List<CampanaCrowdfunding>> ObtenerCampanasActivasAsync(int pagina = 1, int porPagina = 20, string? categoria = null)
        {
            var query = _context.CampanasCrowdfunding
                .Include(c => c.Creador)
                .Where(c => c.Estado == EstadoCampanaCrowdfunding.Activa && c.EsVisible)
                .Where(c => c.FechaLimite > DateTime.Now);

            if (!string.IsNullOrEmpty(categoria))
            {
                query = query.Where(c => c.Categoria == categoria);
            }

            return await query
                .OrderByDescending(c => c.TotalRecaudado / c.Meta) // Ordenar por progreso
                .ThenByDescending(c => c.TotalAportantes)
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();
        }

        public async Task<List<CampanaCrowdfunding>> ObtenerCampanasDelCreadorAsync(string creadorId)
        {
            return await _context.CampanasCrowdfunding
                .Include(c => c.Creador)
                .Where(c => c.CreadorId == creadorId)
                .OrderByDescending(c => c.FechaCreacion)
                .ToListAsync();
        }

        public async Task<CampanaCrowdfunding?> ObtenerCampanaAsync(int id)
        {
            return await _context.CampanasCrowdfunding
                .Include(c => c.Creador)
                .Include(c => c.Aportes.OrderByDescending(a => a.Monto).Take(10))
                    .ThenInclude(a => a.Aportante)
                .Include(c => c.ContenidoEntregado)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<AporteCrowdfunding>> ObtenerAportantesAsync(int campanaId, int limite = 50)
        {
            return await _context.AportesCrowdfunding
                .Include(a => a.Aportante)
                .Where(a => a.CampanaId == campanaId && a.Estado == EstadoAporte.Confirmado)
                .OrderByDescending(a => a.Monto)
                .Take(limite)
                .ToListAsync();
        }

        public async Task<AporteCrowdfunding?> ObtenerAporteUsuarioAsync(int campanaId, string usuarioId)
        {
            return await _context.AportesCrowdfunding
                .FirstOrDefaultAsync(a => a.CampanaId == campanaId && a.AportanteId == usuarioId && a.Estado == EstadoAporte.Confirmado);
        }

        public async Task<bool> UsuarioYaAportoAsync(int campanaId, string usuarioId)
        {
            return await _context.AportesCrowdfunding
                .AnyAsync(a => a.CampanaId == campanaId && a.AportanteId == usuarioId && a.Estado == EstadoAporte.Confirmado);
        }

        public async Task<decimal> ObtenerTotalAportadoPorUsuarioAsync(string usuarioId)
        {
            return await _context.AportesCrowdfunding
                .Where(a => a.AportanteId == usuarioId && a.Estado == EstadoAporte.Confirmado)
                .SumAsync(a => a.Monto);
        }

        public async Task<List<CampanaCrowdfunding>> ObtenerCampanasAportadasAsync(string usuarioId)
        {
            var campanaIds = await _context.AportesCrowdfunding
                .Where(a => a.AportanteId == usuarioId && a.Estado == EstadoAporte.Confirmado)
                .Select(a => a.CampanaId)
                .Distinct()
                .ToListAsync();

            return await _context.CampanasCrowdfunding
                .Include(c => c.Creador)
                .Where(c => campanaIds.Contains(c.Id))
                .OrderByDescending(c => c.FechaCreacion)
                .ToListAsync();
        }

        #endregion

        #region Operaciones de Campana

        public async Task<(bool exito, string mensaje, int? campanaId)> CrearCampanaAsync(CampanaCrowdfunding campana)
        {
            try
            {
                // Validaciones
                if (campana.Meta < 10)
                {
                    return (false, "La meta minima es de $10", null);
                }

                if (campana.FechaLimite <= DateTime.Now.AddDays(1))
                {
                    return (false, "La fecha limite debe ser al menos 1 dia en el futuro", null);
                }

                if (campana.FechaLimite > DateTime.Now.AddDays(90))
                {
                    return (false, "La fecha limite no puede ser mayor a 90 dias", null);
                }

                // Verificar que el creador existe y es creador verificado
                var creador = await _context.Users.FindAsync(campana.CreadorId);
                if (creador == null || !creador.EsCreador)
                {
                    return (false, "Solo los creadores pueden crear campanas", null);
                }

                campana.Estado = EstadoCampanaCrowdfunding.Borrador;
                campana.FechaCreacion = DateTime.Now;
                campana.TotalRecaudado = 0;
                campana.TotalAportantes = 0;

                _context.CampanasCrowdfunding.Add(campana);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Campana de crowdfunding creada: {CampanaId} por creador {CreadorId}", campana.Id, campana.CreadorId);

                return (true, "Campana creada exitosamente", campana.Id);
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, campana.CreadorId, null);
                return (false, "Error al crear la campana", null);
            }
        }

        public async Task<(bool exito, string mensaje)> ActualizarCampanaAsync(CampanaCrowdfunding campana)
        {
            try
            {
                var campanaExistente = await _context.CampanasCrowdfunding.FindAsync(campana.Id);
                if (campanaExistente == null)
                {
                    return (false, "Campana no encontrada");
                }

                // Solo se puede editar si esta en borrador
                if (campanaExistente.Estado != EstadoCampanaCrowdfunding.Borrador)
                {
                    return (false, "Solo se pueden editar campanas en borrador");
                }

                campanaExistente.Titulo = campana.Titulo;
                campanaExistente.Descripcion = campana.Descripcion;
                campanaExistente.Meta = campana.Meta;
                campanaExistente.AporteMinimo = campana.AporteMinimo;
                campanaExistente.FechaLimite = campana.FechaLimite;
                campanaExistente.ImagenPreview = campana.ImagenPreview;
                campanaExistente.VideoPreview = campana.VideoPreview;
                campanaExistente.TipoLado = campana.TipoLado;
                campanaExistente.Categoria = campana.Categoria;
                campanaExistente.Tags = campana.Tags;

                await _context.SaveChangesAsync();

                return (true, "Campana actualizada");
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, campana.CreadorId, null);
                return (false, "Error al actualizar la campana");
            }
        }

        public async Task<(bool exito, string mensaje)> PublicarCampanaAsync(int campanaId, string creadorId)
        {
            try
            {
                var campana = await _context.CampanasCrowdfunding.FindAsync(campanaId);
                if (campana == null)
                {
                    return (false, "Campana no encontrada");
                }

                if (campana.CreadorId != creadorId)
                {
                    return (false, "No tienes permiso para publicar esta campana");
                }

                if (campana.Estado != EstadoCampanaCrowdfunding.Borrador)
                {
                    return (false, "La campana ya fue publicada");
                }

                // Validar que tenga todos los campos requeridos
                if (string.IsNullOrEmpty(campana.Titulo) || string.IsNullOrEmpty(campana.Descripcion))
                {
                    return (false, "La campana debe tener titulo y descripcion");
                }

                if (campana.FechaLimite <= DateTime.Now)
                {
                    return (false, "La fecha limite debe ser en el futuro");
                }

                campana.Estado = EstadoCampanaCrowdfunding.Activa;
                campana.FechaPublicacion = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Campana {CampanaId} publicada", campanaId);

                return (true, "Campana publicada exitosamente");
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, creadorId, null);
                return (false, "Error al publicar la campana");
            }
        }

        public async Task<(bool exito, string mensaje)> CancelarCampanaAsync(int campanaId, string creadorId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var campana = await _context.CampanasCrowdfunding
                    .Include(c => c.Aportes)
                    .FirstOrDefaultAsync(c => c.Id == campanaId);

                if (campana == null)
                {
                    return (false, "Campana no encontrada");
                }

                if (campana.CreadorId != creadorId)
                {
                    return (false, "No tienes permiso para cancelar esta campana");
                }

                if (campana.Estado == EstadoCampanaCrowdfunding.Exitosa || campana.Estado == EstadoCampanaCrowdfunding.Fallida)
                {
                    return (false, "La campana ya fue finalizada");
                }

                // Devolver todos los aportes
                foreach (var aporte in campana.Aportes.Where(a => a.Estado == EstadoAporte.Confirmado))
                {
                    var aportante = await _context.Users.FindAsync(aporte.AportanteId);
                    if (aportante != null)
                    {
                        aportante.Saldo += aporte.Monto;

                        // Registrar transaccion de devolucion
                        var transaccionDevolucion = new Transaccion
                        {
                            UsuarioId = aporte.AportanteId,
                            TipoTransaccion = TipoTransaccion.Reembolso,
                            Monto = aporte.Monto,
                            Descripcion = $"Devolucion por cancelacion de campana: {campana.Titulo}",
                            FechaTransaccion = DateTime.Now,
                            EstadoTransaccion = EstadoTransaccion.Completada
                        };
                        _context.Transacciones.Add(transaccionDevolucion);

                        aporte.Estado = EstadoAporte.Devuelto;
                        aporte.FechaDevolucion = DateTime.Now;
                    }
                }

                campana.Estado = EstadoCampanaCrowdfunding.Cancelada;
                campana.FechaFinalizacion = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Campana {CampanaId} cancelada, {NumAportes} aportes devueltos", campanaId, campana.Aportes.Count);

                return (true, "Campana cancelada y aportes devueltos");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, creadorId, null);
                return (false, "Error al cancelar la campana");
            }
        }

        #endregion

        #region Aportes

        public async Task<(bool exito, string mensaje)> RealizarAporteAsync(int campanaId, string aportanteId, decimal monto, string? mensaje = null, bool esAnonimo = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var campana = await _context.CampanasCrowdfunding.FindAsync(campanaId);
                if (campana == null)
                {
                    return (false, "Campana no encontrada");
                }

                if (!campana.PuedeRecibAportes)
                {
                    return (false, "Esta campana ya no acepta aportes");
                }

                if (monto < campana.AporteMinimo)
                {
                    return (false, $"El aporte minimo es de ${campana.AporteMinimo}");
                }

                // No puede aportar a su propia campana
                if (campana.CreadorId == aportanteId)
                {
                    return (false, "No puedes aportar a tu propia campana");
                }

                // Verificar saldo del aportante
                var aportante = await _context.Users.FindAsync(aportanteId);
                if (aportante == null)
                {
                    return (false, "Usuario no encontrado");
                }

                if (aportante.Saldo < monto)
                {
                    return (false, $"Saldo insuficiente. Tu saldo es ${aportante.Saldo:F2}");
                }

                // Verificar si ya aporto (puede aportar multiples veces)
                var aporteExistente = await _context.AportesCrowdfunding
                    .Where(a => a.CampanaId == campanaId && a.AportanteId == aportanteId && a.Estado == EstadoAporte.Confirmado)
                    .FirstOrDefaultAsync();

                // Descontar saldo
                aportante.Saldo -= monto;

                // Registrar transaccion
                var transaccion = new Transaccion
                {
                    UsuarioId = aportanteId,
                    TipoTransaccion = TipoTransaccion.CompraContenido,
                    Monto = monto,
                    Descripcion = $"Aporte a campana: {campana.Titulo}",
                    FechaTransaccion = DateTime.Now,
                    EstadoTransaccion = EstadoTransaccion.Completada
                };
                _context.Transacciones.Add(transaccion);
                await _context.SaveChangesAsync();

                // Crear aporte
                var aporte = new AporteCrowdfunding
                {
                    CampanaId = campanaId,
                    AportanteId = aportanteId,
                    Monto = monto,
                    Estado = EstadoAporte.Confirmado,
                    FechaAporte = DateTime.Now,
                    Mensaje = mensaje,
                    EsAnonimo = esAnonimo,
                    TransaccionId = transaccion.Id
                };
                _context.AportesCrowdfunding.Add(aporte);

                // Actualizar totales de la campana
                campana.TotalRecaudado += monto;
                if (aporteExistente == null)
                {
                    campana.TotalAportantes++;
                }

                // Verificar si se alcanzo la meta
                if (campana.TotalRecaudado >= campana.Meta && campana.Estado == EstadoCampanaCrowdfunding.Activa)
                {
                    campana.Estado = EstadoCampanaCrowdfunding.MetaAlcanzada;

                    // Notificar al creador
                    if (_notificacionService != null)
                    {
                        await _notificacionService.CrearNotificacionSistemaAsync(
                            campana.CreadorId,
                            $"Tu campana '{campana.Titulo}' alcanzo la meta de ${campana.Meta:F2}. Ahora puedes entregar el contenido prometido.",
                            "/Crowdfunding/MisCampanas"
                        );
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Aporte de ${Monto} a campana {CampanaId} por usuario {UsuarioId}", monto, campanaId, aportanteId);

                // Notificar al creador del aporte
                if (_notificacionService != null && !esAnonimo)
                {
                    await _notificacionService.CrearNotificacionSistemaAsync(
                        campana.CreadorId,
                        $"{aportante.NombreCompleto} aporto ${monto:F2} a tu campana '{campana.Titulo}'",
                        $"/Crowdfunding/Detalles/{campanaId}"
                    );
                }

                return (true, "Aporte realizado exitosamente");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, aportanteId, null);
                return (false, "Error al realizar el aporte");
            }
        }

        public async Task<decimal> CalcularProgresoAsync(int campanaId)
        {
            var campana = await _context.CampanasCrowdfunding.FindAsync(campanaId);
            if (campana == null || campana.Meta == 0) return 0;

            return Math.Min(100, (campana.TotalRecaudado / campana.Meta) * 100);
        }

        #endregion

        #region Finalizacion

        public async Task<(bool exito, string mensaje)> FinalizarCampanaExitosaAsync(int campanaId, int contenidoId, string? mensajeAgradecimiento = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var campana = await _context.CampanasCrowdfunding
                    .Include(c => c.Aportes)
                        .ThenInclude(a => a.Aportante)
                    .FirstOrDefaultAsync(c => c.Id == campanaId);

                if (campana == null)
                {
                    return (false, "Campana no encontrada");
                }

                if (campana.Estado != EstadoCampanaCrowdfunding.MetaAlcanzada && campana.Estado != EstadoCampanaCrowdfunding.Activa)
                {
                    return (false, "La campana no esta en estado valido para finalizar");
                }

                // Verificar que el contenido existe
                var contenido = await _context.Contenidos.FindAsync(contenidoId);
                if (contenido == null)
                {
                    return (false, "Contenido no encontrado");
                }

                if (contenido.UsuarioId != campana.CreadorId)
                {
                    return (false, "El contenido debe ser del creador de la campana");
                }

                // Transferir fondos al creador (descontando comision del 20%)
                var creador = await _context.Users.FindAsync(campana.CreadorId);
                if (creador != null)
                {
                    var comision = campana.TotalRecaudado * 0.20m;
                    var montoNeto = campana.TotalRecaudado - comision;

                    creador.Saldo += montoNeto;
                    creador.TotalGanancias += montoNeto;

                    // Registrar transaccion del creador
                    var transaccionCreador = new Transaccion
                    {
                        UsuarioId = campana.CreadorId,
                        TipoTransaccion = TipoTransaccion.VentaContenido,
                        Monto = campana.TotalRecaudado,
                        MontoNeto = montoNeto,
                        Comision = comision,
                        Descripcion = $"Fondos de campana exitosa: {campana.Titulo}",
                        FechaTransaccion = DateTime.Now,
                        EstadoTransaccion = EstadoTransaccion.Completada
                    };
                    _context.Transacciones.Add(transaccionCreador);
                }

                // Dar acceso a todos los aportantes
                foreach (var aporte in campana.Aportes.Where(a => a.Estado == EstadoAporte.Confirmado))
                {
                    // Registrar compra del contenido
                    var compraExistente = await _context.ComprasContenido
                        .AnyAsync(c => c.ContenidoId == contenidoId && c.UsuarioId == aporte.AportanteId);

                    if (!compraExistente)
                    {
                        var compra = new CompraContenido
                        {
                            ContenidoId = contenidoId,
                            UsuarioId = aporte.AportanteId,
                            Monto = 0, // Ya pagaron via crowdfunding
                            FechaCompra = DateTime.Now
                        };
                        _context.ComprasContenido.Add(compra);
                    }

                    aporte.AccesoOtorgado = true;
                    aporte.FechaAcceso = DateTime.Now;

                    // Notificar al aportante
                    if (_notificacionService != null && aporte.Aportante != null)
                    {
                        await _notificacionService.CrearNotificacionSistemaAsync(
                            aporte.AportanteId,
                            $"La campana '{campana.Titulo}' ha sido completada. Ya puedes ver el contenido exclusivo.",
                            $"/Feed/Detalle/{contenidoId}"
                        );
                    }
                }

                campana.Estado = EstadoCampanaCrowdfunding.Exitosa;
                campana.ContenidoEntregadoId = contenidoId;
                campana.MensajeAgradecimiento = mensajeAgradecimiento;
                campana.FechaFinalizacion = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Campana {CampanaId} finalizada exitosamente, {NumAportantes} aportantes recibieron acceso", campanaId, campana.TotalAportantes);

                return (true, "Campana finalizada exitosamente. Todos los aportantes ahora tienen acceso al contenido.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, null, null);
                return (false, "Error al finalizar la campana");
            }
        }

        public async Task ProcesarCampanasExpiradasAsync()
        {
            try
            {
                var campanasExpiradas = await _context.CampanasCrowdfunding
                    .Where(c => c.Estado == EstadoCampanaCrowdfunding.Activa)
                    .Where(c => c.FechaLimite < DateTime.Now)
                    .Where(c => c.TotalRecaudado < c.Meta)
                    .ToListAsync();

                foreach (var campana in campanasExpiradas)
                {
                    var resultado = await ProcesarCampanaFallidaAsync(campana.Id);
                    _logger.LogInformation("Campana expirada {CampanaId} procesada: {Resultado}", campana.Id, resultado.mensaje);
                }

                _logger.LogInformation("Procesadas {Cantidad} campanas expiradas", campanasExpiradas.Count);
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, null, null);
            }
        }

        public async Task<(bool exito, string mensaje)> ProcesarCampanaFallidaAsync(int campanaId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var campana = await _context.CampanasCrowdfunding
                    .Include(c => c.Aportes)
                        .ThenInclude(a => a.Aportante)
                    .FirstOrDefaultAsync(c => c.Id == campanaId);

                if (campana == null)
                {
                    return (false, "Campana no encontrada");
                }

                // Devolver todos los aportes
                foreach (var aporte in campana.Aportes.Where(a => a.Estado == EstadoAporte.Confirmado))
                {
                    if (aporte.Aportante != null)
                    {
                        aporte.Aportante.Saldo += aporte.Monto;

                        // Registrar transaccion de devolucion
                        var transaccionDevolucion = new Transaccion
                        {
                            UsuarioId = aporte.AportanteId,
                            TipoTransaccion = TipoTransaccion.Reembolso,
                            Monto = aporte.Monto,
                            Descripcion = $"Devolucion - campana no alcanzo meta: {campana.Titulo}",
                            FechaTransaccion = DateTime.Now,
                            EstadoTransaccion = EstadoTransaccion.Completada
                        };
                        _context.Transacciones.Add(transaccionDevolucion);
                        await _context.SaveChangesAsync();

                        aporte.Estado = EstadoAporte.Devuelto;
                        aporte.FechaDevolucion = DateTime.Now;
                        aporte.TransaccionDevolucionId = transaccionDevolucion.Id;

                        // Notificar al aportante
                        if (_notificacionService != null)
                        {
                            await _notificacionService.CrearNotificacionSistemaAsync(
                                aporte.AportanteId,
                                $"La campana '{campana.Titulo}' no alcanzo su meta. Te devolvimos ${aporte.Monto:F2} a tu saldo.",
                                "/Billetera"
                            );
                        }
                    }
                }

                campana.Estado = EstadoCampanaCrowdfunding.Fallida;
                campana.FechaFinalizacion = DateTime.Now;

                // Notificar al creador
                if (_notificacionService != null)
                {
                    await _notificacionService.CrearNotificacionSistemaAsync(
                        campana.CreadorId,
                        $"Tu campana '{campana.Titulo}' no alcanzo la meta. Los aportes han sido devueltos a los aportantes.",
                        "/Crowdfunding/MisCampanas"
                    );
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Campana fallida {CampanaId}: ${Recaudado} de ${Meta}, {NumAportes} aportes devueltos",
                    campanaId, campana.TotalRecaudado, campana.Meta, campana.Aportes.Count);

                return (true, "Campana procesada como fallida, aportes devueltos");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, null, null);
                return (false, "Error al procesar campana fallida");
            }
        }

        #endregion

        #region Estadisticas

        public async Task<CrowdfundingEstadisticas> ObtenerEstadisticasCreadorAsync(string creadorId)
        {
            var campanas = await _context.CampanasCrowdfunding
                .Where(c => c.CreadorId == creadorId)
                .ToListAsync();

            var exitosas = campanas.Count(c => c.Estado == EstadoCampanaCrowdfunding.Exitosa);
            var fallidas = campanas.Count(c => c.Estado == EstadoCampanaCrowdfunding.Fallida);
            var finalizadas = exitosas + fallidas;

            return new CrowdfundingEstadisticas
            {
                TotalCampanas = campanas.Count,
                CampanasActivas = campanas.Count(c => c.Estado == EstadoCampanaCrowdfunding.Activa || c.Estado == EstadoCampanaCrowdfunding.MetaAlcanzada),
                CampanasExitosas = exitosas,
                CampanasFallidas = fallidas,
                TotalRecaudado = campanas.Where(c => c.Estado == EstadoCampanaCrowdfunding.Exitosa).Sum(c => c.TotalRecaudado),
                TotalAportantes = campanas.Sum(c => c.TotalAportantes),
                PromedioRecaudado = exitosas > 0 ? campanas.Where(c => c.Estado == EstadoCampanaCrowdfunding.Exitosa).Average(c => c.TotalRecaudado) : 0,
                TasaExito = finalizadas > 0 ? (decimal)exitosas / finalizadas * 100 : 0
            };
        }

        public async Task IncrementarVistasAsync(int campanaId)
        {
            var campana = await _context.CampanasCrowdfunding.FindAsync(campanaId);
            if (campana != null)
            {
                campana.Vistas++;
                await _context.SaveChangesAsync();
            }
        }

        #endregion
    }
}
