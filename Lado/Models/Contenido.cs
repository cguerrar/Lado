namespace Lado.Models
{
    public class Contenido
    {
        public int Id { get; set; }
        public string UsuarioId { get; set; } = string.Empty;

        public TipoContenido TipoContenido { get; set; }
        public string? Descripcion { get; set; }
        public string? RutaArchivo { get; set; }
        public string? Thumbnail { get; set; }

        public bool EsPremium { get; set; } = false;
        public decimal? PrecioDesbloqueo { get; set; }

        public bool EsBorrador { get; set; } = false;
        public bool EstaActivo { get; set; } = true;
        public bool Censurado { get; set; } = false;
        public string? RazonCensura { get; set; }

        public int NumeroLikes { get; set; } = 0;
        public int NumeroComentarios { get; set; } = 0;
        public int NumeroVistas { get; set; } = 0;

        public DateTime FechaPublicacion { get; set; } = DateTime.Now;
        public DateTime? FechaActualizacion { get; set; }

        public ApplicationUser? Usuario { get; set; }
        public ICollection<Like> Likes { get; set; } = new List<Like>();
        public ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();
    }
}