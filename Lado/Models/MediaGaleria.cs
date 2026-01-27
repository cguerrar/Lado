using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Tipo de media en la galería
    /// </summary>
    public enum TipoMediaGaleria
    {
        Imagen = 0,
        Video = 1
    }

    /// <summary>
    /// Archivo multimedia almacenado en la galería privada del usuario
    /// </summary>
    public class MediaGaleria
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        /// <summary>
        /// Album al que pertenece (null = sin album / galería general)
        /// </summary>
        public int? AlbumId { get; set; }

        [Required]
        [MaxLength(500)]
        public string RutaArchivo { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Thumbnail { get; set; }

        [MaxLength(255)]
        public string? NombreOriginal { get; set; }

        [Required]
        public TipoMediaGaleria TipoMedia { get; set; }

        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        public long TamanoBytes { get; set; }

        /// <summary>
        /// Duración en segundos (solo para videos)
        /// </summary>
        public int? DuracionSegundos { get; set; }

        /// <summary>
        /// Ancho de la imagen/video en píxeles
        /// </summary>
        public int? Ancho { get; set; }

        /// <summary>
        /// Alto de la imagen/video en píxeles
        /// </summary>
        public int? Alto { get; set; }

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Tags separados por coma para búsqueda
        /// </summary>
        [MaxLength(500)]
        public string? Tags { get; set; }

        [Required]
        public DateTime FechaSubida { get; set; } = DateTime.Now;

        /// <summary>
        /// ID del contenido si este archivo fue usado en una publicación
        /// </summary>
        public int? ContenidoAsociadoId { get; set; }

        /// <summary>
        /// ID del mensaje si este archivo fue enviado por chat
        /// </summary>
        public int? MensajeAsociadoId { get; set; }

        /// <summary>
        /// Marcado como favorito para acceso rápido
        /// </summary>
        public bool EsFavorito { get; set; } = false;

        /// <summary>
        /// Hash del archivo para detectar duplicados
        /// </summary>
        [MaxLength(64)]
        public string? HashArchivo { get; set; }

        // Navegación
        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        [ForeignKey("AlbumId")]
        public virtual Album? Album { get; set; }

        [ForeignKey("ContenidoAsociadoId")]
        public virtual Contenido? ContenidoAsociado { get; set; }

        [ForeignKey("MensajeAsociadoId")]
        public virtual ChatMensaje? MensajeAsociado { get; set; }
    }
}
