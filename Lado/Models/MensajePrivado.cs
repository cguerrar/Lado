using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public class MensajePrivado
    {
        public int Id { get; set; }
        public string RemitenteId { get; set; } = string.Empty;
        public string DestinatarioId { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public DateTime FechaEnvio { get; set; } = DateTime.Now;
        public bool Leido { get; set; } = false;
        public bool EliminadoPorRemitente { get; set; } = false;
        public bool EliminadoPorDestinatario { get; set; } = false;

        // ========================================
        // ARCHIVOS ADJUNTOS
        // ========================================

        /// <summary>
        /// Tipo de mensaje: Texto, Imagen, Video
        /// </summary>
        public TipoMensaje TipoMensaje { get; set; } = TipoMensaje.Texto;

        /// <summary>
        /// Ruta del archivo adjunto (si aplica)
        /// </summary>
        [StringLength(500)]
        public string? RutaArchivo { get; set; }

        /// <summary>
        /// Nombre original del archivo
        /// </summary>
        [StringLength(255)]
        public string? NombreArchivoOriginal { get; set; }

        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        public long? TamanoArchivo { get; set; }

        // ========================================
        // SISTEMA DE RESPUESTAS (HILOS)
        // ========================================

        /// <summary>
        /// ID del mensaje al que se responde (null si no es respuesta)
        /// </summary>
        public int? MensajeRespondidoId { get; set; }

        /// <summary>
        /// Navegación al mensaje respondido
        /// </summary>
        [ForeignKey("MensajeRespondidoId")]
        public virtual MensajePrivado? MensajeRespondido { get; set; }

        /// <summary>
        /// Fecha en que se leyó el mensaje
        /// </summary>
        public DateTime? FechaLectura { get; set; }

        // ========================================
        // RESPUESTA A STORY
        // ========================================

        /// <summary>
        /// ID de la historia a la que se responde (null si no es respuesta a story)
        /// </summary>
        public int? StoryReferenciaId { get; set; }

        /// <summary>
        /// Tipo de respuesta a story (texto normal, reacción rápida)
        /// </summary>
        public TipoRespuestaStory? TipoRespuestaStory { get; set; }

        // ========================================
        // RELACIONES
        // ========================================

        public ApplicationUser? Remitente { get; set; }
        public ApplicationUser? Destinatario { get; set; }

        [ForeignKey("StoryReferenciaId")]
        public virtual Story? StoryReferencia { get; set; }
    }
}