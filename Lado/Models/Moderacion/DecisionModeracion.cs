using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models.Moderacion
{
    /// <summary>
    /// Registra cada decisión tomada por un supervisor (auditoría)
    /// </summary>
    public class DecisionModeracion
    {
        public int Id { get; set; }

        /// <summary>Item de la cola de moderación</summary>
        public int ColaModeracionId { get; set; }

        [ForeignKey("ColaModeracionId")]
        public virtual ColaModeracion ColaModeracion { get; set; } = null!;

        /// <summary>Supervisor que tomó la decisión</summary>
        [Required]
        public string SupervisorId { get; set; } = string.Empty;

        [ForeignKey("SupervisorId")]
        public virtual ApplicationUser Supervisor { get; set; } = null!;

        /// <summary>Tipo de decisión tomada</summary>
        public TipoDecisionModeracion Decision { get; set; }

        /// <summary>Razón de rechazo si aplica</summary>
        public RazonRechazo? RazonRechazo { get; set; }

        /// <summary>Detalle o comentario adicional</summary>
        [MaxLength(1000)]
        public string? Comentario { get; set; }

        /// <summary>Fecha y hora de la decisión</summary>
        public DateTime FechaDecision { get; set; } = DateTime.UtcNow;

        /// <summary>Tiempo que tardó en revisar (segundos)</summary>
        public int TiempoRevisionSegundos { get; set; }

        /// <summary>Si la decisión fue revertida</summary>
        public bool FueRevertida { get; set; } = false;

        /// <summary>Razón de reversión (si aplica)</summary>
        [MaxLength(500)]
        public string? RazonReversion { get; set; }

        /// <summary>Quién revirtió la decisión</summary>
        public string? RevertidoPorId { get; set; }

        [ForeignKey("RevertidoPorId")]
        public virtual ApplicationUser? RevertidoPor { get; set; }

        /// <summary>Fecha de reversión</summary>
        public DateTime? FechaReversion { get; set; }

        /// <summary>IP desde donde se tomó la decisión</summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /// <summary>User-Agent del navegador</summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }
    }
}
