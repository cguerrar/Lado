// Crear este archivo en Models/Tip.cs

namespace Lado.Models
{
    public class Tip
    {
        public int Id { get; set; }

        public string FanId { get; set; }
        public ApplicationUser Fan { get; set; }

        public string CreadorId { get; set; }
        public ApplicationUser Creador { get; set; }

        public decimal Monto { get; set; }
        public string? Mensaje { get; set; }

        public DateTime FechaEnvio { get; set; } = DateTime.Now;
    }
}