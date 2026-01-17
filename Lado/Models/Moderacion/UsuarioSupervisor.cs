using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models.Moderacion
{
    /// <summary>
    /// Asigna un usuario como supervisor con un rol específico
    /// </summary>
    public class UsuarioSupervisor
    {
        public int Id { get; set; }

        /// <summary>ID del usuario (ApplicationUser)</summary>
        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; } = null!;

        /// <summary>Rol de supervisor asignado</summary>
        public int RolSupervisorId { get; set; }
        public virtual RolSupervisor RolSupervisor { get; set; } = null!;

        /// <summary>Si la asignación está activa</summary>
        public bool EstaActivo { get; set; } = true;

        /// <summary>Fecha de asignación del rol</summary>
        public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;

        /// <summary>Fecha de desactivación (si aplica)</summary>
        public DateTime? FechaDesactivacion { get; set; }

        /// <summary>Usuario admin que hizo la asignación</summary>
        public string? AsignadoPorId { get; set; }

        [ForeignKey("AsignadoPorId")]
        public virtual ApplicationUser? AsignadoPor { get; set; }

        /// <summary>Notas sobre la asignación</summary>
        [MaxLength(500)]
        public string? Notas { get; set; }

        /// <summary>Turno asignado (para organización)</summary>
        [MaxLength(50)]
        public string? Turno { get; set; } // "Mañana", "Tarde", "Noche"

        /// <summary>Última actividad del supervisor</summary>
        public DateTime? UltimaActividad { get; set; }

        /// <summary>Si está actualmente conectado/disponible</summary>
        public bool EstaDisponible { get; set; } = true;

        /// <summary>Items actualmente asignados para revisión</summary>
        public int ItemsAsignados { get; set; } = 0;
    }
}
