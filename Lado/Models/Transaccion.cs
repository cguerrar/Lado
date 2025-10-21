namespace Lado.Models
{
    public class Transaccion
    {
        public int Id { get; set; }

        public string UsuarioId { get; set; } = string.Empty;

        public TipoTransaccion TipoTransaccion { get; set; }

        public decimal Monto { get; set; }

        public decimal? MontoNeto { get; set; }

        public decimal? Comision { get; set; }

        public string? Descripcion { get; set; }

        public DateTime FechaTransaccion { get; set; } = DateTime.Now;

        // ========================================
        // ESTADOS
        // ========================================

        // EstadoPago: se mantiene como string para compatibilidad
        public string? EstadoPago { get; set; } = "Completado";

        // EstadoTransaccion: ahora es enum
        public EstadoTransaccion EstadoTransaccion { get; set; } = EstadoTransaccion.Completada;

        // ========================================
        // INFORMACIÓN ADICIONAL
        // ========================================

        public string? MetodoPago { get; set; }

        public string? Notas { get; set; }

        // ========================================
        // RELACIONES
        // ========================================

        public ApplicationUser? Usuario { get; set; }
    }
}