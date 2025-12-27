using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Plantilla reutilizable para emails masivos
    /// </summary>
    public class PlantillaEmail
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Nombre identificador de la plantilla
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Descripción breve del propósito de la plantilla
        /// </summary>
        [StringLength(255)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Asunto del email (soporta placeholders: {{nombre}}, {{email}}, {{usuario}})
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Asunto { get; set; } = string.Empty;

        /// <summary>
        /// Contenido HTML del email (soporta placeholders)
        /// </summary>
        [Required]
        public string ContenidoHtml { get; set; } = string.Empty;

        /// <summary>
        /// Categoría para organizar plantillas
        /// </summary>
        [StringLength(50)]
        public string Categoria { get; set; } = "Marketing";

        /// <summary>
        /// Si la plantilla está activa y disponible para usar
        /// </summary>
        public bool EstaActiva { get; set; } = true;

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// Última modificación
        /// </summary>
        public DateTime? UltimaModificacion { get; set; }

        // Categorías comunes
        public static readonly string[] CategoriasDisponibles = new[]
        {
            "Marketing",
            "Comunicado",
            "Promoción",
            "Bienvenida",
            "Sistema"
        };

        // Placeholders disponibles
        public static readonly Dictionary<string, string> PlaceholdersDisponibles = new()
        {
            { "{{nombre}}", "Nombre completo del usuario" },
            { "{{email}}", "Email del usuario" },
            { "{{usuario}}", "Nombre de usuario (@handle)" },
            { "{{fecha}}", "Fecha actual" }
        };
    }
}
