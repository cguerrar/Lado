using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Representa una apelacion de un usuario sobre contenido rechazado/eliminado
    /// </summary>
    public class Apelacion
    {
        [Key]
        public int Id { get; set; }

        // Usuario que crea la apelacion
        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public ApplicationUser? Usuario { get; set; }

        // Tipo de contenido apelado
        [Required]
        [StringLength(50)]
        public string TipoContenido { get; set; } = string.Empty; // "Publicacion", "Story", "Comentario", "Perfil"

        // Referencias opcionales al contenido
        public int? ContenidoId { get; set; }

        [ForeignKey("ContenidoId")]
        public Contenido? Contenido { get; set; }

        public int? StoryId { get; set; }

        [ForeignKey("StoryId")]
        public Story? Story { get; set; }

        public int? ComentarioId { get; set; }

        [ForeignKey("ComentarioId")]
        public Comentario? Comentario { get; set; }

        // Razon original del rechazo/eliminacion
        [Required]
        [StringLength(500)]
        public string RazonRechazo { get; set; } = string.Empty;

        // Argumentos del usuario para la apelacion
        [Required]
        [StringLength(2000)]
        public string Argumentos { get; set; } = string.Empty;

        // Evidencia adicional (URLs de imagenes, etc.)
        [StringLength(1000)]
        public string? EvidenciaAdicional { get; set; }

        // Estado de la apelacion
        [Required]
        public EstadoApelacion Estado { get; set; } = EstadoApelacion.Pendiente;

        // Resolucion
        [StringLength(1000)]
        public string? ResolucionComentario { get; set; }

        // Administrador que reviso la apelacion
        public string? AdministradorId { get; set; }

        [ForeignKey("AdministradorId")]
        public ApplicationUser? Administrador { get; set; }

        // Fechas
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? FechaResolucion { get; set; }

        // Si el contenido fue restaurado
        public bool ContenidoRestaurado { get; set; } = false;

        // Prioridad de la apelacion
        public PrioridadApelacion Prioridad { get; set; } = PrioridadApelacion.Normal;

        // Numero de apelacion (para referencia del usuario)
        [StringLength(20)]
        public string? NumeroReferencia { get; set; }
    }

    public enum EstadoApelacion
    {
        Pendiente = 0,
        EnRevision = 1,
        Aprobada = 2,
        Rechazada = 3,
        Escalada = 4 // Escalada a revision superior
    }

    public enum PrioridadApelacion
    {
        Baja = 0,
        Normal = 1,
        Alta = 2,
        Urgente = 3
    }
}
