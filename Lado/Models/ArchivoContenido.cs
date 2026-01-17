using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Representa un archivo (foto o video) dentro de un post/contenido.
    /// Permite múltiples archivos por post (carrusel estilo Instagram).
    /// </summary>
    public class ArchivoContenido
    {
        public int Id { get; set; }

        [Required]
        public int ContenidoId { get; set; }

        [Required]
        [StringLength(500)]
        public string RutaArchivo { get; set; } = string.Empty;

        /// <summary>
        /// Orden del archivo en el carrusel (0 = primero)
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Tipo de archivo: Foto o Video
        /// </summary>
        public TipoArchivo TipoArchivo { get; set; } = TipoArchivo.Foto;

        /// <summary>
        /// Miniatura del archivo (especialmente útil para videos)
        /// </summary>
        [StringLength(500)]
        public string? Thumbnail { get; set; }

        /// <summary>
        /// Ancho del archivo en píxeles
        /// </summary>
        public int? Ancho { get; set; }

        /// <summary>
        /// Alto del archivo en píxeles
        /// </summary>
        public int? Alto { get; set; }

        /// <summary>
        /// Duración en segundos (solo para videos)
        /// </summary>
        public int? DuracionSegundos { get; set; }

        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        public long? TamanoBytes { get; set; }

        /// <summary>
        /// Texto alternativo para accesibilidad
        /// </summary>
        [StringLength(500)]
        public string? AltText { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Shadow hide: Este archivo está oculto para todos excepto el creador.
        /// Útil para ocultar una foto específica de un carrusel sin afectar las demás.
        /// </summary>
        public bool OcultoSilenciosamente { get; set; } = false;

        /// <summary>
        /// Fecha en que se ocultó silenciosamente
        /// </summary>
        public DateTime? FechaOcultoSilenciosamente { get; set; }

        // ========================================
        // RELACIONES
        // ========================================

        [ForeignKey("ContenidoId")]
        public virtual Contenido? Contenido { get; set; }
    }

    public enum TipoArchivo
    {
        Foto = 0,
        Video = 1
    }
}
