using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Redirecciones 301/302 gestionables desde Admin.
    /// Permite redirigir URLs antiguas a nuevas sin modificar código.
    /// </summary>
    public class Redireccion301
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// URL de origen (sin dominio, ej: /old-page)
        /// </summary>
        [Required]
        [Display(Name = "URL de Origen")]
        [StringLength(500)]
        public string UrlOrigen { get; set; } = string.Empty;

        /// <summary>
        /// URL de destino (puede ser relativa o absoluta)
        /// </summary>
        [Required]
        [Display(Name = "URL de Destino")]
        [StringLength(500)]
        public string UrlDestino { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de redirección (301 permanente, 302 temporal)
        /// </summary>
        [Display(Name = "Tipo de Redirección")]
        public TipoRedireccion Tipo { get; set; } = TipoRedireccion.Permanente301;

        /// <summary>
        /// Si la redirección está activa
        /// </summary>
        [Display(Name = "Activa")]
        public bool Activa { get; set; } = true;

        /// <summary>
        /// Si debe preservar query strings (?param=value)
        /// </summary>
        [Display(Name = "Preservar Query String")]
        public bool PreservarQueryString { get; set; } = true;

        /// <summary>
        /// Contador de veces que se ha usado esta redirección
        /// </summary>
        [Display(Name = "Veces Usada")]
        public int ContadorUso { get; set; } = 0;

        /// <summary>
        /// Última vez que se usó esta redirección
        /// </summary>
        [Display(Name = "Último Uso")]
        public DateTime? UltimoUso { get; set; }

        /// <summary>
        /// Nota o comentario sobre esta redirección
        /// </summary>
        [Display(Name = "Nota")]
        [StringLength(500)]
        public string? Nota { get; set; }

        /// <summary>
        /// Fecha de creación
        /// </summary>
        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Usuario que creó la redirección
        /// </summary>
        [Display(Name = "Creado Por")]
        [StringLength(100)]
        public string? CreadoPor { get; set; }
    }

    /// <summary>
    /// Tipo de redirección HTTP
    /// </summary>
    public enum TipoRedireccion
    {
        [Display(Name = "301 - Permanente")]
        Permanente301 = 301,

        [Display(Name = "302 - Temporal")]
        Temporal302 = 302
    }
}
