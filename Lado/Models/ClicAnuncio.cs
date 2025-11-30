using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Lado.Models
{
    [Index(nameof(AnuncioId), nameof(FechaClic))]
    [Index(nameof(UsuarioId), nameof(FechaClic))]
    [Index(nameof(SessionId), nameof(AnuncioId))]
    public class ClicAnuncio
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

        public DateTime FechaClic { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,6)")]
        public decimal CostoClic { get; set; }

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

        // Para anti-fraude
        public bool EsValido { get; set; } = true;

        [StringLength(200)]
        public string? MotivoInvalido { get; set; }
    }
}
