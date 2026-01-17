using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.Models.Moderacion;
using System.Text.Json;

namespace Lado.Services
{
    public class ModeracionService : IModeracionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClaudeClassificationService _claudeService;
        private readonly ILogEventoService _logService;
        private readonly ILogger<ModeracionService> _logger;

        // Timeout de asignación en minutos
        private const int TIMEOUT_ASIGNACION_MINUTOS = 5;

        // Umbral de confianza para auto-aprobar
        private const decimal UMBRAL_AUTO_APROBAR = 0.95m;

        // Umbral de confianza para auto-rechazar
        private const decimal UMBRAL_AUTO_RECHAZAR = 0.90m;

        public ModeracionService(
            ApplicationDbContext context,
            IClaudeClassificationService claudeService,
            ILogEventoService logService,
            ILogger<ModeracionService> logger)
        {
            _context = context;
            _claudeService = claudeService;
            _logService = logService;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════
        // COLA DE MODERACIÓN
        // ═══════════════════════════════════════════════════════════

        public async Task<ColaModeracion> AgregarAColaAsync(int contenidoId, PrioridadModeracion prioridad = PrioridadModeracion.Normal, int? reporteId = null)
        {
            // Verificar si ya existe en cola
            var existente = await _context.ColaModeracion
                .FirstOrDefaultAsync(c => c.ContenidoId == contenidoId &&
                    (c.Estado == EstadoModeracion.Pendiente || c.Estado == EstadoModeracion.EnRevision));

            if (existente != null)
            {
                // Si viene de reporte, subir prioridad
                if (reporteId.HasValue && existente.Prioridad > PrioridadModeracion.Alta)
                {
                    existente.Prioridad = PrioridadModeracion.Alta;
                    existente.EsDeReporte = true;
                    existente.ReporteId = reporteId;
                    await _context.SaveChangesAsync();
                }
                return existente;
            }

            var cola = new ColaModeracion
            {
                ContenidoId = contenidoId,
                Estado = EstadoModeracion.Pendiente,
                Prioridad = prioridad,
                FechaCreacion = DateTime.UtcNow,
                EsDeReporte = reporteId.HasValue,
                ReporteId = reporteId
            };

            _context.ColaModeracion.Add(cola);
            await _context.SaveChangesAsync();

            return cola;
        }

        public async Task<List<ColaModeracion>> ObtenerColaPendienteAsync(int limite = 50, ModuloSupervisor? modulo = null)
        {
            // Liberar items con timeout expirado
            await LiberarItemsExpiradosAsync();

            return await _context.ColaModeracion
                .Include(c => c.Contenido)
                    .ThenInclude(c => c!.Usuario)
                .Include(c => c.Contenido)
                    .ThenInclude(c => c.Archivos)
                .Where(c => c.Estado == EstadoModeracion.Pendiente)
                .OrderBy(c => c.Prioridad)
                .ThenBy(c => c.FechaCreacion)
                .Take(limite)
                .ToListAsync();
        }

        public async Task<ColaModeracion?> ObtenerSiguienteItemAsync(string supervisorId)
        {
            // Liberar items con timeout expirado
            await LiberarItemsExpiradosAsync();

            // Obtener supervisor para verificar límite
            var supervisor = await _context.UsuariosSupervisor
                .Include(s => s.RolSupervisor)
                .FirstOrDefaultAsync(s => s.UsuarioId == supervisorId && s.EstaActivo);

            if (supervisor == null) return null;

            // Verificar si tiene espacio para más items
            var itemsActuales = await _context.ColaModeracion
                .CountAsync(c => c.SupervisorAsignadoId == supervisorId && c.Estado == EstadoModeracion.EnRevision);

            if (itemsActuales >= supervisor.RolSupervisor.MaxItemsSimultaneos)
                return null;

            // Obtener siguiente item pendiente
            var item = await _context.ColaModeracion
                .Include(c => c.Contenido)
                    .ThenInclude(c => c!.Usuario)
                .Include(c => c.Contenido)
                    .ThenInclude(c => c!.Archivos)
                .Where(c => c.Estado == EstadoModeracion.Pendiente)
                .OrderBy(c => c.Prioridad)
                .ThenBy(c => c.FechaCreacion)
                .FirstOrDefaultAsync();

            if (item != null)
            {
                await AsignarItemAsync(item.Id, supervisorId);
            }

            return item;
        }

        public async Task<bool> AsignarItemAsync(int colaId, string supervisorId)
        {
            var item = await _context.ColaModeracion.FindAsync(colaId);
            if (item == null || item.Estado != EstadoModeracion.Pendiente)
                return false;

            item.Estado = EstadoModeracion.EnRevision;
            item.SupervisorAsignadoId = supervisorId;
            item.FechaAsignacion = DateTime.UtcNow;
            item.TimeoutAsignacion = DateTime.UtcNow.AddMinutes(TIMEOUT_ASIGNACION_MINUTOS);

            // Actualizar contador del supervisor
            var supervisor = await _context.UsuariosSupervisor
                .FirstOrDefaultAsync(s => s.UsuarioId == supervisorId);
            if (supervisor != null)
            {
                supervisor.ItemsAsignados++;
                supervisor.UltimaActividad = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> LiberarItemAsync(int colaId)
        {
            var item = await _context.ColaModeracion.FindAsync(colaId);
            if (item == null) return false;

            var supervisorId = item.SupervisorAsignadoId;

            item.Estado = EstadoModeracion.Pendiente;
            item.SupervisorAsignadoId = null;
            item.FechaAsignacion = null;
            item.TimeoutAsignacion = null;
            item.VecesReasignado++;

            // Actualizar contador del supervisor
            if (supervisorId != null)
            {
                var supervisor = await _context.UsuariosSupervisor
                    .FirstOrDefaultAsync(s => s.UsuarioId == supervisorId);
                if (supervisor != null && supervisor.ItemsAsignados > 0)
                {
                    supervisor.ItemsAsignados--;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ColaModeracion?> ObtenerItemAsync(int colaId)
        {
            return await _context.ColaModeracion
                .Include(c => c.Contenido)
                    .ThenInclude(c => c!.Usuario)
                .Include(c => c.Contenido)
                    .ThenInclude(c => c.Archivos)
                .Include(c => c.Decisiones)
                    .ThenInclude(d => d.Supervisor)
                .FirstOrDefaultAsync(c => c.Id == colaId);
        }

        public async Task<List<ColaModeracion>> ObtenerItemsAsignadosAsync(string supervisorId)
        {
            return await _context.ColaModeracion
                .Include(c => c.Contenido)
                    .ThenInclude(c => c.Archivos)
                .Where(c => c.SupervisorAsignadoId == supervisorId && c.Estado == EstadoModeracion.EnRevision)
                .OrderBy(c => c.FechaAsignacion)
                .ToListAsync();
        }

        private async Task LiberarItemsExpiradosAsync()
        {
            var ahora = DateTime.UtcNow;
            var itemsExpirados = await _context.ColaModeracion
                .Where(c => c.Estado == EstadoModeracion.EnRevision &&
                           c.TimeoutAsignacion.HasValue &&
                           c.TimeoutAsignacion < ahora)
                .ToListAsync();

            foreach (var item in itemsExpirados)
            {
                await LiberarItemAsync(item.Id);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // DECISIONES
        // ═══════════════════════════════════════════════════════════

        public async Task<DecisionModeracion> RegistrarDecisionAsync(
            int colaId,
            string supervisorId,
            TipoDecisionModeracion decision,
            RazonRechazo? razonRechazo = null,
            string? comentario = null,
            int tiempoRevisionSegundos = 0,
            string? ipAddress = null,
            string? userAgent = null)
        {
            var decisionEntity = new DecisionModeracion
            {
                ColaModeracionId = colaId,
                SupervisorId = supervisorId,
                Decision = decision,
                RazonRechazo = razonRechazo,
                Comentario = comentario,
                FechaDecision = DateTime.UtcNow,
                TiempoRevisionSegundos = tiempoRevisionSegundos,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            _context.DecisionesModeracion.Add(decisionEntity);
            await _context.SaveChangesAsync();

            // Actualizar métricas
            await ActualizarMetricasAsync(supervisorId, decision, tiempoRevisionSegundos);

            return decisionEntity;
        }

        public async Task<bool> AprobarAsync(int colaId, string supervisorId, string? comentario = null, int tiempoSegundos = 0)
        {
            var item = await _context.ColaModeracion
                .Include(c => c.Contenido)
                .FirstOrDefaultAsync(c => c.Id == colaId);

            if (item == null) return false;

            // Registrar decisión
            await RegistrarDecisionAsync(colaId, supervisorId, TipoDecisionModeracion.Aprobado,
                comentario: comentario, tiempoRevisionSegundos: tiempoSegundos);

            // Actualizar cola
            item.Estado = EstadoModeracion.Aprobado;
            item.FechaResolucion = DateTime.UtcNow;
            item.DecisionFinal = TipoDecisionModeracion.Aprobado;
            item.TiempoRevisionSegundos = tiempoSegundos;

            // El contenido ya está activo, no hacemos nada adicional

            // Actualizar contador del supervisor
            await ActualizarContadorSupervisor(supervisorId, -1);

            await _context.SaveChangesAsync();

            await _logService.RegistrarEventoAsync(
                $"Contenido {item.ContenidoId} aprobado por supervisor",
                CategoriaEvento.Contenido,
                TipoLogEvento.Evento,
                supervisorId,
                null,
                $"ColaId: {colaId}, Tiempo: {tiempoSegundos}s");

            return true;
        }

        public async Task<bool> RechazarAsync(int colaId, string supervisorId, RazonRechazo razon, string? detalleRazon = null, int tiempoSegundos = 0)
        {
            var item = await _context.ColaModeracion
                .Include(c => c.Contenido)
                    .ThenInclude(c => c!.Usuario)
                .FirstOrDefaultAsync(c => c.Id == colaId);

            if (item == null) return false;

            // Registrar decisión
            await RegistrarDecisionAsync(colaId, supervisorId, TipoDecisionModeracion.Rechazado,
                razonRechazo: razon, comentario: detalleRazon, tiempoRevisionSegundos: tiempoSegundos);

            // Actualizar cola
            item.Estado = EstadoModeracion.Rechazado;
            item.FechaResolucion = DateTime.UtcNow;
            item.DecisionFinal = TipoDecisionModeracion.Rechazado;
            item.RazonRechazo = razon;
            item.DetalleRazon = detalleRazon;
            item.TiempoRevisionSegundos = tiempoSegundos;

            // Desactivar contenido
            item.Contenido.EstaActivo = false;

            // Notificar al creador
            var notificacion = new Notificacion
            {
                UsuarioId = item.Contenido.UsuarioId,
                Tipo = TipoNotificacion.Sistema,
                Mensaje = $"Tu contenido ha sido rechazado. Razón: {ObtenerTextoRazon(razon)}",
                FechaCreacion = DateTime.UtcNow,
                Leida = false
            };
            _context.Notificaciones.Add(notificacion);

            await ActualizarContadorSupervisor(supervisorId, -1);
            await _context.SaveChangesAsync();

            await _logService.RegistrarEventoAsync(
                $"Contenido {item.ContenidoId} rechazado por supervisor",
                CategoriaEvento.Contenido,
                TipoLogEvento.Warning,
                supervisorId,
                null,
                $"Razón: {razon}, Detalle: {detalleRazon}");

            return true;
        }

        public async Task<bool> CensurarAsync(int colaId, string supervisorId, string razonCensura, int tiempoSegundos = 0)
        {
            var item = await _context.ColaModeracion
                .Include(c => c.Contenido)
                .FirstOrDefaultAsync(c => c.Id == colaId);

            if (item == null) return false;

            await RegistrarDecisionAsync(colaId, supervisorId, TipoDecisionModeracion.Censurado,
                comentario: razonCensura, tiempoRevisionSegundos: tiempoSegundos);

            item.Estado = EstadoModeracion.Censurado;
            item.FechaResolucion = DateTime.UtcNow;
            item.DecisionFinal = TipoDecisionModeracion.Censurado;
            item.DetalleRazon = razonCensura;
            item.TiempoRevisionSegundos = tiempoSegundos;

            // Marcar contenido como censurado (visible con advertencia)
            item.Contenido.Censurado = true;
            item.Contenido.RazonCensura = razonCensura;

            await ActualizarContadorSupervisor(supervisorId, -1);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> EscalarAsync(int colaId, string supervisorId, string motivo, int tiempoSegundos = 0)
        {
            var item = await _context.ColaModeracion.FindAsync(colaId);
            if (item == null) return false;

            await RegistrarDecisionAsync(colaId, supervisorId, TipoDecisionModeracion.Escalado,
                comentario: motivo, tiempoRevisionSegundos: tiempoSegundos);

            item.Estado = EstadoModeracion.Escalado;
            item.Prioridad = PrioridadModeracion.Urgente;
            item.NotasInternas = $"[Escalado por supervisor] {motivo}";
            item.TiempoRevisionSegundos = tiempoSegundos;

            await ActualizarContadorSupervisor(supervisorId, -1);
            await _context.SaveChangesAsync();

            await _logService.RegistrarEventoAsync(
                $"Contenido {item.ContenidoId} escalado a administrador",
                CategoriaEvento.Admin,
                TipoLogEvento.Warning,
                supervisorId,
                null,
                motivo);

            return true;
        }

        public async Task<bool> RevertirDecisionAsync(int decisionId, string adminId, string razonReversion)
        {
            var decision = await _context.DecisionesModeracion
                .Include(d => d.ColaModeracion)
                    .ThenInclude(c => c.Contenido)
                .FirstOrDefaultAsync(d => d.Id == decisionId);

            if (decision == null || decision.FueRevertida)
                return false;

            decision.FueRevertida = true;
            decision.RazonReversion = razonReversion;
            decision.RevertidoPorId = adminId;
            decision.FechaReversion = DateTime.UtcNow;

            // Restaurar contenido si fue rechazado
            if (decision.Decision == TipoDecisionModeracion.Rechazado)
            {
                decision.ColaModeracion.Contenido.EstaActivo = true;
            }

            // Quitar censura si fue censurado
            if (decision.Decision == TipoDecisionModeracion.Censurado)
            {
                decision.ColaModeracion.Contenido.Censurado = false;
                decision.ColaModeracion.Contenido.RazonCensura = null;
            }

            // Actualizar métricas (incrementar revertidos)
            var metricaHoy = await ObtenerOCrearMetricaHoyAsync(decision.SupervisorId);
            metricaHoy.Revertidos++;

            await _context.SaveChangesAsync();

            await _logService.RegistrarEventoAsync(
                $"Decisión {decisionId} revertida por admin",
                CategoriaEvento.Admin,
                TipoLogEvento.Warning,
                adminId,
                null,
                razonReversion);

            return true;
        }

        private async Task ActualizarContadorSupervisor(string supervisorId, int delta)
        {
            var supervisor = await _context.UsuariosSupervisor
                .FirstOrDefaultAsync(s => s.UsuarioId == supervisorId);

            if (supervisor != null)
            {
                supervisor.ItemsAsignados = Math.Max(0, supervisor.ItemsAsignados + delta);
                supervisor.UltimaActividad = DateTime.UtcNow;
            }
        }

        private string ObtenerTextoRazon(RazonRechazo razon)
        {
            return razon switch
            {
                RazonRechazo.ContenidoProhibido => "Contenido prohibido por las políticas de la plataforma",
                RazonRechazo.MenorDeEdadAparente => "Posible menor de edad en el contenido",
                RazonRechazo.ViolenciaExplicita => "Contenido con violencia explícita",
                RazonRechazo.SpamPublicidad => "Spam o publicidad no autorizada",
                RazonRechazo.InfraccionCopyright => "Infracción de derechos de autor",
                RazonRechazo.CalidadMuyBaja => "Calidad de contenido muy baja",
                RazonRechazo.InformacionPersonalExpuesta => "Información personal expuesta",
                RazonRechazo.IncitacionOdio => "Incitación al odio",
                RazonRechazo.ContenidoIlegal => "Contenido ilegal",
                _ => "Violación de políticas de la plataforma"
            };
        }

        // ═══════════════════════════════════════════════════════════
        // SUPERVISORES
        // ═══════════════════════════════════════════════════════════

        public async Task<bool> EsSupervisorAsync(string usuarioId)
        {
            return await _context.UsuariosSupervisor
                .AnyAsync(s => s.UsuarioId == usuarioId && s.EstaActivo);
        }

        public async Task<List<string>> ObtenerPermisosAsync(string usuarioId)
        {
            var supervisor = await _context.UsuariosSupervisor
                .Include(s => s.RolSupervisor)
                    .ThenInclude(r => r.RolesPermisos)
                        .ThenInclude(rp => rp.PermisoSupervisor)
                .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.EstaActivo);

            if (supervisor == null) return new List<string>();

            return supervisor.RolSupervisor.RolesPermisos
                .Where(rp => rp.PermisoSupervisor.Activo)
                .Select(rp => rp.PermisoSupervisor.Codigo)
                .ToList();
        }

        public async Task<bool> TienePermisoAsync(string usuarioId, string codigoPermiso)
        {
            var permisos = await ObtenerPermisosAsync(usuarioId);
            return permisos.Contains(codigoPermiso);
        }

        public async Task<UsuarioSupervisor?> ObtenerSupervisorAsync(string usuarioId)
        {
            return await _context.UsuariosSupervisor
                .Include(s => s.Usuario)
                .Include(s => s.RolSupervisor)
                .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.EstaActivo);
        }

        public async Task<List<UsuarioSupervisor>> ObtenerSupervisoresActivosAsync()
        {
            return await _context.UsuariosSupervisor
                .Include(s => s.Usuario)
                .Include(s => s.RolSupervisor)
                .Where(s => s.EstaActivo)
                .OrderBy(s => s.RolSupervisor.Nombre)
                .ThenBy(s => s.Usuario.UserName)
                .ToListAsync();
        }

        public async Task<bool> ActualizarDisponibilidadAsync(string supervisorId, bool disponible)
        {
            var supervisor = await _context.UsuariosSupervisor
                .FirstOrDefaultAsync(s => s.UsuarioId == supervisorId);

            if (supervisor == null) return false;

            supervisor.EstaDisponible = disponible;
            supervisor.UltimaActividad = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task ActualizarActividadAsync(string supervisorId)
        {
            var supervisor = await _context.UsuariosSupervisor
                .FirstOrDefaultAsync(s => s.UsuarioId == supervisorId);

            if (supervisor != null)
            {
                supervisor.UltimaActividad = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        // ═══════════════════════════════════════════════════════════
        // MÉTRICAS
        // ═══════════════════════════════════════════════════════════

        public async Task<MetricaSupervisor?> ObtenerMetricasHoyAsync(string supervisorId)
        {
            var hoy = DateTime.UtcNow.Date;
            return await _context.MetricasSupervisor
                .FirstOrDefaultAsync(m => m.SupervisorId == supervisorId && m.Fecha == hoy);
        }

        public async Task<ResumenMetricasSupervisor> ObtenerResumenMetricasAsync(string supervisorId, DateTime desde, DateTime hasta)
        {
            var metricas = await _context.MetricasSupervisor
                .Where(m => m.SupervisorId == supervisorId && m.Fecha >= desde.Date && m.Fecha <= hasta.Date)
                .ToListAsync();

            var supervisor = await _context.UsuariosSupervisor
                .Include(s => s.Usuario)
                .Include(s => s.RolSupervisor)
                .FirstOrDefaultAsync(s => s.UsuarioId == supervisorId);

            var resumen = new ResumenMetricasSupervisor
            {
                SupervisorId = supervisorId,
                SupervisorNombre = supervisor?.Usuario?.NombreCompleto ?? supervisor?.Usuario?.UserName ?? "Desconocido",
                SupervisorAvatar = supervisor?.Usuario?.FotoPerfil,
                RolNombre = supervisor?.RolSupervisor?.Nombre ?? "Sin rol",
                RolColor = supervisor?.RolSupervisor?.ColorBadge ?? "#4682B4",
                TotalRevisados = metricas.Sum(m => m.TotalRevisados),
                Aprobados = metricas.Sum(m => m.Aprobados),
                Rechazados = metricas.Sum(m => m.Rechazados),
                Escalados = metricas.Sum(m => m.Escalados),
                DiasActivos = metricas.Count,
                EstaActivo = supervisor?.EstaActivo ?? false,
                UltimaActividad = supervisor?.UltimaActividad
            };

            resumen.TasaAprobacion = resumen.TotalRevisados > 0
                ? (decimal)resumen.Aprobados / resumen.TotalRevisados * 100
                : 0;

            resumen.TiempoPromedioSegundos = metricas.Count > 0
                ? metricas.Average(m => m.TiempoPromedioSegundos)
                : 0;

            resumen.PromedioRevisadosPorDia = resumen.DiasActivos > 0
                ? (decimal)resumen.TotalRevisados / resumen.DiasActivos
                : 0;

            return resumen;
        }

        public async Task<List<ResumenMetricasSupervisor>> ObtenerRankingSupervisoresAsync(DateTime desde, DateTime hasta, int limite = 10)
        {
            var supervisores = await _context.UsuariosSupervisor
                .Where(s => s.EstaActivo)
                .Select(s => s.UsuarioId)
                .ToListAsync();

            var ranking = new List<ResumenMetricasSupervisor>();

            foreach (var supervisorId in supervisores)
            {
                var resumen = await ObtenerResumenMetricasAsync(supervisorId, desde, hasta);
                ranking.Add(resumen);
            }

            return ranking
                .OrderByDescending(r => r.TotalRevisados)
                .Take(limite)
                .Select((r, i) => { r.Ranking = i + 1; return r; })
                .ToList();
        }

        public async Task ActualizarMetricasAsync(string supervisorId, TipoDecisionModeracion decision, int tiempoSegundos)
        {
            var metrica = await ObtenerOCrearMetricaHoyAsync(supervisorId);

            metrica.TotalRevisados++;

            switch (decision)
            {
                case TipoDecisionModeracion.Aprobado:
                    metrica.Aprobados++;
                    break;
                case TipoDecisionModeracion.Rechazado:
                    metrica.Rechazados++;
                    break;
                case TipoDecisionModeracion.Censurado:
                    metrica.Censurados++;
                    break;
                case TipoDecisionModeracion.Escalado:
                    metrica.Escalados++;
                    break;
            }

            // Actualizar tiempo promedio
            var totalTiempo = metrica.TiempoTotalSegundos + tiempoSegundos;
            metrica.TiempoTotalSegundos = totalTiempo;
            metrica.TiempoPromedioSegundos = (decimal)totalTiempo / metrica.TotalRevisados;

            if (!metrica.TiempoMinimoSegundos.HasValue || tiempoSegundos < metrica.TiempoMinimoSegundos)
                metrica.TiempoMinimoSegundos = tiempoSegundos;

            if (!metrica.TiempoMaximoSegundos.HasValue || tiempoSegundos > metrica.TiempoMaximoSegundos)
                metrica.TiempoMaximoSegundos = tiempoSegundos;

            // Actualizar tasas
            metrica.TasaAprobacion = metrica.TotalRevisados > 0
                ? (decimal)metrica.Aprobados / metrica.TotalRevisados * 100
                : 0;

            metrica.TasaEscalamiento = metrica.TotalRevisados > 0
                ? (decimal)metrica.Escalados / metrica.TotalRevisados * 100
                : 0;

            metrica.HoraUltimaActividad = DateTime.UtcNow;
            metrica.UltimaActualizacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private async Task<MetricaSupervisor> ObtenerOCrearMetricaHoyAsync(string supervisorId)
        {
            var hoy = DateTime.UtcNow.Date;
            var metrica = await _context.MetricasSupervisor
                .FirstOrDefaultAsync(m => m.SupervisorId == supervisorId && m.Fecha == hoy);

            if (metrica == null)
            {
                metrica = new MetricaSupervisor
                {
                    SupervisorId = supervisorId,
                    Fecha = hoy,
                    HoraInicioActividad = DateTime.UtcNow,
                    NumeroSesiones = 1
                };
                _context.MetricasSupervisor.Add(metrica);
                await _context.SaveChangesAsync();
            }

            return metrica;
        }

        // ═══════════════════════════════════════════════════════════
        // ESTADÍSTICAS GENERALES
        // ═══════════════════════════════════════════════════════════

        public async Task<EstadisticasCola> ObtenerEstadisticasColaAsync()
        {
            var hoy = DateTime.UtcNow.Date;

            var stats = new EstadisticasCola
            {
                TotalPendientes = await _context.ColaModeracion
                    .CountAsync(c => c.Estado == EstadoModeracion.Pendiente),

                TotalEnRevision = await _context.ColaModeracion
                    .CountAsync(c => c.Estado == EstadoModeracion.EnRevision),

                TotalUrgentes = await _context.ColaModeracion
                    .CountAsync(c => c.Estado == EstadoModeracion.Pendiente &&
                                    c.Prioridad == PrioridadModeracion.Urgente),

                TotalAltaPrioridad = await _context.ColaModeracion
                    .CountAsync(c => c.Estado == EstadoModeracion.Pendiente &&
                                    c.Prioridad == PrioridadModeracion.Alta),

                AprobadosHoy = await _context.ColaModeracion
                    .CountAsync(c => c.Estado == EstadoModeracion.Aprobado &&
                                    c.FechaResolucion.HasValue &&
                                    c.FechaResolucion.Value.Date == hoy),

                RechazadosHoy = await _context.ColaModeracion
                    .CountAsync(c => c.Estado == EstadoModeracion.Rechazado &&
                                    c.FechaResolucion.HasValue &&
                                    c.FechaResolucion.Value.Date == hoy),

                EscaladosHoy = await _context.ColaModeracion
                    .CountAsync(c => c.Estado == EstadoModeracion.Escalado &&
                                    c.FechaResolucion.HasValue &&
                                    c.FechaResolucion.Value.Date == hoy),

                SupervisoresActivos = await _context.UsuariosSupervisor
                    .CountAsync(s => s.EstaActivo && s.EstaDisponible),

                UltimaActualizacion = DateTime.UtcNow
            };

            // Tiempo promedio de revisión hoy
            var tiemposHoy = await _context.ColaModeracion
                .Where(c => c.FechaResolucion.HasValue &&
                           c.FechaResolucion.Value.Date == hoy &&
                           c.TiempoRevisionSegundos.HasValue)
                .Select(c => c.TiempoRevisionSegundos!.Value)
                .ToListAsync();

            stats.TiempoPromedioRevision = tiemposHoy.Count > 0 ? (decimal)tiemposHoy.Average() : 0;

            return stats;
        }

        public async Task<List<DecisionModeracion>> ObtenerHistorialDecisionesAsync(string? supervisorId = null, int dias = 7, int limite = 100)
        {
            var desde = DateTime.UtcNow.AddDays(-dias);

            var query = _context.DecisionesModeracion
                .Include(d => d.Supervisor)
                .Include(d => d.ColaModeracion)
                    .ThenInclude(c => c.Contenido)
                .Where(d => d.FechaDecision >= desde);

            if (!string.IsNullOrEmpty(supervisorId))
            {
                query = query.Where(d => d.SupervisorId == supervisorId);
            }

            return await query
                .OrderByDescending(d => d.FechaDecision)
                .Take(limite)
                .ToListAsync();
        }

        // ═══════════════════════════════════════════════════════════
        // CLASIFICACIÓN IA
        // ═══════════════════════════════════════════════════════════

        public async Task<ResultadoClasificacionIA> ClasificarContenidoIAAsync(int contenidoId)
        {
            var resultado = new ResultadoClasificacionIA();

            try
            {
                var contenido = await _context.Contenidos
                    .Include(c => c.Archivos)
                    .FirstOrDefaultAsync(c => c.Id == contenidoId);

                if (contenido == null)
                {
                    resultado.Exitoso = false;
                    return resultado;
                }

                // Obtener el primer archivo para clasificar
                byte[]? imagenBytes = null;
                string? mimeType = null;
                var primerArchivo = contenido.Archivos?.FirstOrDefault();

                if (primerArchivo != null && !string.IsNullOrEmpty(primerArchivo.RutaArchivo))
                {
                    try
                    {
                        var rutaCompleta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", primerArchivo.RutaArchivo.TrimStart('/'));
                        if (File.Exists(rutaCompleta))
                        {
                            imagenBytes = await File.ReadAllBytesAsync(rutaCompleta);
                            mimeType = primerArchivo.TipoArchivo == TipoArchivo.Video ? "video/mp4" : "image/jpeg";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo leer archivo para clasificación: {Ruta}", primerArchivo.RutaArchivo);
                    }
                }

                // Usar el servicio de clasificación con los parámetros correctos
                var clasificacion = await _claudeService.ClasificarContenidoDetalladoAsync(
                    imagenBytes,
                    contenido.Descripcion,
                    mimeType);

                resultado.Exitoso = true;
                resultado.Confianza = clasificacion?.Exito == true ? 0.85m : 0.5m;
                resultado.EsSeguro = !contenido.Censurado;
                resultado.RequiereRevision = true; // Por defecto requiere revisión
                resultado.CategoriasDetectadas = new List<string>();

                if (clasificacion?.CategoriaId.HasValue == true)
                {
                    resultado.ResultadoJson = $"{{\"categoriaId\": {clasificacion.CategoriaId}}}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al clasificar contenido {ContenidoId} con IA", contenidoId);
                resultado.Exitoso = false;
            }

            return resultado;
        }

        public async Task<ResultadoProcesamientoContenido> ProcesarContenidoNuevoAsync(int contenidoId)
        {
            var resultado = new ResultadoProcesamientoContenido { Exitoso = true };

            try
            {
                // Clasificar con IA
                var clasificacionIA = await ClasificarContenidoIAAsync(contenidoId);
                resultado.ClasificacionIA = clasificacionIA;

                if (clasificacionIA.Exitoso)
                {
                    // Auto-aprobar si confianza alta y es seguro
                    if (clasificacionIA.Confianza >= UMBRAL_AUTO_APROBAR && clasificacionIA.EsSeguro)
                    {
                        resultado.FueAutoAprobado = true;
                        resultado.EstadoFinal = EstadoModeracion.AutoAprobado;
                        resultado.Mensaje = "Contenido auto-aprobado por IA";

                        // Registrar en cola como auto-aprobado
                        var cola = await AgregarAColaAsync(contenidoId, PrioridadModeracion.Baja);
                        cola.Estado = EstadoModeracion.AutoAprobado;
                        cola.ClasificadoPorIA = true;
                        cola.ConfianzaIA = clasificacionIA.Confianza;
                        cola.ResultadoClasificacionIA = clasificacionIA.ResultadoJson;
                        cola.FechaResolucion = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        resultado.ColaModeracionId = cola.Id;
                        return resultado;
                    }

                    // Auto-rechazar si es claramente prohibido
                    if (clasificacionIA.Confianza >= UMBRAL_AUTO_RECHAZAR && clasificacionIA.EsProhibido)
                    {
                        resultado.FueAutoRechazado = true;
                        resultado.EstadoFinal = EstadoModeracion.AutoRechazado;
                        resultado.Mensaje = $"Contenido auto-rechazado: {clasificacionIA.RazonProhibido}";

                        // Desactivar contenido
                        var contenido = await _context.Contenidos.FindAsync(contenidoId);
                        if (contenido != null)
                        {
                            contenido.EstaActivo = false;
                        }

                        var cola = await AgregarAColaAsync(contenidoId, PrioridadModeracion.Alta);
                        cola.Estado = EstadoModeracion.AutoRechazado;
                        cola.ClasificadoPorIA = true;
                        cola.ConfianzaIA = clasificacionIA.Confianza;
                        cola.ResultadoClasificacionIA = clasificacionIA.ResultadoJson;
                        cola.IADetectoProblema = true;
                        cola.FechaResolucion = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        resultado.ColaModeracionId = cola.Id;
                        return resultado;
                    }
                }

                // Enviar a cola para revisión humana
                var prioridad = clasificacionIA.RequiereRevision
                    ? PrioridadModeracion.Normal
                    : PrioridadModeracion.Baja;

                if (clasificacionIA.Advertencias.Count > 0)
                {
                    prioridad = PrioridadModeracion.Alta;
                }

                var colaModeracion = await AgregarAColaAsync(contenidoId, prioridad);
                colaModeracion.ClasificadoPorIA = clasificacionIA.Exitoso;
                colaModeracion.ConfianzaIA = clasificacionIA.Confianza;
                colaModeracion.ResultadoClasificacionIA = clasificacionIA.ResultadoJson;
                colaModeracion.CategoriasIA = string.Join(",", clasificacionIA.CategoriasDetectadas);
                await _context.SaveChangesAsync();

                resultado.FueEnviadoACola = true;
                resultado.EstadoFinal = EstadoModeracion.Pendiente;
                resultado.ColaModeracionId = colaModeracion.Id;
                resultado.Mensaje = "Contenido enviado a cola de moderación";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar contenido nuevo {ContenidoId}", contenidoId);
                resultado.Exitoso = false;
                resultado.Mensaje = ex.Message;
            }

            return resultado;
        }

        // ═══════════════════════════════════════════════════════════
        // INICIALIZACIÓN
        // ═══════════════════════════════════════════════════════════

        public async Task InicializarPermisosYRolesAsync()
        {
            // Verificar si ya están creados
            if (await _context.PermisosSupervisor.AnyAsync())
                return;

            // Crear permisos
            var permisos = PermisosPredefinidos.ObtenerTodos();
            _context.PermisosSupervisor.AddRange(permisos);
            await _context.SaveChangesAsync();

            // Crear roles predefinidos
            var rolesPredefinidos = RolesPredefinidos.ObtenerRolesPredefinidos();

            foreach (var (nombre, descripcion, color, icono, codigosPermisos) in rolesPredefinidos)
            {
                var rol = new RolSupervisor
                {
                    Nombre = nombre,
                    Descripcion = descripcion,
                    ColorBadge = color,
                    Icono = icono,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.RolesSupervisor.Add(rol);
                await _context.SaveChangesAsync();

                // Asignar permisos al rol
                foreach (var codigo in codigosPermisos)
                {
                    var permiso = await _context.PermisosSupervisor
                        .FirstOrDefaultAsync(p => p.Codigo == codigo);

                    if (permiso != null)
                    {
                        _context.RolesSupervisorPermisos.Add(new RolSupervisorPermiso
                        {
                            RolSupervisorId = rol.Id,
                            PermisoSupervisorId = permiso.Id
                        });
                    }
                }

                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Permisos y roles de supervisor inicializados correctamente");
        }
    }
}
