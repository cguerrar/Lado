using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    public class Contenido
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        [Required]
        public TipoContenido TipoContenido { get; set; }

        public string? Descripcion { get; set; }
        public string? RutaArchivo { get; set; }
        public string? Thumbnail { get; set; }

        // ========================================
        // SISTEMA DE MONETIZACIÓN
        // ========================================

        public bool EsPremium { get; set; } = false;

        public decimal? PrecioDesbloqueo { get; set; }

        // ========================================
        // SISTEMA LADO A / LADO B
        // ========================================

        [Required]
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        public bool EsGratis { get; set; } = true;

        public string? NombreMostrado { get; set; }

        // ========================================
        // SISTEMA DE PREVIEWS
        // ========================================

        public bool TienePreview { get; set; } = false;

        public int? DuracionPreviewSegundos { get; set; }

        public string? RutaPreview { get; set; }

        // ========================================
        // ESTADO DEL CONTENIDO
        // ========================================

        public bool EsBorrador { get; set; } = false;
        public bool EstaActivo { get; set; } = true;
        public bool Censurado { get; set; } = false;
        public string? RazonCensura { get; set; }

        // ========================================
        // MÉTRICAS
        // ========================================

        public int NumeroLikes { get; set; } = 0;
        public int NumeroComentarios { get; set; } = 0;
        public int NumeroVistas { get; set; } = 0;
        public int NumeroCompartidos { get; set; } = 0;

        // ========================================
        // FECHAS
        // ========================================

        public DateTime FechaPublicacion { get; set; } = DateTime.Now;
        public DateTime? FechaActualizacion { get; set; }

        // ========================================
        // RELACIONES
        // ========================================

        public ApplicationUser? Usuario { get; set; }

        public ICollection<Like> Likes { get; set; } = new List<Like>();

        public ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

        // Nuevas relaciones para sistema premium
        public ICollection<Reaccion> Reacciones { get; set; } = new List<Reaccion>();

        public ICollection<ContenidoColeccion> Colecciones { get; set; } = new List<ContenidoColeccion>();

        public ICollection<CompraContenido> Compras { get; set; } = new List<CompraContenido>();
    }
}