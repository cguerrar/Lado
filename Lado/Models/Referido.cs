using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Relación entre referidor y referido para el sistema de referidos.
    /// Permite tracking de bonos y comisiones del 10% por 3 meses.
    /// </summary>
    public class Referido
    {
        public int Id { get; set; }

        /// <summary>
        /// Usuario que invitó (referidor)
        /// </summary>
        [Required]
        public string ReferidorId { get; set; } = string.Empty;

        /// <summary>
        /// Usuario que fue invitado (referido)
        /// </summary>
        [Required]
        public string ReferidoUsuarioId { get; set; } = string.Empty;

        /// <summary>
        /// Código de referido usado para el registro
        /// </summary>
        [Required]
        [StringLength(20)]
        public string CodigoUsado { get; set; } = string.Empty;

        [Display(Name = "Fecha de Registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha hasta la cual el referidor gana comisión del 10% (3 meses desde registro)
        /// </summary>
        [Display(Name = "Fecha Expiración Comisión")]
        public DateTime FechaExpiracionComision { get; set; }

        /// <summary>
        /// Total de comisiones ganadas por el referidor de este referido
        /// </summary>
        [Display(Name = "Total Comisión Ganada")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalComisionGanada { get; set; } = 0;

        /// <summary>
        /// Si ya se entregó el bono de $10 al referidor
        /// </summary>
        [Display(Name = "Bono Referidor Entregado")]
        public bool BonoReferidorEntregado { get; set; } = false;

        /// <summary>
        /// Si ya se entregó el bono de $15 al referido
        /// </summary>
        [Display(Name = "Bono Referido Entregado")]
        public bool BonoReferidoEntregado { get; set; } = false;

        /// <summary>
        /// Si ya se entregó el bono de $50 cuando el referido se volvió creador LadoB
        /// </summary>
        [Display(Name = "Bono Creador LadoB Entregado")]
        public bool BonoCreadorLadoBEntregado { get; set; } = false;

        /// <summary>
        /// Si la relación de comisión está activa (dentro de los 3 meses)
        /// </summary>
        [Display(Name = "Comisión Activa")]
        public bool ComisionActiva { get; set; } = true;

        /// <summary>
        /// Última fecha en que se procesó comisión
        /// </summary>
        [Display(Name = "Última Comisión")]
        public DateTime? UltimaComision { get; set; }

        // Navegación
        [ForeignKey("ReferidorId")]
        public virtual ApplicationUser? Referidor { get; set; }

        [ForeignKey("ReferidoUsuarioId")]
        public virtual ApplicationUser? ReferidoUsuario { get; set; }
    }
}
