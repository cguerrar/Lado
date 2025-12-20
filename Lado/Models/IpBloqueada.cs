using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Tipo de bloqueo de IP
    /// </summary>
    public enum TipoBloqueoIp
    {
        Manual = 0,         // Bloqueado por admin
        Automatico = 1      // Bloqueado por sistema (rate limit excedido)
    }

    /// <summary>
    /// Tipo de ataque detectado
    /// </summary>
    public enum TipoAtaque
    {
        Ninguno = 0,
        SpamContenido = 1,      // Crear muchos posts
        FuerzaBruta = 2,        // Intentos de login
        SpamMensajes = 3,       // Muchos mensajes
        SpamRegistro = 4,       // Múltiples registros
        Scraping = 5,           // Acceso excesivo a páginas
        Otro = 99
    }

    /// <summary>
    /// Modelo para gestionar IPs bloqueadas.
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
        [StringLength(45)]
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
        /// ID del administrador que realizó el bloqueo (null si es automático)
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

        /// <summary>
        /// Tipo de bloqueo (manual por admin o automático por sistema)
        /// </summary>
        public TipoBloqueoIp TipoBloqueo { get; set; } = TipoBloqueoIp.Manual;

        /// <summary>
        /// Tipo de ataque detectado (solo para bloqueos automáticos)
        /// </summary>
        public TipoAtaque TipoAtaque { get; set; } = TipoAtaque.Ninguno;

        /// <summary>
        /// Número de violaciones de rate limit antes del bloqueo
        /// </summary>
        public int ViolacionesRateLimit { get; set; } = 0;
    }

    /// <summary>
    /// Registro de intentos de ataque detectados (para estadísticas)
    /// </summary>
    public class IntentoAtaque
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(45)]
        public string DireccionIp { get; set; } = string.Empty;

        public DateTime Fecha { get; set; } = DateTime.Now;

        public TipoAtaque TipoAtaque { get; set; }

        [StringLength(200)]
        public string? Endpoint { get; set; }

        [StringLength(100)]
        public string? UsuarioId { get; set; }

        [StringLength(100)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Indica si este intento resultó en un bloqueo
        /// </summary>
        public bool ResultoEnBloqueo { get; set; } = false;
    }
}
