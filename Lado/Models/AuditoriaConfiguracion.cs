using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Registro de auditoría para cambios en configuraciones del sistema
    /// </summary>
    public class AuditoriaConfiguracion
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Tipo de configuración modificada
        /// </summary>
        [Required]
        public TipoConfiguracion TipoConfiguracion { get; set; }

        /// <summary>
        /// Nombre del campo/propiedad modificada
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Campo { get; set; } = "";

        /// <summary>
        /// Valor anterior (serializado como JSON si es complejo)
        /// </summary>
        [StringLength(2000)]
        public string? ValorAnterior { get; set; }

        /// <summary>
        /// Valor nuevo (serializado como JSON si es complejo)
        /// </summary>
        [StringLength(2000)]
        public string? ValorNuevo { get; set; }

        /// <summary>
        /// ID de la entidad modificada (si aplica)
        /// </summary>
        [StringLength(100)]
        public string? EntidadId { get; set; }

        /// <summary>
        /// Descripción legible del cambio
        /// </summary>
        [StringLength(500)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Admin que realizó el cambio
        /// </summary>
        [Required]
        public string ModificadoPorId { get; set; } = "";

        [ForeignKey("ModificadoPorId")]
        public ApplicationUser? ModificadoPor { get; set; }

        /// <summary>
        /// Fecha y hora del cambio
        /// </summary>
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        /// <summary>
        /// IP desde donde se realizó el cambio
        /// </summary>
        [StringLength(45)]
        public string? IpOrigen { get; set; }

        /// <summary>
        /// User Agent del navegador
        /// </summary>
        [StringLength(500)]
        public string? UserAgent { get; set; }
    }

    public enum TipoConfiguracion
    {
        Plataforma = 0,        // ConfiguracionPlataforma
        Algoritmo = 1,         // AlgoritmoFeed
        Confianza = 2,         // ConfiguracionConfianza
        LadoCoins = 3,         // ConfiguracionLadoCoin
        Seo = 4,               // ConfiguracionSeo
        Retencion = 5,         // RetencionPais
        TasaCambio = 6,        // TasaCambio
        Mantenimiento = 7,     // ModoMantenimiento
        Permisos = 8,          // Permisos de supervisores
        Rol = 9,               // Roles de usuarios
        Popup = 10,            // Popups del sistema
        Email = 11,            // Configuración de emails
        Otro = 99              // Otros cambios
    }
}
