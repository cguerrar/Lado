using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Evento en el calendario de administración
    /// </summary>
    public class EventoAdmin
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Titulo { get; set; } = "";

        public string? Descripcion { get; set; }

        public TipoEventoAdmin Tipo { get; set; } = TipoEventoAdmin.General;

        public ColorEvento Color { get; set; } = ColorEvento.Azul;

        /// <summary>
        /// Fecha y hora de inicio del evento
        /// </summary>
        public DateTime FechaInicio { get; set; }

        /// <summary>
        /// Fecha y hora de fin del evento (opcional)
        /// </summary>
        public DateTime? FechaFin { get; set; }

        /// <summary>
        /// Si es un evento de todo el día
        /// </summary>
        public bool TodoElDia { get; set; } = false;

        /// <summary>
        /// Si es un evento recurrente
        /// </summary>
        public bool EsRecurrente { get; set; } = false;

        /// <summary>
        /// Tipo de recurrencia (diario, semanal, mensual)
        /// </summary>
        public TipoRecurrencia? Recurrencia { get; set; }

        /// <summary>
        /// Fecha fin de recurrencia
        /// </summary>
        public DateTime? FinRecurrencia { get; set; }

        /// <summary>
        /// Ubicación/enlace del evento
        /// </summary>
        [MaxLength(500)]
        public string? Ubicacion { get; set; }

        /// <summary>
        /// Si requiere confirmación de asistencia
        /// </summary>
        public bool RequiereConfirmacion { get; set; } = false;

        /// <summary>
        /// Si enviar recordatorio
        /// </summary>
        public bool EnviarRecordatorio { get; set; } = false;

        /// <summary>
        /// Minutos antes para recordatorio
        /// </summary>
        public int MinutosAnteRecordatorio { get; set; } = 30;

        /// <summary>
        /// Si el recordatorio ya fue enviado
        /// </summary>
        public bool RecordatorioEnviado { get; set; } = false;

        /// <summary>
        /// Admin que creó el evento
        /// </summary>
        [Required]
        public string CreadoPorId { get; set; } = "";

        [ForeignKey("CreadoPorId")]
        public ApplicationUser? CreadoPor { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Notas privadas del evento
        /// </summary>
        public string? Notas { get; set; }

        /// <summary>
        /// Si el evento está cancelado
        /// </summary>
        public bool Cancelado { get; set; } = false;

        /// <summary>
        /// Participantes asignados al evento
        /// </summary>
        public ICollection<ParticipanteEvento> Participantes { get; set; } = new List<ParticipanteEvento>();
    }

    /// <summary>
    /// Participante en un evento
    /// </summary>
    public class ParticipanteEvento
    {
        [Key]
        public int Id { get; set; }

        public int EventoId { get; set; }

        [ForeignKey("EventoId")]
        public EventoAdmin? Evento { get; set; }

        public string UsuarioId { get; set; } = "";

        [ForeignKey("UsuarioId")]
        public ApplicationUser? Usuario { get; set; }

        /// <summary>
        /// Estado de confirmación
        /// </summary>
        public EstadoParticipacion Estado { get; set; } = EstadoParticipacion.Pendiente;

        public DateTime? FechaRespuesta { get; set; }
    }

    public enum TipoEventoAdmin
    {
        General = 0,
        Reunion = 1,
        Mantenimiento = 2,
        Lanzamiento = 3,
        Revision = 4,
        Capacitacion = 5,
        Deadline = 6,
        Otro = 99
    }

    public enum ColorEvento
    {
        Azul = 0,
        Verde = 1,
        Rojo = 2,
        Amarillo = 3,
        Morado = 4,
        Rosa = 5,
        Naranja = 6,
        Gris = 7
    }

    public enum TipoRecurrencia
    {
        Diario = 0,
        Semanal = 1,
        Quincenal = 2,
        Mensual = 3
    }

    public enum EstadoParticipacion
    {
        Pendiente = 0,
        Confirmado = 1,
        Rechazado = 2,
        Tentativo = 3
    }
}
