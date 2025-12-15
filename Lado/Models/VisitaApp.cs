namespace Lado.Models
{
    public class VisitaApp
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int Contador { get; set; }
        public int VisitasUnicas { get; set; }
    }

    public class VisitaDetalle
    {
        public int Id { get; set; }
        public DateTime FechaHora { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Pagina { get; set; }
        public string? UsuarioId { get; set; }
        public bool EsNuevoVisitante { get; set; }
    }
}
