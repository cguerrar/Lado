using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Notas internas para comunicación entre admins/supervisores
    /// Pueden asociarse a usuarios, contenidos, reportes, etc.
    /// </summary>
    public class NotaInterna
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Tipo de entidad a la que se asocia la nota
        /// </summary>
        [Required]
        public TipoEntidadNota TipoEntidad { get; set; }

        /// <summary>
        /// ID de la entidad (UserId, ContenidoId, ReporteId, etc.)
        /// </summary>
        [Required]
        [StringLength(100)]
        public string EntidadId { get; set; } = "";

        /// <summary>
        /// Contenido de la nota
        /// </summary>
        [Required]
        [StringLength(2000)]
        public string Contenido { get; set; } = "";

        /// <summary>
        /// Prioridad o importancia de la nota
        /// </summary>
        public PrioridadNota Prioridad { get; set; } = PrioridadNota.Normal;

        /// <summary>
        /// Si la nota está fijada/destacada
        /// </summary>
        public bool EsFijada { get; set; } = false;

        /// <summary>
        /// Si la nota está activa (soft delete)
        /// </summary>
        public bool EstaActiva { get; set; } = true;

        /// <summary>
        /// Admin/Supervisor que creó la nota
        /// </summary>
        [Required]
        public string CreadoPorId { get; set; } = "";

        [ForeignKey("CreadoPorId")]
        public ApplicationUser? CreadoPor { get; set; }

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha de última edición
        /// </summary>
        public DateTime? FechaEdicion { get; set; }

        /// <summary>
        /// Admin/Supervisor que editó la nota por última vez
        /// </summary>
        public string? EditadoPorId { get; set; }

        [ForeignKey("EditadoPorId")]
        public ApplicationUser? EditadoPor { get; set; }

        /// <summary>
        /// Tags/etiquetas de la nota (separadas por coma)
        /// </summary>
        [StringLength(200)]
        public string? Tags { get; set; }
    }

    public enum TipoEntidadNota
    {
        Usuario = 0,
        Contenido = 1,
        Reporte = 2,
        Apelacion = 3,
        Verificacion = 4,
        Transaccion = 5,
        General = 6  // Notas generales sin asociación específica
    }

    public enum PrioridadNota
    {
        Baja = 0,
        Normal = 1,
        Alta = 2,
        Urgente = 3
    }
}
