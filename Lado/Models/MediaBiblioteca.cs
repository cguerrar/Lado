using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public enum EstadoMediaBiblioteca
    {
        Pendiente = 0,      // Subido pero no programado
        Programado = 1,     // Tiene fecha de publicación asignada
        Publicado = 2,      // Ya fue publicado
        Error = 3,          // Error al publicar
        Cancelado = 4       // Cancelado manualmente
    }

    public enum TipoMediaBiblioteca
    {
        Imagen = 0,
        Video = 1
    }

    public enum TipoPublicacionMedia
    {
        Contenido = 0,  // Publicación normal del feed
        Story = 1       // Historia (24 horas)
    }

    /// <summary>
    /// Almacena los medios pendientes de publicar para usuarios administrados.
    /// Permite subir una biblioteca de contenido que se publicará gradualmente.
    /// </summary>
    public class MediaBiblioteca
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Usuario al que pertenece este medio
        /// </summary>
        [Required]
        public string UsuarioId { get; set; } = null!;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser Usuario { get; set; } = null!;

        /// <summary>
        /// Ruta del archivo en el servidor
        /// </summary>
        [Required]
        [StringLength(500)]
        public string RutaArchivo { get; set; } = null!;

        /// <summary>
        /// Nombre original del archivo
        /// </summary>
        [StringLength(255)]
        public string? NombreOriginal { get; set; }

        /// <summary>
        /// Tipo de medio (imagen o video)
        /// </summary>
        public TipoMediaBiblioteca TipoMedia { get; set; }

        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        public long TamanoBytes { get; set; }

        /// <summary>
        /// Duración del video en segundos (null para imágenes)
        /// </summary>
        public int? DuracionSegundos { get; set; }

        /// <summary>
        /// Descripción/texto que acompañará al post cuando se publique
        /// </summary>
        [StringLength(2000)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Hashtags a incluir en la publicación
        /// </summary>
        [StringLength(500)]
        public string? Hashtags { get; set; }

        /// <summary>
        /// Estado actual del medio
        /// </summary>
        public EstadoMediaBiblioteca Estado { get; set; } = EstadoMediaBiblioteca.Pendiente;

        /// <summary>
        /// Fecha en que se subió a la biblioteca
        /// </summary>
        public DateTime FechaSubida { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha programada para publicación (null si no está programado)
        /// </summary>
        public DateTime? FechaProgramada { get; set; }

        /// <summary>
        /// Fecha en que realmente se publicó
        /// </summary>
        public DateTime? FechaPublicado { get; set; }

        /// <summary>
        /// Tipo de publicación: Contenido normal o Story
        /// </summary>
        public TipoPublicacionMedia TipoPublicacion { get; set; } = TipoPublicacionMedia.Contenido;

        /// <summary>
        /// ID del contenido una vez publicado (si es tipo Contenido)
        /// </summary>
        public int? ContenidoPublicadoId { get; set; }

        [ForeignKey("ContenidoPublicadoId")]
        public virtual Contenido? ContenidoPublicado { get; set; }

        /// <summary>
        /// ID de la story una vez publicada (si es tipo Story)
        /// </summary>
        public int? StoryPublicadoId { get; set; }

        [ForeignKey("StoryPublicadoId")]
        public virtual Story? StoryPublicado { get; set; }

        /// <summary>
        /// Lado en el que se publicará (A o B)
        /// </summary>
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        /// <summary>
        /// Si el contenido es solo para suscriptores
        /// </summary>
        public bool SoloSuscriptores { get; set; } = false;

        /// <summary>
        /// Precio en LadoCoins si es contenido de pago (null = gratis)
        /// </summary>
        public int? PrecioLadoCoins { get; set; }

        /// <summary>
        /// Orden en la cola de publicación (menor = primero)
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Mensaje de error si falló la publicación
        /// </summary>
        [StringLength(1000)]
        public string? MensajeError { get; set; }

        /// <summary>
        /// Número de intentos de publicación
        /// </summary>
        public int IntentosPublicacion { get; set; } = 0;
    }
}
