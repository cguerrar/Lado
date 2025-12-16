namespace Lado.Models
{
    public class Feedback
    {
        public int Id { get; set; }
        public string? UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public TipoFeedback Tipo { get; set; }
        public string Asunto { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public DateTime FechaEnvio { get; set; } = DateTime.Now;
        public EstadoFeedback Estado { get; set; } = EstadoFeedback.Pendiente;
        public string? RespuestaAdmin { get; set; }
        public DateTime? FechaRespuesta { get; set; }
        public string? AdminId { get; set; }

        // Navegación
        public ApplicationUser? Usuario { get; set; }
        public ApplicationUser? Admin { get; set; }
    }

    public enum TipoFeedback
    {
        Sugerencia = 0,
        Error = 1,
        Pregunta = 2,
        Queja = 3,
        Otro = 4
    }

    public enum EstadoFeedback
    {
        Pendiente = 0,
        EnRevision = 1,
        Respondido = 2,
        Cerrado = 3
    }
}
