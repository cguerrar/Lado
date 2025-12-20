using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Tipo de log/evento
    /// </summary>
    public enum TipoLogEvento
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Evento = 3
    }

    /// <summary>
    /// Categoría del evento para filtrado
    /// </summary>
    public enum CategoriaEvento
    {
        Sistema = 0,    // Errores de app, BD, servicios externos
        Auth = 1,       // Login, logout, registro, cambio password
        Usuario = 2,    // Acciones de usuario
        Pago = 3,       // Suscripciones, pagos, retiros, propinas
        Contenido = 4,  // Publicaciones, comentarios, likes
        Admin = 5       // Acciones administrativas
    }

    /// <summary>
    /// Modelo para registrar logs y eventos del sistema
    /// </summary>
    public class LogEvento
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Fecha y hora del evento
        /// </summary>
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tipo de log (Error, Warning, Info, Evento)
        /// </summary>
        public TipoLogEvento Tipo { get; set; }

        /// <summary>
        /// Categoría para filtrado
        /// </summary>
        public CategoriaEvento Categoria { get; set; }

        /// <summary>
        /// Mensaje principal del log
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>
        /// Detalles adicionales (stack trace, datos JSON, etc.)
        /// </summary>
        public string? Detalle { get; set; }

        /// <summary>
        /// ID del usuario relacionado (opcional)
        /// </summary>
        [MaxLength(450)]
        public string? UsuarioId { get; set; }

        /// <summary>
        /// Nombre o email del usuario para referencia rápida
        /// </summary>
        [MaxLength(256)]
        public string? UsuarioNombre { get; set; }

        /// <summary>
        /// Dirección IP del cliente
        /// </summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /// <summary>
        /// User Agent del navegador
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// URL donde ocurrió el evento
        /// </summary>
        [MaxLength(2000)]
        public string? Url { get; set; }

        /// <summary>
        /// Método HTTP (GET, POST, etc.)
        /// </summary>
        [MaxLength(10)]
        public string? MetodoHttp { get; set; }

        /// <summary>
        /// Nombre de la excepción si es un error
        /// </summary>
        [MaxLength(200)]
        public string? TipoExcepcion { get; set; }

        // Navegación (opcional, no requerido)
        [ForeignKey("UsuarioId")]
        public virtual ApplicationUser? Usuario { get; set; }
    }
}
