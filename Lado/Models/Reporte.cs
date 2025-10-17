namespace Lado.Models
{
    public class Reporte
    {
        public int Id { get; set; }

        public string UsuarioReportadorId { get; set; } = string.Empty;
        public ApplicationUser? UsuarioReportador { get; set; }

        public string? UsuarioReportadoId { get; set; }
        public ApplicationUser? UsuarioReportado { get; set; }

        public int? ContenidoReportadoId { get; set; }
        public int? ContenidoId { get; set; }
        public Contenido? ContenidoReportado { get; set; }
        public Contenido? Contenido { get; set; }

        public string TipoReporte { get; set; } = string.Empty;
        public string Motivo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }

        public string Estado { get; set; } = "Pendiente";
        public string? Accion { get; set; }

        public DateTime FechaReporte { get; set; } = DateTime.Now;
        public DateTime? FechaResolucion { get; set; }
    }
}