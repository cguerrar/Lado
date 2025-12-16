using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Modelo para gestionar bloqueos entre usuarios.
    /// Cuando un usuario bloquea a otro, no podrán interactuar entre sí.
    /// </summary>
    public class BloqueoUsuario
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del usuario que realiza el bloqueo
        /// </summary>
        [Required]
        public string BloqueadorId { get; set; } = string.Empty;

        /// <summary>
        /// Usuario que realiza el bloqueo
        /// </summary>
        [ForeignKey("BloqueadorId")]
        public virtual ApplicationUser? Bloqueador { get; set; }

        /// <summary>
        /// ID del usuario que es bloqueado
        /// </summary>
        [Required]
        public string BloqueadoId { get; set; } = string.Empty;

        /// <summary>
        /// Usuario que es bloqueado
        /// </summary>
        [ForeignKey("BloqueadoId")]
        public virtual ApplicationUser? Bloqueado { get; set; }

        /// <summary>
        /// Fecha y hora en que se realizó el bloqueo
        /// </summary>
        public DateTime FechaBloqueo { get; set; } = DateTime.Now;

        /// <summary>
        /// Razón opcional del bloqueo
        /// </summary>
        [StringLength(500)]
        public string? Razon { get; set; }
    }
}
