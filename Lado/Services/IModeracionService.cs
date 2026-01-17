using Lado.Models;
using Lado.Models.Moderacion;

namespace Lado.Services
{
    /// <summary>
    /// Servicio para gestión de moderación de contenido
    /// </summary>
    public interface IModeracionService
    {
        // ═══════════════════════════════════════════════════════════
        // COLA DE MODERACIÓN
        // ═══════════════════════════════════════════════════════════

        /// <summary>Agregar contenido a la cola de moderación</summary>
        Task<ColaModeracion> AgregarAColaAsync(int contenidoId, PrioridadModeracion prioridad = PrioridadModeracion.Normal, int? reporteId = null);

        /// <summary>Obtener items pendientes de la cola</summary>
        Task<List<ColaModeracion>> ObtenerColaPendienteAsync(int limite = 50, ModuloSupervisor? modulo = null);

        /// <summary>Obtener siguiente item para un supervisor</summary>
        Task<ColaModeracion?> ObtenerSiguienteItemAsync(string supervisorId);

        /// <summary>Asignar item a un supervisor</summary>
        Task<bool> AsignarItemAsync(int colaId, string supervisorId);

        /// <summary>Liberar item (timeout o cancelación)</summary>
        Task<bool> LiberarItemAsync(int colaId);

        /// <summary>Obtener item por ID</summary>
        Task<ColaModeracion?> ObtenerItemAsync(int colaId);

        /// <summary>Obtener items asignados a un supervisor</summary>
        Task<List<ColaModeracion>> ObtenerItemsAsignadosAsync(string supervisorId);

        // ═══════════════════════════════════════════════════════════
        // DECISIONES
        // ═══════════════════════════════════════════════════════════

        /// <summary>Registrar decisión de moderación</summary>
        Task<DecisionModeracion> RegistrarDecisionAsync(
            int colaId,
            string supervisorId,
            TipoDecisionModeracion decision,
            RazonRechazo? razonRechazo = null,
            string? comentario = null,
            int tiempoRevisionSegundos = 0,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>Aprobar contenido</summary>
        Task<bool> AprobarAsync(int colaId, string supervisorId, string? comentario = null, int tiempoSegundos = 0);

        /// <summary>Rechazar contenido</summary>
        Task<bool> RechazarAsync(int colaId, string supervisorId, RazonRechazo razon, string? detalleRazon = null, int tiempoSegundos = 0);

        /// <summary>Censurar contenido</summary>
        Task<bool> CensurarAsync(int colaId, string supervisorId, string razonCensura, int tiempoSegundos = 0);

        /// <summary>Escalar a administrador</summary>
        Task<bool> EscalarAsync(int colaId, string supervisorId, string motivo, int tiempoSegundos = 0);

        /// <summary>Revertir decisión</summary>
        Task<bool> RevertirDecisionAsync(int decisionId, string adminId, string razonReversion);

        // ═══════════════════════════════════════════════════════════
        // SUPERVISORES
        // ═══════════════════════════════════════════════════════════

        /// <summary>Verificar si usuario es supervisor</summary>
        Task<bool> EsSupervisorAsync(string usuarioId);

        /// <summary>Obtener permisos de un supervisor</summary>
        Task<List<string>> ObtenerPermisosAsync(string usuarioId);

        /// <summary>Verificar si supervisor tiene permiso específico</summary>
        Task<bool> TienePermisoAsync(string usuarioId, string codigoPermiso);

        /// <summary>Obtener rol de supervisor</summary>
        Task<UsuarioSupervisor?> ObtenerSupervisorAsync(string usuarioId);

        /// <summary>Obtener todos los supervisores activos</summary>
        Task<List<UsuarioSupervisor>> ObtenerSupervisoresActivosAsync();

        /// <summary>Actualizar disponibilidad del supervisor</summary>
        Task<bool> ActualizarDisponibilidadAsync(string supervisorId, bool disponible);

        /// <summary>Registrar actividad del supervisor</summary>
        Task ActualizarActividadAsync(string supervisorId);

        // ═══════════════════════════════════════════════════════════
        // MÉTRICAS
        // ═══════════════════════════════════════════════════════════

        /// <summary>Obtener métricas del día para un supervisor</summary>
        Task<MetricaSupervisor?> ObtenerMetricasHoyAsync(string supervisorId);

        /// <summary>Obtener resumen de métricas para un período</summary>
        Task<ResumenMetricasSupervisor> ObtenerResumenMetricasAsync(string supervisorId, DateTime desde, DateTime hasta);

        /// <summary>Obtener ranking de supervisores</summary>
        Task<List<ResumenMetricasSupervisor>> ObtenerRankingSupervisoresAsync(DateTime desde, DateTime hasta, int limite = 10);

        /// <summary>Actualizar métricas del día</summary>
        Task ActualizarMetricasAsync(string supervisorId, TipoDecisionModeracion decision, int tiempoSegundos);

        // ═══════════════════════════════════════════════════════════
        // ESTADÍSTICAS GENERALES
        // ═══════════════════════════════════════════════════════════

        /// <summary>Obtener estadísticas de la cola</summary>
        Task<EstadisticasCola> ObtenerEstadisticasColaAsync();

        /// <summary>Obtener historial de decisiones</summary>
        Task<List<DecisionModeracion>> ObtenerHistorialDecisionesAsync(string? supervisorId = null, int dias = 7, int limite = 100);

        // ═══════════════════════════════════════════════════════════
        // CLASIFICACIÓN IA
        // ═══════════════════════════════════════════════════════════

        /// <summary>Pre-clasificar contenido con IA</summary>
        Task<ResultadoClasificacionIA> ClasificarContenidoIAAsync(int contenidoId);

        /// <summary>Procesar contenido nuevo (clasificar y decidir si va a cola)</summary>
        Task<ResultadoProcesamientoContenido> ProcesarContenidoNuevoAsync(int contenidoId);

        // ═══════════════════════════════════════════════════════════
        // INICIALIZACIÓN
        // ═══════════════════════════════════════════════════════════

        /// <summary>Inicializar permisos y roles predefinidos</summary>
        Task InicializarPermisosYRolesAsync();
    }

