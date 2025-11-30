using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Lado.Models
{
    [Index(nameof(UsuarioId), nameof(CategoriaInteresId), IsUnique = true)]
    public class InteresUsuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        [Required]
        public int CategoriaInteresId { get; set; }

        [ForeignKey("CategoriaInteresId")]
        public virtual CategoriaInteres? CategoriaInteres { get; set; }

        public TipoInteres Tipo { get; set; } = TipoInteres.Explicito;

        [Column(TypeName = "decimal(5,2)")]
        public decimal PesoInteres { get; set; } = 1.0m;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime UltimaInteraccion { get; set; } = DateTime.Now;

        public int ContadorInteracciones { get; set; } = 1;
    }
}
