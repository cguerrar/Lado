using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Borrador de Story/Reel para guardar trabajo en progreso
    /// </summary>
    public class StoryDraft
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public ApplicationUser? Usuario { get; set; }

        /// <summary>
        /// Tipo: story o reel
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Tipo { get; set; } = "reel";

        /// <summary>
        /// Nombre del borrador (opcional)
        /// </summary>
        [StringLength(100)]
        public string? Nombre { get; set; }

        /// <summary>
        /// Estado del canvas serializado (Fabric.js JSON)
        /// </summary>
        public string? CanvasState { get; set; }

        /// <summary>
        /// URL del archivo de media principal (video/imagen)
        /// </summary>
        [StringLength(500)]
        public string? MediaUrl { get; set; }

        /// <summary>
        /// Tipo de media: video, image
        /// </summary>
        [StringLength(20)]
        public string? MediaType { get; set; }

        /// <summary>
        /// Configuración de música (JSON)
        /// </summary>
        public string? MusicConfig { get; set; }

        /// <summary>
        /// Efectos de video aplicados (JSON)
        /// </summary>
        public string? VideoEffects { get; set; }

        /// <summary>
        /// Configuración de Beat Sync (JSON)
        /// </summary>
        public string? BeatSyncConfig { get; set; }

        /// <summary>
        /// Thumbnail del borrador (base64 o URL)
        /// </summary>
        public string? Thumbnail { get; set; }

        /// <summary>
        /// LadoA o LadoB
        /// </summary>
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        /// <summary>
        /// Duración del video en segundos
        /// </summary>
        public double? Duracion { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime FechaModificacion { get; set; } = DateTime.Now;
    }
}