    /// <summary>
    /// Estadísticas de la cola de moderación
    /// </summary>
    public class EstadisticasCola
    {
        public int TotalPendientes { get; set; }
        public int TotalEnRevision { get; set; }
        public int TotalUrgentes { get; set; }
        public int TotalAltaPrioridad { get; set; }
        public int AprobadosHoy { get; set; }
        public int RechazadosHoy { get; set; }
        public int EscaladosHoy { get; set; }
        public decimal TiempoPromedioRevision { get; set; }
        public int SupervisoresActivos { get; set; }
        public DateTime? UltimaActualizacion { get; set; }
    }

    /// <summary>
    /// Resultado de clasificación con IA
    /// </summary>
    public class ResultadoClasificacionIA
    {
        public bool Exitoso { get; set; }
        public decimal Confianza { get; set; }
        public bool EsSeguro { get; set; }
        public bool RequiereRevision { get; set; }
        public bool EsProhibido { get; set; }
        public string? RazonProhibido { get; set; }
        public List<string> CategoriasDetectadas { get; set; } = new();
        public List<string> Advertencias { get; set; } = new();
        public string? ResultadoJson { get; set; }
    }

    /// <summary>
    /// Resultado del procesamiento de contenido nuevo
    /// </summary>
    public class ResultadoProcesamientoContenido
    {
        public bool Exitoso { get; set; }
        public EstadoModeracion EstadoFinal { get; set; }
        public bool FueAutoAprobado { get; set; }
        public bool FueAutoRechazado { get; set; }
        public bool FueEnviadoACola { get; set; }
        public int? ColaModeracionId { get; set; }
        public string? Mensaje { get; set; }
        public ResultadoClasificacionIA? ClasificacionIA { get; set; }
    }
}
