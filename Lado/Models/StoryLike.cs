using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Like en una historia (story)
    /// </summary>
    public class StoryLike
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StoryId { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        public DateTime FechaLike { get; set; } = DateTime.Now;

        // Navegacion
        [ForeignKey("StoryId")]
        public virtual Story Story { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; }
    }
}
