using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class Like
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; }

        [Required]
        public int ContenidoId { get; set; }

        [ForeignKey("ContenidoId")]
        public virtual Contenido Contenido { get; set; }

        public DateTime FechaLike { get; set; } = DateTime.Now;
    }
}