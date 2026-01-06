using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Modelo para almacenar órdenes de PayPal pendientes de captura.
    /// Esto permite rastrear el estado de los pagos y evitar duplicados.
    /// </summary>
    public class OrdenPayPalPendiente
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID de la orden en PayPal (ej: 5O190127TN364715T)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// ID del usuario que creó la orden
        /// </summary>
        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        /// <summary>
        /// Monto de la orden en USD
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        /// <summary>
        /// Estado de la orden: CREATED, APPROVED, COMPLETED, CANCELLED
        /// </summary>
        [StringLength(20)]
        public string Estado { get; set; } = "CREATED";

        /// <summary>
        /// Fecha de creación de la orden
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha en que se completó el pago (null si no se ha completado)
        /// </summary>
        public DateTime? FechaCompletado { get; set; }

        /// <summary>
        /// ID de la captura de PayPal (después de completar el pago)
        /// </summary>
        [StringLength(50)]
        public string? CaptureId { get; set; }

        /// <summary>
        /// Email del pagador en PayPal
        /// </summary>
        [StringLength(256)]
        public string? PayerEmail { get; set; }
    }
}
