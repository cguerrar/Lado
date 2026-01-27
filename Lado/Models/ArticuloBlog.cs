using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Artículo del blog para SEO y contenido informativo
    /// </summary>
    public class ArticuloBlog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Resumen { get; set; }

        [Required]
        public string Contenido { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ImagenPortada { get; set; }

        [StringLength(200)]
        public string? MetaTitulo { get; set; }

        [StringLength(300)]
        public string? MetaDescripcion { get; set; }

        [StringLength(500)]
        public string? PalabrasClave { get; set; }

        public CategoriaBlog Categoria { get; set; } = CategoriaBlog.General;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public DateTime? FechaPublicacion { get; set; }

        public DateTime? FechaModificacion { get; set; }

        public bool EstaPublicado { get; set; } = false;

        public bool EsDestacado { get; set; } = false;

        public int Vistas { get; set; } = 0;

        public int TiempoLecturaMinutos { get; set; } = 5;

        // Relación con el autor
        public string? AutorId { get; set; }

        [ForeignKey("AutorId")]
        public ApplicationUser? Autor { get; set; }

        // Generar slug desde título
        public static string GenerarSlug(string titulo)
        {
            if (string.IsNullOrEmpty(titulo)) return string.Empty;

            var slug = titulo.ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
                .Replace(" ", "-")
                .Replace(".", "").Replace(",", "").Replace(":", "")
                .Replace(";", "").Replace("!", "").Replace("?", "")
                .Replace("¿", "").Replace("¡", "").Replace("\"", "")
                .Replace("'", "").Replace("(", "").Replace(")", "");

            // Remover caracteres no alfanuméricos excepto guiones
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");

            // Remover guiones múltiples
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

            // Remover guiones al inicio y final
            slug = slug.Trim('-');

            return slug.Length > 200 ? slug.Substring(0, 200) : slug;
        }

        // Calcular tiempo de lectura
        public void CalcularTiempoLectura()
        {
            if (string.IsNullOrEmpty(Contenido)) return;

            var palabras = Contenido.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            TiempoLecturaMinutos = Math.Max(1, (int)Math.Ceiling(palabras / 200.0)); // 200 palabras por minuto
        }
    }

    public enum CategoriaBlog
    {
        General = 0,
        Novedades = 1,
        Tutoriales = 2,
        Consejos = 3,
        Actualizaciones = 4,
        Comunidad = 5,
        Monetizacion = 6,
        Seguridad = 7
    }
}
