namespace Lado.Models
{
    public class TransaccionDto
    {
        public int Id { get; set; }
        public DateTime FechaTransaccion { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Estado { get; set; } = string.Empty;
        public TipoTransaccion TipoTransaccion { get; set; }
    }
}