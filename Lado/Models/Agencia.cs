using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class Agencia
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        [Required]
        [StringLength(100)]
        public string NombreEmpresa { get; set; } = string.Empty;

        [StringLength(100)]
        public string? RazonSocial { get; set; }

        [StringLength(20)]
        public string? NIF { get; set; }

        [StringLength(200)]
        public string? Direccion { get; set; }

        [StringLength(50)]
        public string? Ciudad { get; set; }

        [StringLength(50)]
        public string? Pais { get; set; }

        [StringLength(10)]
        public string? CodigoPostal { get; set; }

        [StringLength(20)]
        public string? Telefono { get; set; }

        [StringLength(200)]
        public string? SitioWeb { get; set; }

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [StringLength(300)]
        public string? LogoUrl { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaldoPublicitario { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGastado { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRecargado { get; set; } = 0;

        public EstadoAgencia Estado { get; set; } = EstadoAgencia.Pendiente;

        public bool EstaVerificada { get; set; } = false;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public DateTime? FechaAprobacion { get; set; }

        public DateTime? FechaSuspension { get; set; }

        [StringLength(500)]
        public string? MotivoRechazo { get; set; }

        [StringLength(500)]
        public string? MotivoSuspension { get; set; }

        // Navegacion
        public virtual ICollection<Anuncio> Anuncios { get; set; } = new List<Anuncio>();
        public virtual ICollection<TransaccionAgencia> Transacciones { get; set; } = new List<TransaccionAgencia>();
    }
}
