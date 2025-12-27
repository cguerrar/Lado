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

        public int NumeroLikes { get; set; } = 0;

        [MaxLength(500)]
        public string? Texto { get; set; } // Texto simple (legacy, ahora usar ElementosJson)

        // Sistema LadoA / LadoB para stories
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        // ========================================
        // EDITOR DE STORIES - Elementos visuales
        // ========================================

        /// <summary>
        /// JSON con todos los elementos del editor:
        /// - textos: array de objetos {id, texto, x, y, fontSize, color, fontFamily, rotation, backgroundColor}
        /// - stickers: array de objetos {id, tipo, valor, x, y, scale, rotation}
        /// - dibujos: array de objetos {id, pathData, color, strokeWidth}
        /// - menciones: array de objetos {id, usuarioId, username, x, y}
        /// </summary>
        public string? ElementosJson { get; set; }

        /// <summary>
        /// IDs de usuarios mencionados (para notificaciones)
        /// </summary>
        [MaxLength(2000)]
        public string? MencionesIds { get; set; }

        // ========================================
        // MÚSICA EN STORIES
        // ========================================

        /// <summary>
        /// ID de la pista musical (si tiene música)
        /// </summary>
        public int? PistaMusicalId { get; set; }

        /// <summary>
        /// Segundo de inicio de la música (ej: 30 para empezar en 0:30)
        /// </summary>
        public int? MusicaInicioSegundos { get; set; }

        /// <summary>
        /// Volumen de la música (0-100)
        /// </summary>
        public int? MusicaVolumen { get; set; }

        // Navegación
        [ForeignKey("CreadorId")]
        public virtual ApplicationUser Creador { get; set; }

        [ForeignKey("PistaMusicalId")]
        public virtual PistaMusical? PistaMusical { get; set; }

        public virtual ICollection<StoryVista> Vistas { get; set; }

        public virtual ICollection<StoryLike> Likes { get; set; }
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