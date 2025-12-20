using Lado.DTOs.Usuario;

namespace Lado.DTOs.Feed
{
    /// <summary>
    /// DTO de contenido para el feed
    /// </summary>
    public class ContenidoDto
    {
        public int Id { get; set; }
        public UsuarioDto Creador { get; set; } = new();

        // Contenido
        public string? Texto { get; set; }
        public string TipoContenido { get; set; } = string.Empty; // Foto, Video, Audio, etc.
        public string TipoLado { get; set; } = string.Empty; // A, B
        public bool EsGratuito { get; set; }
        public decimal? PrecioDesbloqueo { get; set; }

        // Archivos
        public List<ArchivoContenidoDto> Archivos { get; set; } = new();
        public string? RutaPreview { get; set; }
        public bool TienePreview { get; set; }

        // Para contenido bloqueado
        public bool EstaDesbloqueado { get; set; }
        public int CantidadArchivos { get; set; }

        // Estadisticas
        public int NumeroLikes { get; set; }
        public int NumeroComentarios { get; set; }
        public int NumeroVistas { get; set; }

        // Estado del usuario actual
        public bool MeGusta { get; set; }
        public bool EsFavorito { get; set; }
        public string? MiReaccion { get; set; } // Tipo de reaccion si existe

        // Fechas
        public DateTime FechaPublicacion { get; set; }
        public string TiempoRelativo { get; set; } = string.Empty; // "hace 2 horas"
    }

    /// <summary>
    /// DTO de archivo de contenido
    /// </summary>
    public class ArchivoContenidoDto
    {
        public int Id { get; set; }
        public string RutaArchivo { get; set; } = string.Empty;
        public string TipoArchivo { get; set; } = string.Empty;
        public string? Thumbnail { get; set; }
        public int? Duracion { get; set; } // Para videos/audios
        public int Orden { get; set; }
    }

    /// <summary>
    /// DTO de comentario
    /// </summary>
    public class ComentarioDto
    {
        public int Id { get; set; }
        public UsuarioDto Usuario { get; set; } = new();
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public string TiempoRelativo { get; set; } = string.Empty;
        public bool EsMio { get; set; }
    }

    /// <summary>
    /// DTO para crear contenido
    /// </summary>
    public class CrearContenidoRequest
    {
        public string? Texto { get; set; }
        public string TipoContenido { get; set; } = "Foto";
        public string TipoLado { get; set; } = "A"; // A = publico, B = premium
        public bool EsGratuito { get; set; } = true;
        public decimal? PrecioDesbloqueo { get; set; }
        public int? CategoriaInteresId { get; set; }
    }

    /// <summary>
    /// DTO para crear comentario
    /// </summary>
    public class CrearComentarioRequest
    {
        public string Texto { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para reaccion
    /// </summary>
    public class ReaccionRequest
    {
        public string TipoReaccion { get; set; } = "Like"; // Like, Love, Fire, Sad, etc.
    }

    /// <summary>
    /// DTO de Story
    /// </summary>
    public class StoryDto
    {
        public int Id { get; set; }
        public UsuarioDto Creador { get; set; } = new();
        public string RutaArchivo { get; set; } = string.Empty;
        public string TipoArchivo { get; set; } = string.Empty;
        public string? Texto { get; set; }
        public string TipoLado { get; set; } = string.Empty;
        public int NumeroVistas { get; set; }
        public DateTime FechaPublicacion { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public bool YaVista { get; set; }
    }

    /// <summary>
    /// DTO agrupado de stories por creador
    /// </summary>
    public class StoriesCreadorDto
    {
        public UsuarioDto Creador { get; set; } = new();
        public List<StoryDto> Stories { get; set; } = new();
        public bool TodasVistas { get; set; }
        public int TotalStories { get; set; }
    }
}
