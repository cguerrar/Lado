using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Historia temporal que expira en 24 horas
    /// </summary>
    public class Story
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CreadorId { get; set; }

        [Required]
        [MaxLength(500)]
        public string RutaArchivo { get; set; }

        [Required]
        public TipoContenido TipoContenido { get; set; }

        [Required]
        public DateTime FechaPublicacion { get; set; }

        [Required]
        public DateTime FechaExpiracion { get; set; } // FechaPublicacion + 24 horas

        public bool EstaActivo { get; set; } = true;

        public int NumeroVistas { get; set; } = 0;

        [MaxLength(500)]
        public string? Texto { get; set; } // Texto opcional sobre la imagen/video

        // Sistema LadoA / LadoB para stories
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        // Navegación
        [ForeignKey("CreadorId")]
        public virtual ApplicationUser Creador { get; set; }

        public virtual ICollection<StoryVista> Vistas { get; set; }
    }

    /// <summary>
    /// Registro de quién vio cada story
    /// </summary>
    public class StoryVista
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int StoryId { get; set; }

        [Required]
        public string UsuarioId { get; set; }

        [Required]
        public DateTime FechaVista { get; set; }

        // Navegación
        [ForeignKey("StoryId")]
        public virtual Story Story { get; set; }

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; }
    }
}