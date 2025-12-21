namespace Lado.Models
{
    /// <summary>
    /// Duración de la suscripción
    /// </summary>
    public enum DuracionSuscripcion
    {
        Dia = 1,      // 24 horas
        Semana = 7,   // 7 días
        Mes = 30      // 30 días (mensual)
    }

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

        // TipoLado al que se suscribe (LadoA o LadoB)
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        // Duración de la suscripción (24h, 7 días, mensual)
        public DuracionSuscripcion Duracion { get; set; } = DuracionSuscripcion.Mes;

        public ApplicationUser Fan { get; set; } = null!;
        public ApplicationUser Creador { get; set; } = null!;

        /// <summary>
        /// Calcula el precio según la duración
        /// </summary>
        public static decimal CalcularPrecio(decimal precioMensual, DuracionSuscripcion duracion)
        {
            return duracion switch
            {
                DuracionSuscripcion.Dia => Math.Round(precioMensual * 0.15m, 2),    // 15% del mensual
                DuracionSuscripcion.Semana => Math.Round(precioMensual * 0.40m, 2), // 40% del mensual
                DuracionSuscripcion.Mes => precioMensual,                            // 100%
                _ => precioMensual
            };
        }

        /// <summary>
        /// Calcula la fecha de fin según la duración
        /// </summary>
        public static DateTime CalcularFechaFin(DateTime inicio, DuracionSuscripcion duracion)
        {
            return duracion switch
            {
                DuracionSuscripcion.Dia => inicio.AddHours(24),
                DuracionSuscripcion.Semana => inicio.AddDays(7),
                DuracionSuscripcion.Mes => inicio.AddMonths(1),
                _ => inicio.AddMonths(1)
            };
        }

        /// <summary>
        /// Obtiene el texto descriptivo de la duración
        /// </summary>
        public static string ObtenerTextoDuracion(DuracionSuscripcion duracion)
        {
            return duracion switch
            {
                DuracionSuscripcion.Dia => "24 horas",
                DuracionSuscripcion.Semana => "7 días",
                DuracionSuscripcion.Mes => "1 mes",
                _ => "1 mes"
            };
        }

        /// <summary>
        /// Verifica si la suscripción ha expirado
        /// </summary>
        public bool HaExpirado()
        {
            if (FechaFin.HasValue && DateTime.Now > FechaFin.Value)
                return true;
            return false;
        }
    }
}