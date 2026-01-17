using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models.Moderacion
{
    /// <summary>
    /// Cola de moderación para contenido pendiente de revisión
    /// </summary>
    public class ColaModeracion
    {
        public int Id { get; set; }

        /// <summary>Contenido a moderar</summary>
        public int ContenidoId { get; set; }

        [ForeignKey("ContenidoId")]
        public virtual Contenido Contenido { get; set; } = null!;

        /// <summary>Estado actual en la cola</summary>
        public EstadoModeracion Estado { get; set; } = EstadoModeracion.Pendiente;

        /// <summary>Prioridad de revisión</summary>
        public PrioridadModeracion Prioridad { get; set; } = PrioridadModeracion.Normal;

        /// <summary>Supervisor asignado (si hay)</summary>
        public string? SupervisorAsignadoId { get; set; }

        [ForeignKey("SupervisorAsignadoId")]
        public virtual ApplicationUser? SupervisorAsignado { get; set; }

        /// <summary>Fecha de entrada a la cola</summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>Fecha de asignación a supervisor</summary>
        public DateTime? FechaAsignacion { get; set; }

        /// <summary>Fecha de resolución final</summary>
        public DateTime? FechaResolucion { get; set; }

        /// <summary>Tiempo límite para la revisión (timeout)</summary>
        public DateTime? TimeoutAsignacion { get; set; }

        // ═══════════════════════════════════════════════════════════
        // CLASIFICACIÓN IA
        // ═══════════════════════════════════════════════════════════

        /// <summary>Si fue pre-clasificado por IA</summary>
        public bool ClasificadoPorIA { get; set; } = false;

        /// <summary>Resultado de clasificación IA (JSON)</summary>
        public string? ResultadoClasificacionIA { get; set; }

        /// <summary>Confianza de la clasificación IA (0-1)</summary>
        [Column(TypeName = "decimal(5,4)")]
        public decimal? ConfianzaIA { get; set; }

        /// <summary>Si la IA detectó contenido potencialmente problemático</summary>
        public bool? IADetectoProblema { get; set; }

        /// <summary>Categorías detectadas por IA</summary>
        [MaxLength(500)]
        public string? CategoriasIA { get; set; }

        // ═══════════════════════════════════════════════════════════
        // CONTEXTO ADICIONAL
        // ═══════════════════════════════════════════════════════════

        /// <summary>Si viene de un reporte de usuario</summary>
        public bool EsDeReporte { get; set; } = false;

        /// <summary>ID del reporte relacionado (si aplica)</summary>
        public int? ReporteId { get; set; }

        [ForeignKey("ReporteId")]
        public virtual Reporte? Reporte { get; set; }

        /// <summary>Número de veces que ha sido reasignado</summary>
        public int VecesReasignado { get; set; } = 0;

        /// <summary>Notas internas para el equipo</summary>
        [MaxLength(1000)]
        public string? NotasInternas { get; set; }

        // ═══════════════════════════════════════════════════════════
        // RESULTADO DE LA MODERACIÓN
        // ═══════════════════════════════════════════════════════════

        /// <summary>Decisión final tomada</summary>
        public TipoDecisionModeracion? DecisionFinal { get; set; }

        /// <summary>Razón del rechazo (si aplica)</summary>
        public RazonRechazo? RazonRechazo { get; set; }

        /// <summary>Detalle adicional de la razón</summary>
        [MaxLength(500)]
        public string? DetalleRazon { get; set; }

        /// <summary>Tiempo total de revisión en segundos</summary>
        public int? TiempoRevisionSegundos { get; set; }

        // Navegación
        public virtual ICollection<DecisionModeracion> Decisiones { get; set; } = new List<DecisionModeracion>();
    }
}
