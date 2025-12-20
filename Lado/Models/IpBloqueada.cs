using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Modelo para gestionar IPs bloqueadas por el administrador.
    /// Las IPs bloqueadas no pueden acceder a la plataforma.
    /// </summary>
    public class IpBloqueada
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Dirección IP bloqueada (IPv4 o IPv6)
        /// </summary>
        [Required]
        [StringLength(45)] // IPv6 puede tener hasta 45 caracteres
        public string DireccionIp { get; set; } = string.Empty;

        /// <summary>
        /// Razón del bloqueo
        /// </summary>
        [StringLength(500)]
        public string? Razon { get; set; }

        /// <summary>
        /// Fecha y hora en que se realizó el bloqueo
        /// </summary>
        public DateTime FechaBloqueo { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha de expiración del bloqueo (null = permanente)
        /// </summary>
        public DateTime? FechaExpiracion { get; set; }

        /// <summary>
        /// ID del administrador que realizó el bloqueo
        /// </summary>
        public string? AdminId { get; set; }

        /// <summary>
        /// Indica si el bloqueo está activo
        /// </summary>
        public bool EstaActivo { get; set; } = true;

        /// <summary>
        /// Número de intentos de acceso bloqueados desde esta IP
        /// </summary>
        public int IntentosBloqueos { get; set; } = 0;

        /// <summary>
        /// Último intento de acceso desde esta IP
        /// </summary>
        public DateTime? UltimoIntento { get; set; }
    }
}
