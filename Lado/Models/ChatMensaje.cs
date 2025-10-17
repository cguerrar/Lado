namespace Lado.Models
{
    public class ChatMensaje
    {
        public int Id { get; set; }
        public string RemitenteId { get; set; } = string.Empty;
        public string DestinatarioId { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public DateTime FechaEnvio { get; set; } = DateTime.Now;
        public bool Leido { get; set; } = false;
        public bool EliminadoPorRemitente { get; set; } = false;
        public bool EliminadoPorDestinatario { get; set; } = false;

        public ApplicationUser? Remitente { get; set; }
        public ApplicationUser? Destinatario { get; set; }
    }
}