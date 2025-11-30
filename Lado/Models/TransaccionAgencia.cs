using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Lado.Models
{
    [Index(nameof(AgenciaId), nameof(FechaTransaccion))]
    public class TransaccionAgencia
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AgenciaId { get; set; }

        [ForeignKey("AgenciaId")]
        public virtual Agencia? Agencia { get; set; }

        public TipoTransaccionAgencia Tipo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoAnterior { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoPosterior { get; set; }

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [StringLength(100)]
        public string? Referencia { get; set; }

        // Para recargas - datos del pago
        [StringLength(50)]
        public string? MetodoPago { get; set; }

        [StringLength(100)]
        public string? IdTransaccionExterna { get; set; }

        // Para cobros de anuncios
        public int? AnuncioId { get; set; }

        [ForeignKey("AnuncioId")]
        public virtual Anuncio? Anuncio { get; set; }

        public DateTime FechaTransaccion { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string? Estado { get; set; } = "Completado";
    }
}
