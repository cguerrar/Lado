using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Rutas permitidas o bloqueadas para robots.txt, gestionables desde Admin.
    /// </summary>
    public class RutaRobotsTxt
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Ruta a configurar (ej: /Admin/, /api/, /*?sort=)
        /// </summary>
        [Required]
        [Display(Name = "Ruta")]
        [StringLength(200)]
        public string Ruta { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de regla (Allow o Disallow)
        /// </summary>
        [Display(Name = "Tipo de Regla")]
        public TipoReglaRobots Tipo { get; set; } = TipoReglaRobots.Disallow;

        /// <summary>
        /// A qué user-agent aplica (* para todos)
        /// </summary>
        [Display(Name = "User-Agent")]
        [StringLength(100)]
        public string UserAgent { get; set; } = "*";

        /// <summary>
        /// Si la regla está activa
        /// </summary>
        [Display(Name = "Activa")]
        public bool Activa { get; set; } = true;

        /// <summary>
        /// Orden de prioridad (menor = más prioritario)
        /// </summary>
        [Display(Name = "Orden")]
        public int Orden { get; set; } = 100;

        /// <summary>
        /// Descripción o nota sobre esta regla
        /// </summary>
        [Display(Name = "Descripción")]
        [StringLength(200)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Tipo de regla para robots.txt
    /// </summary>
    public enum TipoReglaRobots
    {
        [Display(Name = "Permitir (Allow)")]
        Allow,

        [Display(Name = "Bloquear (Disallow)")]
        Disallow
    }
}
