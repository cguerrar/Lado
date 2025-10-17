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

        public string? EstadoPago { get; set; } = "Completado";
        public string? EstadoTransaccion { get; set; } = "Completado";

        public string? MetodoPago { get; set; }
        public string? Notas { get; set; }

        public ApplicationUser? Usuario { get; set; }
    }

   
}