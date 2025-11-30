using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Lado.Models
{
    [Index(nameof(AnuncioId), nameof(FechaImpresion))]
    [Index(nameof(UsuarioId), nameof(FechaImpresion))]
    public class ImpresionAnuncio
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int AnuncioId { get; set; }

        [ForeignKey("AnuncioId")]
        public virtual Anuncio? Anuncio { get; set; }

        public string? UsuarioId { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        public DateTime FechaImpresion { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,6)")]
        public decimal CostoImpresion { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }

        [StringLength(50)]
        public string? Dispositivo { get; set; }

        [StringLength(10)]
        public string? Pais { get; set; }

        [StringLength(100)]
        public string? Ciudad { get; set; }

        [StringLength(100)]
        public string? SessionId { get; set; }
    }
}
