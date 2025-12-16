using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    public enum TipoNotificacion
    {
        NuevoMensaje = 0,
        NuevoSeguidor = 1,
        NuevoContenido = 2,      // Nuevo post de alguien que sigues
        NuevoLike = 3,
        NuevoComentario = 4,
        NuevaSuscripcion = 5,    // Alguien se suscribió a ti
        NuevoDesafio = 6,
        PropuestaDesafio = 7,
        PagoRecibido = 8,
        RetiroCompletado = 9,
        Sistema = 10             // Notificaciones del sistema
    }

    public class Notificacion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        public TipoNotificacion Tipo { get; set; }

        [Required]
        [MaxLength(500)]
        public string Mensaje { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Titulo { get; set; }

        // Usuario que generó la notificación (quien dio like, comentó, etc.)
        public string? UsuarioOrigenId { get; set; }

        [ForeignKey("UsuarioOrigenId")]
        public virtual ApplicationUser? UsuarioOrigen { get; set; }

        // Referencias opcionales a entidades relacionadas
        public int? ContenidoId { get; set; }
        public int? MensajeId { get; set; }
        public int? DesafioId { get; set; }
        public int? ComentarioId { get; set; }

        // URL para navegar al hacer clic
        [MaxLength(500)]
        public string? UrlDestino { get; set; }

        // Imagen/icono de la notificación (foto del usuario origen, etc.)
        [MaxLength(500)]
        public string? ImagenUrl { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public bool Leida { get; set; } = false;

        public DateTime? FechaLectura { get; set; }

        public bool EstaActiva { get; set; } = true;
    }
}
