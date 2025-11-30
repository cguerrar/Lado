namespace Lado.Models
{
    public class Favorito
    {
        public int Id { get; set; }

        public int ContenidoId { get; set; }
        public Contenido? Contenido { get; set; }

        public string UsuarioId { get; set; } = string.Empty;
        public ApplicationUser? Usuario { get; set; }

        public DateTime FechaAgregado { get; set; } = DateTime.Now;
    }
}
