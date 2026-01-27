using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class CategoriaInteres
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Descripcion { get; set; }

        [StringLength(50)]
        public string? Icono { get; set; }

        [StringLength(20)]
        public string? Color { get; set; }

        [StringLength(100)]
        public string? Slug { get; set; }

        [StringLength(300)]
        public string? ImagenPortada { get; set; }

        // Jerarquia (categoria padre para subcategorias)
        public int? CategoriaPadreId { get; set; }

        [ForeignKey("CategoriaPadreId")]
        public virtual CategoriaInteres? CategoriaPadre { get; set; }

        public int Orden { get; set; } = 0;

        public bool EstaActiva { get; set; } = true;

        // Navegacion
        public virtual ICollection<CategoriaInteres> Subcategorias { get; set; } = new List<CategoriaInteres>();
        public virtual ICollection<InteresUsuario> UsuariosInteresados { get; set; } = new List<InteresUsuario>();
    }
}
