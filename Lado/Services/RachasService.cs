using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface IRachasService
    {
        // Obtener/Crear
        Task<RachaUsuario> ObtenerOCrearRachaAsync(string usuarioId);

        // Registrar actividades
        Task<bool> RegistrarLoginAsync(string usuarioId);
        Task<bool> RegistrarLikeAsync(string usuarioId);
        Task<bool> RegistrarComentarioAsync(string usuarioId);
        Task<bool> RegistrarContenidoAsync(string usuarioId);

        // Consultas
        Task<int> ObtenerRachaActualAsync(string usuarioId);
        Task<int> ObtenerRachaMaximaAsync(string usuarioId);
        Task<(int likes, int comentarios, int contenidos)> ObtenerContadoresHoyAsync(string usuarioId);

        // Reset diario
        Task ResetearContadoresDiariosAsync();
    }

    public class RachasService : IRachasService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly IReferidosService _referidosService;
        private readonly ILogger<RachasService> _logger;
        private readonly ILogEventoService _logEventoService;
        private readonly INotificationService? _notificacionService;
        private readonly IDateTimeService _dateTimeService;

        public RachasService(
            ApplicationDbContext context,
            ILadoCoinsService ladoCoinsService,
            IReferidosService referidosService,
            ILogger<RachasService> logger,
            ILogEventoService logEventoService,
            IDateTimeService dateTimeService,
            INotificationService? notificacionService = null)
        {
            _context = context;
            _ladoCoinsService = ladoCoinsService;
            _referidosService = referidosService;
            _logger = logger;
            _logEventoService = logEventoService;
            _dateTimeService = dateTimeService;
            _notificacionService = notificacionService;
        }

        #region Obtener/Crear

        public async Task<RachaUsuario> ObtenerOCrearRachaAsync(string usuarioId)
        {
            // Usar la zona horaria del usuario específico (no la global de plataforma)
            var fechaLocalHoy = (await _dateTimeService.GetUserLocalNowAsync(usuarioId)).Date;

            var racha = await _context.RachasUsuarios
                .FirstOrDefaultAsync(r => r.UsuarioId == usuarioId);

            if (racha == null)
            {
                racha = new RachaUsuario
                {
                    UsuarioId = usuarioId,
                    RachaActual = 0,
                    RachaMaxima = 0,
                    FechaReset = fechaLocalHoy,
                    FechaCreacion = await _dateTimeService.GetUserLocalNowAsync(usuarioId)
                };

                _context.RachasUsuarios.Add(racha);
                await _context.SaveChangesAsync();
                _logger.LogInformation("🆕 Racha creada: Usuario={UsuarioId}, FechaReset={FechaReset}, ZonaHoraria={ZonaHoraria}",
                    usuarioId, racha.FechaReset, await _dateTimeService.GetUserTimeZoneIdAsync(usuarioId));
            }

            // Verificar si necesita reset (usando hora local del usuario)
            if (racha.FechaReset.Date < fechaLocalHoy)
            {
                _logger.LogInformation("🔄 Reseteando racha: Usuario={UsuarioId}, FechaReset={FechaReset}, FechaLocalHoy={FechaLocalHoy}",
                    usuarioId, racha.FechaReset, fechaLocalHoy);
                racha.ResetearContadores(fechaLocalHoy);
                await _context.SaveChangesAsync();
            }

            return racha;
        }

        #endregion

        #region Registrar Actividades

        public async Task<bool> RegistrarLoginAsync(string usuarioId)
        {
            try
            {
                var racha = await ObtenerOCrearRachaAsync(usuarioId);

                // Si ya recibió premio hoy, no hacer nada
                if (racha.PremioLoginHoy) return false;

                // Verificar si la racha sigue activa (48 horas desde último login)
                var horaLocal = await _dateTimeService.GetUserLocalNowAsync(usuarioId);
                var rachaEstabaContinua = racha.UltimoLoginPremio.HasValue &&
                    (horaLocal - racha.UltimoLoginPremio.Value).TotalHours <= 48;

                // Actualizar racha
                if (rachaEstabaContinua)
                {
                    racha.RachaActual++;
                }
                else
                {
                    // Reiniciar racha
                    racha.RachaActual = 1;
                }

                // Actualizar máxima si es necesario
                if (racha.RachaActual > racha.RachaMaxima)
                {
                    racha.RachaMaxima = racha.RachaActual;
                }

                racha.UltimoLoginPremio = await _dateTimeService.GetUserLocalNowAsync(usuarioId);
                racha.PremioLoginHoy = true;

                await _context.SaveChangesAsync();

                // Dar bono de login diario ($0.50)
                var bonoLoginEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                    usuarioId,
                    TipoTransaccionLadoCoin.BonoLoginDiario,
                    $"Login diario - Día {racha.RachaActual} de racha"
                );

                // Procesar comisión para referidor
                if (bonoLoginEntregado)
                {
                    var montoBono = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_LOGIN_DIARIO);
                    await _referidosService.ProcesarComisionAsync(usuarioId, montoBono);
                }

                // Verificar bono de racha 7 días
                if (racha.RachaActual == 7)
                {
                    var bonoRachaEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                        usuarioId,
                        TipoTransaccionLadoCoin.BonoRacha7Dias,
                        "¡Racha de 7 días consecutivos!"
                    );

                    if (bonoRachaEntregado && _notificacionService != null)
                    {
                        await _notificacionService.CrearNotificacionSistemaAsync(
                            usuarioId,
                            "¡Racha de 7 días!",
                            "Has logrado 7 días consecutivos. ¡Ganaste un bono extra!",
                            "/LadoCoins"
                        );
                    }

                    // Comisión del bono de racha
                    if (bonoRachaEntregado)
                    {
                        var montoBono = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_RACHA_7_DIAS);
                        await _referidosService.ProcesarComisionAsync(usuarioId, montoBono);
                    }
                }

                _logger.LogInformation("Login registrado para {UsuarioId}, racha actual: {Racha}", usuarioId, racha.RachaActual);

                return true;
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Usuario, usuarioId, null);
                return false;
            }
        }

        public async Task<bool> RegistrarLikeAsync(string usuarioId)
        {
            try
            {
                var zonaHoraria = await _dateTimeService.GetUserTimeZoneIdAsync(usuarioId);
                var fechaLocalUsuario = await _dateTimeService.GetUserLocalNowAsync(usuarioId);

                var racha = await ObtenerOCrearRachaAsync(usuarioId);
                var likesAntes = racha.LikesHoy;

                racha.LikesHoy++;
                await _context.SaveChangesAsync();

                // Registrar en /Admin/Logs para diagnóstico
                await _logEventoService.RegistrarEventoAsync(
                    $"Like registrado: {likesAntes}→{racha.LikesHoy}",
                    CategoriaEvento.Usuario,
                    TipoLogEvento.Evento,
                    usuarioId,
                    null,
                    $"ZonaHoraria: {zonaHoraria}, FechaLocal: {fechaLocalUsuario:yyyy-MM-dd HH:mm:ss}, FechaReset: {racha.FechaReset:yyyy-MM-dd}"
                );

                // Verificar si alcanzó 5 likes y no ha recibido premio
                if (racha.LikesHoy >= 5 && !racha.Premio5LikesHoy)
                {
                    racha.Premio5LikesHoy = true;
                    await _context.SaveChangesAsync();

                    var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                        usuarioId,
                        TipoTransaccionLadoCoin.BonoDarLikes,
                        "Bono por dar 5 likes hoy"
                    );

                    if (bonoEntregado)
                    {
                        var montoBono = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_5_LIKES);
                        await _referidosService.ProcesarComisionAsync(usuarioId, montoBono);
                    }

                    await _logEventoService.RegistrarEventoAsync(
                        "Bono 5 likes entregado",
                        CategoriaEvento.Usuario,
                        TipoLogEvento.Evento,
                        usuarioId,
                        null,
                        $"LikesHoy: {racha.LikesHoy}"
                    );
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Usuario, usuarioId, null);
                return false;
            }
        }

        public async Task<bool> RegistrarComentarioAsync(string usuarioId)
        {
            try
            {
                var zonaHoraria = await _dateTimeService.GetUserTimeZoneIdAsync(usuarioId);
                var fechaLocalUsuario = await _dateTimeService.GetUserLocalNowAsync(usuarioId);

                var racha = await ObtenerOCrearRachaAsync(usuarioId);
                var comentariosAntes = racha.ComentariosHoy;

                racha.ComentariosHoy++;
                await _context.SaveChangesAsync();

                // Registrar en /Admin/Logs para diagnóstico
                await _logEventoService.RegistrarEventoAsync(
                    $"Comentario registrado: {comentariosAntes}→{racha.ComentariosHoy}",
                    CategoriaEvento.Usuario,
                    TipoLogEvento.Evento,
                    usuarioId,
                    null,
                    $"ZonaHoraria: {zonaHoraria}, FechaLocal: {fechaLocalUsuario:yyyy-MM-dd HH:mm:ss}, FechaReset: {racha.FechaReset:yyyy-MM-dd}"
                );

                // Verificar si alcanzó 3 comentarios y no ha recibido premio
                if (racha.ComentariosHoy >= 3 && !racha.Premio3ComentariosHoy)
                {
                    racha.Premio3ComentariosHoy = true;
                    await _context.SaveChangesAsync();

                    var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                        usuarioId,
                        TipoTransaccionLadoCoin.BonoComentar,
                        "Bono por hacer 3 comentarios hoy"
                    );

                    if (bonoEntregado)
                    {
                        var montoBono = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_3_COMENTARIOS);
                        await _referidosService.ProcesarComisionAsync(usuarioId, montoBono);
                    }

                    await _logEventoService.RegistrarEventoAsync(
                        "Bono 3 comentarios entregado",
                        CategoriaEvento.Usuario,
                        TipoLogEvento.Evento,
                        usuarioId,
                        null,
                        $"ComentariosHoy: {racha.ComentariosHoy}"
                    );
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Usuario, usuarioId, null);
                return false;
            }
        }

        public async Task<bool> RegistrarContenidoAsync(string usuarioId)
        {
            try
            {
                var zonaHoraria = await _dateTimeService.GetUserTimeZoneIdAsync(usuarioId);
                var fechaLocalUsuario = await _dateTimeService.GetUserLocalNowAsync(usuarioId);

                var racha = await ObtenerOCrearRachaAsync(usuarioId);
                var contenidosAntes = racha.ContenidosHoy;

                racha.ContenidosHoy++;
                await _context.SaveChangesAsync();

                // Registrar en /Admin/Logs para diagnóstico
                await _logEventoService.RegistrarEventoAsync(
                    $"Contenido registrado: {contenidosAntes}→{racha.ContenidosHoy}",
                    CategoriaEvento.Contenido,
                    TipoLogEvento.Evento,
                    usuarioId,
                    null,
                    $"ZonaHoraria: {zonaHoraria}, FechaLocal: {fechaLocalUsuario:yyyy-MM-dd HH:mm:ss}, FechaReset: {racha.FechaReset:yyyy-MM-dd}"
                );

                // Verificar si es el primer contenido del día y no ha recibido premio
                if (racha.ContenidosHoy == 1 && !racha.PremioContenidoHoy)
                {
                    racha.PremioContenidoHoy = true;
                    await _context.SaveChangesAsync();

                    var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                        usuarioId,
                        TipoTransaccionLadoCoin.BonoSubirContenido,
                        "Bono por subir contenido hoy"
                    );

                    if (bonoEntregado)
                    {
                        var montoBono = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_CONTENIDO_DIARIO);
                        await _referidosService.ProcesarComisionAsync(usuarioId, montoBono);
                    }

                    await _logEventoService.RegistrarEventoAsync(
                        "Bono contenido diario entregado",
                        CategoriaEvento.Contenido,
                        TipoLogEvento.Evento,
                        usuarioId,
                        null,
                        $"ContenidosHoy: {racha.ContenidosHoy}"
                    );
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Usuario, usuarioId, null);
                return false;
            }
        }

        #endregion

        #region Consultas

        public async Task<int> ObtenerRachaActualAsync(string usuarioId)
        {
            var racha = await _context.RachasUsuarios
                .FirstOrDefaultAsync(r => r.UsuarioId == usuarioId);

            if (racha == null) return 0;

            // Verificar si la racha sigue activa (48 horas desde último login)
            var horaLocal = await _dateTimeService.GetUserLocalNowAsync(usuarioId);
            if (!racha.UltimoLoginPremio.HasValue ||
                (horaLocal - racha.UltimoLoginPremio.Value).TotalHours > 48)
            {
                return 0;
            }

            return racha.RachaActual;
        }

        public async Task<int> ObtenerRachaMaximaAsync(string usuarioId)
        {
            return await _context.RachasUsuarios
                .Where(r => r.UsuarioId == usuarioId)
                .Select(r => r.RachaMaxima)
                .FirstOrDefaultAsync();
        }

        public async Task<(int likes, int comentarios, int contenidos)> ObtenerContadoresHoyAsync(string usuarioId)
        {
            // Usar la zona horaria del usuario específico
            var fechaLocalHoy = (await _dateTimeService.GetUserLocalNowAsync(usuarioId)).Date;
            var zonaHoraria = await _dateTimeService.GetUserTimeZoneIdAsync(usuarioId);

            // Consulta separada para evitar conflictos de tracking con otras operaciones
            var racha = await _context.RachasUsuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UsuarioId == usuarioId);

            // Usar hora local del usuario, NO hora del servidor
            if (racha == null)
            {
                _logger.LogDebug("📊 CONTADORES: Usuario={UsuarioId}, Racha=null, Retornando (0,0,0)", usuarioId);
                return (0, 0, 0);
            }

            if (racha.FechaReset.Date < fechaLocalHoy)
            {
                _logger.LogDebug("📊 CONTADORES: Usuario={UsuarioId}, FechaReset={FechaReset} < FechaLocalHoy={FechaLocalHoy}, Retornando (0,0,0)",
                    usuarioId, racha.FechaReset.ToString("yyyy-MM-dd"), fechaLocalHoy.ToString("yyyy-MM-dd"));
                return (0, 0, 0);
            }

            _logger.LogDebug("📊 CONTADORES: Usuario={UsuarioId}, ZonaHoraria={ZonaHoraria}, Likes={Likes}, Comentarios={Comentarios}, Contenidos={Contenidos}",
                usuarioId, zonaHoraria, racha.LikesHoy, racha.ComentariosHoy, racha.ContenidosHoy);

            return (racha.LikesHoy, racha.ComentariosHoy, racha.ContenidosHoy);
        }

        #endregion

        #region Reset Diario

        public async Task ResetearContadoresDiariosAsync()
        {
            try
            {
                var fechaLocalHoy = _dateTimeService.GetLocalNow().Date;

                // Obtener rachas que necesitan reset
                var rachasAResetear = await _context.RachasUsuarios
                    .Where(r => r.FechaReset < fechaLocalHoy)
                    .ToListAsync();

                foreach (var racha in rachasAResetear)
                {
                    racha.ResetearContadores(fechaLocalHoy);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Reseteados contadores diarios de {Count} usuarios a fecha {FechaLocal}",
                    rachasAResetear.Count, fechaLocalHoy);
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, null, null);
            }
        }

        #endregion
    }
}
