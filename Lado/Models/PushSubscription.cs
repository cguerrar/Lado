using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Suscripción Push de un usuario para recibir notificaciones en su dispositivo
    /// </summary>
    public class PushSubscription
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID del usuario suscrito
        /// </summary>
        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        /// <summary>
        /// Endpoint del servicio push (URL única por suscripción)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Clave P256DH para cifrado
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string P256dh { get; set; } = string.Empty;

        /// <summary>
        /// Clave de autenticación
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Auth { get; set; } = string.Empty;

        /// <summary>
        /// Identificador único del dispositivo/navegador
        /// </summary>
        [MaxLength(500)]
        public string? DeviceId { get; set; }

        /// <summary>
        /// User Agent del navegador (para identificar dispositivo)
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Fecha de creación de la suscripción
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Última vez que se envió una notificación exitosamente
        /// </summary>
        public DateTime? UltimaNotificacion { get; set; }

        /// <summary>
        /// Si la suscripción está activa
        /// </summary>
        public bool Activa { get; set; } = true;

        /// <summary>
        /// Número de fallos consecutivos al enviar notificaciones
        /// </summary>
        public int FallosConsecutivos { get; set; } = 0;
    }

    /// <summary>
    /// Preferencias de notificaciones del usuario
    /// </summary>
    public class PreferenciasNotificacion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }

        /// <summary>
        /// Recibir notificaciones de nuevos mensajes
        /// </summary>
        public bool NotificarMensajes { get; set; } = true;

        /// <summary>
        /// Recibir notificaciones de nuevos likes
        /// </summary>
        public bool NotificarLikes { get; set; } = true;

        /// <summary>
        /// Recibir notificaciones de nuevos comentarios
        /// </summary>
        public bool NotificarComentarios { get; set; } = true;

        /// <summary>
        /// Recibir notificaciones de nuevos seguidores
        /// </summary>
        public bool NotificarSeguidores { get; set; } = true;

        /// <summary>
        /// Recibir notificaciones de nuevas suscripciones (pago)
        /// </summary>
        public bool NotificarSuscripciones { get; set; } = true;

        /// <summary>
        /// Recibir notificaciones de propinas
        /// </summary>
        public bool NotificarPropinas { get; set; } = true;

        /// <summary>
        /// Recibir notificaciones de menciones
        /// </summary>
        public bool NotificarMenciones { get; set; } = true;

        /// <summary>
        /// Horas de silencio - Inicio (ej: 22:00)
        /// </summary>
        public TimeOnly? HoraSilencioInicio { get; set; }

        /// <summary>
        /// Horas de silencio - Fin (ej: 08:00)
        /// </summary>
        public TimeOnly? HoraSilencioFin { get; set; }

        /// <summary>
        /// Zona horaria del usuario para horas de silencio
        /// </summary>
        [MaxLength(50)]
        public string? ZonaHoraria { get; set; }
    }

    /// <summary>
    /// Tipos de notificación push
    /// </summary>
    public enum TipoNotificacionPush
    {
        NuevoMensaje,
        NuevoLike,
        NuevoComentario,
        NuevoSeguidor,
        NuevaSuscripcion,
        NuevaPropina,
        Mencion,
        Sistema,
        Promocion
    }
}
