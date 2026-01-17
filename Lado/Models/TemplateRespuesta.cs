using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Templates de respuestas predefinidas para moderación
    /// </summary>
    public class TemplateRespuesta
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Nombre/Título del template
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = "";

        /// <summary>
        /// Categoría del template
        /// </summary>
        [Required]
        public CategoriaTemplate Categoria { get; set; }

        /// <summary>
        /// Contenido del template (puede contener placeholders como {usuario}, {contenido})
        /// </summary>
        [Required]
        [StringLength(2000)]
        public string Contenido { get; set; } = "";

        /// <summary>
        /// Descripción o instrucciones de uso
        /// </summary>
        [StringLength(500)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Atajo de teclado (ej: "r1", "a1")
        /// </summary>
        [StringLength(10)]
        public string? Atajo { get; set; }

        /// <summary>
        /// Orden de visualización
        /// </summary>
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Si el template está activo
        /// </summary>
        public bool EstaActivo { get; set; } = true;

        /// <summary>
        /// Quién creó el template
        /// </summary>
        public string? CreadoPorId { get; set; }

        [ForeignKey("CreadoPorId")]
        public ApplicationUser? CreadoPor { get; set; }

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Contador de veces usado
        /// </summary>
        public int VecesUsado { get; set; } = 0;
    }

    public enum CategoriaTemplate
    {
        Reporte = 0,           // Respuestas para reportes
        Apelacion = 1,         // Respuestas para apelaciones
        Verificacion = 2,      // Respuestas para verificaciones
        Soporte = 3,           // Respuestas de soporte general
        Suspension = 4,        // Mensajes de suspensión
        Rechazo = 5,           // Mensajes de rechazo de contenido
        Aprobacion = 6,        // Mensajes de aprobación
        Otro = 99
    }
}
