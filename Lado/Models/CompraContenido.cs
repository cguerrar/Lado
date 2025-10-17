// Crear este archivo en la carpeta Models

namespace Lado.Models
{
    public class CompraContenido
    {
        public int Id { get; set; }

        public int ContenidoId { get; set; }
        public Contenido Contenido { get; set; }

        public string UsuarioId { get; set; }
        public ApplicationUser Usuario { get; set; }

        public decimal Monto { get; set; }
        public DateTime FechaCompra { get; set; } = DateTime.Now;
    }
}