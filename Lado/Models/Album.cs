using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Album para organizar archivos de la galería privada del usuario
    /// </summary>
    public class Album
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = "Sin nombre";

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        [MaxLength(500)]
        public string? ImagenPortada { get; set; }

        /// <summary>
        /// Si es privado, solo el dueño puede verlo (siempre true para galería personal)
        /// </summary>
        public bool EsPrivado { get; set; } = true;

        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? FechaActualizacion { get; set; }

        /// <summary>
        /// Orden de visualización en la lista de álbumes
        /// </summary>
        public int Orden { get; set; } = 0;

        // Navegación
        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        public virtual ICollection<MediaGaleria> Archivos { get; set; } = new List<MediaGaleria>();
    }
}
