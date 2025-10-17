namespace Lado.Models
{
    public class Suscripcion
    {
        public int Id { get; set; }

        public string FanId { get; set; } = string.Empty;
        public string CreadorId { get; set; } = string.Empty;

        public decimal PrecioMensual { get; set; }
        public decimal Precio { get; set; }

        public DateTime FechaInicio { get; set; } = DateTime.Now;
        public DateTime? FechaCancelacion { get; set; }
        public DateTime? FechaFin { get; set; }
        public DateTime ProximaRenovacion { get; set; }

        public bool EstaActiva { get; set; } = true;
        public bool RenovacionAutomatica { get; set; } = true;

        public ApplicationUser Fan { get; set; } = null!;
        public ApplicationUser Creador { get; set; } = null!;
    }
}