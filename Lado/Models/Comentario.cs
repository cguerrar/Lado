using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class Comentario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContenidoId { get; set; }

        [ForeignKey("ContenidoId")]
        public virtual Contenido Contenido { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; }

        [Required]
        [StringLength(1000)]
        public string Texto { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public bool EstaActivo { get; set; } = true;
    }
}