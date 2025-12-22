using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class LikeComentario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ComentarioId { get; set; }

        [ForeignKey("ComentarioId")]
        public virtual Comentario Comentario { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; }

        public DateTime FechaLike { get; set; } = DateTime.Now;
    }
}
